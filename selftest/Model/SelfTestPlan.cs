using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// The parts a session plans for a change (<c>BepInEx/Scry-selftest.plan</c>), one line each,
    /// a word and a tab before it: <c>why</c> the change the plan is for, <c>part</c> a part by its
    /// whole name, <c>words</c> every part whose name holds all the words, as a run by words takes
    /// them, <c>run on-load</c> to start the plan once the world is ready, and <c>shots yes</c> for
    /// its parts to take the docs' pictures clean as a full run does. A part comes off once it passes;
    /// words become the parts they matched as the plan is written again; the plan is done once nothing
    /// is left.
    /// </summary>
    internal sealed class SelfTestPlan
    {
        private readonly List<(string Kind, string Text)> _asked = new List<(string, string)>();

        /// <summary>The change the plan is for, as the session wrote it; none where it said nothing.</summary>
        public string Why { get; private set; }

        /// <summary>Whether the plan starts once the world is ready.</summary>
        public bool OnLoad { get; private set; }

        /// <summary>Whether its parts take the docs' pictures clean, as a full run's do.</summary>
        public bool TakesShots { get; private set; }

        /// <summary>Whether nothing is left to run.</summary>
        public bool Done => _asked.Count == 0;

        public static SelfTestPlan Load(string text)
        {
            var plan = new SelfTestPlan();
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                var kind = line.Substring(0, tab).Trim().ToLowerInvariant();
                var value = line.Substring(tab + 1).Trim();
                if (value.Length == 0) continue;
                switch (kind)
                {
                    case "why": plan.Why = value; break;
                    case "run": plan.OnLoad = value.Equals("on-load", StringComparison.OrdinalIgnoreCase); break;
                    case "shots": plan.TakesShots = value.Equals("yes", StringComparison.OrdinalIgnoreCase); break;
                    case "part":
                    case "words":
                        plan._asked.Add((kind, value));
                        break;
                }
            }
            return plan;
        }

        /// <summary>The parts it asks for, in the order the run takes them, each once.</summary>
        public List<string> Parts(IReadOnlyList<string> all)
        {
            var wanted = new HashSet<string>(_asked.SelectMany(a => Matching(a, all)), StringComparer.Ordinal);
            return all.Where(wanted.Contains).ToList();
        }

        /// <summary>What it asks for that matches no part, as written.</summary>
        public List<string> Unmatched(IReadOnlyList<string> all) => _asked.Where(a => !Matching(a, all).Any()).Select(a => a.Text).ToList();

        /// <summary>The plan after a run: what it asked for less every part that passed, words turned into the parts they matched.</summary>
        public SelfTestPlan After(IReadOnlyList<string> all, IEnumerable<(string Part, Result Result)> ran)
        {
            var passed = new HashSet<string>(ran.Where(r => r.Result == Result.Pass).Select(r => r.Part), StringComparer.Ordinal);
            var left = new SelfTestPlan { Why = Why, OnLoad = OnLoad, TakesShots = TakesShots };
            foreach (var part in Parts(all)) if (!passed.Contains(part)) left._asked.Add(("part", part));
            return left;
        }

        public string Save()
        {
            var text = new StringBuilder();
            if (Why != null) text.Append("why\t").Append(Why).Append('\n');
            foreach (var (kind, value) in _asked) text.Append(kind).Append('\t').Append(value).Append('\n');
            if (OnLoad) text.Append("run\ton-load\n");
            if (TakesShots) text.Append("shots\tyes\n");
            return text.ToString();
        }

        private static IEnumerable<string> Matching((string Kind, string Text) asked, IReadOnlyList<string> all)
        {
            if (asked.Kind == "part") return all.Where(n => n.Equals(asked.Text, StringComparison.OrdinalIgnoreCase));
            var words = asked.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return all.Where(n => words.All(w => n.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }
}
