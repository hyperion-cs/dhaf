﻿using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Dhaf.CLI
{
    [Verb("status-services", HelpText = "Show all services status information.")]
    public class StatusServicesOptions : IConfigPath
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string Config { get; set; }

        [Usage(ApplicationAlias = Definitions.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Show all services status information using configuration file <config_file>",
                    new StatusServiceOptions { Config = "<config_file>" });
            }
        }
    }
}
