using System.Collections.Generic;
using System.Linq;

namespace LumaShot
{
    // Elapsed monotonic seconds: changing the system clock must not change preview lifetime.
    internal sealed class CapturePreviewLifetime
    {
        readonly Dictionary<string, double> deadlines = new Dictionary<string, double>();

        internal void Add(string id, double now)
        {
            deadlines[id] = now + 15;
        }

        internal bool IsExpired(string id, double now)
        {
            double deadline;
            return deadlines.TryGetValue(id, out deadline) && now >= deadline;
        }

        internal void Retain(IEnumerable<string> visible)
        {
            var retained = new HashSet<string>(visible);
            foreach (string id in deadlines.Keys.Where(id => !retained.Contains(id)).ToList())
                deadlines.Remove(id);
        }
    }
}
