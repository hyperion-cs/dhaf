﻿using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Dhaf.Core
{
    public class DhafInternalConfig
    {
        public DhafInternalConfigEtcd Etcd { get; set; } = new DhafInternalConfigEtcd();
        public DhafInternalConfigWebApi WebApi { get; set; } = new DhafInternalConfigWebApi();

        public int DefHeartbeatInterval { get; set; } = 5;
        public int DefTactInterval { get; set; } = 10;
        public int DefHealthyNodeStatusTtl { get; set; } = 30;

        public List<string> Extensions { get; set; } = new List<string>()
        {
            "health-checkers/web", "health-checkers/exec", "switchers/cloudflare", "switchers/exec"
        };

        public static Encoding ConfigsEncoding { get; set; } = Encoding.UTF8;

        public static JsonSerializerOptions JsonSerializerOptions { get; set; }
            = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public class DhafInternalConfigEtcd
    {
        public string LeaderPath { get; set; } = "leader";
        public int DefLeaderKeyTtl { get; set; } = 15;

        public string NodesPath { get; set; } = "nodes/";
        public string HealthPath { get; set; } = "health/";
        public string ManualSwitchingPath { get; set; } = "manual_switching";
    }

    public class DhafInternalConfigWebApi
    {
        public string DefHost { get; set; } = "localhost";
        public int DefPort { get; set; } = 8128;
    }
}
