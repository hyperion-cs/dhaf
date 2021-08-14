using System;
using System.Threading.Tasks;

namespace Dhaf.Core
{
    public static class Actions
    {
        public static async Task<int> ExecuteRunAndReturnExitCode(RunOptions opt)
        {
            Console.WriteLine($"Start dhaf cluster node using configuration file {opt.Config}...");
            return 0;
        }

        public static async Task<int> ExecuteStatusAndReturnExitCode(StatusOptions opt)
        {
            Console.WriteLine($"Find out dhaf cluster status using configuration file {opt.Config}...");
            return 0;
        }

        public static async Task<int> ExecuteSwitchoverAndReturnExitCode(SwitchoverOptions opt)
        {
            Console.WriteLine($"Manually switch to {opt.To} using configuration file {opt.Config}...");
            return 0;
        }
    }
}
