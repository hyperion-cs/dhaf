namespace Dhaf.Core
{
    public enum NotifierEvent
    {
        NcDown, NcUp,                    // NcHealthChanged
        Failover, Switchover, Switching, // CurrentNcChanged
        ServiceUp, ServiceDown,          // ServiceHealthChanged
        DhafNodeUp, DhafNodeDown         // DhafNodeHealthChanged
    }
}
