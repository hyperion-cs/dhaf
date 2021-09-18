using Dhaf.Core;
using dotnet_etcd;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            var ntfLogger = _servicesProvider.GetRequiredService<ILogger<INotifier>>();

            dhafNodeLogger.LogInformation($"Configuration file is <{_argsOptions.ConfigPath}>.");

            var extensionsScope = ExtensionsScopeFactory
                .GetExtensionsScope(dhafInternalConfig.Extensions);

            var clusterConfigParser = new ClusterConfigParser(_argsOptions.ConfigPath, extensionsScope);
            var parsedClusterConfig = await clusterConfigParser.Parse();

            var etcdClient = new EtcdClient(parsedClusterConfig.Etcd.Hosts);


            dhafNodeLogger.LogInformation($"I am <{parsedClusterConfig.Dhaf.NodeName}> in the <{parsedClusterConfig.Dhaf.ClusterName}> cluster.");

            var services = new ConcurrentBag<DhafService>();

            var extInitorTasks = parsedClusterConfig.Services.Select(async servConf =>
            {
                dhafNodeLogger.LogDebug($"Switcher provider in <{servConf.Name}> is <{servConf.Switcher.ExtensionName}>.");

                dhafNodeLogger
                    .LogDebug($"Health checker provider in <{servConf.Name}> is <{servConf.HealthChecker.ExtensionName}>.");

                var healthCheckerTemplate = extensionsScope.HealthCheckers
                    .First(x => x.Instance.ExtensionName == servConf.HealthChecker.ExtensionName);

                var switcherTemplate = extensionsScope.Switchers
                    .First(x => x.Instance.ExtensionName == servConf.Switcher.ExtensionName);

                var healthChecker = ExtensionsScopeFactory.CreateSuchAs(healthCheckerTemplate.Instance);
                var switcher = ExtensionsScopeFactory.CreateSuchAs(switcherTemplate.Instance);

                var hcInternalConfig = await clusterConfigParser.ParseExtensionInternal<IHealthCheckerInternalConfig>
                        (healthCheckerTemplate.ExtensionPath, healthChecker.InternalConfigType);

                var swInternalConfig = await clusterConfigParser.ParseExtensionInternal<ISwitcherInternalConfig>
                    (switcherTemplate.ExtensionPath, switcher.InternalConfigType);

                var hcInitOptions = new HealthCheckerInitOptions
                {
                    Logger = hcLogger,
                    Config = servConf.HealthChecker,
                    ClusterServiceConfig = servConf,
                    InternalConfig = hcInternalConfig,
                    Storage = new ExtensionStorageProvider(etcdClient, parsedClusterConfig, dhafInternalConfig,
                        dhafInternalConfig.Etcd.ExtensionStorageHcPrefix + servConf.HealthChecker.ExtensionName)
                };

                var swInitOptions = new SwitcherInitOptions
                {
                    Logger = swLogger,
                    Config = servConf.Switcher,
                    ClusterServiceConfig = servConf,
                    InternalConfig = swInternalConfig,
                    Storage = new ExtensionStorageProvider(etcdClient, parsedClusterConfig, dhafInternalConfig,
                        dhafInternalConfig.Etcd.ExtensionStorageSwPrefix + servConf.Switcher.ExtensionName)
                };

                await healthChecker.Init(hcInitOptions);
                await switcher.Init(swInitOptions);

                services.Add(new DhafService(servConf.Name,
                    servConf.Domain,
                    servConf.NetworkConfigurations,
                    switcher, healthChecker));
            });

            await Task.WhenAll(extInitorTasks);

            var notifierTemplates = extensionsScope.Notifiers;
            var notifiers = new List<INotifier>();

            foreach (var notifierConfig in parsedClusterConfig.Notifiers)
            {
                var notifierTemplate = notifierTemplates
                    .FirstOrDefault(x => x.Instance.ExtensionName == notifierConfig.ExtensionName);

                var instance = ExtensionsScopeFactory.CreateSuchAs(notifierTemplate.Instance);

                var ntfInternalConfig = await clusterConfigParser.ParseExtensionInternal<INotifierInternalConfig>
                    (notifierTemplate.ExtensionPath, notifierTemplate.Instance.InternalConfigType);

                var ntfInitOptions = new NotifierInitOptions
                {
                    Logger = ntfLogger,
                    Config = notifierConfig,
                    InternalConfig = ntfInternalConfig,
                    Storage = new ExtensionStorageProvider(etcdClient, parsedClusterConfig, dhafInternalConfig,
                        dhafInternalConfig.Etcd.ExtensionStorageNtfPrefix
                            + notifierConfig.ExtensionName + $"/{notifierConfig.Name}")
                };

                notifiers.Add(instance);
                await instance.Init(ntfInitOptions);
            }

            IDhafNode dhafNode = new DhafNode(parsedClusterConfig, dhafInternalConfig,
                services, notifiers, etcdClient, dhafNodeLogger);

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
