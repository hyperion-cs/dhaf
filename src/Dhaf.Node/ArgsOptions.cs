using CommandLine;

namespace Dhaf.Node
{
    public class ArgsOptions
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file.")]
        public string ConfigPath { get; set; }
    }
}
