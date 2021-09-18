﻿using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.CLI
{
    [Verb("switchover-purge", HelpText = "Purge the switchover requirement.")]
    public class SwitchoverPurgeOptions : IConfigPath
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
                yield return new Example("Purge the switchover requirement in service <s> using configuration file <config_file>",
                    new SwitchoverPurgeOptions { Config = "<config_file>", ServiceName = "<s>" });
            }
        }
    }
}
