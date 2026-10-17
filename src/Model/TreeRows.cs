using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The plan box's tree of a dungeon's example: a row for each room more in from the entrance,
    /// the entrance alone at the top (<see cref="DungeonWays.StepsIn"/>), and rooms no way
    /// reaches in a row of their own under the rest. Each room stands across where it lies across
    /// the dungeon, as the plan turned with the view has it, so the tree reads as the dungeon
    /// does; the fullest row spreads evenly, and no two rooms of a row stand nearer than it spreads
    /// its own, a room giving way as little as it can in the order they lie. The same ways lay out
    /// the same each time.
    /// </summary>
    internal sealed class TreeRows
    {
        private readonly Dictionary<PlacedRoom, int> _row = new Dictionary<PlacedRoom, int>();
        private readonly Dictionary<PlacedRoom, float> _across = new Dictionary<PlacedRoom, float>();

        /// <summary>How many rows the tree has.</summary>
        public int Rows { get; private set; }

        /// <summary>A room's row, from 0 at the entrance; -1 for a room not among the ways.</summary>
        public int RowOf(PlacedRoom room) => _row.TryGetValue(room, out var row) ? row : -1;

        /// <summary>Where a room stands across its row, from 0 to 1.</summary>
        public float AcrossOf(PlacedRoom room) => _across.TryGetValue(room, out var across) ? across : 0.5f;

        /// <summary>Lays out the tree of the ways, each room across where <paramref name="across"/> says it lies.</summary>
        public static TreeRows Of(DungeonWays ways, Func<PlacedRoom, float> across)
        {
            var tree = new TreeRows();
            var deepest = ways.Rooms.Select(ways.StepsIn).DefaultIfEmpty(0).Max();
            var rows = new List<List<PlacedRoom>>();
            foreach (var room in ways.Rooms)
            {
                var steps = ways.StepsIn(room);
                var row = steps >= 0 ? steps : deepest + 1;
                while (rows.Count <= row) rows.Add(new List<PlacedRoom>());
                rows[row].Add(room);
                tree._row[room] = row;
            }
            tree.Rows = rows.Count;
            if (rows.Count == 0) return tree;

            // Where each lies, from the westmost at the tree's left end to the eastmost at its
            // right, the ends half the gap in.
            var placed = ways.Rooms.ToList();
            var lies = placed.ToDictionary(r => r, across);
            var least = lies.Values.Min();
            var span = Math.Max(0.001f, lies.Values.Max() - least);
            var gap = 1f / rows.Max(r => r.Count);
            float Lies(PlacedRoom room) => gap / 2f + (lies[room] - least) / span * (1f - gap);

            foreach (var row in rows)
            {
                row.Sort((a, b) =>
                {
                    var side = lies[a].CompareTo(lies[b]);
                    return side != 0 ? side : placed.IndexOf(a).CompareTo(placed.IndexOf(b));
                });
                var at = row.Select(Lies).ToArray();
                // Each at least the gap after the one before it, then at least the gap before the one after it.
                at[0] = Math.Max(at[0], gap / 2f);
                for (var j = 1; j < at.Length; j++) at[j] = Math.Max(at[j], at[j - 1] + gap);
                at[at.Length - 1] = Math.Min(at[at.Length - 1], 1f - gap / 2f);
                for (var j = at.Length - 2; j >= 0; j--) at[j] = Math.Min(at[j], at[j + 1] - gap);
                for (var j = 0; j < row.Count; j++) tree._across[row[j]] = at[j];
            }
            return tree;
        }
    }
}
