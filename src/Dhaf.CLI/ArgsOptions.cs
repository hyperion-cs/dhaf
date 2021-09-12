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

    [Verb("switchover-purge", HelpText = "Purge the switchover requirement.")]
    public class SwitchoverPurgeOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Purge the switchover requirement using configuration file <config_file>",
                    new SwitchoverPurgeOptions { Config = "<config_file>" });
            }
        }
    }

    [Verb("switchover-to", HelpText = "Switchover to the specified network configuration.")]
    public class SwitchoverToOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Value(0, Required = true, HelpText = "Network configuration name.")]
        public string NcName { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Switchover to the specified network configuration <nc-1> using configuration file <config_file>",
                    new SwitchoverToOptions { NcName = "nc-1", Config = "<config_file>" });
            }
        }
    }

    [Verb("node-decommission", HelpText = "Decommission the dhaf node.")]
    public class NodeDecommissionOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Value(0, Required = true, HelpText = "Node name.")]
        public string NodeName { get; set; }


        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Decommission the dhaf node <n1> using configuration file <config_file>",
                    new NodeDecommissionOptions { Config = "<config_file>", NodeName = "n1" });
            }
        }
    }
}
