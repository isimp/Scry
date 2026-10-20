using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// How long each part took the last time it ran, kept from run to run beside the log, so a
    /// run says before it starts about how long it will take, a quick run's times apart from the
    /// others' as its long parts go through fewer places; and the names of the parts that failed
    /// in the last run, for <c>/scry selftest failed</c>.
    /// </summary>
    internal sealed class SelfTestTimes
    {
        /// <summary>The seconds counted for a part never timed: about the usual part's.</summary>
        public const double Unknown = 5;

        /// <summary>What a quick run's time of a part is kept under, before the part's name.</summary>
        private const string QuickMark = "quick: ";

        private readonly Dictionary<string, double> _seconds = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>A part's time this run, in place of the one kept for runs of its kind.</summary>
        public void Took(string part, double seconds, bool quick) => _seconds[quick ? QuickMark + part : part] = seconds;

        /// <summary>About how long the parts take together, by their last times in runs of this kind, else in any run.</summary>
        public double Estimate(IEnumerable<string> parts, bool quick) => parts.Sum(p => Of(p, quick) ?? Unknown);

        /// <summary>The seconds kept for a part in runs of this kind, else in any run; none for one never timed.</summary>
        public double? Of(string part, bool quick) =>
            quick && _seconds.TryGetValue(QuickMark + part, out var fast) ? fast : _seconds.TryGetValue(part, out var s) ? s : (double?)null;

        /// <summary>The times as kept: a part a line, its seconds, a tab and its name.</summary>
        public string Save()
        {
            var text = new StringBuilder();
            foreach (var pair in _seconds.OrderBy(p => p.Key, StringComparer.Ordinal)) text.Append(Stored.Number(pair.Value, 1)).Append('\t').Append(pair.Key).Append('\n');
            return text.ToString();
        }

        /// <summary>The times kept; none where nothing was kept or a line is not one.</summary>
        public static SelfTestTimes Load(string text)
        {
            var times = new SelfTestTimes();
            foreach (var line in (text ?? "").Split('\n'))
            {
                var tab = line.IndexOf('\t');
                if (tab <= 0 || !Stored.TryNumber(line.Substring(0, tab), out var seconds)) continue;
                var name = line.Substring(tab + 1).TrimEnd('\r');
                if (name.Length > 0) times._seconds[name] = seconds;
            }
            return times;
        }

        /// <summary>The failed parts as kept, a name a line.</summary>
        public static string FailedText(IEnumerable<string> parts) => string.Join("\n", parts) + "\n";

        /// <summary>The failed parts kept; none where nothing was kept.</summary>
        public static List<string> FailedOf(string text) =>
            (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
    }
}
