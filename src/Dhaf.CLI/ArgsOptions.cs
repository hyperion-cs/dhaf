using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.Node
{
    public interface IConfigPath
    {
        public string Config { get; set; }
    }


    [Verb("status-dhaf", HelpText = "Show dhaf cluster status information.")]
    public class StatusDhafOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Show dhaf cluster status information using configuration file <config_file>",
                    new StatusDhafOptions { Config = "<config_file>" });
            }
        }
    }

    [Verb("status-service", HelpText = "Show service status information.")]
    public class StatusServiceOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Show service status information using configuration file <config_file>",
                    new StatusServiceOptions { Config = "<config_file>" });
            }
        }
    }
}
