using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a location or dungeon room stands on the stage, and how it is cut open to look into.</summary>
    public static class PlaceView
    {
        /// <summary>How far above a floor the stage cuts: over a person's head, so walls stand and the roof goes.</summary>
        public const float CutAboveFloor = 2.5f;

        /// <summary>Doorways closer in height than this are on one floor: a step or a slope, not a storey.</summary>
        public const float SameFloor = 2f;

        /// <summary>
        /// How far apart an example's floors stand at the least: a dungeon's rooms are a storey
        /// each, so ground less than this below the next, step after step (a cave's slope, a
        /// raised floor), is one floor.
        /// </summary>
        public const float Storey = 3f;

        /// <summary>How far under the floor above the cut stays, to take that floor away whole.</summary>
        public const float UnderFloorAbove = 0.6f;

        /// <summary>The lowest the cut goes over its floor, to keep what stands on it.</summary>
        public const float LeastHeadroom = 1.4f;

        /// <summary>Where the stage cuts to open a floor.</summary>
        public static float CutHeight(float floor) => CutHeight(floor, null);

        /// <summary>Where the stage cuts to open a floor: head height over it, or just under the floor above where that is lower, but never lower than <see cref="LeastHeadroom"/>.</summary>
        public static float CutHeight(float floor, float? above)
        {
            var cut = floor + CutAboveFloor;
            if (above.HasValue && cut > above.Value - UnderFloorAbove) cut = Math.Max(floor + LeastHeadroom, above.Value - UnderFloorAbove);
            return cut;
        }

        /// <summary>Where each of the floors (from the top down) is cut, each with the one above it.</summary>
        public static List<float> CutHeights(IReadOnlyList<float> floors) =>
            floors.Select((floor, i) => CutHeight(floor, i == 0 ? (float?)null : floors[i - 1])).ToList();

        /// <summary>
        /// The cut a floor down or up: down from the roof opens the top floor and stays on the
        /// lowest; up from the top floor puts the roof back on. <paramref name="level"/> is the
        /// floor opened, from the top, or <paramref name="floors"/> for the roof on.
        /// </summary>
        public static int StepCut(int level, int floors, bool down)
        {
            if (down) return level >= floors ? 0 : Math.Min(level + 1, floors - 1);
            if (level >= floors) return floors;
            return level == 0 ? floors : level - 1;
        }

        /// <summary>A location's floors with its ground among them, its root, where none lies near it (bare earth, the world's own, is no part of it).</summary>
        public static List<float> WithGround(IReadOnlyList<float> floors)
        {
            var all = floors.ToList();
            if (!all.Any(f => Math.Abs(f) < SameFloor)) all.Add(0f);
            return all.OrderByDescending(f => f).ToList();
        }

        /// <summary>The floor a cut set by hand opens: the highest it is half a metre or more over; the lowest below them all.</summary>
        public static int LevelAt(IReadOnlyList<float> floors, float height)
        {
            for (var i = 0; i < floors.Count; i++)
            {
                if (floors[i] + 0.5f <= height) return i;
            }
            return Math.Max(0, floors.Count - 1);
        }

        /// <summary>A room's floors above its root, from the top down: its doorways' heights (<see cref="Floors"/>); one at its root with none.</summary>
        public static List<float> RoomFloors(RoomShape room)
        {
            var floors = Floors(room?.Doorways.Select(d => d.Position.Y) ?? Enumerable.Empty<float>());
            if (floors.Count == 0) floors.Add(0f);
            return floors;
        }

        /// <summary>An example dungeon's or camp's floors, from the top down: where its rooms' doorways are.</summary>
        public static List<float> ExampleFloors(DungeonExample example) =>
            Floors(example.Rooms.SelectMany(r => Enumerable.Range(0, r.Room.Doorways.Count).Select(i => r.DoorwayAt(i).Y)));

        /// <summary>
        /// An example's floors with one more for each room that stands whole on none of them, by
        /// <paramref name="onFloor"/>: at its lowest doorway, where it is walked into, so every
        /// room can be opened and gone to (a sloping corridor has no flat ground to find). End
        /// caps and dividers, and rooms with no doorway, add none. From the top down.
        /// </summary>
        public static List<float> ReachingEveryRoom(List<float> floors, IEnumerable<PlacedRoom> rooms, Func<PlacedRoom, float, bool> onFloor)
        {
            var all = new List<float>(floors);
            foreach (var room in rooms)
            {
                if (room.Room.EndCap || room.Room.Divider || room.Room.Doorways.Count == 0) continue;
                if (all.Any(floor => onFloor(room, floor))) continue;
                all.Add(Enumerable.Range(0, room.Room.Doorways.Count).Min(i => room.DoorwayAt(i).Y));
            }
            return all.OrderByDescending(floor => floor).ToList();
        }

        /// <summary>Heights as floors, from the top down, those less than <see cref="SameFloor"/> below the last one kept taken as that one.</summary>
        public static List<float> Floors(IEnumerable<float> heights)
        {
            var floors = new List<float>();
            foreach (var height in heights.OrderByDescending(h => h))
            {
                if (floors.Count > 0 && floors[floors.Count - 1] - height < SameFloor) continue;
                floors.Add(height);
            }
            return floors;
        }

        /// <summary>What the stage's cut is called: the roof off one of so many floors, or on (<paramref name="level"/> past the last).</summary>
        public static string CutLabel(int level, int floors)
        {
            if (level >= floors) return "Roof on";
            if (floors == 1) return "Roof off";
            return level == 0 ? "Roof off, top floor" : $"Roof off, floor {level + 1} of {floors}";
        }

        /// <summary>
        /// The floor opened, named beside the ruler, from the top as the cut's chip counts them,
        /// with how many rooms of an example stand on it (none told when <paramref name="rooms"/>
        /// is below nought); null with the roof on, or for a lone floor with no rooms to count.
        /// </summary>
        public static string FloorLabel(int level, int floors, int rooms)
        {
            if (level >= floors) return null;
            var parts = new List<string>();
            if (floors > 1) parts.Add(level == 0 ? "Top floor" : $"Floor {level + 1} of {floors}");
            if (rooms >= 0) parts.Add(rooms == 0 ? "no rooms" : rooms == 1 ? "1 room" : $"{rooms} rooms");
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        /// <summary>
        /// Where a place's ground is, above its root: a location's is its root, where the game
        /// stands it on the terrain, which hides whatever of it reaches below; a room's is its
        /// lowest doorway, the floor it is walked into on (<c>RoomConnection</c>), as rooms are
        /// joined level at their doorways.
        /// </summary>
        public static float Ground(PlaceContents contents, bool room)
        {
            if (!room) return 0f;
            var doorways = contents?.Room?.Doorways;
            return doorways == null || doorways.Count == 0 ? 0f : doorways.Min(d => d.Position.Y);
        }
    }
}
