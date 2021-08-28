using Dhaf.Core;
using dotnet_etcd;
using Etcdserverpb;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Dhaf.Node
{
    public interface IDhafNode
    {
        /// <summary>Checks if a cluster leader exists.</summary>
        /// <returns>The name of the leader node or null if the cluster leader does not exist.</returns>
        Task<string> GetLeaderOrDefault();

        /// <summary>To try to become a cluster leader.</summary>
        Task<PromotionStatus> TryPromotion();

        /// <summary>Tell the cluster that the current dhaf node is healthy.</summary>
        Task Heartbeat();

        /// <summary>Shutdown the current cluster node.</summary>
        Task Shutdown();

        /// <summary>
        /// Checks the health of all network configurations (both active and others).
        /// The result (as a set of votes) is sent to DCS.
        /// </summary>
        Task NetworkConfigurationsHealthCheck();

        /// <summary>
        /// Inspects the results of network configuration health checks from all active nodes in the dhaf cluster.
        /// A vote determines the health of each configuration by majority vote.
        /// </summary>
        /// <returns>The status of each network configuration.</returns>
        Task<IEnumerable<NetworkConfigurationStatus>> InspectResultsOfNetworkConfigurationsHealthCheck();

        /// <summary>
        /// If any of the nodes have not sent a heartbeat illegally for too long,
        /// it will be marked as unhealthy in DCS.
        /// </summary>
        Task FetchDhafNodeStatuses();

        Task<DecisionOfNetworkConfigurationSwitching> IsAutoSwitchingOfNetworkConfigurationRequired();

        Task<DecisionOfNetworkConfigurationSwitching> IsManualSwitchingOfNetworkConfigurationRequired();

        /// <summary>
        /// The dhaf cluster node tact, which does all the necessary things from the node lifecycle.
        /// </summary>
        Task Tact();

        Task<bool> IsShutdownRequested();
    }

    public class DhafNode : IDhafNode
    {
        private readonly EtcdClient _etcdClient;
        private readonly DhafNodeBackgroundTasks _backgroundTasks;
        private readonly ISwitcher _switcher;
        private readonly IHealthChecker _healthChecker;

        /// <summary>
        /// The current role of the dhaf node.
        /// </summary>
        protected DhafNodeRole Role = DhafNodeRole.Follower;

        /// <summary>
        /// The etcd lease identifier for the leader key.
        /// </summary>
        protected long? LeaderLeaseId;

        protected string LastKnownLeader;

        protected ClusterConfig _clusterConfig { get; set; }
        protected DhafInternalConfig _dhafInternalConfig { get; set; }
        protected string EtcdClusterRoot { get => $"/{_clusterConfig.Dhaf.ClusterName}/"; }

        public Dictionary<string, EtcdNodeStatus> EtcdNodeStatuses { get; set; }
            = new Dictionary<string, EtcdNodeStatus>();

        public DhafNode(ClusterConfig clusterConfig,
            DhafInternalConfig dhafInternalConfig,
            ISwitcher switcher,
            IHealthChecker healthChecker)
        {
            _clusterConfig = clusterConfig;
            _dhafInternalConfig = dhafInternalConfig;

            _healthChecker = healthChecker;
            _switcher = switcher;

            _etcdClient = new EtcdClient(_clusterConfig.Etcd.Hosts);
            _backgroundTasks = new DhafNodeBackgroundTasks();

            // TODO: Transfer to the cluster configuration parser.
            if ((_clusterConfig.Etcd.LeaderKeyTtl ?? _dhafInternalConfig.Etcd.DefLeaderKeyTtl)
                <= (_clusterConfig.Dhaf.HeartbeatInterval ?? _dhafInternalConfig.DefHeartbeatInterval))
            {
                throw new ArgumentException("The TTL of the leader key in the ETCD must be greater than the heartbeat interval of the Dhaf node.");
            }

            Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] Node has been successfully initialized.");

            _backgroundTasks.HeartbeatWithIntervalTask = HeartbeatWithInterval();
        }

        public async Task<string> GetLeaderOrDefault()
        {
            var leader = await _etcdClient.GetValAsync(EtcdClusterRoot + _dhafInternalConfig.Etcd.LeaderPath);

            if (string.IsNullOrEmpty(leader))
            {
                return null;
            }

            return leader;
        }

        protected async Task ParticipateInLeaderElection()
        {
            Role = DhafNodeRole.Candidate;
            Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] There is no leader. Participating in the election...");
            var promotionStatus = await TryPromotion();

            if (promotionStatus.Success)
            {
                Role = DhafNodeRole.Leader;
                LeaderLeaseId = promotionStatus.LeaderLeaseId;

                _backgroundTasks.FetchDhafNodeStatusesWithIntervalTask = FetchDhafNodeStatusesWithInterval();

                Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] I'm a leader now.");
            }
            else
            {
                Role = DhafNodeRole.Follower;
                Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] {promotionStatus.Leader} is now the leader.");
            }

            LastKnownLeader = promotionStatus.Leader;
        }

        public async Task<DemotionStatus> TryDemotion()
        {
            var leaderPath = ByteString.CopyFromUtf8(EtcdClusterRoot + _dhafInternalConfig.Etcd.LeaderPath);
            var isCurrentNodeLeader = new Compare
            {
                Key = leaderPath,
                Value = ByteString.CopyFromUtf8(_clusterConfig.Dhaf.NodeName),
                Target = Compare.Types.CompareTarget.Value,
                Result = Compare.Types.CompareResult.Equal
            };

            var txnRequest = new TxnRequest();
            txnRequest.Compare.Add(isCurrentNodeLeader);
            txnRequest.Success.Add(new RequestOp()
            {
                RequestDeleteRange = new DeleteRangeRequest { Key = leaderPath }
            });

            var txnResponse = await _etcdClient.TransactionAsync(txnRequest);

            if (txnResponse.Succeeded)
            {
                return new DemotionStatus { Success = true };
            }
            else
            {
                var leader = await GetLeaderOrDefault();
                return new DemotionStatus { Success = false, Leader = leader };
            }
        }

        public async Task<PromotionStatus> TryPromotion()
        {
            const int EMPTY_CREATE_REVISION = 0;

            var lease = await _etcdClient.LeaseGrantAsync(
                new LeaseGrantRequest
                {
                    TTL = _clusterConfig.Etcd.LeaderKeyTtl ?? _dhafInternalConfig.Etcd.DefLeaderKeyTtl
                });

            var putRequest = new PutRequest
            {
                Key = ByteString.CopyFromUtf8(EtcdClusterRoot + _dhafInternalConfig.Etcd.LeaderPath),
                Value = ByteString.CopyFromUtf8(_clusterConfig.Dhaf.NodeName),
                Lease = lease.ID,
            };

            var isLeaderKeyDoesNotExist = new Compare
            {
                CreateRevision = EMPTY_CREATE_REVISION,
                Key = ByteString.CopyFromUtf8(EtcdClusterRoot + _dhafInternalConfig.Etcd.LeaderPath),

            };

            var txnRequest = new TxnRequest();
            txnRequest.Compare.Add(isLeaderKeyDoesNotExist);
            txnRequest.Success.Add(new RequestOp()
            {
                RequestPut = putRequest,
            });

            var txnResponse = await _etcdClient.TransactionAsync(txnRequest);

            if (txnResponse.Succeeded)
            {
                return new PromotionStatus
                {
                    Success = true,
                    Leader = _clusterConfig.Dhaf.NodeName,
                    LeaderLeaseId = lease.ID
                };
            }
            else
            {
                await _etcdClient.LeaseRevokeAsync(new LeaseRevokeRequest { ID = lease.ID });
                var leader = await GetLeaderOrDefault();

                return new PromotionStatus { Success = false, Leader = leader };
            }
        }

        public async Task HeartbeatWithInterval()
        {
            var interval = _clusterConfig.Dhaf.HeartbeatInterval ?? _dhafInternalConfig.DefHeartbeatInterval;

            while (!_backgroundTasks.HeartbeatWithIntervalCts.IsCancellationRequested)
            {
                await Heartbeat();
                await Task.Delay(TimeSpan.FromSeconds(interval));
            }
        }

        public async Task Heartbeat()
        {
            if (Role == DhafNodeRole.Leader)
            {
                await _etcdClient.LeaseKeepAlive(new LeaseKeepAliveRequest
                {
                    ID = LeaderLeaseId.Value
                }, (lkaResp) => { }, CancellationToken.None);

                Console.WriteLine($"[{ _clusterConfig.Dhaf.ClusterName}/{ _clusterConfig.Dhaf.NodeName}] The leader key with lease ID {LeaderLeaseId.Value} is kept alive.");
            }

            var key = EtcdClusterRoot
                + _dhafInternalConfig.Etcd.NodesPath
                + _clusterConfig.Dhaf.NodeName;

            var heartbeatTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            var nodeStatus = new EtcdNodeStatus { LastHeartbeatTimestamp = heartbeatTimestamp };
            string content = JsonSerializer.Serialize(nodeStatus, DhafInternalConfig.JsonSerializerOptions);

            var putRequest = new PutRequest
            {
                Key = ByteString.CopyFromUtf8(key),
                Value = ByteString.CopyFromUtf8(content)
            };

            await _etcdClient.PutAsync(putRequest);
            Console.WriteLine($"[{ _clusterConfig.Dhaf.ClusterName}/{ _clusterConfig.Dhaf.NodeName}] Heartbeat *knock-knock*.");
        }

        public async Task Shutdown()
        {
            Console.WriteLine($"[{ _clusterConfig.Dhaf.ClusterName}/{ _clusterConfig.Dhaf.NodeName}] Shutdown requested...");

            _backgroundTasks.FetchDhafNodeStatusesWithIntervalCts.Cancel();
            _backgroundTasks.HeartbeatWithIntervalCts.Cancel();

            // To be sure that an asynchronous heartbeat does not happen during node shutdown.
            await _backgroundTasks.HeartbeatWithIntervalTask;

            if (Role == DhafNodeRole.Leader)
            {
                await TryDemotion();
            }

            var nodeKey = EtcdClusterRoot
                + _dhafInternalConfig.Etcd.NodesPath
                + _clusterConfig.Dhaf.NodeName;

            var shutdownKey = EtcdClusterRoot
                + _dhafInternalConfig.Etcd.ShutdownsPath
                + _clusterConfig.Dhaf.NodeName;

            await _etcdClient.DeleteAsync(nodeKey);
            await _etcdClient.DeleteAsync(shutdownKey);

            Console.WriteLine("* Dhaf node exit...");
            Environment.Exit(0);
        }

        public async Task NetworkConfigurationsHealthCheck()
        {
            foreach (var host in _clusterConfig.Service.Hosts)
            {
                Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] Check host <{host.Name}>...");

                var opt = new HealthCheckerCheckOptions { HostName = host.Name };
                await _healthChecker.Check(opt);
            }

            Console.WriteLine("The health of the service's hosts has been checked.");
        }

        public async Task<IEnumerable<NetworkConfigurationStatus>> InspectResultsOfNetworkConfigurationsHealthCheck()
        {
            throw new NotImplementedException();
        }

        public async Task FetchDhafNodeStatusesWithInterval()
        {
            var interval = _clusterConfig.Dhaf.FetchDhafNodeStatusesInterval
                ?? _dhafInternalConfig.DefFetchDhafNodeStatusesInterval;

            while (Role == DhafNodeRole.Leader
                && !_backgroundTasks.FetchDhafNodeStatusesWithIntervalCts.IsCancellationRequested)
            {
                await FetchDhafNodeStatuses();
                await Task.Delay(TimeSpan.FromSeconds(interval));
            }
        }

        public async Task FetchDhafNodeStatuses()
        {
            var keyPrefix = EtcdClusterRoot
                + _dhafInternalConfig.Etcd.NodesPath;

            var values = await _etcdClient.GetRangeValAsync(keyPrefix);
            var entities = values.ToDictionary(k => k.Key,
                v => JsonSerializer.Deserialize<EtcdNodeStatus>(v.Value, DhafInternalConfig.JsonSerializerOptions));

            EtcdNodeStatuses = entities;
        }

        public async Task<DecisionOfNetworkConfigurationSwitching> IsAutoSwitchingOfNetworkConfigurationRequired()
        {
            throw new NotImplementedException();
        }

        public async Task<DecisionOfNetworkConfigurationSwitching> IsManualSwitchingOfNetworkConfigurationRequired()
        {
            throw new NotImplementedException();
        }

        public async Task TactWithInterval()
        {
            var interval = _clusterConfig.Dhaf.TactInterval ?? _dhafInternalConfig.DefTactInterval;

            while (true)
            {
                await Tact();
                await Task.Delay(TimeSpan.FromSeconds(interval));
            }
        }

        public async Task Tact()
        {
            if (Role == DhafNodeRole.Follower)
            {
                var leader = await GetLeaderOrDefault();

                if (leader == _clusterConfig.Dhaf.NodeName)
                {
                    // We are leaders, but for some reason we don't know about it.
                    // This is not normal.

                    Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] WARN: Mismatch between the role of the current node in the local (follower) and remote (leader) storages. Try demotion...");

                    var demotionStatus = await TryDemotion();
                    if (demotionStatus.Success)
                    {
                        leader = null;
                    }
                    else
                    {
                        // Someone has already taken the leader's seat.
                        leader = demotionStatus.Leader;
                    }
                }

                if (leader == null)
                {
                    await ParticipateInLeaderElection();
                }

                if (leader != null && LastKnownLeader != leader)
                {
                    Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] {leader} is now the leader.");
                    LastKnownLeader = leader;
                }
            }

            await NetworkConfigurationsHealthCheck();

            if (Role == DhafNodeRole.Leader)
            {
                // Произвести инспекцию всех чекеров здоровья СЕРВИСА (не то что выше!)...
                // Результатов должно быть не меньше, чем 50%+1 ЗДОРОВЫХ узлов кластера.
            }

            var isShutdownRequested = await IsShutdownRequested();
            if (isShutdownRequested)
            {
                await Shutdown();
            }
        }

        public async Task<bool> IsShutdownRequested()
        {
            var key = EtcdClusterRoot
                + _dhafInternalConfig.Etcd.ShutdownsPath
                + _clusterConfig.Dhaf.NodeName;

            var value = await _etcdClient.GetValAsync(key);

            return value == _clusterConfig.Dhaf.NodeName;
        }
    }

    /// <summary>
    /// Background tasks that should not be expected to complete via await. These are infinite loops.
    /// </summary>
    public class DhafNodeBackgroundTasks
    {
        public Task HeartbeatWithIntervalTask { get; set; }
        public CancellationTokenSource HeartbeatWithIntervalCts { get; set; }
            = new CancellationTokenSource();

        public Task FetchDhafNodeStatusesWithIntervalTask { get; set; }
        public CancellationTokenSource FetchDhafNodeStatusesWithIntervalCts { get; set; }
            = new CancellationTokenSource();
    }
}
