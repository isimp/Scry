using BepInEx.Logging;

namespace Scry
{
    /// <summary>
    /// The log, written one way: a failure through <see cref="Faults"/>; and here, a line Scry
    /// always writes, something a player should know about their game or PC, and a note of what
    /// a preview did, written only with <see cref="Settings.LogPreviews"/> on. Only this and
    /// <see cref="Faults"/> hold BepInEx's log.
    /// </summary>
    internal static class Log
    {
        /// <summary>BepInEx's log for Scry, given by the plugin as it wakes.</summary>
        public static ManualLogSource Source { get; set; }

        /// <summary>A line Scry always writes: the startup check, how long reading took, what was asked for.</summary>
        public static void Report(string line) => Source?.LogInfo(line);

        /// <summary>Something about the game or the PC a player should know, not a fault of Scry's: a key taken, no free layer.</summary>
        public static void Warn(string line) => Source?.LogWarning(line);

        /// <summary>A note for the log about what a preview did, written only when <see cref="Settings.LogPreviews"/> is on.</summary>
        public static void Note(string line)
        {
            if (Settings.LogPreviews) Source?.LogInfo(line);
        }
    }
}
