using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>A chain of steps a thing goes through, each step one or several prefabs or keys, under its title.</summary>
    internal sealed class Chain
    {
        public string Title;
        public List<string[]> Steps;

        /// <summary>The first step holding a prefab or key, or -1.</summary>
        public int StepOf(string key) => Steps.FindIndex(s => s.Contains(key));
    }

    /// <summary>
    /// The chains read for a world: what one page tells of a step, told whole on every page of
    /// it. Each chain is kept once; one of a single step tells nothing and is not kept.
    /// </summary>
    internal sealed class ChainBook
    {
        /// <summary>The most steps a chain follows, so one through a mod's odd prefabs ends.</summary>
        public const int Most = 8;

        private readonly List<Chain> _chains = new List<Chain>();

        public void Add(string title, List<string[]> steps)
        {
            if (steps == null || steps.Count < 2) return;
            if (_chains.Any(c => c.Title == title && c.Steps.Count == steps.Count && c.Steps.Zip(steps, (a, b) => a.SequenceEqual(b)).All(same => same))) return;
            _chains.Add(new Chain { Title = title, Steps = steps });
        }

        /// <summary>The chains a prefab or key is a step of.</summary>
        public List<Chain> Of(string key) => _chains.Where(c => c.StepOf(key) >= 0).ToList();

        public void Clear() => _chains.Clear();

        /// <summary>
        /// The steps from one prefab on, each what the one before leads to, going on from the
        /// first of several; a step met again ends the chain, shown once more so a life cycle
        /// ends where it began.
        /// </summary>
        public static List<string[]> Walk(string start, Func<string, IReadOnlyList<string>> next)
        {
            var steps = new List<string[]> { new[] { start } };
            var seen = new HashSet<string> { start };
            var at = start;
            while (steps.Count < Most)
            {
                var then = next(at);
                if (then == null || then.Count == 0) break;
                steps.Add(then.ToArray());
                at = then[0];
                if (!seen.Add(at)) break;
            }
            return steps;
        }
    }
}
