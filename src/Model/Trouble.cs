using System;
using System.Collections.Generic;
using System.Reflection;

namespace Scry
{
    /// <summary>
    /// What went wrong while reading, sorted by what it means. A member the game no longer has
    /// (renamed or removed by an update) fails the first time the code naming it runs, and means
    /// a whole feature is off until Scry is updated: that is told at once, by the feature, and
    /// kept for the panel to show. Anything else is most likely one odd prefab, a mod's, and costs
    /// only that prefab's part: those are counted by part and told once, with an example, when
    /// the reading is done.
    /// </summary>
    public sealed class Trouble
    {
        private readonly Dictionary<string, (int Count, string Example)> _skips = new Dictionary<string, (int, string)>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();
        private readonly List<string> _changed = new List<string>();

        /// <summary>Whether a failure means the game no longer has something Scry names, however it is wrapped.</summary>
        public static bool IsGameChange(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is MissingMemberException || e is TypeLoadException || e is BadImageFormatException) return true;
                if (!(e is TypeInitializationException || e is TargetInvocationException)) return false;
            }
            return false;
        }

        /// <summary>The features a game change has turned off, in the order found.</summary>
        public IReadOnlyList<string> ChangedFeatures => _changed;

        /// <summary>Notes a feature the game change turned off; true the first time, to be told then.</summary>
        public bool Changed(string feature, Exception ex)
        {
            if (string.IsNullOrEmpty(feature) || _changed.Contains(feature)) return false;
            _changed.Add(feature);
            return true;
        }

        /// <summary>Counts a part of one prefab left out.</summary>
        public void Skip(string part, string prefab, Exception ex)
        {
            if (_skips.TryGetValue(part, out var known))
            {
                _skips[part] = (known.Count + 1, known.Example);
                return;
            }
            _order.Add(part);
            _skips[part] = (1, $"{prefab}: {ex?.GetType().Name} {ex?.Message}".Trim());
        }

        /// <summary>A line for each part with prefabs left out: how many, and the first.</summary>
        public IEnumerable<string> Summary()
        {
            foreach (var part in _order)
            {
                var (count, example) = _skips[part];
                yield return $"{part}: {count} {(count == 1 ? "prefab" : "prefabs")}, the first {example}";
            }
        }

        /// <summary>Starts counting skips again, for a new reading; game changes stay known.</summary>
        public void ForgetSkips()
        {
            _skips.Clear();
            _order.Clear();
        }

        public void Forget()
        {
            ForgetSkips();
            _changed.Clear();
        }
    }
}
