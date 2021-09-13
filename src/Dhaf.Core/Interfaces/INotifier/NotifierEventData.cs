using System;

namespace Dhaf.Core
{
    public interface INotifierEventData
    {
        string DhafCluster { get; }
        string Domain { get; }
        DateTime Timestamp { get; }
    }

    public class BaseEventData : INotifierEventData
    {
        public DateTime Timestamp { get; set; }

        public string DhafCluster { get; set; }
        public string Domain { get; set; }
    }

    public class NcHealthChangedEventData : BaseEventData
    {
        public string NcName { get; set; }
        public string Reason { get; set; }
    }

    public class CurrentNcChangedEventData : BaseEventData
    {
        public string FromNc { get; set; }
        public string ToNc { get; set; }
    }

    public class DhafNodeHealthChangedEventData : BaseEventData
    {
        public string NodeName { get; set; }
    }

    public class ServiceHealthChangedEventData : BaseEventData { }
}
