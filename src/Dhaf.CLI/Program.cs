using CommandLine;
using System.Threading.Tasks;

namespace Dhaf.Node
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<StatusDhafOptions, StatusServiceOptions>(args)
                .MapResult
                (
                   (StatusDhafOptions opts) => Actions.ExecuteStatusDhafAndReturnExitCode(opts),
                   (StatusServiceOptions opts) => Actions.ExecuteStatusServiceAndReturnExitCode(opts),
                       errs => Task.FromResult(0)
                );
        }
    }
}
