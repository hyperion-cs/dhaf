using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.CLI
{
    [Verb("status-service", HelpText = "Show service status information.")]
    public class StatusServiceOptions : IConfigPath
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
                yield return new Example("Show service <s> status information using configuration file <config_file>",
                    new StatusServiceOptions { Config = "<config_file>", ServiceName = "<s>" });
            }
        }
    }
}
