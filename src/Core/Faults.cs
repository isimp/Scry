using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What went wrong in Scry's own work, each kind told once. Everything Scry does each frame is
    /// caught part by part, so one part that fails (on a mod's odd prefab, say) neither stops the
    /// rest nor fills the log a line a frame. A failure that means the game changed under Scry (a
    /// member an update renamed) is told as the feature it turns off, and the panel shows those
    /// features; one odd prefab's part is counted and told with the others once the catalog is read.
    /// </summary>
    [Diagnostic]
    internal static class Faults
    {
        private static readonly HashSet<string> Told = new HashSet<string>();
        private static readonly Trouble Found = new Trouble();

        /// <summary>The features a game update has turned off in this session, for the panel.</summary>
        public static IReadOnlyList<string> ChangedFeatures => Found.ChangedFeatures;

        /// <summary>How many failures of a part there have been this session, told or not, for the self-test to see any during a scenario.</summary>
        public static int Count { get; private set; }

        /// <summary>The latest failure, by its part and message.</summary>
        public static string Latest { get; private set; } = "";

        /// <summary>How many parts of single prefabs have been left out this session (<see cref="Skip(string, string, Exception)"/>), which Scry is made to bear.</summary>
        public static int Skipped { get; private set; }

        /// <summary>The latest part of a prefab left out, by its part, prefab and message.</summary>
        public static string LatestSkipped { get; private set; } = "";

        private static string Words(string part, Exception ex) => part + ": " + (ex is Failure ? "" : ex.GetType().Name + ": ") + ex.Message;

        /// <summary>A failure found by checking, not thrown: something that did not load, a file that could not be read.</summary>
        private sealed class Failure : Exception
        {
            public Failure(string why) : base(why)
            {
            }
        }

        /// <summary>A failure told whole: one found by checking by its words alone, one thrown with where it came from.</summary>
        private static string Whole(Exception ex) => ex is Failure ? ex.Message : ex.ToString();

        /// <summary>A part failed, found by checking rather than thrown; told as <see cref="Tell(string, Exception)"/> tells.</summary>
        public static void Tell(string part, string why) => Tell(part, new Failure(why));

        /// <summary>A part of one item left out, found by checking rather than thrown; told as <see cref="Skip(string, string, Exception)"/> tells.</summary>
        public static void Skip(string part, string prefab, string why) => Skip(part, prefab, new Failure(why));

        /// <summary>Tells a failure in the log the first time this part fails this way.</summary>
        public static void Tell(string part, Exception ex)
        {
            if (ex == null) return;
            Count++;
            Latest = Words(part, ex);
            if (GameChanged(part, ex)) return;
            var key = part + "|" + ex.GetType().Name + "|" + ex.Message + "|" + TopFrame(ex);
            if (Told.Count > 500 || !Told.Add(key)) return;
            Log.Source.LogError($"Scry failed in {part} (told once): {Whole(ex)}");
        }

        /// <summary>
        /// A part of one item left out, a prefab's while reading or one shown. A game change is
        /// told at once as the feature it turns off; anything else is told whole the first time
        /// that part fails that way, then only counted, and summed up by <see cref="TellSkipped"/>.
        /// </summary>
        public static void Skip(string part, string prefab, Exception ex)
        {
            if (ex == null) return;
            Skipped++;
            LatestSkipped = Words(part + " of " + prefab, ex);
            if (GameChanged(part, ex)) return;
            if (Found.Skip(part, prefab, ex)) Log.Source.LogWarning($"Scry left out the {part} of {prefab} (told whole once for this failure, then counted): {Whole(ex)}");
            else Log.Source.LogDebug($"Scry left out the {part} of {prefab}: {ex.Message}");
        }

        /// <summary>Tells, once a reading is done, what it left out, and starts counting again.</summary>
        public static void TellSkipped()
        {
            var any = false;
            foreach (var line in Found.Summary())
            {
                if (!any) Log.Source.LogInfo("Scry could not read some parts of some prefabs, most likely mods' own, and left those parts out:");
                any = true;
                Log.Source.LogInfo("  " + line);
            }
            Found.ForgetSkips();
        }

        /// <summary>Whether the failure is the game having changed; if so, told once as the feature it turns off.</summary>
        private static bool GameChanged(string feature, Exception ex)
        {
            if (!Trouble.IsGameChange(ex)) return false;
            if (Found.Changed(feature, ex))
            {
                Log.Source.LogWarning($"Scry: the game has changed in a way this version does not know, so {feature} is off until Scry is updated. The rest works on. ({Innermost(ex).Message})");
            }
            return true;
        }

        private static Exception Innermost(Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            return ex;
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
