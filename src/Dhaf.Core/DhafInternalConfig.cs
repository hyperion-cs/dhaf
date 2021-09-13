using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Dhaf.Core
{
    public class DhafInternalConfig
    {
        public DhafInternalConfigEtcd Etcd { get; set; }
        public DhafInternalConfigWebApi WebApi { get; set; }

        public int DefHeartbeatInterval { get; set; }
        public int DefTactInterval { get; set; }
        public int DefHealthyNodeStatusTtl { get; set; }

        public List<string> Extensions { get; set; }

        public static Encoding ConfigsEncoding { get; set; } = Encoding.UTF8;

        public static JsonSerializerOptions JsonSerializerOptions { get; set; }
            = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public class DhafInternalConfigEtcd
    {
        public string LeaderPath { get; set; }
        public int DefLeaderKeyTtl { get; set; }

        public string NodesPath { get; set; }
        public string HealthPath { get; set; }
        public string SwitchoverPath { get; set; }
        public string ExtensionStoragePath { get; set; }

        public string ExtensionStorageHcPrefix { get; set; }
        public string ExtensionStorageSwPrefix { get; set; }
        public string ExtensionStorageNtfPrefix { get; set; }
    }

    public class DhafInternalConfigWebApi
    {
        public string DefHost { get; set; }
        public int DefPort { get; set; }
    }
}
