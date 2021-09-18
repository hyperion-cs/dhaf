using CommandLine;
using Dhaf.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                var configuration = new ConfigurationBuilder()
                   .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                   .Build();

                var servicesProvider = BuildDi(configuration);
                using (servicesProvider as IDisposable)
                {
                    ArgsOptions argsOptions = null;
                    Parser.Default.ParseArguments<ArgsOptions>(args)
                        .WithParsed(p => argsOptions = p);

                    if (argsOptions is null)
                    {
                        throw new ArgumentNullException("The command line arguments cannot be null.");
                    }

                    var startup = new Startup(servicesProvider, configuration, argsOptions);
                    await startup.Go();
                }
            }
            catch (ConfigParsingException ex)
            {
                logger.Fatal($"Config parser error {ex.Code}: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.Fatal($"Further work of the node is impossible because of a fatal error:\n{ex.Message}");
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
