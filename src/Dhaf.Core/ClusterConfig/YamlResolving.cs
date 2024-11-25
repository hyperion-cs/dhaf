using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Dhaf.Core
{
    public class ExtensionConfigTypeMap
    {
        public string ExtensionName { get; set; }
        public Type ConfigTypeInterface { get; set; }
        public Type ImplType { get; set; }

        public ExtensionConfigTypeMap(Type configTypeInterface, Type implType)
        {
            ConfigTypeInterface = configTypeInterface;
            ImplType = implType;
        }

        public ExtensionConfigTypeMap(string extensionName, Type configTypeInterface, Type implType)
        {
            ExtensionName = extensionName;
            ConfigTypeInterface = configTypeInterface;
            ImplType = implType;
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

    public class ExtensionConfigYamlMark
    {
        public int AbsoluteOffset { get; set; }
        public Type InterfaceType { get; set; }
        public string ExtensionName { get; set; }
    }

    public enum EctcMode
    {
        CollectYamlMarks, ConvertWithMap
    }

    public class ExtensionConfigTypeConverter : IYamlTypeConverter
    {
        private readonly IDeserializer _deserializer;

        public List<ExtensionConfigTypeMap> Map { get; set; } = new();
        public EctcMode Mode { get; set; } = EctcMode.CollectYamlMarks;
        public List<ExtensionConfigYamlMark> YamlMarks { get; set; } = new();

        public List<Type> CatchTypes { get; set; } = new List<Type>()
        {
            typeof(SwitcherDefaultConfig),
            typeof(HealthCheckerDefaultConfig),
            typeof(NotifierDefaultConfig),
        };

        public List<Type> AvailableInterfaces { get; set; } = new List<Type>()
        {
            typeof(ISwitcherConfig),
            typeof(IHealthCheckerConfig),
            typeof(INotifierConfig),
        };

        public bool Accepts(Type type)
        {
            return CatchTypes.Contains(type);
        }

        public ExtensionConfigTypeConverter()
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            _deserializer = deserializer;
        }

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            var offset = parser.Current.Start.Index;
            var typeInterfaces = type.GetInterfaces();

            var configTypeInterface = AvailableInterfaces
                .FirstOrDefault(x => typeInterfaces.Contains(x));

            object obj = null;

            if (Mode == EctcMode.ConvertWithMap)
            {
                var yamlMark = YamlMarks.FirstOrDefault(x => x.AbsoluteOffset == offset
                    && x.InterfaceType == configTypeInterface);

                var realConfigType = Map
                    .FirstOrDefault(x => x.ConfigTypeInterface == yamlMark.InterfaceType
                        && (x.ExtensionName is null || x.ExtensionName == yamlMark.ExtensionName))
                    .ImplType;

                obj = _deserializer.Deserialize(parser, realConfigType);
            }

            if (Mode == EctcMode.CollectYamlMarks)
            {
                obj = _deserializer.Deserialize(parser, type);
                var yamlMark = new ExtensionConfigYamlMark
                {
                    ExtensionName = ((IExtensionConfig)obj).ExtensionName,
                    AbsoluteOffset = (int)offset,
                    InterfaceType = configTypeInterface
                };

                YamlMarks.Add(yamlMark);
            }

            return obj;
        }

        public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
