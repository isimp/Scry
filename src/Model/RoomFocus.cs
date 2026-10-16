using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a room of a dungeon's example shows while one is gone into.</summary>
    internal enum RoomLook
    {
        /// <summary>As it is: the room gone into, or every room with none.</summary>
        Whole,

        /// <summary>Half dimmed: on the level of the room gone into.</summary>
        Near,

        /// <summary>Dimmed: below it.</summary>
        Below,

        /// <summary>Put away: above it, where the cut would leave it over the room.</summary>
        Away,
    }

    /// <summary>
    /// A dungeon example with one room gone into: that room stands whole, cut at head height over
    /// its own ground, the rooms on its level half dimmed, those below dimmed and those above put
    /// away. A room's ground is its own main floor where its rays were read, as a room's box
    /// reaches past its floor, else its lowest doorway, where it is walked into.
    /// </summary>
    internal static class RoomFocus
    {
        /// <summary>How near another room's ground must be to that of the room gone into to be on its level, in metres.</summary>
        public const float SameLevel = 1.5f;

        /// <summary>A room's ground: its own main floor where its rays found one, else its lowest doorway, else the foot of its box.</summary>
        public static float Ground(float? mainFloor, IReadOnlyList<float> doorways, float foot)
        {
            if (mainFloor is float main) return main;
            return doorways.Count > 0 ? doorways.Min() : foot;
        }

        /// <summary>How a room shows with another gone into, by the two rooms' ground.</summary>
        public static RoomLook Look(bool goneInto, float ground, float groundGoneInto)
        {
            if (goneInto) return RoomLook.Whole;
            if (Math.Abs(ground - groundGoneInto) <= SameLevel) return RoomLook.Near;
            return ground < groundGoneInto ? RoomLook.Below : RoomLook.Away;
        }

        /// <summary>Where the stage is cut with a room gone into: head height over the level opened, under the ceiling over it where that is lower (<see cref="PlaceView.CutHeight(float, float?, float?)"/>).</summary>
        public static float Cut(float level, float? ceiling) => PlaceView.CutHeight(level, null, ceiling);
    }
}
