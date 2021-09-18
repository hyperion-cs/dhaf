using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.CLI
{
    [Verb("switchover-to", HelpText = "Switchover to the specified network configuration.")]
    public class SwitchoverToOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Option('s', "service", Required = true, HelpText = "Service name.")]
        public string ServiceName { get; set; }

        [Value(0, Required = true, HelpText = "Network configuration name.")]
        public string NcName { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Switchover to the specified network configuration <nc-1> in service <s> using configuration file <config_file>",
                    new SwitchoverToOptions { NcName = "nc-1", Config = "<config_file>", ServiceName = "<s>" });
            }
        }
    }
}
