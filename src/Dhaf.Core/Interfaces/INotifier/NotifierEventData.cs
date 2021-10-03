using Dhaf.Core;
using System;
using System.Collections.Generic;

namespace Dhaf.NotifierEventData
{
    public class Base : INotifierEventData
    {
        public DateTime UtcTimestamp { get; set; }

        public string DhafCluster { get; set; }
        public string Service { get; set; }
    }

    // Note: "Ep" is "Entry point".

    public class EpHealthChanged : Base
    {
        public string EpName { get; set; }
        public IEnumerable<string> Reasons { get; set; }
    }

    public class CurrentEpChanged : Base
    {
        public string FromEp { get; set; }
        public string ToEp { get; set; }
    }

    public class SwitchoverPurged : Base
    {
        public string SwitchoverEp { get; set; }
    }

    public class DhafNewLeader : Base
    {
        public string Leader { get; set; }
    }

    public class DhafNodeHealthChanged : Base
    {
        public string NodeName { get; set; }
    }

    public class ServiceHealthChanged : Base { }
}
