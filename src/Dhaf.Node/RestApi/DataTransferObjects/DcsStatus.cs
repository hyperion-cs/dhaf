namespace Dhaf.Node
{
    public class DcsStatus
    {
        public int NodesCount { get; set; }
        public int Majority { get; set; }
        public int FailureTolerance { get; set; }
        public bool DcsClusterHealthy { get; set; }
    }
}
