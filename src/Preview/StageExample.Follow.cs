using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>The example following the fog laid again in place: rooms joining it are made after those standing, rooms leaving it are taken down, the rest stand as they were.</summary>
    internal sealed partial class StageExample
    {
        /// <summary>
        /// Follows the example it was begun for as the fog changes its rooms in place
        /// (<see cref="DungeonFog.Relay"/>): the rooms that left are taken down, their copies and
        /// what was read of them, and the rooms that joined are placed from the generator after
        /// those standing, made over the next frames as any room is; nothing for another example.
        /// </summary>
        public void Follow(DungeonExample of, IReadOnlyList<PlacedRoom> added, IReadOnlyList<PlacedRoom> removed)
        {
            if (Holder == null || Placed == null || !ReferenceEquals(Of, of)) return;
            foreach (var room in removed) TakeOut(room);
            var standing = new Dictionary<int, PlacedRoom>();
            foreach (var room in Placed.Rooms) if (room.SavedAt >= 0) standing[room.SavedAt] = room;
            foreach (var room in added)
            {
                var placed = of.FromGenerator(room, joined => joined.SavedAt >= 0 && standing.TryGetValue(joined.SavedAt, out var to) ? to : null);
                Placed.Rooms.Add(placed);
                if (placed.SavedAt >= 0) standing[placed.SavedAt] = placed;
            }
            Ways = IsDungeon ? DungeonWays.Of(Placed.Rooms) : null;
            ForgetLooks();
        }

        /// <summary>Takes a room out of the example, by its place in the saved rooms: its copy, being made or standing, and what was read and worked out of it.</summary>
        private void TakeOut(PlacedRoom room)
        {
            var at = room.SavedAt >= 0 ? Placed.Rooms.FindIndex(r => r.SavedAt == room.SavedAt) : -1;
            if (at < 0) return;
            var placed = Placed.Rooms[at];
            if (at == Next && Build != null)
            {
                Build.Cancel();
                Build = null;
                BuildRoll = null;
                BuildPaints = null;
            }
            Placed.Rooms.RemoveAt(at);
            if (at < Next) Next--;
            var copy = CopyOf(placed);
            if (copy != null)
            {
                _rooms.RemoveAll(pair => pair.Key == placed);
                Copies--;
                _dimmed.Remove(copy);
                _unlit.Remove(copy);
                _renderersOf.Remove(copy);
                Object.Destroy(copy);
            }
            if (_roomGround.TryGetValue(placed, out var ground))
            {
                _patches.Remove(ground);
                _roomGround.Remove(placed);
            }
            _loot.Remove(placed);
            _focusGrounds.Remove(placed);
            _focusOver.Remove(placed);
            _ownFloor.Remove(placed);
            if (Focused == placed) Unfocus();
        }

        /// <summary>Lets go of what was worked out over the rooms, to be worked out again with the rooms as they stand now.</summary>
        private void ForgetLooks()
        {
            _focusLooks.Clear();
            _focusRead = -1;
            _focusAcross = null;
            _countedAt = -1;
            _ownFloorRead = -1;
            _acrossRooms = -1;
            _acrossOf = null;
        }

        /// <summary>A room's copy standing, by the room's place in the saved rooms; none for a room not standing.</summary>
        [ForTest]
        public GameObject CopyAt(int savedAt)
        {
            foreach (var pair in _rooms) if (pair.Key.SavedAt == savedAt) return pair.Value;
            return null;
        }
    }
}
