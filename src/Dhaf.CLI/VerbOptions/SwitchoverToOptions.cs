using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.CLI
{
    [Verb("switchover-to", HelpText = "Switchover to the specified entry point.")]
    public class SwitchoverToOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Option('s', "service", Required = true, HelpText = "Service name.")]
        public string ServiceName { get; set; }

        [Value(0, Required = true, HelpText = "Entry point name.")]
        public string EpName { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Switchover to the specified entry point <nc-1> in service <s> using configuration file <config_file>",
                    new SwitchoverToOptions { EpName = "ep-1", Config = "<config_file>", ServiceName = "<s>" });
            }
        }
    }
}
