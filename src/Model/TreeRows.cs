using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The plan box's tree of a dungeon's example: a row for each room more in from the entrance,
    /// the entrance alone at the top (<see cref="DungeonWays.StepsIn"/>), each room hanging under
    /// the room it is first reached from on the way in (<see cref="DungeonWays.CameFrom"/>), the
    /// rooms hanging under one room in the order they were placed. A room with none under it has
    /// a place of its own, all such places evenly across; a room with some stands over the middle
    /// of them, so each branch keeps its own span and the tree's lines never cross; ways closing a
    /// loop are drawn apart. Rooms no way reaches stand in a row of their own under the rest, after
    /// the tree. The tree stands the same whichever way the view turns, and the same ways lay out
    /// the same each time.
    /// </summary>
    internal sealed class TreeRows
    {
        private readonly Dictionary<PlacedRoom, int> _row = new Dictionary<PlacedRoom, int>();
        private readonly Dictionary<PlacedRoom, float> _across = new Dictionary<PlacedRoom, float>();
        private readonly Dictionary<PlacedRoom, int> _order = new Dictionary<PlacedRoom, int>();
        private readonly Dictionary<PlacedRoom, float> _room = new Dictionary<PlacedRoom, float>();

        /// <summary>How many rows the tree has.</summary>
        public int Rows { get; private set; }

        /// <summary>A room's row, from 0 at the entrance; -1 for a room not among the ways.</summary>
        public int RowOf(PlacedRoom room) => _row.TryGetValue(room, out var row) ? row : -1;

        /// <summary>Where a room stands across, from 0 to 1.</summary>
        public float AcrossOf(PlacedRoom room) => _across.TryGetValue(room, out var across) ? across : 0.5f;

        /// <summary>A room's place in its row, from 0 at the left.</summary>
        public int OrderInRow(PlacedRoom room) => _order.TryGetValue(room, out var order) ? order : 0;

        /// <summary>How far across a room has before the nearest room of its row stands, from 0 to 1; the whole width for one alone.</summary>
        public float RoomInRow(PlacedRoom room) => _room.TryGetValue(room, out var space) ? space : 1f;

        /// <summary>Lays out the tree of the ways.</summary>
        public static TreeRows Of(DungeonWays ways)
        {
            var tree = new TreeRows();
            var placed = ways.Rooms.ToList();
            var deepest = placed.Select(ways.StepsIn).DefaultIfEmpty(0).Max();
            var rows = 0;
            foreach (var room in placed)
            {
                var steps = ways.StepsIn(room);
                var row = steps >= 0 ? steps : deepest + 1;
                tree._row[room] = row;
                rows = System.Math.Max(rows, row + 1);
            }
            tree.Rows = rows;

            // The rooms hanging under each room, in the order they were placed.
            var under = new Dictionary<PlacedRoom, List<PlacedRoom>>();
            foreach (var room in placed)
            {
                var from = ways.CameFrom(room);
                if (from == null) continue;
                if (!under.TryGetValue(from, out var hanging)) under[from] = hanging = new List<PlacedRoom>();
                hanging.Add(room);
            }

            // Places from the left, one for each room with none under it, the rest over the middle of theirs.
            var places = 0f;
            float Place(PlacedRoom room)
            {
                if (!under.TryGetValue(room, out var hanging))
                {
                    var own = places + 0.5f;
                    places += 1f;
                    return tree._across[room] = own;
                }
                var first = Place(hanging[0]);
                var last = first;
                for (var i = 1; i < hanging.Count; i++) last = Place(hanging[i]);
                return tree._across[room] = (first + last) / 2f;
            }
            // From the first room placed, the entrance, then any no way reaches.
            foreach (var room in placed)
            {
                if (!tree._across.ContainsKey(room)) Place(room);
            }
            foreach (var room in placed) tree._across[room] /= System.Math.Max(1f, places);

            // Each room's place in its row and the room it has there.
            foreach (var row in placed.GroupBy(r => tree._row[r]))
            {
                var inRow = row.OrderBy(r => tree._across[r]).ToList();
                for (var i = 0; i < inRow.Count; i++)
                {
                    tree._order[inRow[i]] = i;
                    var space = 1f;
                    if (i > 0) space = System.Math.Min(space, tree._across[inRow[i]] - tree._across[inRow[i - 1]]);
                    if (i < inRow.Count - 1) space = System.Math.Min(space, tree._across[inRow[i + 1]] - tree._across[inRow[i]]);
                    tree._room[inRow[i]] = space;
                }
            }
            return tree;
        }
    }
}
