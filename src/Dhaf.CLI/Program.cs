﻿using CommandLine;
using System.Threading.Tasks;

namespace Dhaf.CLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<StatusDhafOptions,
                StatusServiceOptions, StatusServicesOptions,
                SwitchoverCandidatesOptions, SwitchoverToOptions,
                SwitchoverPurgeOptions, NodeDecommissionOptions>(args)
                .MapResult
                (
                   (StatusDhafOptions opts) => Actions.ExecuteStatusDhafAndReturnExitCode(opts),
                   (StatusServiceOptions opts) => Actions.ExecuteStatusServiceAndReturnExitCode(opts),
                   (StatusServicesOptions opts) => Actions.ExecuteStatusServicesAndReturnExitCode(opts),
                   (SwitchoverCandidatesOptions opts) => Actions.ExecuteSwitchoverCandidatesAndReturnExitCode(opts),
                   (SwitchoverToOptions opts) => Actions.ExecuteSwitchoverToAndReturnExitCode(opts),
                   (SwitchoverPurgeOptions opts) => Actions.ExecuteSwitchoverPurgeAndReturnExitCode(opts),
                   (NodeDecommissionOptions opts) => Actions.ExecuteNodeDecommissionAndReturnExitCode(opts),
                       errs => Task.FromResult(0)
                );
        }
    }
}
