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
    /// away. A room's ground is its own main floor where its rays were read and it has room to
    /// stand about, as a room's box reaches past its floor, else its lowest doorway, where it is
    /// walked into.
    /// </summary>
    internal static class RoomFocus
    {
        /// <summary>How near another room's ground must be to that of the room gone into to be on its level, in metres.</summary>
        public const float SameLevel = 1.5f;

        /// <summary>Whether a room's main floor is one to stand about on: as much room as a level of its own needs (<see cref="RoomLevels.LeastRoom"/>), where a tunnel's scrap of flat rock has a few square metres.</summary>
        public static bool RealFloor(float? mainFloor, float room) => mainFloor.HasValue && room >= RoomLevels.LeastRoom;

        /// <summary>A room's ground: its own main floor where that is a real floor (<see cref="RealFloor"/>), else its lowest doorway, else the foot of its box.</summary>
        public static float Ground(float? mainFloor, float room, IReadOnlyList<float> doorways, float foot)
        {
            if (RealFloor(mainFloor, room)) return mainFloor.Value;
            return doorways.Count > 0 ? doorways.Min() : foot;
        }

        /// <summary>How a room shows with another gone into, by the two rooms' ground.</summary>
        public static RoomLook Look(bool goneInto, float ground, float groundGoneInto)
        {
            if (goneInto) return RoomLook.Whole;
            if (Math.Abs(ground - groundGoneInto) <= SameLevel) return RoomLook.Near;
            return ground < groundGoneInto ? RoomLook.Below : RoomLook.Away;
        }

        /// <summary>How far the cut must rise past the ground of a room above the room gone into, and past where the room was first cut, for it to come back, in metres.</summary>
        public const float BackOver = 0.5f;

        /// <summary>
        /// A room's look as the cut over the room gone into is raised: one above, put away, comes
        /// back half dimmed once the cut is half a metre over its ground; one standing over the room
        /// gone into (<paramref name="over"/>, by its box seen from above) only once the cut is also
        /// raised from where the room was first cut (<paramref name="from"/>), so a shaft cut a head's
        /// height over its highest doorway does not bring back a room over it there; every one with
        /// the room's roof on; no other look changes.
        /// </summary>
        public static RoomLook WithCut(RoomLook look, float ground, float cut, float from, bool over) =>
            look == RoomLook.Away && cut > ground + BackOver && (!over || cut > from + BackOver) ? RoomLook.Near : look;

        /// <summary>
        /// Whether a room higher than the level opened would hide it, and is put away: one standing
        /// over a room of it (<paramref name="over"/>, by its box seen from above), or one whose floor
        /// is not half a metre under the cut, of which only what is under its floor would show.
        /// </summary>
        public static bool HidesFromAbove(bool over, float ground, float cut) => over || cut <= ground + BackOver;

        /// <summary>Whether the plan from above, its switch for the rooms below off, leaves a room out: one whose ground lies below the level looked at, the room gone into's or the floor opened; none with neither.</summary>
        public static bool HiddenBelow(float ground, float? level) => level is float at && ground < at - SameLevel;

        /// <summary>Where the stage is cut with a room gone into: head height over the level opened, under the ceiling over it where that is lower (<see cref="PlaceView.CutHeight(float, float?, float?)"/>).</summary>
        public static float Cut(float level, float? ceiling) => PlaceView.CutHeight(level, null, ceiling);
    }
}
