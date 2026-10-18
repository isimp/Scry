namespace Scry
{
    /// <summary>
    /// What each of the selection's buttons does, as its tip says it. A button that stays lit
    /// while what it started goes on says, while lit, that a click undoes it.
    /// </summary>
    internal static class ActionWords
    {
        /// <summary>A sound's play button.</summary>
        public static string PlaySound(int variants) => variants > 1 ? "Play one of its clips, picked at random as the game picks" : "Play it";

        /// <summary>A sound's pause button.</summary>
        public static string PauseSound(bool paused) => paused ? "Go on from where it was paused" : "Pause it where it is";

        /// <summary>A sound's stop button.</summary>
        public const string StopSound = "Stop it";

        /// <summary>The one repeat switch, for whatever plays.</summary>
        public static string Repeat(bool on) => on
            ? "Stop repeating clips, effects and sounds"
            : "Play clips, effects and sounds again each time they end";

        /// <summary>An effect played where you look.</summary>
        public static string PlayThere(bool playing) => playing ? "Stop it" : "Play it in the world where you look";

        /// <summary>An effect played on you.</summary>
        public static string PlayOnYou(bool playing) => playing ? "Stop it" : "Play it on you, as the game plays it on a player";

        /// <summary>The replay button of effects.</summary>
        public const string Replay = "Play it again on the stage, for an effect that has played out";

        /// <summary>A raid's roll.</summary>
        public const string RollRaid = "Roll the raid's creatures again, as the game rolls them";

        /// <summary>A biome's search for what is there.</summary>
        public const string EverythingHere = "Search for everything that lives, grows or is found in this biome";

        /// <summary>A place's roll.</summary>
        public const string RollPlace = "Roll again what the game leaves to chance here: which chests, piles and creatures appear";

        /// <summary>A dungeon's or camp's new layout.</summary>
        public const string AnotherLayout = "Lay out another example, as the game might in another world";

        /// <summary>A projectile's fire button.</summary>
        public const string Fire = "Fire it from you toward where you look";

        /// <summary>An item worn by the person on the stage.</summary>
        public static string Wear(bool worn) => worn ? "Take it off the person" : "Put it on a person on the stage";

        /// <summary>An item kept on the person while other things are looked at.</summary>
        public static string Keep(bool kept) => kept ? "Take it off when you look at something else" : "Keep it on the person while you look at other things";

        /// <summary>A creature's ragdoll.</summary>
        public static string Ragdoll(bool falling) => falling ? "Put it back" : "Let it fall as its ragdoll, on the stage and in the world";

        /// <summary>A log or an item let fall.</summary>
        public static string LetFall(bool falling) => falling ? "Put it back" : "Let it fall and roll, on the stage and in the world";

        /// <summary>The selection shown in the world.</summary>
        public static string InWorld(bool shown) => shown ? "Take the preview out of the world" : "Put a preview in the world where you look, which only you see";

        /// <summary>The copy in the world moved.</summary>
        public const string MoveHere = "Move the preview to where you look";

        /// <summary>The copy in the world left standing.</summary>
        public const string Pin = "Leave this preview standing, so the next can be shown beside it";

        /// <summary>A status effect's look on you.</summary>
        public static string ShowStatus(bool showing) => showing ? "Take its look off you" : "Show its look on you; the effect itself is never applied";

        /// <summary>One of a status effect's other effect lists, played on you.</summary>
        public static string PlayStatusList(bool playing) => playing ? "Stop it" : "Play it on you once";

        /// <summary>The copy button beside the name.</summary>
        public static string CopyName(string prefab) => $"Copy {prefab}, the name the console and mods use";
    }
}
