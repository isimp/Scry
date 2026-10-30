using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's tally of what LINKED holds over every page.</summary>
    internal static partial class SelfTest
    {
        /// <summary>Where the tally is written, beside the self-test's results.</summary>
        public static string LinkedFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-linked.log");

        /// <summary>How long the tally works a frame, in milliseconds, so the game keeps running.</summary>
        private const long TallyMsAFrame = 8;

        /// <summary>
        /// Every page's LINKED as its page makes it, before any rules leave some out: for each kind
        /// of page, how many hold it, each group on how many pages, how many links, the kinds of
        /// thing they go to and a few pages as examples, written to <see cref="LinkedFile"/>. For
        /// deciding which groups belong in a topic of In the game; a full run or one naming it takes it.
        /// </summary>
        private static IEnumerator LinkedTallied(Probe p)
        {
            var tally = new LinkedTally();
            var entries = X.Entries.All.ToList();
            var failed = 0;
            var clock = Stopwatch.StartNew();
            foreach (var entry in entries)
            {
                if (clock.ElapsedMilliseconds >= TallyMsAFrame)
                {
                    yield return null;
                    clock.Restart();
                }
                tally.Page(entry.Kind);
                var error = Steps.Run(() => ScryPanel.LinkedRowsForTest(X, entry), out var rows, null);
                if (error != null)
                {
                    failed++;
                    continue;
                }
                foreach (var (group, targets) in rows) tally.Add(entry.Kind, entry.Name, group, targets.Select(t => (t, X.Find(t)?.Kind.ToString() ?? "no entry")));
            }
            var lines = tally.Lines();
            lines.Insert(0, $"==== Scry {About.Version} LINKED over every page, {DateTime.Now:yyyy-MM-dd HH:mm:ss}: {Numbers.Count(tally.Pages)} pages, {Numbers.Count(tally.Links)} links ====");
            File.WriteAllLines(LinkedFile, lines);
            p.Note($"{Numbers.Count(tally.Pages)} pages walked, {Numbers.Count(tally.Links)} links under LINKED, written to BepInEx/Scry-linked.log");
            p.Check(failed == 0, "every page's LINKED is made without failing", failed == 0 ? null : $"{Numbers.Count(failed)} pages failed");
        }
    }
}
