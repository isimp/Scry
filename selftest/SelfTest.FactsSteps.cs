using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>The self-test's case for an entry's facts worked out a step at a time over frames.</summary>
    internal static partial class SelfTest
    {
        /// <summary>How many entries of each kind have their facts worked out both ways.</summary>
        private const int StepsPerKind = 120;

        /// <summary>How many builds run side by side, a step each a frame.</summary>
        private const int StepsSideBySide = 50;

        /// <summary>
        /// For entries of every kind, the facts worked out a step a frame, many builds side by side
        /// so each one's steps run between the others', are the facts worked out at once: the same
        /// rows, lines, links and parts, in the same order. A page laid out over frames builds its
        /// facts so.
        /// </summary>
        private static IEnumerator FactsSteppedAsWhole(Probe p)
        {
            yield return FactsBuildBegunAfresh(p);
            yield return FactsSteppedAsWholeAll(p);
        }

        /// <summary>
        /// A page's facts worked out over frames begin afresh where what they tell changed since
        /// their first step, and where the entry's facts were forgotten meanwhile, rather than
        /// finish with their first parts told as they were.
        /// </summary>
        private static IEnumerator FactsBuildBegunAfresh(Probe p)
        {
            var creature = Pick(Kind.Creature, "Troll", "Greydwarf");
            if (!p.Check(creature != null, "a creature to work out the facts of")) yield break;
            Facts.Forget(creature);
            var before = Facts.BuiltSinceStartForTest;
            p.Check(Facts.Building(creature, () => false) == null, $"{creature.Name}'s facts are not done after one step");
            Facts.AgeBuildForTest();
            p.Check(Facts.Building(creature, () => true) != null && Facts.BuiltSinceStartForTest == before + 2,
                "a build begun before what it tells changed begins afresh", $"{Numbers.Count(Facts.BuiltSinceStartForTest - before)} begun");
            Facts.Forget(creature);
            Facts.Building(creature, () => false);
            Learned.About(creature);
            p.Check(Facts.Building(creature, () => true) != null && Facts.BuiltSinceStartForTest == before + 4,
                "a build of facts forgotten meanwhile begins afresh", $"{Numbers.Count(Facts.BuiltSinceStartForTest - before)} begun");
            yield break;
        }

        /// <summary>Every sampled entry's facts worked out a step a frame side by side against the same worked out at once.</summary>
        private static IEnumerator FactsSteppedAsWholeAll(Probe p)
        {
            var sample = X.Entries.All.GroupBy(e => e.Kind).SelectMany(g => g.Take(StepsPerKind)).ToList();
            var running = new List<(Entry Entry, Func<Facts> Step, int Steps)>();
            var next = 0;
            var compared = 0;
            var steps = 0;
            var differ = new List<string>();
            string firstDifference = null;
            while (next < sample.Count || running.Count > 0)
            {
                while (running.Count < StepsSideBySide && next < sample.Count)
                {
                    var entry = sample[next++];
                    running.Add((entry, Facts.StepsForTest(entry), 0));
                }
                for (var i = running.Count - 1; i >= 0; i--)
                {
                    var (entry, step, taken) = running[i];
                    var done = step();
                    steps++;
                    if (done == null)
                    {
                        running[i] = (entry, step, taken + 1);
                        continue;
                    }
                    running.RemoveAt(i);
                    compared++;
                    var stepped = FactsText(done);
                    var whole = FactsText(Facts.Built(entry));
                    if (stepped == whole) continue;
                    differ.Add(entry.Name);
                    if (firstDifference == null) firstDifference = $"{entry.Name}: {FirstDifference(stepped, whole)}";
                }
                yield return null;
            }
            p.Note($"{Numbers.Count(compared)} entries of every kind worked out both ways, {Numbers.Count(steps)} steps in all");
            p.Check(differ.Count == 0, "facts worked out a step a frame are the facts worked out at once",
                differ.Count == 0 ? null : $"{Numbers.Count(differ.Count)} differ, among them {string.Join(", ", differ.Take(8))}; first: {firstDifference}");
        }

        /// <summary>The first line two texts differ in, each side shown.</summary>
        private static string FirstDifference(string a, string b)
        {
            var left = a.Split('\n');
            var right = b.Split('\n');
            for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
            {
                var l = i < left.Length ? left[i] : "(none)";
                var r = i < right.Length ? right[i] : "(none)";
                if (l != r) return $"line {Numbers.Count(i + 1)}, stepped '{l}' against whole '{r}'";
            }
            return "same lines";
        }

        /// <summary>Everything a Facts holds, field by field and into what its fields hold, as text, to compare two.</summary>
        private static string FactsText(Facts facts)
        {
            var text = new StringBuilder();
            Dump(text, facts, 0, new HashSet<object>(ReferenceComparer.Instance));
            return text.ToString();
        }

        private static void Dump(StringBuilder text, object value, int depth, HashSet<object> seen)
        {
            var pad = new string(' ', depth * 2);
            if (value == null)
            {
                text.Append(pad).Append("null\n");
                return;
            }
            var type = value.GetType();
            // An entry stands for itself by its key: what it links to is the catalog's, not the facts'.
            if (value is Entry entry)
            {
                text.Append(pad).Append(entry.Key).Append('\n');
                return;
            }
            if (value is UnityEngine.Object unity)
            {
                text.Append(pad).Append(unity != null ? unity.name : "destroyed").Append('\n');
                return;
            }
            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal)
            {
                text.Append(pad).Append(value).Append('\n');
                return;
            }
            if (value is Delegate || depth > 8 || !seen.Add(value))
            {
                text.Append(pad).Append('<').Append(type.Name).Append(">\n");
                return;
            }
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry pair in dictionary)
                {
                    text.Append(pad).Append("key\n");
                    Dump(text, pair.Key, depth + 1, seen);
                    Dump(text, pair.Value, depth + 1, seen);
                }
                return;
            }
            if (value is IEnumerable list)
            {
                foreach (var item in list) Dump(text, item, depth + 1, seen);
                return;
            }
            foreach (var field in TypeFields.Of(type))
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                text.Append(pad).Append(field.Name).Append('\n');
                Dump(text, TypeFields.Value(field, value), depth + 1, seen);
            }
        }

        /// <summary>Compares by reference, so a value met twice in a dump is told once.</summary>
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }
}
