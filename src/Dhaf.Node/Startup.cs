using Dhaf.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dhaf.Node
{
    public class Startup
    {
        private readonly IServiceProvider _servicesProvider;
        private readonly IConfiguration _configuration;
        private readonly ArgsOptions _argsOptions;

        public Startup(IServiceProvider servicesProvider,
            IConfiguration configuration,
            ArgsOptions argsOptions)
        {
            _servicesProvider = servicesProvider;
            _configuration = configuration;
            _argsOptions = argsOptions;
        }

        public async Task Go()
        {
            var dhafInternalConfig = new DhafInternalConfig();
            _configuration.Bind(dhafInternalConfig);

            var dhafNodeLogger = _servicesProvider.GetRequiredService<ILogger<IDhafNode>>();
            var swLogger = _servicesProvider.GetRequiredService<ILogger<ISwitcher>>();
            var hcLogger = _servicesProvider.GetRequiredService<ILogger<IHealthChecker>>();

            dhafNodeLogger.LogInformation($"Configuration file is <{_argsOptions.ConfigPath}>.");

            var extensionsScope = ExtensionsScopeFactory
                .GetExtensionsScope(dhafInternalConfig.Extensions);

            var clusterConfigParser = new ClusterConfigParser(_argsOptions.ConfigPath, extensionsScope);
            var parsedClusterConfig = await clusterConfigParser.Parse();

            dhafNodeLogger.LogInformation($"I am <{parsedClusterConfig.Dhaf.NodeName}> in the <{parsedClusterConfig.Dhaf.ClusterName}> cluster.");
            dhafNodeLogger.LogDebug($"Switcher provider is <{parsedClusterConfig.Switcher.ExtensionName}>.");
            dhafNodeLogger.LogDebug($"Health checker provider is <{parsedClusterConfig.HealthCheck.ExtensionName}>.");

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
                Logger = hcLogger,
                Config = parsedClusterConfig.HealthCheck,
                ClusterServiceConfig = parsedClusterConfig.Service,
                InternalConfig = hcInternalConfig
            };

            var swInitOptions = new SwitcherInitOptions
            {
                Logger = swLogger,
                Config = parsedClusterConfig.Switcher,
                ClusterServiceConfig = parsedClusterConfig.Service,
                InternalConfig = swInternalConfig
            };

            await healthChecker.Instance.Init(hcInitOptions);
            await switcher.Instance.Init(swInitOptions);

            IDhafNode dhafNode = new DhafNode(parsedClusterConfig, dhafInternalConfig,
                switcher.Instance, healthChecker.Instance, dhafNodeLogger);

            dhafNodeLogger.LogTrace("[rest api] Init process...");

            var restApiFactory = new RestApiFactory();
            var restApiHost = parsedClusterConfig.Dhaf.WebApi.Host ?? dhafInternalConfig.WebApi.DefHost;
            var restApiPort = parsedClusterConfig.Dhaf.WebApi.Port ?? dhafInternalConfig.WebApi.DefPort;
            var restApiUrl = $"http://{restApiHost}:{restApiPort}/";

            var resApiServer = restApiFactory.CreateWebServer(restApiUrl, dhafNode, dhafNodeLogger);
            var restApiTask = resApiServer.RunAsync();

            dhafNodeLogger.LogInformation($"[rest api] Started on {restApiUrl}.");
            dhafNodeLogger.LogInformation("Node has been successfully initialized.");

            await dhafNode.TactWithInterval();
            resApiServer.Dispose();
        }
    }
}
