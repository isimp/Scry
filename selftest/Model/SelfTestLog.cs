using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The self-test's log kept to its last runs: each run opens with a line starting
    /// <see cref="RunMark"/>, and once the log grows past <see cref="KeptBytes"/> the runs before
    /// the last <see cref="RunsKept"/> go to the older log, which then holds only those, so
    /// reading the log for the latest result never wades through months of runs.
    /// </summary>
    internal static class SelfTestLog
    {
        /// <summary>What opens a run's lines in the log; a run's summary opens with "==== Summary" and is no run of its own.</summary>
        public const string RunMark = "==== Scry ";

        /// <summary>How many of the latest runs the log keeps once it is split.</summary>
        public const int RunsKept = 5;

        /// <summary>How large the log grows before it is split, in bytes.</summary>
        public const long KeptBytes = 4L * 1024 * 1024;

        /// <summary>
        /// A log's text split before its last runs, the blank line before a run's mark going with
        /// it: what goes to the older log, and what stays; nothing to move where it holds no more
        /// runs than are kept.
        /// </summary>
        public static (string Older, string Kept) Split(string text, int runs = RunsKept)
        {
            if (string.IsNullOrEmpty(text)) return ("", "");
            var starts = new List<int>();
            for (var at = text.IndexOf(RunMark, StringComparison.Ordinal); at >= 0; at = text.IndexOf(RunMark, at + 1, StringComparison.Ordinal))
            {
                if (at == 0 || text[at - 1] == '\n') starts.Add(at);
            }
            if (starts.Count <= runs) return ("", text);
            var cut = starts[starts.Count - runs];
            // The line written blank before a run's mark opens the run.
            if (cut >= 1 && text[cut - 1] == '\n') cut--;
            if (cut >= 1 && text[cut - 1] == '\r') cut--;
            return (text.Substring(0, cut), text.Substring(cut));
        }
    }
}
