using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyriteCliCommon.Contracts
{
    public class SetInfo
    {
        public string Id { get; set; }
        public string Container { get; set; }
        public string Path { get; set; }
        public DateTime QueuedAt { get; set; }
        public string Status { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int ExpectedWorkItems { get; set; }
        public ICollection<WorkItem> InProgressWorkItems { get; set; }
        public ICollection<WorkItem> CompletedWorkItems { get; set; }
    }
}
