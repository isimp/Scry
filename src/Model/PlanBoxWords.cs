using System;

namespace Scry
{
    /// <summary>How the plan box shows a dungeon's example: seen from above, as a map across against depth, or as a tree of its ways.</summary>
    internal enum PlanView
    {
        Above,
        Map,
        Tree,
    }

    /// <summary>What the plan box of a dungeon's example says: its caption, views, buttons and their tips, the map's angle and overlaps, and how deep each node of the tree is.</summary>
    internal static class PlanBoxWords
    {
        /// <summary>What the caption says with no room under the mouse.</summary>
        public static string Caption(int rooms) => "Whole dungeon, " + Naming.Count(rooms, "room", "rooms");

        /// <summary>How many rooms cover each other on the map at the angle shown.</summary>
        public static string Overlapping(int pairs) => pairs == 0 ? "None overlapping" : Numbers.Count(pairs) + " overlapping";

        /// <summary>The map's angle on its dial, to the whole degree.</summary>
        public static string Angle(float angle) => Numbers.Count((long)Math.Round(angle, MidpointRounding.AwayFromZero) % 360) + "°";

        /// <summary>A node's depth in the tree: its ground's whole metres from the entrance's, signed.</summary>
        public static string Depth(float rise) => Numbers.Count((long)Math.Round(rise, MidpointRounding.AwayFromZero), signed: true);

        public static string ViewName(PlanView view) => view == PlanView.Map ? "Map" : view == PlanView.Tree ? "Tree" : "Above";

        public static string ViewTip(PlanView view)
        {
            switch (view)
            {
                case PlanView.Map: return "Each room across against its depth, the ground at the top; drag across it to turn it";
                case PlanView.Tree: return "Each room by its ways, a row for each room more in from the entrance, under the room it is reached from";
                default: return "Seen from above, turned with the view, each room shaded by its depth";
            }
        }

        public static string GrowTip(bool big) => big ? "Shrinks the plan back" : "Grows the plan and names its rooms";

        public const string LeastOverlap = "Least overlap";
        public const string LeastOverlapTip = "Turns the map to the angle where the fewest rooms cover each other";
        public const string Follow = "Follow the view";
        public const string FollowTip = "Turns the map with the stage, what stands right on the stage right on the map";
        public const string DialTip = "The map's angle; drag across the map to turn it";
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
