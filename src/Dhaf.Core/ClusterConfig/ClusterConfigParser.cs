using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Dhaf.Core
{
    public class ClusterConfigParser
    {
        public string Path { get; set; }
        public ExtensionsScope ExtensionsScope { get; private set; }
        public DhafInternalConfig InternalConfig { get; private set; }

        public ClusterConfigParser(string path, DhafInternalConfig internalConfig)
        {
            Path = path;
            InternalConfig = internalConfig;
        }

        public ClusterConfigParser(string path, ExtensionsScope extensionsScope, DhafInternalConfig internalConfig)
        {
            Path = path;

            ExtensionsScope = extensionsScope
                ?? throw new Exception("Extensions scope cannot be null when using the current constructor.");

            InternalConfig = internalConfig
                ?? throw new Exception("Dhaf internal config cannot be null when using the current constructor.");
        }

        public async Task<ClusterConfig> Parse()
        {
            /*
            * This uses a two-phase deserialization of YAML.
            * The first phase recognizes specific types. The second phase serializes into the exact types.
            */

            var ectc = new ExtensionConfigTypeConverter();

            var defaultMap = new Dictionary<Type, Type>() {
                { typeof(ISwitcherConfig), typeof(SwitcherDefaultConfig) },
                { typeof(IHealthCheckerConfig), typeof(HealthCheckerDefaultConfig) },
                { typeof(INotifierConfig), typeof(NotifierDefaultConfig) }
            };

            var des = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithNodeTypeResolver(new ClusterConfigInterfacesResolver(defaultMap))
                .WithTypeConverter(ectc)
                .Build();


            if (!File.Exists(Path))
            {
                throw new ConfigParsingException(1418, $"The configuration file in the specified path was not found.\nPath: <{Path}>");
            }

            var configYaml = await File.ReadAllTextAsync(Path, DhafInternalConfig.ConfigsEncoding);

            var firstPhase = des.Deserialize<ClusterConfig>(configYaml);
            firstPhase = SetDefaultValues(firstPhase);

            if (ExtensionsScope == null)
            {
                // A constructor without extensions scope was used,
                // therefore, the first phase of deserialization is sufficient.
                return firstPhase;
            }

            var realMap = new List<ExtensionConfigTypeMap>();

            foreach (var service in firstPhase.Services)
            {
                if (service.Switcher is null)
                {
                    throw new ConfigParsingException(1416, $"Switcher for service <{service.Name}> is not specified.");
                }

                var switcher = ExtensionsScope.Switchers
                    .FirstOrDefault(x => x.Instance.ExtensionName == service.Switcher.ExtensionName);

                if (switcher == null)
                {
                    throw new ConfigParsingException(1405, $"Switcher <{service.Switcher.ExtensionName}> is not found in any of the extensions.");
                }

                if (service.HealthChecker is null)
                {
                    throw new ConfigParsingException(1417, $"Health checker for service <{service.Name}> is not specified.");
                }

                var healthChecker = ExtensionsScope.HealthCheckers
                    .FirstOrDefault(x => x.Instance.ExtensionName == service.HealthChecker.ExtensionName);

                if (healthChecker == null)
                {
                    throw new ConfigParsingException(1404, $"Health checker <{service.HealthChecker.ExtensionName}> is not found in any of the extensions.");
                }

                var existingSwMap = realMap.FirstOrDefault(x => x.ExtensionName == switcher.Instance.ExtensionName
                                                    && x.ConfigTypeInterface == typeof(ISwitcherConfig));

                var existingHcMap = realMap.FirstOrDefault(x => x.ExtensionName == healthChecker.Instance.ExtensionName
                                                    && x.ConfigTypeInterface == typeof(IHealthCheckerConfig));

                if (existingSwMap is null)
                {
                    realMap.Add(new ExtensionConfigTypeMap(switcher.Instance.ExtensionName,
                                    typeof(ISwitcherConfig), switcher.Instance.ConfigType));
                }

                if (existingHcMap is null)
                {
                    realMap.Add(new ExtensionConfigTypeMap(healthChecker.Instance.ExtensionName,
                                    typeof(IHealthCheckerConfig), healthChecker.Instance.ConfigType));
                }
            }

            foreach (var notifierConfig in firstPhase.Notifiers)
            {
                var notifier = ExtensionsScope.Notifiers
                    .FirstOrDefault(x => x.Instance.ExtensionName == notifierConfig.ExtensionName);

                if (notifier == null)
                {
                    throw new ConfigParsingException(1406, $"Notifier <{notifierConfig.ExtensionName}> is not found in any of the extensions.");
                }

                var existingMap = realMap.FirstOrDefault(x => x.ExtensionName == notifier.Instance.ExtensionName
                                                    && x.ConfigTypeInterface == typeof(INotifierConfig));
                if (existingMap is null)
                {
                    realMap.Add(new ExtensionConfigTypeMap(notifier.Instance.ExtensionName,
                                    typeof(INotifierConfig), notifier.Instance.ConfigType));
                }
            }

            ectc.Map = realMap;
            ectc.Mode = EctcMode.ConvertWithMap;

            var secondPhase = des.Deserialize<ClusterConfig>(configYaml);
            secondPhase = SetDefaultValues(secondPhase);

            await ConfigCommonCheck(secondPhase);

            return secondPhase;
        }

        public ClusterConfig SetDefaultValues(ClusterConfig clusterConfig)
        {
            if (clusterConfig.Dhaf.WebApi.Host is null)
            {
                clusterConfig.Dhaf.WebApi.Host = InternalConfig.WebApi.DefHost;
            }

            if (clusterConfig.Dhaf.WebApi.Port is null)
            {
                clusterConfig.Dhaf.WebApi.Port = InternalConfig.WebApi.DefPort;
            }

            // TODO: Set the other default values here (and don't do it in Dhaf.Node).

            return clusterConfig;
        }

        public async Task ConfigCommonCheck(ClusterConfig config)
        {
            var nameRegex = new Regex($"^[a-zA-Z0-9\\-_]{{1,{InternalConfig.NameMaxLength}}}$");

            string incorrectNameErr(string prop, string currVal) => $"Incorrect value for \"{prop}\" in dhaf config. " +
               $"The name is required and must contain only letters of the " +
               $"Latin alphabet and hyphen \"-\" / underscore \"_\" characters. " +
               $"The maximum length of the name is {InternalConfig.NameMaxLength} character(s). " +
               $"Current value: \"{currVal ?? "<none>"}\".";

            if (!nameRegex.IsMatch(config.Dhaf.NodeName ?? string.Empty))
            {
                throw new ConfigParsingException(1400, incorrectNameErr("dhaf.node-name", config.Dhaf.NodeName));
            }

            if (!nameRegex.IsMatch(config.Dhaf.ClusterName ?? string.Empty))
            {
                throw new ConfigParsingException(1400, incorrectNameErr("dhaf.cluster-name", config.Dhaf.ClusterName));
            }

            if (!config.Services.Any())
            {
                throw new ConfigParsingException(1401, "No services were found in the dhaf config. There is no reason for dhaf to work.");
            }

            foreach (var service in config.Services)
            {
                if (!nameRegex.IsMatch(service.Name ?? string.Empty))
                {
                    throw new ConfigParsingException(1400, incorrectNameErr("service.name", service.Name));
                }

                if (string.IsNullOrEmpty(service.Domain))
                {
                    throw new ConfigParsingException(1403, $"Domain name is not set for the \"{service.Name}\" service.");
                }

                if (!service.EntryPoints.Any())
                {
                    throw new ConfigParsingException(1402, $"No entry points were found for the \"{service.Name}\" service.");
                }

                foreach (var entryPoint in service.EntryPoints)
                {
                    if (!nameRegex.IsMatch(entryPoint.Id ?? string.Empty))
                    {
                        throw new ConfigParsingException(1400,
                            incorrectNameErr($"service <{service.Name}>.entry-points.name", entryPoint.Id));
                    }
                }

                var uniqueNcNamesCount = service.EntryPoints
                    .Select(x => x.Id)
                    .Distinct()
                    .Count();

                if (uniqueNcNamesCount != service.EntryPoints.Count)
                {
                    throw new ConfigParsingException(1410, $"Not all entry point names " +
                        $"in service \"{service.Name}\" are unique.");
                }
            }

            var uniqueServiceNamesCount = config.Services
                    .Select(x => x.Name)
                    .Distinct()
                    .Count();

            if (uniqueServiceNamesCount != config.Services.Count)
            {
                throw new ConfigParsingException(1411, $"Not all service names are unique.");
            }

            foreach (var ntf in config.Notifiers)
            {
                if (!nameRegex.IsMatch(ntf.Name ?? string.Empty))
                {
                    throw new ConfigParsingException(1400,
                        incorrectNameErr($"notifier.name", ntf.Name));
                }
            }

            var uniqueNotifierNamesCount = config.Notifiers
                    .Select(x => x.Name)
                    .Distinct()
                    .Count();

            if (uniqueNotifierNamesCount != config.Notifiers.Count)
            {
                throw new ConfigParsingException(1411, $"Not all notifier names are unique.");
            }

            if ((config.Etcd.LeaderKeyTtl ?? InternalConfig.Etcd.DefLeaderKeyTtl)
                <= (config.Dhaf.HeartbeatInterval ?? InternalConfig.DefHeartbeatInterval))
            {
                throw new ConfigParsingException(1412, $"The TTL of the leader key in the ETCD must be greater than the heartbeat interval of the Dhaf node.");
            }

            if ((!string.IsNullOrEmpty(config.Etcd.Username) && string.IsNullOrEmpty(config.Etcd.Password))
                || (string.IsNullOrEmpty(config.Etcd.Username) && !string.IsNullOrEmpty(config.Etcd.Password)))
            {
                throw new ConfigParsingException(1413, $"Etcd credentials (username, password) must BOTH be either missing or present.");
            }

            var tactInterval = config.Dhaf.TactInterval ?? InternalConfig.DefTactInterval;
            if (InternalConfig.TactIntervalMin > tactInterval || tactInterval > InternalConfig.TactIntervalMax)
            {
                throw new ConfigParsingException(1414, $"Tact interval must be in the range {InternalConfig.TactIntervalMin}-{InternalConfig.TactIntervalMax} seconds.");
            }

            var tactPostSwitchDelay = config.Dhaf.TactPostSwitchDelay ?? InternalConfig.DefTactPostSwitchDelay;
            if (InternalConfig.TactPostSwitchDelayMin > tactPostSwitchDelay || tactPostSwitchDelay > InternalConfig.TactPostSwitchDelayMax)
            {
                throw new ConfigParsingException(1415, $"Tact post switch delay must be in the range {InternalConfig.TactPostSwitchDelayMin}-{InternalConfig.TactPostSwitchDelayMax} seconds.");
            }
        }

        public async Task<T> ParseExtensionInternal<T>(string path, Type internalConfigType)
        {
            var extensionDir = $"extensions/{path}/";
            var metaRaw = await File.ReadAllTextAsync(extensionDir + "extension.json");

            var metaType = typeof(ExtensionMeta<>).MakeGenericType(internalConfigType);
            var meta = JsonSerializer.Deserialize(metaRaw, metaType, DhafInternalConfig.JsonSerializerOptions);

            var config = (T)(meta as dynamic).InternalConfiguration;
            if (config == null)
            {
                config = (T)Activator.CreateInstance(internalConfigType);
            }

            return config;
        }
    }
}
