using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.Core
{
    [Verb("run", HelpText = "Start dhaf cluster node.")]
    public class RunOptions
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Start dhaf cluster node using configuration file <config_file>",
                    new RunOptions { Config = "<config_file>" });
            }
        }
    }

    [Verb("status", HelpText = "Find out dhaf cluster status.")]
    public class StatusOptions
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Find out dhaf cluster status using configuration file <config_file>",
                    new StatusOptions { Config = "<config_file>" });
            }
        }
    }

    [Verb("switchover", HelpText = "Manually switch to master or replica")]
    public class SwitchoverOptions
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Option('t', "to", Required = true, HelpText = "Where you need to switch. Possible options: master, replica.")]
        public string To { get; set; }


        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Manually switch to master using configuration file <config_file>",
                    new SwitchoverOptions { Config = "<config_file>", To = "master" });
                yield return new Example("Manually switch to replica using configuration file <config_file>",
                    new SwitchoverOptions { Config = "<config_file>", To = "replica" });
            }
        }
    }
}
