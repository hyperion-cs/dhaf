using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace Dhaf.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();

            var clpResult = Parser.Default.ParseArguments<RunOptions, StatusOptions, SwitchoverOptions>(args)
                .MapResult
                (
                   (RunOptions opts)        => Actions.ExecuteRunAndReturnExitCode(opts),
                   (StatusOptions opts)     => Actions.ExecuteStatusAndReturnExitCode(opts),
                   (SwitchoverOptions opts) => Actions.ExecuteSwitchoverAndReturnExitCode(opts),

                   errs => Task.FromResult(0)
                );

            await host.RunAsync();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, configuration) =>
            {
                configuration.Sources.Clear();

                IHostEnvironment env = hostingContext.HostingEnvironment;

                configuration
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            })
            .ConfigureServices((_, services) => { });
    }
}
