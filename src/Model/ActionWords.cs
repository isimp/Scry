namespace Scry
{
    /// <summary>
    /// What each of the selection's buttons does, as its tip says it. A button that stays lit
    /// while what it started goes on says, while lit, that a click undoes it.
    /// </summary>
    internal static class ActionWords
    {
        /// <summary>A sound's play button.</summary>
        public static string PlaySound(int variants) => variants > 1 ? "Plays one of its clips, picked at random as the game picks" : "Plays it";

        /// <summary>A sound's pause button.</summary>
        public static string PauseSound(bool paused) => paused ? "Goes on from where it was paused" : "Pauses it where it is";

        /// <summary>A sound's stop button.</summary>
        public const string StopSound = "Stops it";

        /// <summary>The one repeat switch, for whatever plays.</summary>
        public static string Repeat(bool on) => on
            ? "Repeating: clips, effects and sounds play again each time they end. Click to stop repeating"
            : "Plays clips, effects and sounds again each time they end";

        /// <summary>An effect played where you look.</summary>
        public static string PlayThere(bool playing) => playing ? "Stops it" : "Plays it in the world where you look";

        /// <summary>An effect played on you.</summary>
        public static string PlayOnYou(bool playing) => playing ? "Stops it" : "Plays it on you, as the game plays it on a player";

        /// <summary>The replay button of effects.</summary>
        public const string Replay = "Plays it again on the stage, for an effect that has played out";

        /// <summary>A raid's roll.</summary>
        public const string RollRaid = "Rolls the raid's creatures again, as the game rolls them";

        /// <summary>A biome's search for what is there.</summary>
        public const string EverythingHere = "Searches for everything that lives, grows or is found in this biome";

        /// <summary>A place's roll.</summary>
        public const string RollPlace = "Rolls again what the game leaves to chance here: which chests, piles and creatures appear";

        /// <summary>A dungeon's or camp's new layout.</summary>
        public const string AnotherLayout = "Lays it out anew, as the game might in another world";

        /// <summary>A projectile's fire button.</summary>
        public const string Fire = "Fires it from you toward where you look";

        /// <summary>An item worn by the person on the stage.</summary>
        public static string Wear(bool worn) => worn ? "Takes it off the person" : "Puts it on a person on the stage";

        /// <summary>An item kept on the person while other things are looked at.</summary>
        public static string Keep(bool kept) => kept ? "Takes it off when you look at something else" : "Keeps it on the person while you look at other things";

        /// <summary>A creature's ragdoll.</summary>
        public static string Ragdoll(bool falling) => falling ? "Puts it back" : "Lets it fall as its ragdoll, on the stage and in the world";

        /// <summary>A log or an item let fall.</summary>
        public static string LetFall(bool falling) => falling ? "Puts it back" : "Lets it fall and roll, on the stage and in the world";

        /// <summary>The selection shown in the world.</summary>
        public static string InWorld(bool shown) => shown ? "Takes the copy out of the world" : "Puts a copy in the world where you look, which only you see";

        /// <summary>The copy in the world moved.</summary>
        public const string MoveHere = "Moves the copy to where you look";

        /// <summary>The copy in the world left standing.</summary>
        public const string Pin = "Leaves this copy standing, so the next can be shown beside it";

        /// <summary>A status effect's look on you.</summary>
        public static string ShowStatus(bool showing) => showing ? "Takes its look off you" : "Shows its look on you; the effect itself is never applied";

        /// <summary>One of a status effect's other effect lists, played on you.</summary>
        public static string PlayStatusList(bool playing) => playing ? "Stops it" : "Plays it on you once";

        /// <summary>The copy button beside the name.</summary>
        public static string CopyName(string prefab) => $"Copies {prefab}, the name the console and mods use";
    }
}
