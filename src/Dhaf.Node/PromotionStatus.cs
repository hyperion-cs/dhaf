namespace Dhaf.Node
{
    public class PromotionStatus
    {
        /// <summary>Flag the success of the promotion of the current node to cluster leader.</summary>
        public bool Success { get; set; }

        /// <summary>The node name of the current cluster leader.</summary>
        public string Leader { get; set; }

        /// <summary>If it was successful to become a leader, it contains the lease id of the leader's key.</summary>
        public long? LeaderLeaseId { get; set; }
    }
}
