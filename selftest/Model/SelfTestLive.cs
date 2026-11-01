using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a part stands in the live view.</summary>
    internal enum TileState { ToCome, Running, Pass, Fail, Busy, Untried, Skipped }

    /// <summary>What a line of the feed tells.</summary>
    internal enum FeedKind { Failed, Busy, Untried }

    /// <summary>A line that needs a look: a check failed or not judged, or a row untried, with its part and when it came.</summary>
    internal sealed class FeedEntry
    {
        public readonly FeedKind Kind;
        public readonly string Part;
        public readonly string Text;
        public readonly double At;

        public FeedEntry(FeedKind kind, string part, string text, double at)
        {
            Kind = kind;
            Part = part;
            Text = text;
            At = at;
        }
    }

    /// <summary>
    /// The self-test as it runs, for the strip and the card: a tile a part, by area in the order
    /// the areas first come, each coloured by how its part went (a failure first, then a check
    /// not judged, then a row untried), the running one lit and the rest to come; and the feed,
    /// every line that needs a look, newest first.
    /// </summary>
    internal sealed class SelfTestLive
    {
        /// <summary>How long a new line of the feed stands in the strip, in seconds.</summary>
        public const double ShownFor = 8.0;

        public List<(string Area, List<(string Name, TileState State)> Tiles)> Areas { get; } = new List<(string, List<(string, TileState)>)>();
        public List<(string Name, TileState State)> InRunOrder { get; } = new List<(string, TileState)>();
        public List<FeedEntry> Feed { get; } = new List<FeedEntry>();

        public static SelfTestLive Of(ScenarioRunner runner)
        {
            var live = new SelfTestLive();
            var reports = runner.Reports.ToDictionary(r => r.Name, StringComparer.Ordinal);
            foreach (var part in runner.Parts)
            {
                var state = reports.TryGetValue(part.Name, out var report) ? StateOf(report) : part.Name == runner.Current ? TileState.Running : TileState.ToCome;
                live.InRunOrder.Add((part.Name, state));
                var area = part.Area ?? "";
                var row = live.Areas.FindIndex(a => a.Area == area);
                if (row < 0)
                {
                    live.Areas.Add((area, new List<(string, TileState)>()));
                    row = live.Areas.Count - 1;
                }
                live.Areas[row].Tiles.Add((part.Name, state));
            }
            for (var i = runner.Marked.Count - 1; i >= 0; i--)
            {
                var (part, line, at) = runner.Marked[i];
                var entry = EntryOf(part, line, at);
                if (entry != null) live.Feed.Add(entry);
            }
            return live;
        }

        private static TileState StateOf(ScenarioReport report)
        {
            if (report.Result == Result.Fail) return TileState.Fail;
            if (report.Result == Result.Skip) return TileState.Skipped;
            if (report.Lines.Any(l => l.StartsWith(BusyMark, StringComparison.Ordinal))) return TileState.Busy;
            if (report.Lines.Any(l => l.StartsWith(UntriedMark, StringComparison.Ordinal))) return TileState.Untried;
            return TileState.Pass;
        }

        private const string FailMark = "  FAIL ";
        private const string BusyMark = "  BUSY ";
        private const string UntriedMark = "  untried ";

        /// <summary>A line written as a feed entry; none for a line that needs no look.</summary>
        public static FeedEntry EntryOf(string part, string line, double at)
        {
            if (line.StartsWith(FailMark, StringComparison.Ordinal)) return new FeedEntry(FeedKind.Failed, part, line.Substring(FailMark.Length), at);
            if (line.StartsWith(BusyMark, StringComparison.Ordinal)) return new FeedEntry(FeedKind.Busy, part, line.Substring(BusyMark.Length), at);
            if (line.StartsWith(UntriedMark, StringComparison.Ordinal)) return new FeedEntry(FeedKind.Untried, part, line.Substring(UntriedMark.Length), at);
            return null;
        }

        /// <summary>How a kind of feed line is named in the strip and the card.</summary>
        public static string Word(FeedKind kind) => kind == FeedKind.Failed ? "failed" : kind == FeedKind.Busy ? "not judged" : "untried";

        /// <summary>The newest line of the feed for the strip, while it is new (<see cref="ShownFor"/>); none after.</summary>
        public static string Latest(FeedEntry newest, double now) =>
            newest == null || now - newest.At > ShownFor ? null : Word(newest.Kind) + ": " + newest.Text;
    }
}
