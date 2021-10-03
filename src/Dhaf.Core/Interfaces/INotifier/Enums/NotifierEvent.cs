namespace Dhaf.Core
{
    public enum NotifierEvent
    {
        // Note: "Ep" is "Entry point".

        EpDown, EpUp,                    // EpHealthChanged
        Failover, Switchover, Switching, // CurrentEpChanged
        SwitchoverPurged,                // SwitchoverPurged
        ServiceUp, ServiceDown,          // ServiceHealthChanged
        DhafNodeUp, DhafNodeDown,        // DhafNodeHealthChanged
        DhafNewLeader                    // DhafNewLeader
    }
}
