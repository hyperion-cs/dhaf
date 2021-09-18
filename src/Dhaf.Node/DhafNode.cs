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

        Task<ServiceStatus> GetServiceStatus(string serviceName);
        Task<IEnumerable<ServiceStatus>> GetServicesStatus();
        Task<DhafStatus> GetDhafClusterStatus();

        Task DecommissionDhafNode(string name);

        Task Switchover(string serviceName, string ncId);
        Task PurgeSwitchover(string serviceName);
        Task<IEnumerable<SwitchoverCandidate>> GetSwitchoverCandidates(string serviceName);
    }

    public class DhafNode : IDhafNode
    {
        private readonly EtcdClient _etcdClient;
        private readonly DhafNodeBackgroundTasks _backgroundTasks;
        private readonly IEnumerable<INotifier> _notifiers;
        private readonly ILogger<IDhafNode> _logger;

        protected IEnumerable<DhafService> _services;

        /// <summary>
        /// The current role of the dhaf node.
        /// </summary>
        protected DhafNodeRole _role = DhafNodeRole.Follower;

        /// <summary>
        /// The etcd lease identifier for the leader key.
        /// </summary>
        protected long? _leaderLeaseId;

        protected string _lastKnownLeader;

        protected ClusterConfig _clusterConfig;
        protected DhafInternalConfig _dhafInternalConfig;

        protected Dictionary<string, DhafNodeStatus> _dhafNodeStatuses = new();
        protected Dictionary<string, DhafNodeStatus> _previousDhafNodeStatuses = new(); // For tracking changes.

        protected string _etcdClusterRoot { get => $"/{_clusterConfig.Dhaf.ClusterName}/"; }

        public DhafNode(ClusterConfig clusterConfig,
            DhafInternalConfig dhafInternalConfig,
            IEnumerable<DhafService> services,
            IEnumerable<INotifier> notifiers,
            EtcdClient etcdClient,
            ILogger<IDhafNode> logger)
        {
            _clusterConfig = clusterConfig;
            _dhafInternalConfig = dhafInternalConfig;

            _services = services;
            _notifiers = notifiers;

            _logger = logger;

            _etcdClient = etcdClient;
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

        public async Task<ServiceStatus> GetServiceStatus(string serviceName)
        {
            var service = _services.FirstOrDefault(x => x.Name == serviceName);
            if (service is null)
            {
                throw new RestApiException(1301, "No such service was found.");
            }

            var switchoverRequirement = await GetSwitchoverRequirementOrDefault(service);

            var healthy = service.NetworkConfigurationStatuses
                .Where(x => x.Healthy)
                .Select(x => x.NcId)
                .ToHashSet();

            var ncs = service.NetworkConfigurations
                .Select((nc, i) => new ServiceNcStatus
                {
                    Name = nc.Id,
                    Priority = i + 1,
                    Healthy = healthy.Contains(nc.Id)
                });

            return new ServiceStatus
            {
                Name = service.Name,
                Domain = service.Domain,
                CurrentNcName = service.CurrentNetworkConfigurationId,
                NetworkConfigurations = ncs,
                SwitchoverRequirement = switchoverRequirement
            };
        }

        public async Task<IEnumerable<ServiceStatus>> GetServicesStatus()
        {
            var getTasks = _services.Select(x => GetServiceStatus(x.Name));
            var serviceStatuses = await Task.WhenAll(getTasks);

            return serviceStatuses;
        }

        public async Task<DhafStatus> GetDhafClusterStatus()
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var nodeStatuses = _dhafNodeStatuses.Select(x => x.Value);

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

        public async Task Switchover(string serviceName, string ncId)
        {
            var service = _services.FirstOrDefault(x => x.Name == serviceName);
            if (service is null)
            {
                throw new RestApiException(1301, "No such service was found.");
            }

            var ncStatus = service.NetworkConfigurationStatuses.FirstOrDefault(x => x.NcId == ncId);
            if (ncStatus == null)
            {
                throw new RestApiException(1101, "There is no network configuration with this ID.");
            }

            if (!ncStatus.Healthy)
            {
                throw new RestApiException(1102, "Cannot switchover to the specified network configuration because it is unhealthy.");
            }

            await PurgeSwitchover(serviceName);

            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.SwitchoverPath + service.Name;

            var entity = new EtcdManualSwitching
            {
                NCId = ncId,
                DhafNode = _clusterConfig.Dhaf.NodeName
            };

            var value = JsonSerializer.Serialize(entity, DhafInternalConfig.JsonSerializerOptions);
            await _etcdClient.PutAsync(key, value);
        }

        public async Task PurgeSwitchover(string serviceName)
        {
            var service = _services.FirstOrDefault(x => x.Name == serviceName);
            if (service is null)
            {
                throw new RestApiException(1301, "No such service was found.");
            }

            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.SwitchoverPath + service.Name;

            await _etcdClient.DeleteAsync(key);
        }

        public async Task<IEnumerable<SwitchoverCandidate>> GetSwitchoverCandidates(string serviceName)
        {
            var service = _services.FirstOrDefault(x => x.Name == serviceName);
            if (service is null)
            {
                throw new RestApiException(1301, "No such service was found.");
            }

            var healthyNodes = service.NetworkConfigurationStatuses
                .Where(x => x.Healthy)
                .Select(x => x.NcId);

            var candidates = service.NetworkConfigurations
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
                foreach (var service in _services)
                {
                    _logger.LogDebug($"In service <{service.Name}> NC is <{service.CurrentNetworkConfigurationId}>.");
                }
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

            var ncHealthCheckTasks = _services.Select(x => NetworkConfigurationsHealthCheck(x));
            await Task.WhenAll(ncHealthCheckTasks);

            // The following task need to be performed NOT ONLY by the leader
            // in order for the CLI/REST API to work fully.

            var inspectNcTasks = _services.Select(async x =>
            {
                x.PreviousNetworkConfigurationStatuses = x.NetworkConfigurationStatuses;
                x.NetworkConfigurationStatuses = await InspectResultsOfNetworkConfigurationsHealthCheck(x);
            });

            await Task.WhenAll(inspectNcTasks);

            if (_role == DhafNodeRole.Leader)
            {
                var serviceMaintenanceTasks = _services.Select(x => ServiceMaintenance(x));
                await Task.WhenAll(serviceMaintenanceTasks);

                await NotifyChangesInDhafNodesHealth();
            }

            var updateCurrNcTasks = _services.Select(async x =>
            {
                x.CurrentNetworkConfigurationId = await x.Switcher.GetCurrentNetworkConfigurationId();
                _logger.LogDebug($"In service <{x.Name}> NC is <{x.CurrentNetworkConfigurationId}>.");
            });

            await Task.WhenAll(updateCurrNcTasks);

            _logger.LogTrace("Tact is over.");
        }

        protected async Task ServiceMaintenance(DhafService service)
        {
            var panicModeChanges = await UpdatePanicModeRelevance(service);
            if (panicModeChanges.HasStatusChanged)
            {
                if (service.PanicMode)
                {
                    _logger.LogError($"Panic mode in service <{service.Name}> is ON. All NCs are unhealthy. The service <{service.Name}> is DOWN and dhaf is physically unable to fix the situation on its own.");

                    var eventData = await GetBaseEventData<NotifierEventData.ServiceHealthChanged>(service.Name);
                    await PushToNotifiers(new NotifierPushOptions
                    {
                        Level = NotifierLevel.Critical,
                        Event = NotifierEvent.ServiceDown,
                        EventData = eventData
                    });
                }
                else
                {
                    _logger.LogInformation($"Panic mode in service <{service.Name}> is OFF.");

                    var eventData = await GetBaseEventData<NotifierEventData.ServiceHealthChanged>(service.Name);
                    await PushToNotifiers(new NotifierPushOptions
                    {
                        Level = NotifierLevel.Info,
                        Event = NotifierEvent.ServiceUp,
                        EventData = eventData
                    });
                }
            }

            service.CurrentNetworkConfigurationId = await service.Switcher.GetCurrentNetworkConfigurationId();

            var autoSwitchRequirement = await IsAutoSwitchOfNetworkConfigurationRequired(service);
            var switchoverRequirement = await IsSwitchoverOfNetworkConfigurationRequired(service);

            // It can be FALSE even if there is a requirement in the storage.
            // For example: the switchover requirement has already been completed.
            var mustSwitchover = switchoverRequirement.IsRequired && !autoSwitchRequirement.Failover;

            var switchoverRequirementInStorage = await GetSwitchoverRequirementOrDefault(service);

            if (!mustSwitchover && switchoverRequirementInStorage != service.SwitchoverLastRequirementInStorage)
            {
                if (switchoverRequirementInStorage is null)
                {
                    _logger.LogInformation($"The switchover requirement in service <{service.Name}> are purged.");

                    var eventData = await GetBaseEventData<NotifierEventData.SwitchoverPurged>(service.Name);
                    eventData.SwitchoverNc = service.SwitchoverLastRequirementInStorage;

                    await PushToNotifiers(new NotifierPushOptions
                    {
                        Level = NotifierLevel.Info,
                        Event = NotifierEvent.SwitchoverPurged,
                        EventData = eventData
                    });
                }
                else
                {
                    _logger.LogInformation($"There is switchover requirement to <{switchoverRequirementInStorage}> in service <{service.Name}>. No action is taken because the current configuration is already so.");
                }
            }

            service.SwitchoverLastRequirementInStorage = switchoverRequirementInStorage;

            var mustSwitching = autoSwitchRequirement.IsRequired
                && !autoSwitchRequirement.Failover && switchoverRequirementInStorage == null;

            if (autoSwitchRequirement.Failover)
            {
                _logger.LogWarning($"Current NC <{service.Name}/{service.CurrentNetworkConfigurationId}> is DOWN. A failover has been started...");

                if (switchoverRequirementInStorage != null)
                {
                    _logger.LogWarning($"Purge switchover requirement in service <{service.Name}> because failover is required...");
                    await PurgeSwitchover(service.Name);
                }

                await service.Switcher.Switch(new SwitcherSwitchOptions
                {
                    NcId = autoSwitchRequirement.SwitchTo,
                    Failover = autoSwitchRequirement.Failover
                });

                var eventData = await GetBaseEventData<NotifierEventData.CurrentNcChanged>(service.Name);
                eventData.FromNc = service.CurrentNetworkConfigurationId;
                eventData.ToNc = autoSwitchRequirement.SwitchTo;

                await PushToNotifiers(new NotifierPushOptions
                {
                    Level = NotifierLevel.Warning,
                    Event = NotifierEvent.Failover,
                    EventData = eventData
                });
            }
            if (mustSwitchover)
            {
                var proposedHost = service.NetworkConfigurationStatuses
                    .FirstOrDefault(x => x.NcId == switchoverRequirement.SwitchTo);

                if (proposedHost.Healthy)
                {
                    _logger.LogInformation($"Switchover in service <{service.Name}> is requested...");
                    await service.Switcher.Switch(new SwitcherSwitchOptions
                    {
                        NcId = switchoverRequirement.SwitchTo,
                        Failover = switchoverRequirement.Failover
                    });

                    var eventData = await GetBaseEventData<NotifierEventData.CurrentNcChanged>(service.Name);
                    eventData.FromNc = service.CurrentNetworkConfigurationId;
                    eventData.ToNc = switchoverRequirement.SwitchTo;

                    await PushToNotifiers(new NotifierPushOptions
                    {
                        Level = NotifierLevel.Info,
                        Event = NotifierEvent.Switchover,
                        EventData = eventData
                    });
                }
                else
                {
                    _logger.LogError($"Swithover in service <{service.Name}> is requested but it is not possible because the specified network configuration <{switchoverRequirement.SwitchTo}> is unhealthy. The requirement to switchover will be purged.");

                    await PurgeSwitchover(service.Name);
                    mustSwitching = autoSwitchRequirement.IsRequired && !autoSwitchRequirement.Failover;
                }
            }

            if (mustSwitching)
            {
                _logger.LogInformation($"Switching to a higher priority healthy NC <{service.Name}/{autoSwitchRequirement.SwitchTo}>...");

                await service.Switcher.Switch(new SwitcherSwitchOptions
                {
                    NcId = autoSwitchRequirement.SwitchTo,
                    Failover = autoSwitchRequirement.Failover
                });

                var eventData = await GetBaseEventData<NotifierEventData.CurrentNcChanged>(service.Name);
                eventData.FromNc = service.CurrentNetworkConfigurationId;
                eventData.ToNc = autoSwitchRequirement.SwitchTo;

                await PushToNotifiers(new NotifierPushOptions
                {
                    Level = NotifierLevel.Info,
                    Event = NotifierEvent.Switching,
                    EventData = eventData
                });
            }

            await NotifyChangesInNcHealth(service);
        }

        protected async Task<T> GetBaseEventData<T>(string serviceName = null)
            where T : NotifierEventData.Base, new()
        {
            var data = new T()
            {
                DhafCluster = _clusterConfig.Dhaf.ClusterName,
                Service = serviceName,
                UtcTimestamp = DateTime.UtcNow
            };

            return data;
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
            await SendDhafNodeRoleChangedEvent(_role);

            _logger.LogWarning("There is no leader. Participating in the election...");

            var promotionStatus = await TryPromotion();

            if (promotionStatus.Success)
            {
                _role = DhafNodeRole.Leader;
                _leaderLeaseId = promotionStatus.LeaderLeaseId;

                await SendDhafNodeRoleChangedEvent(_role);

                _logger.LogInformation("I'm a leader now.");

                var eventData = await GetBaseEventData<NotifierEventData.DhafNewLeader>();
                eventData.Leader = _clusterConfig.Dhaf.NodeName;

                await PushToNotifiers(new NotifierPushOptions
                {
                    Level = NotifierLevel.Info,
                    Event = NotifierEvent.DhafNewLeader,
                    EventData = eventData
                });
            }
            else
            {
                _role = DhafNodeRole.Follower;
                await SendDhafNodeRoleChangedEvent(_role);

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

            var nodeStatus = new DhafNodeStatusRaw { LastHeartbeatTimestamp = heartbeatTimestamp };
            var content = JsonSerializer.Serialize(nodeStatus, DhafInternalConfig.JsonSerializerOptions);

            await _etcdClient.PutAsync(key, content);
            _logger.LogTrace("Heartbeat *knock-knock*.");
        }

        /// <summary>
        /// Checks the health of all network configurations (both active and others).
        /// The result (as a set of votes) is sent to DCS.
        /// </summary>
        protected async Task NetworkConfigurationsHealthCheck(DhafService service)
        {
            var tasks = service
                .NetworkConfigurations
                .Select(x => NetworkConfigurationHealthCheck(service, x));

            await Task.WhenAll(tasks);

            _logger.LogTrace($"The health of all NC's in service <{service.Name}> has been checked.");
        }

        protected async Task NetworkConfigurationHealthCheck(DhafService service, ClusterServiceNetworkConfig nc)
        {
            _logger.LogTrace($"Check NC <{service.Name}/{nc.Id}>...");

            var status = await service.HealthChecker.Check(new HealthCheckerCheckOptions { NcId = nc.Id });

            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.HealthPath
                + $"{service.Name}/{_clusterConfig.Dhaf.NodeName}/{nc.Id}";

            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var serviceHealth = new EtcdServiceHealth
            {
                Timestamp = timestamp,
                Healthy = status.Healthy,
                ReasonCode = status.ReasonCode
            };

            var value = JsonSerializer.Serialize(serviceHealth, DhafInternalConfig.JsonSerializerOptions);

            await _etcdClient.PutAsync(key, value);

            if (status.Healthy)
            {
                _logger.LogInformation($"NC <{service.Name}/{nc.Id}> status: Healthy :)");
            }
            else
            {
                _logger.LogWarning($"NC <{service.Name}/{nc.Id}> status: Unhealthy.");
            }
        }

        /// <summary>
        /// Inspects the results of network configuration health checks from all active nodes in the dhaf cluster.
        /// A vote determines the health of each configuration by majority vote.
        /// </summary>
        /// <returns>The status of each network configuration.</returns>
        protected async Task<IEnumerable<NetworkConfigurationStatus>>
            InspectResultsOfNetworkConfigurationsHealthCheck(DhafService service)
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var healthyNodes = _dhafNodeStatuses
                .Where(x => x.Value.Healthy)
                .Select(x => x.Key);

            var hostsHealthOpinions = new Dictionary<string, List<EtcdServiceHealth>>();

            foreach (var node in healthyNodes)
            {
                var keyPrefix = _etcdClusterRoot
                    + _dhafInternalConfig.Etcd.HealthPath
                    + $"{service.Name}/{node}/";

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
                else
                {
                    var reasonCodeTasks = hostHealthOpinions.Value
                        .Where(x => !x.Healthy && x.ReasonCode.HasValue)
                        .Select(x => x.ReasonCode)
                        .Distinct()
                        .Select(async x => await service.HealthChecker.ResolveUnhealthinessReasonCode(x.Value));

                    var reasonCodes = await Task.WhenAll(reasonCodeTasks);
                    ncStatus.Reasons = reasonCodes;
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
                v => JsonSerializer.Deserialize<DhafNodeStatusRaw>(v.Value, DhafInternalConfig.JsonSerializerOptions));

            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            var nodeStatuses = entities
                .Select(x => new DhafNodeStatus
                {
                    Name = x.Key,
                    Healthy = ((timestamp - x.Value.LastHeartbeatTimestamp)
                                        <= _dhafInternalConfig.DefHealthyNodeStatusTtl)
                })
                .ToDictionary(x => x.Name);

            _previousDhafNodeStatuses = _dhafNodeStatuses;
            _dhafNodeStatuses = nodeStatuses;
        }

        protected async Task<UpdatePanicModeRelevanceResult> UpdatePanicModeRelevance(DhafService service)
        {
            var oldValue = service.PanicMode;
            var isAnyNcAvailable = service.NetworkConfigurationStatuses
                .Any(x => x.Healthy);

            service.PanicMode = !isAnyNcAvailable;

            return new UpdatePanicModeRelevanceResult
            {
                HasStatusChanged = service.PanicMode != oldValue
            };
        }

        /// <summary>
        /// Determines whether the network configuration should be switched automatically.
        /// This is not always failover (e.g. switching to a higher priority network configuration
        /// which has become healthy again).
        /// </summary>
        protected async Task<DecisionOfNetworkConfigurationSwitching>
            IsAutoSwitchOfNetworkConfigurationRequired(DhafService service)
        {
            var negativeDecision = new DecisionOfNetworkConfigurationSwitching
            {
                Failover = false,
                IsRequired = false
            };

            var priorityNetConf = service.NetworkConfigurationStatuses.FirstOrDefault(x => x.Healthy);
            if (service.PanicMode || priorityNetConf == null)
            {
                return negativeDecision;
            }

            var currentNetConf = service.NetworkConfigurationStatuses
                .FirstOrDefault(x => x.NcId == service.CurrentNetworkConfigurationId);

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
            IsSwitchoverOfNetworkConfigurationRequired(DhafService service)
        {
            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.SwitchoverPath + service.Name;

            var rawValue = await _etcdClient.GetValAsync(key);

            if (!string.IsNullOrEmpty(rawValue))
            {
                var value = JsonSerializer.Deserialize<EtcdManualSwitching>(rawValue, DhafInternalConfig.JsonSerializerOptions);

                if (service.CurrentNetworkConfigurationId != value.NCId)
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

        protected async Task NotifyChangesInNcHealth(DhafService service)
        {
            foreach (var curr in service.NetworkConfigurationStatuses)
            {
                var pushRequired = false;

                if (service.PreviousNetworkConfigurationStatuses is null
                    || !service.PreviousNetworkConfigurationStatuses.Any())
                {
                    if (!curr.Healthy)
                    {
                        pushRequired = true;
                    }
                }
                else
                {
                    var prev = service.PreviousNetworkConfigurationStatuses.FirstOrDefault(x => x.NcId == curr.NcId);
                    if (prev is not null && curr.Healthy != prev.Healthy)
                    {
                        pushRequired = true;
                    }
                }

                if (pushRequired)
                {
                    var eventData = await GetBaseEventData<NotifierEventData.NcHealthChanged>(service.Name);
                    eventData.NcName = curr.NcId;

                    if (!curr.Healthy)
                    {
                        eventData.Reasons = curr.Reasons;
                    }

                    await PushToNotifiers(new NotifierPushOptions
                    {
                        ServiceName = service.Name,
                        Level = curr.Healthy ? NotifierLevel.Info : NotifierLevel.Warning,
                        Event = curr.Healthy ? NotifierEvent.NcUp : NotifierEvent.NcDown,
                        EventData = eventData
                    });
                }
            }
        }

        protected async Task NotifyChangesInDhafNodesHealth()
        {
            foreach (var curr in _dhafNodeStatuses)
            {
                var pushRequired = false;

                if (_previousDhafNodeStatuses is null || !_previousDhafNodeStatuses.Any())
                {
                    if (!curr.Value.Healthy)
                    {
                        pushRequired = true;
                    }
                }
                else if (_previousDhafNodeStatuses.ContainsKey(curr.Key))
                {
                    var prev = _previousDhafNodeStatuses[curr.Key];
                    if (curr.Value.Healthy != prev.Healthy)
                    {
                        pushRequired = true;
                    }
                }

                if (pushRequired)
                {
                    var eventData = await GetBaseEventData<NotifierEventData.DhafNodeHealthChanged>();
                    eventData.NodeName = curr.Value.Name;

                    await PushToNotifiers(new NotifierPushOptions
                    {
                        Level = curr.Value.Healthy ? NotifierLevel.Info : NotifierLevel.Warning,
                        Event = curr.Value.Healthy ? NotifierEvent.NcUp : NotifierEvent.NcDown,
                        EventData = eventData
                    });
                }
            }
        }

        protected async Task<string> GetSwitchoverRequirementOrDefault(DhafService service)
        {
            var key = _etcdClusterRoot
                + _dhafInternalConfig.Etcd.SwitchoverPath + service.Name;

            var rawValue = await _etcdClient.GetValAsync(key);

            if (string.IsNullOrEmpty(rawValue))
            {
                return null;
            }

            var value = JsonSerializer.Deserialize<EtcdManualSwitching>(rawValue, DhafInternalConfig.JsonSerializerOptions);

            return value.NCId;
        }

        protected async Task SendDhafNodeRoleChangedEvent(DhafNodeRole role)
        {
            var tasks = new List<Task>();

            foreach (var service in _services)
            {
                tasks.Add(service.Switcher.DhafNodeRoleChangedEventHandler(role));
                tasks.Add(service.HealthChecker.DhafNodeRoleChangedEventHandler(role));
            }

            tasks.AddRange(_notifiers.Select(x => x.DhafNodeRoleChangedEventHandler(role)));

            await Task.WhenAll(tasks);
        }

        protected async Task PushToNotifiers(NotifierPushOptions opt)
        {
            var tasks = _notifiers.Select(async x =>
            {
                try
                {
                    await x.Push(opt);
                }
                catch
                {
                    var serviceInfo = opt.EventData.Service is null ? string.Empty : $" for service <{opt.EventData.Service}>";
                    _logger.LogError($"Error in sending a notification{serviceInfo} via the <{x.ExtensionName}> provider.");
                }
            });

            await Task.WhenAll(tasks);
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
