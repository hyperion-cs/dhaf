using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Core.Events;
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

            var defaultMap = new Dictionary<Type, Type>() {
                { typeof(ISwitcherConfig), typeof(SwitcherDefaultConfig) },
                { typeof(IHealthCheckerConfig), typeof(HealthCheckerDefaultConfig) }
            };

            var configYaml = await File.ReadAllTextAsync(Path, DhafInternalConfig.ConfigsEncoding);
            var firstPhase = GetDeserializer(defaultMap).Deserialize<ClusterConfig>(configYaml);

            if (ExtensionsScope == null)
            {
                // A constructor without extensions scope was used,
                // therefore, the first phase of deserialization is sufficient.
                return firstPhase;
            }

            var switcher = ExtensionsScope.Switchers
                .FirstOrDefault(x => x.Instance.ExtensionName == firstPhase.Switcher.ExtensionName);

            if (switcher == null)
            {
                throw new ArgumentException($"Switcher <{firstPhase.Switcher.ExtensionName}> is not found in any of the extensions.");
            }

            var healthChecker = ExtensionsScope.HealthCheckers
                .FirstOrDefault(x => x.Instance.ExtensionName == firstPhase.HealthCheck.ExtensionName);

            if (healthChecker == null)
            {
                throw new ArgumentException($"Health checker <{firstPhase.HealthCheck.ExtensionName}> is not found in any of the extensions.");
            }

            var realMap = new Dictionary<Type, Type>() {
                { typeof(ISwitcherConfig), switcher.Instance.ConfigType },
                { typeof(IHealthCheckerConfig), healthChecker.Instance.ConfigType }
            };

            var secondPhase = GetDeserializer(realMap).Deserialize<ClusterConfig>(configYaml);
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

        protected static IDeserializer GetDeserializer(Dictionary<Type, Type> map)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithNodeTypeResolver(new ClusterConfigInterfacesResolver(map))
                .Build();

            return deserializer;
        }
    }

    public class ClusterConfigInterfacesResolver : INodeTypeResolver
    {
        protected Dictionary<Type, Type> _map;

        public ClusterConfigInterfacesResolver(Dictionary<Type, Type> map)
        {
            _map = map;
        }

        public bool Resolve(NodeEvent nodeEvent, ref Type type)
        {
            if (_map.TryGetValue(type, out var implementationType))
            {
                type = implementationType;
                return true;
            }

            return false;
        }
    }
}
