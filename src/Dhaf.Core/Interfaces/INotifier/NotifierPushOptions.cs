namespace Dhaf.Core
{
    public class NotifierPushOptions
    {
        public string ServiceName { get; set; }

        public NotifierLevel Level { get; set; }

        public NotifierEvent Event { get; set; }
        public INotifierEventData EventData { get; set; }

    }
}
