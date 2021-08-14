namespace Dhaf.CloudflareSwitch.DataTransferObjects
{
    public class DnsRecordDto
    {
        public string Id { get; set; }
        public string Content { get; set; }
        public bool Proxied { get; set; }
    }
}
