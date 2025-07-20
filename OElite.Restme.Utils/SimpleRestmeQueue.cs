using System.Collections.Generic;

namespace OElite
{
    public class SimpleRestmeQueue<TA>
    {
        public bool ExecutionWaitRequired = false;

        public required List<TA> QueueItems;
    }
}