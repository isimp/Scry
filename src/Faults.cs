using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What went wrong in Scry's own work, each kind told once. Everything Scry does each frame is
    /// caught part by part, so one part that fails (on a mod's odd prefab, say) neither stops the
    /// rest nor fills the log a line a frame.
    /// </summary>
    internal static class Faults
    {
        private static readonly HashSet<string> Told = new HashSet<string>();

        /// <summary>Tells a failure in the log the first time this part fails this way.</summary>
        public static void Tell(string part, Exception ex)
        {
            if (ex == null) return;
            var key = part + "|" + ex.GetType().Name + "|" + ex.Message + "|" + TopFrame(ex);
            if (Told.Count > 500 || !Told.Add(key)) return;
            Plugin.Log.LogError($"Scry failed in {part} (told once): {ex}");
        }

        private static string TopFrame(Exception ex)
        {
            var trace = ex.StackTrace;
            if (string.IsNullOrEmpty(trace)) return "";
            var end = trace.IndexOf('\n');
            return end < 0 ? trace : trace.Substring(0, end);
        }
    }
}
