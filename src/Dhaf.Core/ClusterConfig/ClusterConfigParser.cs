using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Dhaf.Core
{
    public class ClusterConfigParser
    {
        public string Path { get; set; }
        public ExtensionsScope ExtensionsScope { get; set; }

        public ClusterConfigParser(string path)
        {
            Path = path;
        }

        public ClusterConfigParser(string path, ExtensionsScope extensionsScope)
        {
            Path = path;

            ExtensionsScope = extensionsScope
                ?? throw new Exception("Extensions scope cannot be null when using the current constructor.");
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

            var configYaml = await File.ReadAllTextAsync(Path, DhafInternalConfig.ConfigsEncoding);
            var firstPhase = des.Deserialize<ClusterConfig>(configYaml);

            if (ExtensionsScope == null)
            {
                // A constructor without extensions scope was used,
                // therefore, the first phase of deserialization is sufficient.
                return firstPhase;
            }

            var realMap = new List<ExtensionConfigTypeMap>();

            foreach (var service in firstPhase.Services)
            {
                var switcher = ExtensionsScope.Switchers
                    .FirstOrDefault(x => x.Instance.ExtensionName == service.Switcher.ExtensionName);

                if (switcher == null)
                {
                    throw new ArgumentException($"Switcher <{service.Switcher.ExtensionName}> is not found in any of the extensions.");
                }

                var healthChecker = ExtensionsScope.HealthCheckers
                    .FirstOrDefault(x => x.Instance.ExtensionName == service.HealthChecker.ExtensionName);

                if (healthChecker == null)
                {
                    throw new ArgumentException($"Health checker <{service.HealthChecker.ExtensionName}> is not found in any of the extensions.");
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
                    throw new ArgumentException($"Notifier <{notifierConfig.ExtensionName}> is not found in any of the extensions.");
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
            return secondPhase;
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
