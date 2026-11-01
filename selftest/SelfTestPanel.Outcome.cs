using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Scry.ScryPanel;

namespace Scry
{
    /// <summary>
    /// The card of a run held or done, read top down, each thing once (<see cref="SelfTestOutcome"/>):
    /// the verdict in five numbers; the buttons that run what it calls for; every part as a tile
    /// by area, a click opening that part under them; then a box for each kind of thing left: the
    /// failed parts each with its failed checks and Run again, the untried rows by where to stand
    /// with Try where that thing is near, the checks not judged, the skipped parts with why. Boxes
    /// with nothing in them are not drawn. It is laid out twice a draw, once measuring and once
    /// drawing, so its height is the height it draws.
    /// </summary>
    internal static partial class SelfTestPanel
    {
        private static SelfTestOutcome _outcome;
        private static Vector2 _liveScroll;
        private static (ScenarioRunner Runner, int Done) _outcomeKey;
        private static bool _drawing;
        private static float _failedAt, _untriedAt;
        private static readonly List<float> TopicHeights = new List<float>();
        private static int _topic;

        /// <summary>Begins a pass over a card: measuring, or drawing what was measured.</summary>
        private static void Pass(bool drawing)
        {
            _drawing = drawing;
            _topic = 0;
        }

        private static SelfTestOutcome Outcome()
        {
            var runner = SelfTest.LiveRunner;
            if (runner == null) return null;
            var key = (runner, runner.Done);
            if (_outcome == null || key != _outcomeKey)
            {
                _outcome = SelfTestOutcome.Of(runner.Reports);
                _outcomeKey = key;
            }
            return _outcome;
        }

        private static void OutcomeCard(Rect rect)
        {
            var outcome = Outcome();
            var width = CardWidth(rect);
            Pass(false);
            var height = outcome == null ? U(80f) : Body(outcome, U(14f), U(44f), width) - U(44f) + U(60f);
            var card = BeginCard(rect, ref _liveScroll, height, "Self-test");
            var section = TakeOpenSection();
            if (section != Section.None) _liveScroll.y = Mathf.Max(0f, (section == Section.Failed ? _failedAt : _untriedAt) - U(10f));
            Pass(true);
            if (outcome != null) Body(outcome, card.X, card.Y, width);
            if (EndCard(rect, out _))
            {
                if (SelfTest.Running) HidePauseCard(true);
                else DetailsAway();
            }
        }

        private static float Body(SelfTestOutcome outcome, float x, float y, float width)
        {
            if (_drawing) GUI.Label(new Rect(x, y - U(34f), width, U(26f)), SelfTest.RunTold, Skin.DimRight);
            if (SelfTest.Running)
            {
                var held = SelfTest.Progress ?? "";
                var h = Skin.Height(Skin.Wrap, held, width);
                if (_drawing) GUI.Label(new Rect(x, y, width, h), held, Skin.Wrap);
                y += h + U(8f);
            }
            y = Stats(outcome, x, y, width);
            y = Actions(outcome, x, y, width);
            y = Topic(x, y, width, "Every part", "a click opens a part below", null, () => { }, (ty) => TilesIn(outcome, x + U(10f), ty, width - U(20f)));
            var done = !SelfTest.Running;
            if (outcome.FailedParts.Count > 0)
            {
                _failedAt = y;
                y = Topic(x, y, width, "Failed", Parts(outcome.FailedParts.Count), done ? $"Run these {Numbers.Count(outcome.FailedParts.Count)}" : null,
                    () => SayStarted(SelfTest.StartParts(outcome.FailedParts.Select(f => f.Part))),
                    ty => outcome.FailedParts.Aggregate(ty, (py, f) => PartBox(x + U(10f), py, width - U(20f), f.Part, f.Seconds, f.Checks, true)));
            }
            if (outcome.UntriedByWhere.Count > 0)
            {
                _untriedAt = y;
                var parts = outcome.UntriedByWhere.SelectMany(g => g.Parts).Distinct().ToList();
                y = Topic(x, y, width, "Untried, by where to stand", $"{Numbers.Count(outcome.UntriedRows)} {(outcome.UntriedRows == 1 ? "row" : "rows")} in {Parts(parts.Count)}", done ? $"Run all {Numbers.Count(outcome.UntriedRows)}" : null,
                    () => SayStarted(SelfTest.StartParts(parts)),
                    ty => outcome.UntriedByWhere.Aggregate(ty, (py, g) => StandRow(x + U(10f), py, width - U(20f), g, done)));
            }
            if (outcome.NotJudgedParts.Count > 0)
            {
                y = Topic(x, y, width, "Not judged, the machine was busy", $"{Numbers.Count(outcome.NotJudged)} {(outcome.NotJudged == 1 ? "check" : "checks")}", done ? "Run these" : null,
                    () => SayStarted(SelfTest.StartParts(outcome.NotJudgedParts.Select(n => n.Part))),
                    ty => outcome.NotJudgedParts.Aggregate(ty, (py, n) => PartBox(x + U(10f), py, width - U(20f), n.Part, null, n.Checks, false)));
            }
            if (outcome.SkippedParts.Count > 0)
            {
                y = Topic(x, y, width, "Skipped", Parts(outcome.SkippedParts.Count), null, () => { },
                    ty => outcome.SkippedParts.Aggregate(ty, (py, s) => TwoColumns(x + U(10f), py, width - U(20f), s.Part, s.Why, Skin.DimWrap, Skin.DimWrap)));
            }
            return y;
        }

        private static string Parts(int count) => $"{Numbers.Count(count)} {(count == 1 ? "part" : "parts")}";

        private static void SayStarted(string said)
        {
            DetailsAway();
            Say("Scry: " + said);
        }

        /// <summary>The verdict in five numbers, each in a box with its colour.</summary>
        private static float Stats(SelfTestOutcome outcome, float x, float y, float width)
        {
            var stats = new[]
            {
                (outcome.Passed, "passed", Skin.KindColor(Kind.StatusEffect)),
                (outcome.Failed, "failed", Skin.Warn),
                (outcome.UntriedRows, "untried rows", Skin.Immune),
                (outcome.Skipped, "skipped", Skin.Faint),
                (outcome.NotJudged, "not judged", Skin.Faint),
            };
            var gap = U(8f);
            var each = (width - gap * (stats.Length - 1)) / stats.Length;
            var height = U(52f);
            for (var i = 0; i < stats.Length && _drawing; i++)
            {
                var box = new Rect(x + i * (each + gap), y, each, height);
                Skin.Box(box, Skin.TopicFill);
                Skin.Fill(new Rect(box.x, box.y, U(3f), box.height), stats[i].Item3);
                Skin.LabelIn(new Rect(box.x + U(12f), box.y + U(4f), each - U(16f), U(28f)), Numbers.Count(stats[i].Item1), Skin.Title, stats[i].Item3);
                GUI.Label(new Rect(box.x + U(12f), box.y + U(30f), each - U(16f), U(18f)), stats[i].Item2, Skin.FaintLabel);
            }
            return y + height + U(10f);
        }

        /// <summary>The buttons a run held or done calls for, wrapping to another row where they run out of room.</summary>
        private static float Actions(SelfTestOutcome outcome, float x, float y, float width)
        {
            var actions = new List<(string Text, bool Primary, Action Act)>();
            if (SelfTest.Running)
            {
                actions.Add(("Go on", true, () => { SelfTest.TogglePause(); HidePauseCard(false); }));
                actions.Add(("Stop", false, () => Say("Scry: " + SelfTest.Stop())));
            }
            else
            {
                if (outcome.Failed > 0) actions.Add(($"Run the {Numbers.Count(outcome.Failed)} failed", true, () => SayStarted(SelfTest.StartRecommended(RunScope.Failed))));
                var near = outcome.UntriedByWhere.Where(g => SelfTest.IsNear(g.Need)).SelectMany(g => g.Parts).Distinct().ToList();
                if (near.Count > 0) actions.Add(($"Try {Numbers.Count(near.Count)} near you", outcome.Failed == 0, () => SayStarted(SelfTest.StartParts(near))));
                if (outcome.UntriedRows > 0) actions.Add(($"Run all {Numbers.Count(outcome.UntriedRows)} untried", false, () => SayStarted(SelfTest.StartRecommended(RunScope.Untried))));
                if (outcome.NotJudged > 0) actions.Add(($"Run the {Numbers.Count(outcome.NotJudged)} not judged", false, () => SayStarted(SelfTest.StartRecommended(RunScope.Busy))));
                actions.Add(("Copy summary", false, () => { GUIUtility.systemCopyBuffer = SelfTest.LastText; Say("Copied the self-test's summary."); }));
                actions.Add(("Copy log path", false, () => { GUIUtility.systemCopyBuffer = SelfTest.ResultFile; Say("Copied where the self-test's log is."); }));
            }
            var height = U(30f);
            var left = x;
            foreach (var (text, primary, act) in actions)
            {
                var w = Skin.Width(Skin.Button, text) + U(18f);
                if (left > x && left + w > x + width)
                {
                    left = x;
                    y += height + U(6f);
                }
                if (_drawing && GUI.Button(new Rect(left, y, w, height), text, primary ? Skin.Primary : Skin.Button)) act();
                left += w + U(8f);
            }
            return y + height + U(12f);
        }

        /// <summary>
        /// A box in the topic colour with its title, a count beside it and a chip at its right,
        /// around what <paramref name="content"/> lays under the title; measured on the first pass,
        /// drawn behind its content on the second.
        /// </summary>
        private static float Topic(float x, float y, float width, string title, string count, string chip, Action onChip, Func<float, float> content)
        {
            var index = _topic++;
            if (_drawing && index < TopicHeights.Count) Skin.Box(new Rect(x, y, width, TopicHeights[index]), Skin.TopicFill);
            if (_drawing)
            {
                var titleW = Skin.Width(Skin.TopicName, title);
                GUI.Label(new Rect(x + U(10f), y + U(6f), titleW + U(4f), U(22f)), title, Skin.TopicName);
                if (count != null) GUI.Label(new Rect(x + U(18f) + titleW, y + U(6f), width - titleW - U(150f), U(22f)), count, Skin.FaintLabel);
                if (chip != null)
                {
                    var chipW = Skin.Width(Skin.Chip, chip) + U(14f);
                    var at = new Rect(x + width - U(10f) - chipW, y + U(5f), chipW, U(24f));
                    if (GUI.Button(at, chip, Skin.Fitted(Skin.Chip, at))) onChip();
                }
            }
            var end = content(y + U(34f)) + U(8f);
            if (!_drawing)
            {
                while (TopicHeights.Count <= index) TopicHeights.Add(0f);
                TopicHeights[index] = end - y;
            }
            return end + U(10f);
        }
    }
}
