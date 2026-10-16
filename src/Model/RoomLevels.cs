using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The levels inside one room of a dungeon's example, from its own rays: its ground, and each
    /// other level of its own it stands on (<see cref="FloorFinder.StandsOn"/>) with at least
    /// <see cref="LeastRoom"/> to stand about on and at least <see cref="LeastApart"/> from its
    /// ground and from every other level, the bigger kept where two are nearer. Read from the
    /// reference dungeons' rooms: a tall Mörkhalla hall's second storey and a cave's gallery
    /// high up are levels; steps, landings and platforms near the ground are none.
    /// </summary>
    internal static class RoomLevels
    {
        /// <summary>How much room a level must have to stand about on, in square metres.</summary>
        public const float LeastRoom = 30f;

        /// <summary>How far apart levels must lie: head height, so a cut over each shows something else.</summary>
        public const float LeastApart = PlaceView.CutAboveFloor;

        /// <summary>A room's levels from the lowest up, its <paramref name="ground"/> among them; only that without its rays read.</summary>
        public static List<float> Of(FloorPatch patch, float ground)
        {
            var levels = new List<float> { ground };
            if (patch != null)
            {
                var candidates = patch.Bands.Values
                    .Where(b => b.Landed && b.Count > 0 && b.Room >= LeastRoom)
                    .Select(b => (Height: (float)(b.Sum / b.Count), b.Room))
                    .Where(b => FloorFinder.StandsOn(patch, b.Height))
                    .OrderByDescending(b => b.Room);
                foreach (var (height, _) in candidates)
                {
                    if (levels.All(l => Math.Abs(l - height) >= LeastApart)) levels.Add(height);
                }
            }
            levels.Sort();
            return levels;
        }
    }
}
