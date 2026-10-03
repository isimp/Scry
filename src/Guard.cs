using System;
using System.Diagnostics;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The one way a part of Scry's work runs (<see cref="Steps"/>): on its own, a failure told
    /// once as that part's (<see cref="Faults"/>) and stopping nothing after it, and timed as a
    /// part of the frame where a name is given (<see cref="Timing"/>). Reading the catalog runs
    /// its parts the same way, timed for the reading's own report (<see cref="Read"/>).
    /// </summary>
    internal static class Guard
    {
        /// <summary>Runs a part, timed under a name when one is given; true when it finished.</summary>
        public static bool Run(string part, Action step, string timed = null)
        {
            var started = timed != null ? Timing.Start() : default;
            var failure = Steps.Run(step, PassesThrough);
            if (failure != null) Faults.Tell(part, failure);
            if (timed != null) Timing.Add(timed, started);
            return failure == null;
        }

        /// <summary>Runs a part on what it works on, nothing made for the call, timed under a name when one is given; true when it finished.</summary>
        public static bool Run<T>(string part, Action<T> step, T state, string timed = null)
        {
            var started = timed != null ? Timing.Start() : default;
            var failure = Steps.Run(step, state, PassesThrough);
            if (failure != null) Faults.Tell(part, failure);
            if (timed != null) Timing.Add(timed, started);
            return failure == null;
        }

        /// <summary>
        /// A step of reading the catalog: timed for the reading's report, said in the log when it
        /// took 50 ms or more; one that fails is left out, a game change told as the feature it
        /// turns off and anything else as a warning.
        /// </summary>
        public static void Read(string what, Action act)
        {
            var started = CatalogTiming.Start();
            var watch = Stopwatch.StartNew();
            var failure = Steps.Run(act, null);
            if (failure == null)
            {
                if (watch.ElapsedMilliseconds >= 50) Plugin.Note($"Scry read {what} in {watch.ElapsedMilliseconds} ms.");
            }
            else if (Trouble.IsGameChange(failure)) Faults.Skip(what, "this world", failure);
            else Plugin.Log.LogWarning($"Scry could not read {what}, and leaves it out: {failure.Message}");
            CatalogTiming.Add(what, started);
        }

        /// <summary>The GUI leaves a pass by throwing, which must reach Unity.</summary>
        private static bool PassesThrough(Exception ex) => ex is ExitGUIException;
    }
}
