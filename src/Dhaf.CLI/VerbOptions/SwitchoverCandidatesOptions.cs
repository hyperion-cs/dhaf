using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.CLI
{
    [Verb("switchover-candidates", HelpText = "Show suitable network configurations for switchover.")]
    public class SwitchoverCandidatesOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Show suitable network configurations for switchover using configuration file <config_file>",
                    new SwitchoverCandidatesOptions { Config = "<config_file>" });
            }
        }
    }
}
