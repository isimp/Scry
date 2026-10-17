using System;

namespace Scry
{
    /// <summary>How the plan box shows a dungeon's example: seen from above, as its levels from above one under the other, or as a tree of its ways.</summary>
    internal enum PlanView
    {
        Above,
        Levels,
        Tree,
    }

    /// <summary>What the plan box of a dungeon's example says: its caption, views, buttons and their tips, how deep each level is, and how deep each node of the tree is.</summary>
    internal static class PlanBoxWords
    {
        /// <summary>What the caption says with no room under the mouse.</summary>
        public static string Caption(int rooms) => "Whole dungeon, " + Naming.Count(rooms, "room", "rooms");

        /// <summary>A node's depth in the tree: its ground's whole metres from the entrance's, signed.</summary>
        public static string Depth(float rise) => Numbers.Count((long)Math.Round(rise, MidpointRounding.AwayFromZero), signed: true);

        /// <summary>A level's depth beside its plan: its highest room's ground in whole metres from the entrance's.</summary>
        public static string LevelDepth(float rise) => Numbers.Count((long)Math.Round(rise, MidpointRounding.AwayFromZero), signed: true) + " m";

        public static string ViewName(PlanView view) => view == PlanView.Levels ? "Levels" : view == PlanView.Tree ? "Tree" : "Above";

        public static string ViewTip(PlanView view)
        {
            switch (view)
            {
                case PlanView.Levels: return "Each level from above, the highest first, turned with the view; rooms over each other line up, and ways down drop at their doorways";
                case PlanView.Tree: return "Each room by its ways, as walked from the entrance: a row for each room further in, left doors to the left, straight on below, right doors to the right";
                default: return "Seen from above, turned with the view, each room shaded by its depth";
            }
        }

        /// <summary>The tip of the switch for the rooms below the room gone into or the floor opened, in the plan from above.</summary>
        public static string BelowTip(bool shown) => shown
            ? "The plan from above shows the rooms below this level too; click to leave them out of it"
            : "The plan from above leaves out the rooms below this level, its ways down marked; click to show them";

        /// <summary>The way back out of a room gone into, under the plan.</summary>
        public const string Whole = "Whole dungeon";
        public const string WholeTip = "Comes back out to the whole dungeon, as Esc does";

        public static string GrowTip(bool big) => big ? "Shrinks the plan back" : "Grows the plan and names its rooms";

        /// <summary>The tip of the box folded as the stage is too short for it under the room's card.</summary>
        public const string TooShortTip = "The stage is too short for the plan under the room's card: drag the stage taller, or come back out of the room";

        public const string Entrance = "Entrance";
        public const string EntranceTip = "Goes into the entrance";
        public const string Deepest = "Deepest";
        public const string DeepestTip = "Goes into the deepest room";

        /// <summary>What stands between the rooms of the way from the entrance under the plan.</summary>
        public const string TrailStep = "  ›  ";

        /// <summary>What the trail says with no room gone into.</summary>
        public const string NoTrail = "The way from the entrance shows here";
    }
}
