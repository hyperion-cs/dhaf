using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.CLI
{
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
