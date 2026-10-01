using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The parts a large copy is woken in, a few each frame, so that showing a location with
    /// thousands of parts does not hold the game up. Each part woken holds no more than so many
    /// parts with itself; one holding more is gone into, and wakes itself with the copy while its
    /// own parts wait their turn. Parts switched off stay as they are and do not count. A copy no
    /// bigger than one such part wakes at once (none are given).
    /// </summary>
    public static class WakeChunks
    {
        /// <summary>
        /// The parts to wake one after another, in the order they hang in the copy, from each
        /// part's parent (-1 for the root) and whether it is switched on itself.
        /// </summary>
        public static List<int> Pick(IReadOnlyList<int> parent, IReadOnlyList<bool> on, int most)
        {
            var chunks = new List<int>();
            var count = parent.Count;
            var children = new List<int>[count];
            var root = -1;
            for (var i = 0; i < count; i++)
            {
                var up = parent[i];
                if (up < 0) root = i;
                else (children[up] ?? (children[up] = new List<int>())).Add(i);
            }
            if (root < 0 || !on[root]) return chunks;

            // How many parts each one wakes with itself: none for one switched off.
            var size = new int[count];
            var order = new List<int>(count);
            var stack = new Stack<int>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var at = stack.Pop();
                order.Add(at);
                if (children[at] == null) continue;
                foreach (var child in children[at]) if (on[child]) stack.Push(child);
            }
            for (var i = order.Count - 1; i >= 0; i--)
            {
                var at = order[i];
                size[at] = 1;
                if (children[at] == null) continue;
                foreach (var child in children[at]) size[at] += size[child];
            }
            if (size[root] <= most) return chunks;

            Go(root, children, size, most, chunks);
            return chunks;
        }

        private static void Go(int part, List<int>[] children, int[] size, int most, List<int> chunks)
        {
            if (children[part] == null) return;
            foreach (var child in children[part])
            {
                if (size[child] == 0) continue;
                if (size[child] <= most) chunks.Add(child);
                else Go(child, children, size, most, chunks);
            }
        }
    }
}
