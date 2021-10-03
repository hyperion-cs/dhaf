namespace Dhaf.Node
{
    public class DecisionOfEntryPointSwitching
    {
        public bool IsRequired { get; set; }
        public bool Failover { get; set; }

        /// <summary>
        /// Host ID for switching.
        /// </summary>
        public string SwitchTo { get; set; }
    }
}
