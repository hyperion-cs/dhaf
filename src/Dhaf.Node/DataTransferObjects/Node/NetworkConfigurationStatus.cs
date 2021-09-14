using System.Collections.Generic;

namespace Dhaf.Node
{
    public class NetworkConfigurationStatus
    {
        public string NcId { get; set; }
        public bool Healthy { get; set; }

        public IEnumerable<string> Reasons { get; set; }
    }
}
