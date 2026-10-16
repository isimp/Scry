using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The plan box's tree of a dungeon's example: a row for each room more in from the entrance,
    /// the entrance alone at the top (<see cref="DungeonWays.StepsIn"/>), and rooms no way
    /// reaches in a row of their own under the rest. Each row spreads evenly across; its rooms
    /// stand under the rooms they are reached from, by the middle of where those stand in the row
    /// above, so the ways between rows do not cross, and rooms under one room stand in the order
    /// they lie across the plan. The same ways lay out the same each time.
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

        /// <summary>Lays out the tree of the ways, rooms under one room in the order <paramref name="across"/> gives them across the plan.</summary>
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

            var placed = ways.Rooms.ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float Under(PlacedRoom room)
                {
                    var above = ways.WaysOf(room).Where(o => tree._row.TryGetValue(o, out var r) && r == i - 1 && tree._across.ContainsKey(o)).Select(o => tree._across[o]).ToList();
                    return above.Count > 0 ? above.Average() : 0.5f;
                }
                var keys = row.ToDictionary(r => r, r => (Under: i == 0 ? 0f : Under(r), Across: across(r), Placed: placed.IndexOf(r)));
                row.Sort((a, b) =>
                {
                    var under = keys[a].Under.CompareTo(keys[b].Under);
                    if (under != 0) return under;
                    var side = keys[a].Across.CompareTo(keys[b].Across);
                    return side != 0 ? side : keys[a].Placed.CompareTo(keys[b].Placed);
                });
                for (var j = 0; j < row.Count; j++) tree._across[row[j]] = (j + 0.5f) / row.Count;
            }
            return tree;
        }
    }
}
