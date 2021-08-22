namespace Dhaf.Node
{
    public class DemotionStatus
    {
        /// <summary>Flag the success of the demotion of the current node to cluster follower.</summary>
        public bool Success { get; set; }

        /// <summary>The node name of the current cluster leader.</summary>
        public string Leader { get; set; }
    }
}
