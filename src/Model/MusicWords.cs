namespace Scry
{
    /// <summary>How music played for an entry is stopped, by where it was started.</summary>
    internal enum MusicStop
    {
        /// <summary>Started with Enter on a place, stopped with Enter again.</summary>
        Enter,

        /// <summary>Started by a click on a named piece, stopped by another.</summary>
        Click,

        /// <summary>Started by a click or Enter on a raid's or boss's music, stopped by either.</summary>
        ClickOrEnter,
    }

    /// <summary>What playing an entry's music says: what plays and how to stop it, that it stopped, or why nothing plays.</summary>
    internal static class MusicWords
    {
        /// <summary>Said when music playing for an entry is stopped.</summary>
        public const string Stopped = "Stopped its music.";

        /// <summary>Said for an entry without music of its own.</summary>
        public const string None = "It has no music of its own.";

        /// <summary>Said when its music is named but the game has no such piece.</summary>
        public const string NotFound = "Its music could not be found.";

        /// <summary>Said for a place whose model, which holds its music, has not loaded yet.</summary>
        public const string NotLoaded = "Its music is known once its model has loaded.";

        /// <summary>What plays, and how to stop it where it was started.</summary>
        public static string Playing(string name, MusicStop stop)
        {
            switch (stop)
            {
                case MusicStop.Enter:
                    return $"Playing {name}; Enter again stops it.";
                case MusicStop.Click:
                    return $"Playing {name}; click it again to stop it.";
                default:
                    return $"Playing {name}; click it or press Enter again to stop it.";
            }
        }
    }
}
