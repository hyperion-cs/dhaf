namespace Dhaf.Node
{
    public class DecisionOfNetworkConfigurationSwitching
    {
        public bool IsRequired { get; set; }
        public bool Failover { get; set; }

        public string SwitchTo { get; set; }
    }
}
