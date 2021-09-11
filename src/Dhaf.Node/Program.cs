﻿using CommandLine;
using Dhaf.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
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
            var logger = LogManager.GetCurrentClassLogger();

            try
            {
                var config = new ConfigurationBuilder()
                   .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                   .Build();

                var servicesProvider = BuildDi(config);
                using (servicesProvider as IDisposable)
                {
                    ArgsOptions opt = null;
                    Parser.Default.ParseArguments<ArgsOptions>(args)
                        .WithParsed(p => opt = p);

                    if (opt != null)
                    {
                        var dhafNodeLogger = servicesProvider.GetRequiredService<ILogger<IDhafNode>>();
                        var swLogger = servicesProvider.GetRequiredService<ILogger<ISwitcher>>();
                        var hcLogger = servicesProvider.GetRequiredService<ILogger<IHealthChecker>>();

                        dhafNodeLogger.LogInformation($"Configuration file is <{opt.ConfigPath}>.");

                        var dhafInternalConfig = new DhafInternalConfig();
                        var extensionsScope = ExtensionsScopeFactory
                            .GetExtensionsScope(dhafInternalConfig.Extensions);

                        var clusterConfigParser = new ClusterConfigParser(opt.ConfigPath, extensionsScope);
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
            catch (Exception ex)
            {
                logger.Fatal(ex, "Further work of the node is impossible because of a fatal error.");
            }
            finally
            {
                logger.Info("* Dhaf node exit...");
                LogManager.Shutdown();
            }
        }

        private static IServiceProvider BuildDi(IConfiguration config)
        {
            return new ServiceCollection()
               .AddLogging(loggingBuilder =>
               {
                   loggingBuilder.ClearProviders();
                   loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                   loggingBuilder.AddNLog(config);
               })
               .BuildServiceProvider();
        }
    }
}
