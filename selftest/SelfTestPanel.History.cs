using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Scry.ScryPanel;

namespace Scry
{
    /// <summary>
    /// The runs kept, on the card (<see cref="SelfTestHistory"/>): a row of them under the title,
    /// newest at the right, each with when, which run, passed of all, how many failed and a bar of
    /// how it went, the one shown outlined, a click showing that run's card as it was (its buttons
    /// still run against now); a line telling the run shown against the last of its kind; each
    /// number's change since; and a failed part's badge and what its check found in the runs before.
    /// </summary>
    internal static partial class SelfTestPanel
    {
        private static KeptRun _shownRun;

        /// <summary>The newest run shown again, as a run starts or ends.</summary>
        private static void ShowNewest()
        {
            _shownRun = null;
            Unpick();
        }
        private static SelfTestLive _shownLive;
        private static KeptRun _shownLiveOf;
        private static SelfTestOutcome _beforeOutcome;
        private static KeptRun _beforeOf;

        /// <summary>The kept run the card shows between runs: the one picked while kept, else the newest.</summary>
        private static KeptRun ShownRun
        {
            get
            {
                var runs = SelfTest.History.Runs;
                return _shownRun != null && runs.Contains(_shownRun) ? _shownRun : runs.LastOrDefault();
            }
        }

        /// <summary>The tiles of what the card shows: the run going on, else the kept run shown.</summary>
        private static SelfTestLive ShownLive()
        {
            if (SelfTest.Running) return Live();
            var run = ShownRun;
            if (run == null) return null;
            if (_shownLiveOf != run)
            {
                _shownLive = SelfTestLive.Of(run, SelfTest.AreaOf);
                _shownLiveOf = run;
            }
            return _shownLive;
        }

        /// <summary>Which run the card shows, how long it took and when it ended.</summary>
        private static string ShownTold()
        {
            if (SelfTest.Running) return SelfTest.RunTold;
            var run = ShownRun;
            return run == null ? "" : $"{run.Scope.ToString().ToLowerInvariant()} run · {SelfTestWords.Duration(run.Seconds)} · {run.Ended:HH:mm}";
        }

        /// <summary>The outcome of the last run of the shown one's kind before it; none where it is the first.</summary>
        private static SelfTestOutcome BeforeOutcome()
        {
            if (SelfTest.Running || ShownRun == null) return null;
            var before = SelfTest.History.Against(ShownRun).Before;
            if (before == null) return null;
            if (_beforeOf != before)
            {
                _beforeOutcome = SelfTestOutcome.Of(before.Reports);
                _beforeOf = before;
            }
            return _beforeOutcome;
        }

        private static string Delta(int change) => change > 0 ? "+" + Numbers.Count(change) : "−" + Numbers.Count(-change);

        private const float RunWidth = 150f;

        /// <summary>The runs kept, as many as fit, newest at the right; a click shows that run.</summary>
        private static float RunsRow(float x, float y, float width)
        {
            var runs = SelfTest.History.Runs;
            if (runs.Count == 0) return y;
            var gap = U(6f);
            var fit = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (U(RunWidth) + gap)));
            var shown = SelfTest.Running ? null : ShownRun;
            var first = Mathf.Max(0, runs.Count - fit);
            var height = U(46f);
            for (var i = first; i < runs.Count && _drawing; i++)
            {
                var run = runs[i];
                var box = new Rect(x + (i - first) * (U(RunWidth) + gap), y, U(RunWidth), height);
                Skin.Box(box, run == shown ? Skin.Alpha(Skin.Accent, 0.12f) : Skin.TopicFill);
                if (run == shown && Event.current.type == EventType.Repaint) Outline(box, Skin.Accent);
                GUI.Label(new Rect(box.x + U(8f), box.y + U(2f), box.width - U(12f), U(18f)), $"{run.Started:HH:mm} · {run.Scope.ToString().ToLowerInvariant()}", Skin.FaintLabel);
                var told = $"{Numbers.Count(run.Passed)} / {Numbers.Count(run.Reports.Count)}" + (run.Failed > 0 ? $" · {Numbers.Count(run.Failed)} failed" : "");
                Skin.LabelIn(new Rect(box.x + U(8f), box.y + U(18f), box.width - U(12f), U(20f)), told, Skin.Small, run.Failed > 0 ? Skin.Warn : Skin.KindColor(Kind.StatusEffect));
                RunBar(new Rect(box.x + U(8f), box.yMax - U(8f), box.width - U(16f), U(4f)), run);
                if (GUI.Button(box, GUIContent.none, GUIStyle.none) && !SelfTest.Running)
                {
                    _shownRun = run == runs[runs.Count - 1] ? null : run;
                    Unpick();
                }
            }
            return y + height + U(8f);
        }

        /// <summary>A bar of how a run went: passed, failed and skipped in their share.</summary>
        private static void RunBar(Rect bar, KeptRun run)
        {
            if (Event.current.type != EventType.Repaint || run.Reports.Count == 0) return;
            var passed = bar.width * run.Passed / run.Reports.Count;
            var failed = bar.width * run.Failed / run.Reports.Count;
            Skin.Fill(new Rect(bar.x, bar.y, passed, bar.height), Skin.KindColor(Kind.StatusEffect));
            Skin.Fill(new Rect(bar.x + passed, bar.y, failed, bar.height), Skin.Warn);
            Skin.Fill(new Rect(bar.x + passed + failed, bar.y, bar.width - passed - failed, bar.height), Skin.Alpha(Skin.Faint, 0.5f));
        }

        private static void Outline(Rect box, Color colour)
        {
            Skin.Fill(new Rect(box.x, box.y, box.width, 1f), colour);
            Skin.Fill(new Rect(box.x, box.yMax - 1f, box.width, 1f), colour);
            Skin.Fill(new Rect(box.x, box.y, 1f, box.height), colour);
            Skin.Fill(new Rect(box.xMax - 1f, box.y, 1f, box.height), colour);
        }

        /// <summary>The run shown against the last of its kind: what was fixed, what failed new and what failed again; nothing where it is the first of its kind.</summary>
        private static float DiffLine(float x, float y, float width)
        {
            var run = ShownRun;
            if (run == null) return y;
            var (before, fixedNow, failedNew, again) = SelfTest.History.Against(run);
            if (before == null) return y;
            var told = $"Against the {before.Scope.ToString().ToLowerInvariant()} run before ({before.Started:HH:mm}): {Numbers.Count(fixedNow.Count)} fixed"
                + (fixedNow.Count > 0 ? " (" + string.Join(", ", fixedNow) + ")" : "")
                + $", {Numbers.Count(failedNew.Count)} failing new" + (failedNew.Count > 0 ? " (" + string.Join(", ", failedNew) + ")" : "")
                + $", {Numbers.Count(again.Count)} failing again.";
            var height = Skin.Height(Skin.DimWrap, told, width - U(20f)) + U(10f);
            if (_drawing)
            {
                Skin.Box(new Rect(x, y, width, height), Skin.TopicFill);
                Skin.Fill(new Rect(x, y, U(3f), height), failedNew.Count > 0 ? Skin.Warn : Skin.KindColor(Kind.StatusEffect));
                GUI.Label(new Rect(x + U(12f), y + U(5f), width - U(20f), height - U(10f)), told, Skin.DimWrap);
            }
            return y + height + U(10f);
        }

        /// <summary>A failed part's badge and what its check found in the runs kept before, newest first; nothing while a run goes on.</summary>
        private static (string Badge, string Before) TrailOf(string part)
        {
            var run = SelfTest.Running ? null : ShownRun;
            if (run == null) return default;
            var trail = SelfTest.History.Trail(part, run);
            var earlier = trail.Take(trail.Count - 1).Reverse().Take(3)
                .Select(t => $"{t.At:HH:mm} " + (t.Result == Result.Fail ? (t.Found.Length > 0 ? t.Found : "failed") : t.Result == Result.Pass ? "passed" : "skipped")).ToList();
            return (SelfTestHistory.Badge(trail), earlier.Count == 0 ? null : "before: " + string.Join(" · ", earlier));
        }
    }
}
