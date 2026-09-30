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

        /// <summary>Where the stage cuts to open a floor.</summary>
        public static float CutHeight(float floor) => floor + CutAboveFloor;

        /// <summary>A room's floors above its root, from the top down: its doorways' heights (<see cref="Floors"/>); one at its root with none.</summary>
        public static List<float> RoomFloors(RoomShape room)
        {
            var floors = Floors(room?.Doorways.Select(d => d.Position.Y) ?? Enumerable.Empty<float>());
            if (floors.Count == 0) floors.Add(0f);
            return floors;
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
