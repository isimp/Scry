using System;
using System.Linq;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Reading the catalog spread over frames: a piece at a time, for as long as a frame's share
    /// allows, until it is read or the world it was begun in is left.
    /// </summary>
    internal sealed class CatalogJob
    {
        private readonly ZNetScene _scene = ZNetScene.instance;
        private readonly IEnumerator<string> _steps;
        private readonly FrameShare _share = new FrameShare(4);
        private readonly System.Diagnostics.Stopwatch _all = new System.Diagnostics.Stopwatch();

        /// <summary>Pieces that took longer than this are told at the end, with what they read.</summary>
        private const double LongPieceMs = 8;
        private readonly List<(string What, double Ms)> _long = new List<(string, double)>();

        public CatalogJob()
        {
            _steps = CatalogBuilder.Steps(this);
        }

        /// <summary>The catalog, once read.</summary>
        public List<Entry> Entries { get; internal set; }

        /// <summary>The scene of the world it is read for.</summary>
        public ZNetScene Scene => _scene;

        /// <summary>What the piece being read is about, where more can be told than the progress says (a prefab's name).</summary>
        internal string Piece;

        /// <summary>Why it could not be read, when it could not.</summary>
        public string Failure { get; private set; }

        /// <summary>What is being read now, for the panel.</summary>
        public string Progress { get; private set; } = "Reading the catalog";

        public bool Done => Entries != null || Failure != null;

        /// <summary>Scry's own time spent reading it, and over how many frames.</summary>
        public double WorkMs { get; private set; }
        public int Frames { get; private set; }

        /// <summary>Since it began, frames between included.</summary>
        public double ElapsedMs => _all.Elapsed.TotalMilliseconds;

        /// <summary>Reads on for as long as this frame's share allows; true once done.</summary>
        public bool Advance(double budgetMs)
        {
            if (Done) return true;
            if (!ReferenceEquals(ZNetScene.instance, _scene))
            {
                Failure = "the world was left";
                return true;
            }

            _all.Start();
            var frame = System.Diagnostics.Stopwatch.StartNew();
            var done = 0;
            var finished = Guard.Run(Feature.Catalog, "reading the catalog", () =>
            {
                while (_share.MayBegin(frame.Elapsed.TotalMilliseconds, done, budgetMs))
                {
                    var before = frame.Elapsed.TotalMilliseconds;
                    var what = Progress;
                    Piece = null;
                    var more = _steps.MoveNext();
                    var took = frame.Elapsed.TotalMilliseconds - before;
                    _share.Took(took);
                    if (took > LongPieceMs && Settings.LogPreviews) _long.Add((LongPiece(Piece, what), took));
                    done++;
                    if (!more)
                    {
                        if (Entries == null) Failure = "it ended without a catalog";
                        break;
                    }
                    Progress = _steps.Current;
                }
            });
            if (!finished) Failure = "it failed, as the log says";
            WorkMs += frame.Elapsed.TotalMilliseconds;
            Frames++;
            if (Done)
            {
                _all.Stop();
                _steps.Dispose();
                if (_long.Count > 0)
                {
                    Log.Note($"{Naming.Counted($"Scry's catalog pieces over {Numbers.Amount(LongPieceMs, 0)} ms", _long.Count)}: {string.Join(", ", _long.OrderByDescending(p => p.Ms).Take(25).Select(p => $"{p.What} {Numbers.Amount(p.Ms, 0)}"))}.");
                }
            }
            return Done;
        }

        /// <summary>A long piece of the work by the prefab it read, else by its step, for the log.</summary>
        [Diagnostic]
        private static string LongPiece(string prefab, string step) => prefab != null ? "the prefab " + prefab : step;
    }
}
