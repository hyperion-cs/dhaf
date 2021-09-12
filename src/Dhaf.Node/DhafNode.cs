using Dhaf.Core;
using dotnet_etcd;
using Etcdserverpb;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
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
        Task TactWithInterval();

        Task<ServiceStatus> GetServiceStatus();
        Task<DhafStatus> GetDhafClusterStatus();

        Task DecommissionDhafNode(string name);

        Task Switchover(string ncId);
        Task PurgeSwitchover();
        Task<IEnumerable<SwitchoverCandidate>> GetSwitchoverCandidates();
    }

    public class DhafNode : IDhafNode
    {
        private readonly EtcdClient _etcdClient;
        private readonly DhafNodeBackgroundTasks _backgroundTasks;
        private readonly ISwitcher _switcher;
        private readonly IHealthChecker _healthChecker;
        private readonly ILogger<IDhafNode> _logger;

        /// <summary>
        /// The current role of the dhaf node.
        /// </summary>
        protected DhafNodeRole _role = DhafNodeRole.Follower;

        /// <summary>
        /// The flag shows that all NC is unhealthy.
        /// The service is DOWN and dhaf is physically unable to fix the situation on its own.
        /// </summary>
        protected bool _panicMode = false;

        protected string _switchoverLastRequirement = null;

        /// <summary>
        /// The etcd lease identifier for the leader key.
        /// </summary>
        protected long? _leaderLeaseId;

        protected string _lastKnownLeader;
        protected string _currentNetworkConfigurationId;

        protected ClusterConfig _clusterConfig;
        protected DhafInternalConfig _dhafInternalConfig;

        protected Dictionary<string, EtcdNodeStatus> _dhafNodeStatuses = new();
        protected IEnumerable<NetworkConfigurationStatus> _networkConfigurationStatuses;

        protected string _etcdClusterRoot { get => $"/{_clusterConfig.Dhaf.ClusterName}/"; }

        public DhafNode(ClusterConfig clusterConfig,
            DhafInternalConfig dhafInternalConfig,
            ISwitcher switcher,
            IHealthChecker healthChecker,
            ILogger<IDhafNode> logger)
        {
            _clusterConfig = clusterConfig;
            _dhafInternalConfig = dhafInternalConfig;

            _healthChecker = healthChecker;
            _switcher = switcher;

            _logger = logger;

            _etcdClient = new EtcdClient(_clusterConfig.Etcd.Hosts);
            _backgroundTasks = new DhafNodeBackgroundTasks();

            // TODO: Transfer to the cluster configuration parser.
            if ((_clusterConfig.Etcd.LeaderKeyTtl ?? _dhafInternalConfig.Etcd.DefLeaderKeyTtl)
                <= (_clusterConfig.Dhaf.HeartbeatInterval ?? _dhafInternalConfig.DefHeartbeatInterval))
            {
                var err = "The TTL of the leader key in the ETCD must be greater than the heartbeat interval of the Dhaf node.";

                _logger.LogCritical(err);
                throw new ArgumentException(err);
            }

            _backgroundTasks.HeartbeatWithIntervalTask = HeartbeatWithInterval();
        }

        public async Task<ServiceStatus> GetServiceStatus()
        {
            var switchoverRequirement = await GetSwitchoverRequirementOrDefault();

            var healthy = _networkConfigurationStatuses
                .Where(x => x.Healthy)
                .Select(x => x.NcId)
                .ToHashSet();

            var ncs = _clusterConfig.Service.NetworkConfigurations
                .Select((nc, i) => new ServiceNcStatus
                {
                    Name = nc.Id,
                    Priority = i + 1,
                    Healthy = healthy.Contains(nc.Id)
                });

            return new ServiceStatus
            {
                Domain = _clusterConfig.Service.Domain,
                CurrentNcName = _currentNetworkConfigurationId,
                NetworkConfigurations = ncs,
                SwitchoverRequirement = switchoverRequirement
            };
        }

        public async Task<DhafStatus> GetDhafClusterStatus()
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            var nodeStatuses = _dhafNodeStatuses
                .Select(x => new DhafNodeStatus
                {
                    Name = x.Key,
                    Healthy = (timestamp - x.Value.LastHeartbeatTimestamp) <= _dhafInternalConfig.DefHealthyNodeStatusTtl
                });

            return new DhafStatus { Leader = _lastKnownLeader, Nodes = nodeStatuses };
        }

        public async Task DecommissionDhafNode(string name)
        {
            var dhafStatus = await GetDhafClusterStatus();
            var node = dhafStatus.Nodes.FirstOrDefault(x => x.Name == name);

            if (node is null)
            {
                throw new RestApiException(1201, "There is no dhaf node with this name.");
            }

            if (node.Healthy)
            {
                throw new RestApiException(1202, "You can't decommission a healthy node. Turn it off and try again.");
            }

            var nodeStatusPrefix = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.NodesPath;

            var serviceStatusPrefix = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.HealthPath;

            await _etcdClient.DeleteAsync(nodeStatusPrefix + node.Name);
            await _etcdClient.DeleteRangeAsync(serviceStatusPrefix + node.Name);
        }

        public async Task Switchover(string ncId)
        {
            var ncStatus = _networkConfigurationStatuses.FirstOrDefault(x => x.NcId == ncId);

            if (ncStatus == null)
            {
                throw new RestApiException(1101, "There is no network configuration with this ID.");
            }

            if (!ncStatus.Healthy)
            {
                throw new RestApiException(1102, "Cannot switchover to the specified network configuration because it is unhealthy.");
            }

            await PurgeSwitchover();

            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.SwitchoverPath;

            var entity = new EtcdManualSwitching
            {
                NCId = ncId,
                DhafNode = _clusterConfig.Dhaf.NodeName
            };

            var value = JsonSerializer.Serialize(entity, DhafInternalConfig.JsonSerializerOptions);
            await _etcdClient.PutAsync(key, value);
        }

        public async Task PurgeSwitchover()
        {
            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.SwitchoverPath;

            await _etcdClient.DeleteAsync(key);
        }

        public async Task<IEnumerable<SwitchoverCandidate>> GetSwitchoverCandidates()
        {
            var healthyNodes = _networkConfigurationStatuses
                .Where(x => x.Healthy)
                .Select(x => x.NcId);

            var candidates = _clusterConfig.Service.NetworkConfigurations
                .Select((nc, i) => new SwitchoverCandidate { Name = nc.Id, Priority = i + 1 })
                .Where(nc => healthyNodes.Contains(nc.Name));

            return candidates;
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

        /// <summary>
        /// The dhaf cluster node tact, which does all the necessary things from the node lifecycle.
        /// </summary>
        protected async Task Tact()
        {
            _logger.LogTrace("Tact has begun.");

            if (_role == DhafNodeRole.Leader)
            {
                _logger.LogDebug($"NC is <{_currentNetworkConfigurationId}>.");
            }

            if (_role == DhafNodeRole.Follower)
            {
                var leader = await GetLeaderOrDefault();

                if (leader == _clusterConfig.Dhaf.NodeName)
                {
                    // We are leaders, but for some reason we don't know about it.
                    // This is not normal.

                    _logger.LogWarning("Mismatch between the role of the current node in the local (follower) and remote (leader) storages. Try demotion...");

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
                    _logger.LogInformation($"<{leader}> is now the leader.");
                    _lastKnownLeader = leader;
                }
            }

            await FetchDhafNodeStatuses();
            await NetworkConfigurationsHealthCheck();

            // The following task need to be performed NOT ONLY by the leader
            // in order for the CLI/REST API to work fully.
            _networkConfigurationStatuses = await InspectResultsOfNetworkConfigurationsHealthCheck();

            if (_role == DhafNodeRole.Leader)
            {
                var panicModeChanges = await UpdatePanicModeRelevance();
                if (panicModeChanges.HasStatusChanged)
                {
                    if (_panicMode)
                    {
                        _logger.LogError("Panic mode is ON. All NCs are unhealthy. The service is DOWN and dhaf is physically unable to fix the situation on its own.");
                    }
                    else
                    {
                        _logger.LogInformation("Panic mode is OFF.");
                    }
                }

                _currentNetworkConfigurationId = await _switcher.GetCurrentNetworkConfigurationId();
                var autoSwitch = await IsAutoSwitchingOfNetworkConfigurationRequired();
                var manualSwitch = await IsManualSwitchingOfNetworkConfigurationRequired();

                var mustSwitchover = manualSwitch.IsRequired && !autoSwitch.Failover;

                var switchoverRequirement = await GetSwitchoverRequirementOrDefault();

                if (!mustSwitchover && switchoverRequirement != _switchoverLastRequirement)
                {
                    if (switchoverRequirement is null)
                    {
                        _logger.LogInformation("The switchover requirement are purged.");
                    }
                    else
                    {
                        _logger.LogInformation($"There is switchover requirement to <{switchoverRequirement}>. No action is taken because the current configuration is already so.");
                    }
                }

                _switchoverLastRequirement = switchoverRequirement;

                var mustAutoSwitchover = autoSwitch.IsRequired
                    && !autoSwitch.Failover && switchoverRequirement == null;

                if (autoSwitch.Failover)
                {
                    _logger.LogWarning($"Current NC <{_currentNetworkConfigurationId}> is DOWN. A failover has been started...");

                    if (switchoverRequirement != null)
                    {
                        _logger.LogWarning("Purge switchover requirement because failover is required...");
                        await PurgeSwitchover();
                    }

                    await _switcher.Switch(new SwitcherSwitchOptions
                    {
                        NcId = autoSwitch.SwitchTo,
                        Failover = autoSwitch.Failover
                    });
                }
                if (mustSwitchover)
                {
                    var proposedHost = _networkConfigurationStatuses
                        .FirstOrDefault(x => x.NcId == manualSwitch.SwitchTo);

                    if (proposedHost.Healthy)
                    {
                        _logger.LogInformation("Switchover is requested...");
                        await _switcher.Switch(new SwitcherSwitchOptions
                        {
                            NcId = manualSwitch.SwitchTo,
                            Failover = manualSwitch.Failover
                        });
                    }
                    else
                    {
                        _logger.LogError($"Swithover is requested but it is not possible because the specified network configuration <{manualSwitch.SwitchTo}> is unhealthy. The requirement to switchover will be purged.");

                        await PurgeSwitchover();
                        mustAutoSwitchover = autoSwitch.IsRequired && !autoSwitch.Failover;
                    }
                }

                if (mustAutoSwitchover)
                {
                    _logger.LogInformation($"Switching to a higher priority healthy NC <{autoSwitch.SwitchTo}>...");

                    await _switcher.Switch(new SwitcherSwitchOptions
                    {
                        NcId = autoSwitch.SwitchTo,
                        Failover = autoSwitch.Failover
                    });
                }
            }

            _currentNetworkConfigurationId = await _switcher.GetCurrentNetworkConfigurationId();
            _logger.LogTrace("Tact is over.");
            _logger.LogDebug($"NC is <{_currentNetworkConfigurationId}>.");
        }

        /// <summary>Checks if a cluster leader exists.</summary>
        /// <returns>The name of the leader node or null if the cluster leader does not exist.</returns>
        protected async Task<string> GetLeaderOrDefault()
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
            _logger.LogWarning("There is no leader. Participating in the election...");

            var promotionStatus = await TryPromotion();

            if (promotionStatus.Success)
            {
                _role = DhafNodeRole.Leader;
                _leaderLeaseId = promotionStatus.LeaderLeaseId;
                _logger.LogInformation("I'm a leader now.");
            }
            else
            {
                _role = DhafNodeRole.Follower;
                _logger.LogInformation($"I'm a follower now, <{promotionStatus.Leader}> is the leader.");
            }

            _lastKnownLeader = promotionStatus.Leader;
        }

        protected async Task<DemotionStatus> TryDemotion()
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

        /// <summary>To try to become a cluster leader.</summary>
        protected async Task<PromotionStatus> TryPromotion()
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

        protected async Task HeartbeatWithInterval()
        {
            var interval = _clusterConfig.Dhaf.HeartbeatInterval ?? _dhafInternalConfig.DefHeartbeatInterval;

            while (!_backgroundTasks.HeartbeatWithIntervalCts.IsCancellationRequested)
            {
                await Heartbeat();
                await Task.Delay(TimeSpan.FromSeconds(interval));
            }
        }

        /// <summary>Tell the cluster that the current dhaf node is healthy.</summary>
        protected async Task Heartbeat()
        {
            if (_role == DhafNodeRole.Leader)
            {
                await _etcdClient.LeaseKeepAlive(new LeaseKeepAliveRequest
                {
                    ID = _leaderLeaseId.Value
                }, (lkaResp) => { }, CancellationToken.None);

                _logger.LogTrace($"The leader key with lease ID {_leaderLeaseId.Value} is kept alive.");
            }

            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.NodesPath
                + _clusterConfig.Dhaf.NodeName;

            var heartbeatTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            var nodeStatus = new EtcdNodeStatus { LastHeartbeatTimestamp = heartbeatTimestamp };
            var content = JsonSerializer.Serialize(nodeStatus, DhafInternalConfig.JsonSerializerOptions);

            await _etcdClient.PutAsync(key, content);
            _logger.LogTrace("Heartbeat *knock-knock*.");
        }

        /// <summary>
        /// Checks the health of all network configurations (both active and others).
        /// The result (as a set of votes) is sent to DCS.
        /// </summary>
        protected async Task NetworkConfigurationsHealthCheck()
        {
            var tasks = _clusterConfig.Service
                .NetworkConfigurations
                .Select(x => NetworkConfigurationHealthCheck(x));

            await Task.WhenAll(tasks);

            _logger.LogTrace("The health of the service's hosts has been checked.");
        }

        protected async Task NetworkConfigurationHealthCheck(ClusterServiceNetworkConfig nc)
        {
            _logger.LogTrace($"Check NC <{nc.Id}>...");

            var status = await _healthChecker.Check(new HealthCheckerCheckOptions { NcId = nc.Id });

            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.HealthPath
                + _clusterConfig.Dhaf.NodeName + "/"
                + nc.Id;

            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var serviceHealth = new EtcdServiceHealth { Timestamp = timestamp, Healthy = status.Healthy };
            var value = JsonSerializer.Serialize(serviceHealth, DhafInternalConfig.JsonSerializerOptions);

            await _etcdClient.PutAsync(key, value);

            if (status.Healthy)
            {
                _logger.LogInformation($"NC <{nc.Id}> status: Healthy :)");
            }
            else
            {
                _logger.LogWarning($"NC <{nc.Id}> status: Unhealthy.");
            }
        }

        /// <summary>
        /// Inspects the results of network configuration health checks from all active nodes in the dhaf cluster.
        /// A vote determines the health of each configuration by majority vote.
        /// </summary>
        /// <returns>The status of each network configuration.</returns>
        protected async Task<IEnumerable<NetworkConfigurationStatus>>
            InspectResultsOfNetworkConfigurationsHealthCheck()
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var healthyNodes = _dhafNodeStatuses
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
                var ncStatus = new NetworkConfigurationStatus { NcId = hostHealthOpinions.Key };

                var positiveOpinons = hostHealthOpinions.Value.Count(x => x.Healthy);
                if (positiveOpinons >= healthyNodesMostCount)
                {
                    ncStatus.Healthy = true;
                }

                result.Add(ncStatus);
            }

            return result;
        }

        /// <summary>
        /// If any of the nodes have not sent a heartbeat illegally for too long,
        /// it will be marked as unhealthy in DCS.
        /// </summary>
        protected async Task FetchDhafNodeStatuses()
        {
            var keyPrefix = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.NodesPath;

            var values = await _etcdClient.GetRangeValAsync(keyPrefix);
            var entities = values.ToDictionary(k => Path.GetFileName(k.Key),
                v => JsonSerializer.Deserialize<EtcdNodeStatus>(v.Value, DhafInternalConfig.JsonSerializerOptions));

            _dhafNodeStatuses = entities;
        }

        protected async Task<UpdatePanicModeRelevanceResult> UpdatePanicModeRelevance()
        {
            var oldValue = _panicMode;
            var isAnyNcAvailable = _networkConfigurationStatuses
                .Any(x => x.Healthy);

            _panicMode = !isAnyNcAvailable;

            return new UpdatePanicModeRelevanceResult
            {
                HasStatusChanged = _panicMode != oldValue
            };
        }

        /// <summary>
        /// Determines whether the network configuration should be switched automatically.
        /// This is not always failover (e.g. switching to a higher priority network configuration
        /// which has become healthy again).
        /// </summary>
        protected async Task<DecisionOfNetworkConfigurationSwitching>
            IsAutoSwitchingOfNetworkConfigurationRequired()
        {
            var negativeDecision = new DecisionOfNetworkConfigurationSwitching
            {
                Failover = false,
                IsRequired = false
            };

            var priorityNetConf = _networkConfigurationStatuses.FirstOrDefault(x => x.Healthy);
            if (_panicMode || priorityNetConf == null)
            {
                return negativeDecision;
            }

            var currentNetConf = _networkConfigurationStatuses
                .FirstOrDefault(x => x.NcId == _currentNetworkConfigurationId);

            if (!currentNetConf.Healthy)
            {
                return new DecisionOfNetworkConfigurationSwitching
                {
                    Failover = true,
                    IsRequired = true,
                    SwitchTo = priorityNetConf.NcId
                };
            }

            if (priorityNetConf.NcId != currentNetConf.NcId)
            {
                return new DecisionOfNetworkConfigurationSwitching
                {
                    Failover = false,
                    IsRequired = true,
                    SwitchTo = priorityNetConf.NcId
                };
            }

            return negativeDecision;
        }

        protected async Task<DecisionOfNetworkConfigurationSwitching>
            IsManualSwitchingOfNetworkConfigurationRequired()
        {
            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.SwitchoverPath;

            var rawValue = await _etcdClient.GetValAsync(key);

            if (!string.IsNullOrEmpty(rawValue))
            {
                var value = JsonSerializer.Deserialize<EtcdManualSwitching>(rawValue, DhafInternalConfig.JsonSerializerOptions);

                if (_currentNetworkConfigurationId != value.NCId)
                {

                    return new DecisionOfNetworkConfigurationSwitching
                    {
                        Failover = false,
                        IsRequired = true,
                        SwitchTo = value.NCId
                    };
                }
            }

            return new DecisionOfNetworkConfigurationSwitching { Failover = false, IsRequired = false };
        }

        protected async Task<string> GetSwitchoverRequirementOrDefault()
        {
            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.SwitchoverPath;

            var rawValue = await _etcdClient.GetValAsync(key);

            if (string.IsNullOrEmpty(rawValue))
            {
                return null;
            }

            var value = JsonSerializer.Deserialize<EtcdManualSwitching>(rawValue, DhafInternalConfig.JsonSerializerOptions);

            return value.NCId;
        }

        /// <summary>
        /// Background tasks that should not be expected to complete via await. These are infinite loops.
        /// </summary>
        public class DhafNodeBackgroundTasks
        {
            public Task HeartbeatWithIntervalTask { get; set; }
            public CancellationTokenSource HeartbeatWithIntervalCts { get; set; }
                = new CancellationTokenSource();
        }
    }
}
