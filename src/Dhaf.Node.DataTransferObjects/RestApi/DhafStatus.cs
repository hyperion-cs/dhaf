using System.Collections.Generic;

namespace Dhaf.Node
{
    public class DhafStatus
    {
        public string Leader { get; set; }
        public IEnumerable<DhafNodeStatus> Nodes { get; set; }
    }
}
