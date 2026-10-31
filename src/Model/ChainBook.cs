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

        /// <summary>
        /// A summoning chain's step of the altar's place, or -1: the offering before it counts as
        /// seen with it once both were seen with the boss after it, as a call ties all three, so a
        /// book kept before a call tied the offering to the place reads the chain whole.
        /// </summary>
        public int Altar = -1;

        /// <summary>The first step holding a prefab or key, or -1.</summary>
        public int StepOf(string key) => Steps.FindIndex(s => s.Contains(key));

        /// <summary>
        /// Whether a step's key is the page shown, by the page's key alone: a place keeps its
        /// prefab's name, which may be its boss's too (Yagluth's altar and Yagluth are both
        /// "GoblinKing"), so the name would light the boss on the altar's page.
        /// </summary>
        public static bool IsPage(Entry page, string key) => page != null && page.Key == key;

        /// <summary>
        /// The keys the chain names under the strict rules: each seen together with a step next to it,
        /// the way the chain goes, wherever it stands in the chain, so the chain reads the same on
        /// every page of it.
        /// </summary>
        public HashSet<string> Linked(Func<string, string, bool> together)
        {
            var linked = new HashSet<string>(StringComparer.Ordinal);
            for (var s = 0; s + 1 < Steps.Count; s++)
            {
                foreach (var a in Steps[s])
                {
                    foreach (var b in Steps[s + 1])
                    {
                        if (!together(a, b)) continue;
                        linked.Add(a);
                        linked.Add(b);
                    }
                }
            }
            if (Altar < 1 || Altar + 1 >= Steps.Count) return linked;
            foreach (var offering in Steps[Altar - 1])
            {
                foreach (var place in Steps[Altar])
                {
                    if (!Steps[Altar + 1].Any(boss => together(offering, boss) && together(place, boss))) continue;
                    linked.Add(offering);
                    linked.Add(place);
                }
            }
            return linked;
        }
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

        public void Add(string title, List<string[]> steps, int altar = -1)
        {
            if (steps == null || steps.Count < 2) return;
            if (_chains.Any(c => c.Title == title && c.Steps.Count == steps.Count && c.Steps.Zip(steps, (a, b) => a.SequenceEqual(b)).All(same => same))) return;
            _chains.Add(new Chain { Title = title, Steps = steps, Altar = altar });
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
