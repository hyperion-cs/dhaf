using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.CLI
{
    [Verb("switchover-candidates", HelpText = "Show suitable entry points for switchover.")]
    public class SwitchoverCandidatesOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Option('s', "service", Required = true, HelpText = "Service name.")]
        public string ServiceName { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Show suitable entry points in service <s> for switchover using configuration file <config_file>",
                    new SwitchoverCandidatesOptions { Config = "<config_file>", ServiceName = "<s>" });
            }
        }
    }
}
