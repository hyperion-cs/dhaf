using CommandLine;
using Dhaf.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Dhaf.Node
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var logger = LogManager.GetCurrentClassLogger();

            try
            {
                await CreateHostBuilder(args).Build().RunAsync();
            }
            catch (ConfigParsingException ex)
            {
                logger.Fatal($"Config parsing error {ex.Code}: {ex.Message}");
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                logger.Fatal($"Config YAML deserialize error:\n{ex.Message}");
            }
            /*catch (Exception ex)
            {
                logger.Fatal($"Further work of the node is impossible because of a fatal error:\n{ex.Message}");
            }*/
            finally
            {
                logger.Info("* Dhaf node exit...");
                LogManager.Shutdown();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices(services =>
                {

                    services.AddSingleton(servicesProvider =>
                    {
                        ArgsOptions argsOptions = null;
                        Parser.Default.ParseArguments<ArgsOptions>(args)
                            .WithParsed(p => argsOptions = p);

                        return argsOptions;
                    })
                    .AddHostedService<Startup>()
                    .AddLogging(loggingBuilder =>
                    {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                        loggingBuilder.AddNLog();
                    });
                });
    }
}
