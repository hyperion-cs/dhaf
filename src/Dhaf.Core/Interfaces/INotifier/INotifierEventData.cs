using System;

namespace Dhaf.Core
{
    public interface INotifierEventData
    {
        string Service { get; }
        string DhafCluster { get; }
        DateTime UtcTimestamp { get; }
    }
}
