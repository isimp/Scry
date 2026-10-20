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

    /// <summary>What the plan box of a dungeon's example says: its caption, views, buttons and their tips, and how deep each level is.</summary>
    internal static class PlanBoxWords
    {
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

        /// <summary>The mark on the switch for the rooms' names on the grown plan.</summary>
        public const string NamesMark = "Aa";

        /// <summary>The tip of the switch for the rooms' names on the grown plan, in every view.</summary>
        public static string NamesTip(bool shown) => shown
            ? "Leave the rooms' names off the grown plan"
            : "Name the rooms on the grown plan";

        /// <summary>The tip of the switch for the rooms below the room gone into or the floor opened, in the plan from above.</summary>
        public static string BelowTip(bool shown) => shown
            ? "Leave the rooms below this level out of the plan from above"
            : "Show the rooms below this level on the plan from above, where only their ways down are marked now";

        /// <summary>The tip of the button that grows the plan, or shrinks it back; growing names no room by itself, the names switch on the grown plan does.</summary>
        public static string GrowTip(bool big) => big ? "Shrink the plan back" : "Grow the plan";

        /// <summary>The tip of the button that puts the plan away.</summary>
        public const string FoldTip = "Put the plan away";

        /// <summary>The tab the plan is folded to, and its tip, for an example and the world's own dungeon alike.</summary>
        public const string Tab = "Plan";
        public const string TabTip = "Bring the plan back";

        /// <summary>The tip of the box folded as the stage is too short for it under the room's card.</summary>
        public const string TooShortTip = "The stage is too short for the plan under the room's card: drag the stage taller, or come back out of the room";

        public const string Entrance = "Entrance";
        public const string EntranceTip = "Go into the entrance";
        public const string Deepest = "Deepest";
        public const string DeepestTip = "Go into the deepest room, the most rooms in from the entrance";

        /// <summary>What stands between the parts of the breadcrumb over a dungeon's inside.</summary>
        public const string TrailStep = "  ›  ";

        /// <summary>The tip of the breadcrumb's dungeon: how many rooms, and what a click does.</summary>
        public static string TrailDungeonTip(int rooms) => "The whole dungeon, " + Naming.Count(rooms, "room", "rooms") + ": see it all, the roof on";

        /// <summary>The tip of the breadcrumb's level: in a room, coming back out to it; on it, framing it again.</summary>
        public static string TrailLevelTip(bool inRoom) => inRoom ? "Come back out of the room to this level, as Esc does" : "Frame the level again";

        public const string TrailRoomTip = "Frame the room again";

        /// <summary>The mark of the menu beside the breadcrumb's dungeon, and its tip.</summary>
        public const string TrailMenuMark = "▾";
        public static string TrailMenuTip(bool switches) => switches
            ? "Go into the entrance or the deepest room, or switch between your world's dungeon and an example"
            : "Go into the entrance or the deepest room";

        /// <summary>The tag at the head of the breadcrumb over the world's own dungeon, and its tip.</summary>
        public const string ThisDungeon = "In your world";
        public const string ThisDungeonTip = "The rooms as they stand in your world";

        /// <summary>The tag of an example on the stage, where the world's own dungeon is tagged <see cref="ThisDungeon"/>.</summary>
        public const string Example = "Example";

        /// <summary>The tip of an example's tag.</summary>
        public const string ExampleTip = "Rolled by its kind's own rules: one way it can come out. Each one in your world lays out its own, tagged In your world once walked or opened from the big map.";
        public const string ThisDungeonTipFogged = "The rooms as they stand in your world, as far as you have walked them";

        /// <summary>What marks the room the player stands in on the breadcrumb, and the tip of their room there.</summary>
        public const string YouMark = " · you";
        public const string TrailYouTip = "Where you stand; go into it";

        /// <summary>How much of the world's own dungeon the player has walked, over the plan.</summary>
        public static string Walked(int walked, int rooms) => Numbers.Count(walked) + " of " + Naming.Count(rooms, "room", "rooms") + " walked";

        /// <summary>The warning over the plan while peeking shows the rooms not walked, short enough for the box; the rest in its tip.</summary>
        public static string PeekingRooms(int rooms) => "Peeking: " + Naming.Count(rooms, "room not walked", "rooms not walked") + " shown";
        public const string PeekingRoomsTip = "The rooms of this dungeon not walked yet show while you peek, and hide again once peeking stops";

        /// <summary>The tip of a doorway marked as leading on to a room not walked.</summary>
        public const string OpenWayTip = "A way on to a room not walked yet";

        /// <summary>The menu's switch from the world's own dungeon to an example and back.</summary>
        public const string ShowExample = "Show an example";
        public const string ShowExampleTip = "Lay out an example in place of your world's, as the game might in another world";
        public const string ShowThisDungeon = "Show it in your world";
        public const string ShowThisDungeonTip = "Show the dungeon as it stands in your world, in place of the example";
    }
}
