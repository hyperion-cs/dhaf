using System;

namespace Dhaf.Core
{
    public interface INotifierEventData
    {
        string DhafCluster { get; }
        DateTime UtcTimestamp { get; }
    }
}
