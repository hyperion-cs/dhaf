namespace Dhaf.Core
{
    public enum NotifierEvent
    {
        NcDown, NcUp,                    // NcHealthChanged
        Failover, Switchover, Switching, // CurrentNcChanged
        SwitchoverPurged,                // SwitchoverPurged
        ServiceUp, ServiceDown,          // ServiceHealthChanged
        DhafNodeUp, DhafNodeDown,        // DhafNodeHealthChanged
        DhafNewLeader                    // NewDhafLeader
    }
}
