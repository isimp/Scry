using System;

namespace Scry
{
    /// <summary>How the plan box stands on the stage: whole, without its trail and its Entrance and Deepest, or folded to its tab.</summary>
    internal enum BoxShape
    {
        Whole,
        Lean,
        Folded,
    }

    /// <summary>
    /// How the card of the room gone into, in the stage's upper left corner, and the plan box, in
    /// its lower left, share that side so neither covers the other: the plan box stands as tall as
    /// it wants where the stage has room; the card gives way to it, as tall as it wants or as
    /// much as is left over the box, scrolling the rest, down to its least; only then does the
    /// plan give way, down to its own least. On a stage too short for the box whole at its least,
    /// the box drops its trail and its Entrance and Deepest, and with a room gone into, still too
    /// short, folds to its tab. Lengths are the panel's, each constant scaled with it.
    /// </summary>
    internal static class CornerFit
    {
        /// <summary>How far under the stage's top the card begins, and the plan box at the highest, under the kind's tag.</summary>
        public const float Top = 37f;

        /// <summary>How far over the stage's bottom the plan box ends, over the note under the picture.</summary>
        public const float Bottom = 34f;

        /// <summary>The gap between the card and the plan box.</summary>
        public const float Gap = 8f;

        /// <summary>The card's least height: its name, where it is and a couple of lines to scroll.</summary>
        public const float CardLeast = 96f;

        /// <summary>The plan's least height.</summary>
        public const float PlanLeast = 60f;

        /// <summary>
        /// The plan's height in the plan box: as wanted, else what the stage leaves over the box's
        /// other rows, under the kind's tag or, with a card, under the card at its least; never
        /// under its own least.
        /// </summary>
        public static float Plan(float stage, float rows, float wanted, bool card, float scale)
        {
            var over = card ? Top + CardLeast + Gap : Top;
            var room = stage - (Bottom + over) * scale - rows;
            return Math.Max(PlanLeast * scale, Math.Min(wanted, room));
        }

        /// <summary>The card's height: as wanted, else what is left between its top and the gap over the plan box; never under its least.</summary>
        public static float Card(float wanted, float stageTop, float boxTop, float scale)
        {
            var room = boxTop - Gap * scale - (stageTop + Top * scale);
            return Math.Min(wanted, Math.Max(room, CardLeast * scale));
        }

        /// <summary>
        /// How the plan box stands: whole where the stage holds it so at its least, under the
        /// kind's tag or, with a room gone into, under the card at its least; else without its
        /// trail and its Entrance and Deepest; else, with a room gone into, folded to its tab.
        /// </summary>
        public static BoxShape Shape(float stage, float wholeRows, float leanRows, bool card, float scale)
        {
            var over = card ? Top + CardLeast + Gap : Top;
            var room = stage - (over + Bottom + PlanLeast) * scale;
            if (room >= wholeRows) return BoxShape.Whole;
            return room >= leanRows || !card ? BoxShape.Lean : BoxShape.Folded;
        }

        /// <summary>Whether the card scrolls, wanting more than it has.</summary>
        public static bool Scrolls(float wanted, float height) => wanted > height + 0.5f;
    }
}
