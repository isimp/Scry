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
            ? "The grown plan names its rooms; click to leave them off"
            : "The plan names no room; click to name them";

        /// <summary>The tip of the switch for the rooms below the room gone into or the floor opened, in the plan from above.</summary>
        public static string BelowTip(bool shown) => shown
            ? "The plan from above shows the rooms below this level too; click to leave them out of it"
            : "The plan from above leaves out the rooms below this level, its ways down marked; click to show them";

        /// <summary>The tip of the button that grows the plan, or shrinks it back; growing names no room by itself, the names switch on the grown plan does.</summary>
        public static string GrowTip(bool big) => big ? "Shrinks the plan back" : "Grows the plan";

        /// <summary>The tip of the button that puts the plan away.</summary>
        public const string FoldTip = "Puts the plan away";

        /// <summary>The tab the plan is folded to, and its tip, for an example and the world's own dungeon alike.</summary>
        public const string Tab = "Plan";
        public const string TabTip = "Brings the plan back";

        /// <summary>The tip of the box folded as the stage is too short for it under the room's card.</summary>
        public const string TooShortTip = "The stage is too short for the plan under the room's card: drag the stage taller, or come back out of the room";

        public const string Entrance = "Entrance";
        public const string EntranceTip = "Goes into the entrance";
        public const string Deepest = "Deepest";
        public const string DeepestTip = "Goes into the deepest room, the most rooms in from the entrance";

        /// <summary>What stands between the parts of the breadcrumb over a dungeon's inside.</summary>
        public const string TrailStep = "  ›  ";

        /// <summary>The tip of the breadcrumb's dungeon: how many rooms, and what a click does.</summary>
        public static string TrailDungeonTip(int rooms) => "The whole dungeon, " + Naming.Count(rooms, "room", "rooms") + ": click to see it all, the roof on";

        /// <summary>The tip of the breadcrumb's level: in a room, coming back out to it; on it, framing it again.</summary>
        public static string TrailLevelTip(bool inRoom) => inRoom ? "Click to come back out of the room to this level, as Esc does" : "Click to frame the level again";

        public const string TrailRoomTip = "Click to frame the room again";

        /// <summary>The mark of the menu beside the breadcrumb's dungeon, and its tip.</summary>
        public const string TrailMenuMark = "▾";
        public static string TrailMenuTip(bool switches) => switches
            ? "Goes into the entrance or the deepest room, or switches between this dungeon and an example"
            : "Goes into the entrance or the deepest room";

        /// <summary>The tag at the head of the breadcrumb over the world's own dungeon, and its tip.</summary>
        public const string ThisDungeon = "This dungeon";
        public const string ThisDungeonTip = "The rooms as they stand in your world; the menu beside the dungeon shows an example instead";
        public const string ThisDungeonTipFogged = "The rooms as they stand in your world, as far as you have walked them; the menu beside the dungeon shows an example instead";

        /// <summary>What marks the room the player stands in on the breadcrumb, and the tip of their room there.</summary>
        public const string YouMark = " · you";
        public const string TrailYouTip = "Where you stand: click to go into it";

        /// <summary>How much of the world's own dungeon the player has walked, over the plan.</summary>
        public static string Walked(int walked, int rooms) => Numbers.Count(walked) + " of " + Naming.Count(rooms, "room", "rooms") + " walked";

        /// <summary>The warning over the plan while peeking shows the rooms not walked.</summary>
        public static string PeekingRooms(int rooms) => "Peeking: " + Naming.Count(rooms, "room not walked is", "rooms not walked are") + " shown";

        /// <summary>The tip of a doorway marked as leading on to a room not walked.</summary>
        public const string OpenWayTip = "A way on to a room not walked yet";

        /// <summary>The menu's switch from the world's own dungeon to an example and back.</summary>
        public const string ShowExample = "Show an example";
        public const string ShowExampleTip = "Lays out an example in place of this dungeon, as the game might in another world";
        public const string ShowThisDungeon = "Show this dungeon";
        public const string ShowThisDungeonTip = "Shows this dungeon as it stands in your world, in place of the example";
    }
}
