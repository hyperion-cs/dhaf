using CommandLine;
using Dhaf.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.Node
{
    class Program
    {
        public class ArgsOptions
        {
            [Option('c', "config", Required = true, HelpText = "Configuration file.")]
            public string ConfigPath { get; set; }
        }

        static async Task Main(string[] args)
        {
            ArgsOptions opt = null;
            Parser.Default.ParseArguments<ArgsOptions>(args)
                .WithParsed(p => opt = p);

            if (opt != null)
            {
                Console.WriteLine($"Configuration file is <{opt.ConfigPath}>");

                var dhafInternalConfig = new DhafInternalConfig();
                var extensionsScope = ExtensionsScopeFactory.GetExtensionsScope(dhafInternalConfig.Extensions);

                var clusterConfigParser = new ClusterConfigParser(opt.ConfigPath, extensionsScope);
                var parsedClusterConfig = await clusterConfigParser.Parse();

                Console.WriteLine($"Switcher is <{parsedClusterConfig.Switcher.ExtensionName}>");
                Console.WriteLine($"Health checker is <{parsedClusterConfig.HealthCheck.ExtensionName}>");

                var healthChecker = extensionsScope.HealthCheckers
                    .First(x => x.Instance.ExtensionName == parsedClusterConfig.HealthCheck.ExtensionName);

                var switcher = extensionsScope.Switchers
                    .First(x => x.Instance.ExtensionName == parsedClusterConfig.Switcher.ExtensionName);

                var swInternalConfig = await clusterConfigParser.ParseExtensionInternal<ISwitcherInternalConfig>
                    (switcher.ExtensionPath, switcher.Instance.InternalConfigType);

                var hcInternalConfig = await clusterConfigParser.ParseExtensionInternal<IHealthCheckerInternalConfig>
                        (healthChecker.ExtensionPath, healthChecker.Instance.InternalConfigType);

                var hcInitOptions = new HealthCheckerInitOptions
                {
                    Config = parsedClusterConfig.HealthCheck,
                    ClusterServiceConfig = parsedClusterConfig.Service,
                    InternalConfig = hcInternalConfig
                };

                var swInitOptions = new SwitcherInitOptions
                {
                    Config = parsedClusterConfig.Switcher,
                    ClusterServiceConfig = parsedClusterConfig.Service,
                    InternalConfig = swInternalConfig
                };

                await healthChecker.Instance.Init(hcInitOptions);
                await switcher.Instance.Init(swInitOptions);

                var dhafNode = new DhafNode(parsedClusterConfig, dhafInternalConfig,
                    switcher.Instance, healthChecker.Instance);

                await dhafNode.TactWithInterval();
            }

            Console.WriteLine("* Dhaf node exit...");
        }
    }
}
