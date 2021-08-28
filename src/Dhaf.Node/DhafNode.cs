﻿using Dhaf.Core;
using dotnet_etcd;
using Etcdserverpb;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.IO;
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
        protected DhafNodeRole _role = DhafNodeRole.Follower;

        /// <summary>
        /// The etcd lease identifier for the leader key.
        /// </summary>
        protected long? _leaderLeaseId;
        protected string _lastKnownLeader;
        protected string _currentNetworkConfigurationId;

        protected ClusterConfig _clusterConfig;
        protected DhafInternalConfig _dhafInternalConfig;

        protected string _etcdClusterRoot { get => $"/{_clusterConfig.Dhaf.ClusterName}/"; }

        protected Dictionary<string, EtcdNodeStatus> _etcdNodeStatuses
            = new Dictionary<string, EtcdNodeStatus>();

        protected IEnumerable<NetworkConfigurationStatus> _networkConfigurationStatuses;

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
            var leader = await _etcdClient.GetValAsync(_etcdClusterRoot + _dhafInternalConfig.Etcd.LeaderPath);

            if (string.IsNullOrEmpty(leader))
            {
                return null;
            }

            return leader;
        }

        protected async Task ParticipateInLeaderElection()
        {
            _role = DhafNodeRole.Candidate;
            Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] There is no leader. Participating in the election...");
            var promotionStatus = await TryPromotion();

            if (promotionStatus.Success)
            {
                _role = DhafNodeRole.Leader;
                _leaderLeaseId = promotionStatus.LeaderLeaseId;

                _backgroundTasks.FetchDhafNodeStatusesWithIntervalTask = FetchDhafNodeStatusesWithInterval();

                Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] I'm a leader now.");
            }
            else
            {
                _role = DhafNodeRole.Follower;
                Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] {promotionStatus.Leader} is now the leader.");
            }

            _lastKnownLeader = promotionStatus.Leader;
        }

        public async Task<DemotionStatus> TryDemotion()
        {
            var leaderPath = ByteString.CopyFromUtf8(_etcdClusterRoot + _dhafInternalConfig.Etcd.LeaderPath);
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
                Key = ByteString.CopyFromUtf8(_etcdClusterRoot + _dhafInternalConfig.Etcd.LeaderPath),
                Value = ByteString.CopyFromUtf8(_clusterConfig.Dhaf.NodeName),
                Lease = lease.ID,
            };

            var isLeaderKeyDoesNotExist = new Compare
            {
                CreateRevision = EMPTY_CREATE_REVISION,
                Key = ByteString.CopyFromUtf8(_etcdClusterRoot + _dhafInternalConfig.Etcd.LeaderPath),

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
            if (_role == DhafNodeRole.Leader)
            {
                await _etcdClient.LeaseKeepAlive(new LeaseKeepAliveRequest
                {
                    ID = _leaderLeaseId.Value
                }, (lkaResp) => { }, CancellationToken.None);

                Console.WriteLine($"[{ _clusterConfig.Dhaf.ClusterName}/{ _clusterConfig.Dhaf.NodeName}] The leader key with lease ID {_leaderLeaseId.Value} is kept alive.");
            }

            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.NodesPath
                + _clusterConfig.Dhaf.NodeName;

            var heartbeatTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            var nodeStatus = new EtcdNodeStatus { LastHeartbeatTimestamp = heartbeatTimestamp };
            var content = JsonSerializer.Serialize(nodeStatus, DhafInternalConfig.JsonSerializerOptions);

            await _etcdClient.PutAsync(key, content);
            Console.WriteLine($"[{ _clusterConfig.Dhaf.ClusterName}/{ _clusterConfig.Dhaf.NodeName}] Heartbeat *knock-knock*.");
        }

        public async Task Shutdown()
        {
            Console.WriteLine($"[{ _clusterConfig.Dhaf.ClusterName}/{ _clusterConfig.Dhaf.NodeName}] Shutdown requested...");

            _backgroundTasks.FetchDhafNodeStatusesWithIntervalCts.Cancel();
            _backgroundTasks.HeartbeatWithIntervalCts.Cancel();

            // To be sure that an asynchronous heartbeat does not happen during node shutdown.
            await _backgroundTasks.HeartbeatWithIntervalTask;

            if (_role == DhafNodeRole.Leader)
            {
                await TryDemotion();
            }

            var nodeKey = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.NodesPath
                + _clusterConfig.Dhaf.NodeName;

            var shutdownKey = _etcdClusterRoot
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
                Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] Check host <{host.Id}>...");

                var status = await _healthChecker.Check(new HealthCheckerCheckOptions { HostId = host.Id });

                var key = _etcdClusterRoot
                    + _dhafInternalConfig.Etcd.HealthPath
                    + _clusterConfig.Dhaf.NodeName + "/"
                    + host.Id;

                var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                var serviceHealth = new EtcdServiceHealth { Timestamp = timestamp, Healthy = status.Healthy };
                var value = JsonSerializer.Serialize(serviceHealth, DhafInternalConfig.JsonSerializerOptions);

                await _etcdClient.PutAsync(key, value);
            }

            Console.WriteLine("The health of the service's hosts has been checked.");
        }

        public async Task<IEnumerable<NetworkConfigurationStatus>> InspectResultsOfNetworkConfigurationsHealthCheck()
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var healthyNodes = _etcdNodeStatuses
                .Where(x => (timestamp - x.Value.LastHeartbeatTimestamp)
                                <= _dhafInternalConfig.DefHealthyNodeStatusTtl)
                .Select(x => x.Key);

            var hostsHealthOpinions = new Dictionary<string, List<EtcdServiceHealth>>();

            foreach (var node in healthyNodes)
            {
                var keyPrefix = _etcdClusterRoot
                    + _dhafInternalConfig.Etcd.HealthPath
                    + node + "/";

                var items = await _etcdClient.GetRangeValAsync(keyPrefix);

                foreach (var item in items)
                {
                    var hostId = Path.GetFileName(item.Key);
                    var value = JsonSerializer.Deserialize<EtcdServiceHealth>(item.Value, DhafInternalConfig.JsonSerializerOptions);

                    if (!hostsHealthOpinions.ContainsKey(hostId))
                    {
                        hostsHealthOpinions[hostId] = new List<EtcdServiceHealth>();
                    }

                    hostsHealthOpinions[hostId].Add(value);
                }
            }

            var healthyNodesTotalCount = healthyNodes.Count();
            var healthyNodesMostCount = (healthyNodesTotalCount / 2) + 1; // Majority formula: 50% + 1.

            var result = new List<NetworkConfigurationStatus>();

            foreach (var hostHealthOpinions in hostsHealthOpinions)
            {
                var ncStatus = new NetworkConfigurationStatus { HostId = hostHealthOpinions.Key };

                var positiveOpinons = hostHealthOpinions.Value.Count(x => x.Healthy);
                if (positiveOpinons >= healthyNodesMostCount)
                {
                    ncStatus.Healthy = true;
                }

                result.Add(ncStatus);
            }

            return result;
        }

        public async Task FetchDhafNodeStatusesWithInterval()
        {
            var interval = _clusterConfig.Dhaf.FetchDhafNodeStatusesInterval
                ?? _dhafInternalConfig.DefFetchDhafNodeStatusesInterval;

            while (_role == DhafNodeRole.Leader
                && !_backgroundTasks.FetchDhafNodeStatusesWithIntervalCts.IsCancellationRequested)
            {
                await FetchDhafNodeStatuses();
                await Task.Delay(TimeSpan.FromSeconds(interval));
            }
        }

        public async Task FetchDhafNodeStatuses()
        {
            var keyPrefix = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.NodesPath;

            var values = await _etcdClient.GetRangeValAsync(keyPrefix);
            var entities = values.ToDictionary(k => Path.GetFileName(k.Key),
                v => JsonSerializer.Deserialize<EtcdNodeStatus>(v.Value, DhafInternalConfig.JsonSerializerOptions));

            _etcdNodeStatuses = entities;
        }

        /// <summary>
        /// Determines whether the network configuration should be switched automatically.
        /// This is not always failover (e.g. switching to a higher priority network configuration
        /// which has become healthy again).
        /// </summary>
        public async Task<DecisionOfNetworkConfigurationSwitching> IsAutoSwitchingOfNetworkConfigurationRequired()
        {
            var priorityNetConf = _networkConfigurationStatuses.FirstOrDefault(x => x.Healthy);
            var currentNetConf = _networkConfigurationStatuses.FirstOrDefault(x => x.HostId == _currentNetworkConfigurationId);

            if (!currentNetConf.Healthy)
            {
                return new DecisionOfNetworkConfigurationSwitching
                {
                    Failover = true,
                    IsRequired = true,
                    SwitchTo = priorityNetConf.HostId
                };
            }

            if (priorityNetConf.HostId != currentNetConf.HostId)
            {
                return new DecisionOfNetworkConfigurationSwitching
                {
                    Failover = false,
                    IsRequired = true,
                    SwitchTo = priorityNetConf.HostId
                };
            }

            return new DecisionOfNetworkConfigurationSwitching { Failover = false, IsRequired = false };
        }

        public async Task<DecisionOfNetworkConfigurationSwitching> IsManualSwitchingOfNetworkConfigurationRequired()
        {
            return new DecisionOfNetworkConfigurationSwitching { Failover = false, IsRequired = false };
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
            if (_role == DhafNodeRole.Follower)
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

                if (leader != null && _lastKnownLeader != leader)
                {
                    Console.WriteLine($"[{_clusterConfig.Dhaf.ClusterName}/{_clusterConfig.Dhaf.NodeName}] {leader} is now the leader.");
                    _lastKnownLeader = leader;
                }
            }

            await NetworkConfigurationsHealthCheck();

            if (_role == DhafNodeRole.Leader)
            {
                _networkConfigurationStatuses = await InspectResultsOfNetworkConfigurationsHealthCheck();
                _currentNetworkConfigurationId = await _switcher.GetCurrentNetworkConfigurationId();

                var autoSwitch = await IsAutoSwitchingOfNetworkConfigurationRequired();
                if (autoSwitch.IsRequired)
                {
                    await _switcher.Switch(new SwitcherSwitchOptions
                    {
                        HostId = autoSwitch.SwitchTo,
                        Failover = autoSwitch.Failover
                    });
                }
                else
                {
                    var manualSwitch = await IsManualSwitchingOfNetworkConfigurationRequired();
                    if (manualSwitch.IsRequired)
                    {
                        await _switcher.Switch(new SwitcherSwitchOptions
                        {
                            HostId = manualSwitch.SwitchTo,
                            Failover = manualSwitch.Failover
                        });
                    }
                }
            }

            var isShutdownRequested = await IsShutdownRequested();
            if (isShutdownRequested)
            {
                await Shutdown();
            }
        }

        public async Task<bool> IsShutdownRequested()
        {
            var key = _etcdClusterRoot
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
