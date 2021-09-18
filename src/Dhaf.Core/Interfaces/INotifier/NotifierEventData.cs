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

    public class NcHealthChanged : Base
    {
        public string NcName { get; set; }
        public IEnumerable<string> Reasons { get; set; }
    }

    public class CurrentNcChanged : Base
    {
        public string FromNc { get; set; }
        public string ToNc { get; set; }
    }

    public class SwitchoverPurged : Base
    {
        public string SwitchoverNc { get; set; }
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
