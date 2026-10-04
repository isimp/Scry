using System;
using System.Diagnostics;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The one way Scry's work runs where it may fail (<see cref="Steps"/>), by how much a failure
    /// costs. A part of the work (<see cref="Run(Feature, string, Action, string)"/>) fails on its
    /// own, told once as that part's and stopping nothing after it, and is timed as a part of the
    /// frame where a name is given (<see cref="Timing"/>). One item of many, a prefab read or
    /// shown (<see cref="Each(Feature, string, string, Action)"/>), leaves only that item's part
    /// out, counted and summed up with the others. A step of reading the catalog
    /// (<see cref="Read"/>) is timed for the reading's own report. Each has a form that works
    /// out a value and says whether it did. Each names the part for the log and the feature it
    /// belongs to, which a failure because the game changed turns off. What fails is told through
    /// <see cref="Faults"/>.
    /// </summary>
    internal static class Guard
    {
        /// <summary>Runs a part, timed under a name when one is given; true when it finished.</summary>
        public static bool Run(Feature feature, string part, Action step, string timed = null)
        {
            var started = timed != null ? Timing.Start() : default;
            var failure = Steps.Run(step, PassesThrough);
            if (failure != null) Faults.Tell(feature, part, failure);
            if (timed != null) Timing.Add(timed, started);
            return failure == null;
        }

        /// <summary>Runs a part on what it works on, nothing made for the call, timed under a name when one is given; true when it finished.</summary>
        public static bool Run<T>(Feature feature, string part, Action<T> step, T state, string timed = null)
        {
            var started = timed != null ? Timing.Start() : default;
            var failure = Steps.Run(step, state, PassesThrough);
            if (failure != null) Faults.Tell(feature, part, failure);
            if (timed != null) Timing.Add(timed, started);
            return failure == null;
        }

        /// <summary>Works out a value as a part of Scry's work: true with the value when it finished, false with none when it failed, told once as that part's.</summary>
        public static bool Run<T>(Feature feature, string part, Func<T> read, out T value)
        {
            var failure = Steps.Run(read, out value, PassesThrough);
            if (failure != null) Faults.Tell(feature, part, failure);
            return failure == null;
        }

        /// <summary>One item of many: a failure leaves only that item's part out, counted and summed up with the others; true when it finished.</summary>
        public static bool Each(Feature feature, string part, string item, Action step)
        {
            var failure = Steps.Run(step, PassesThrough);
            if (failure != null) Faults.Skip(feature, part, item, failure);
            return failure == null;
        }

        /// <summary>One item of many whose name is worked out only if it fails, as an odd one may not name itself; true when it finished.</summary>
        public static bool Each(Feature feature, string part, Func<string> item, Action step)
        {
            var failure = Steps.Run(step, PassesThrough);
            if (failure == null) return true;
            if (Steps.Run(item, out var name, null) != null || name == null) name = "one that cannot name itself";
            Faults.Skip(feature, part, name, failure);
            return false;
        }

        /// <summary>Works out a value for one item of many: true with the value when it finished, false with none when it failed, that item's part left out.</summary>
        public static bool Each<T>(Feature feature, string part, string item, Func<T> read, out T value)
        {
            var failure = Steps.Run(read, out value, PassesThrough);
            if (failure != null) Faults.Skip(feature, part, item, failure);
            return failure == null;
        }

        /// <summary>
        /// A step of reading the catalog: timed for the reading's report, said in the log when it
        /// took 50 ms or more; one that fails is left out and told as any part's failure.
        /// </summary>
        public static void Read(Feature feature, string what, Action act)
        {
            var started = CatalogTiming.Start();
            var watch = Stopwatch.StartNew();
            var failure = Steps.Run(act, null);
            if (failure == null)
            {
                if (watch.ElapsedMilliseconds >= 50) Log.Note($"Scry read {what} in {Numbers.Count(watch.ElapsedMilliseconds)} ms.");
            }
            else Faults.Tell(feature, "reading " + what, failure);
            CatalogTiming.Add(what, started);
        }

        /// <summary>The GUI leaves a pass by throwing, which must reach Unity.</summary>
        private static bool PassesThrough(Exception ex) => ex is ExitGUIException;
    }
}
