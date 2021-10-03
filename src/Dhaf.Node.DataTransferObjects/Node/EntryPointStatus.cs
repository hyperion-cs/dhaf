using System.Collections.Generic;

namespace Dhaf.Node
{
    public class EntryPointStatus
    {
        public string EntryPointId { get; set; }
        public bool Healthy { get; set; }

        public IEnumerable<string> Reasons { get; set; }
    }
}
