using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What the character acted on lately, used or hit, and where, for the compendium's strict
    /// rules: an item that comes to be or is picked up near one of them soon after came from it
    /// (a smelter's bar, a beehive's honey, a rock's ore), where the catalog says that thing gives
    /// it; and what left the inventory on using something was put into it (ore, fuel, an offering).
    /// </summary>
    internal sealed class ActedOn
    {
        /// <summary>How near a thing acted on an item must come to be, in metres, to have come from it.</summary>
        public const float Near = 8f;

        /// <summary>How long after acting on a thing an item still counts as from it, in seconds: long enough for a smelter's last bar.</summary>
        public const float Lately = 600f;

        /// <summary>How many things acted on are kept, the latest.</summary>
        public const int Kept = 32;

        private readonly List<(string Key, float X, float Z, float At)> _things = new List<(string, float, float, float)>();

        /// <summary>How many things acted on are kept now.</summary>
        public int Count => _things.Count;

        /// <summary>The character acted on a thing, by its key, where it stands, at a time; the same thing again is kept from then.</summary>
        public void Add(string key, float x, float z, float at)
        {
            if (string.IsNullOrEmpty(key)) return;
            _things.RemoveAll(t => t.Key == key && Distance(t, x, z) <= Near);
            _things.Add((key, x, z, at));
            if (_things.Count > Kept) _things.RemoveRange(0, _things.Count - Kept);
        }

        /// <summary>The things acted on lately near where an item came to be that give it, by <paramref name="gives"/> (thing, item), each once.</summary>
        public List<string> Givers(string item, float x, float z, float at, Func<string, string, bool> gives) =>
            _things.Where(t => at >= t.At && at - t.At <= Lately && Distance(t, x, z) <= Near && gives(t.Key, item)).Select(t => t.Key).Distinct().ToList();

        /// <summary>
        /// Whether a thing gives an item itself (<paramref name="gives"/>), or through what it leaves
        /// behind, and that in turn (<paramref name="leavesBehind"/>): a copper deposit breaks into
        /// its fractured copy, which drops the ore; a tree falls into a log, which gives the wood.
        /// </summary>
        public static bool GivesThrough(string thing, string item, Func<string, IEnumerable<string>> leavesBehind, Func<string, string, bool> gives)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var next = new Queue<string>();
            next.Enqueue(thing);
            while (next.Count > 0)
            {
                var at = next.Dequeue();
                if (string.IsNullOrEmpty(at) || !seen.Add(at)) continue;
                if (gives(at, item)) return true;
                foreach (var left in leavesBehind(at)) next.Enqueue(left);
            }
            return false;
        }

        /// <summary>Forgets everything acted on, as on leaving a world.</summary>
        public void Clear() => _things.Clear();

        /// <summary>The items fewer in the inventory after than before, by prefab and count: what was put into the thing used.</summary>
        public static List<string> Taken(IReadOnlyDictionary<string, int> before, IReadOnlyDictionary<string, int> after) =>
            before.Where(b => (after.TryGetValue(b.Key, out var left) ? left : 0) < b.Value).Select(b => b.Key).ToList();

        /// <summary>
        /// Of what a station turns into an item, what the character was seen put into it, by
        /// <paramref name="witnessed"/> (station, input): the ore that made the bar.
        /// </summary>
        public static List<string> TurnedFrom(string station, IEnumerable<string> inputs, Func<string, string, bool> witnessed) =>
            inputs.Where(input => witnessed(station, input)).Distinct().ToList();

        private static float Distance((string Key, float X, float Z, float At) thing, float x, float z)
        {
            var dx = thing.X - x;
            var dz = thing.Z - z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
