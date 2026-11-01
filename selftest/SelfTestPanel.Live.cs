using UnityEngine;
using static Scry.ScryPanel;

namespace Scry
{
    /// <summary>
    /// The run live on the strip: how far and how long, the part running with a clock of its own
    /// against its last time, the newest line needing a look while it is new, counts of what failed
    /// and what was untried so far that pause the run and open the card at their section, Pause
    /// (or Go on) and Stop, and a segment a part along its foot. What is drawn is worked out again
    /// only as the run moves on.
    /// </summary>
    internal static partial class SelfTestPanel
    {
        private static SelfTestLive _live;
        private static (ScenarioRunner Runner, int Done, string Current, int Marked) _liveKey;

        /// <summary>The run's live view, worked out again only once a part ends or begins or a line needing a look comes.</summary>
        private static SelfTestLive Live()
        {
            var runner = SelfTest.LiveRunner;
            if (runner == null) return null;
            var key = (runner, runner.Done, runner.Current, runner.Marked.Count);
            if (_live == null || key != _liveKey)
            {
                _live = SelfTestLive.Of(runner);
                _liveKey = key;
            }
            return _live;
        }

        /// <summary>A tile's colour by how its part went: the panel's own green for a pass, its warning for a failure, half of it for a check not judged, the blue of what is immune for a row untried, the accent for the part running.</summary>
        private static Color ColourOf(TileState state)
        {
            switch (state)
            {
                case TileState.Pass: return Skin.KindColor(Kind.StatusEffect);
                case TileState.Fail: return Skin.Warn;
                case TileState.Busy: return Skin.Alpha(Skin.Warn, 0.45f);
                case TileState.Untried: return Skin.Immune;
                case TileState.Skipped: return Skin.Alpha(Skin.Faint, 0.5f);
                case TileState.Running: return Skin.Accent;
                default: return Skin.Alpha(Skin.Text, 0.10f);
            }
        }

        /// <summary>How many lines of a kind the feed holds so far.</summary>
        private static int CountOf(SelfTestLive live, FeedKind kind)
        {
            var count = 0;
            if (live == null) return 0;
            foreach (var entry in live.Feed) if (entry.Kind == kind) count++;
            return count;
        }

        /// <summary>A chip at the strip's right, left of those before it: true when clicked; its words coloured where a colour is given.</summary>
        private static bool StripChip(ref float right, Rect rect, string text, bool on, Color? colour = null)
        {
            var chipH = U(24f);
            var width = Skin.Width(Skin.Chip, text) + U(14f);
            var chip = new Rect(right - width, rect.y + (rect.height - chipH) / 2f - U(1f), width, chipH);
            right = chip.x - U(6f);
            if (!colour.HasValue) return GUI.Button(chip, text, Skin.Fitted(on ? Skin.ChipOn : Skin.Chip, chip));
            var clicked = GUI.Button(chip, GUIContent.none, Skin.Fitted(Skin.Chip, chip));
            Skin.LabelIn(chip, text, Skin.SmallCenter, colour.Value);
            return clicked;
        }

        private static void LiveStrip(Rect rect)
        {
            var tone = Skin.Accent;
            if (Event.current.type == EventType.Repaint)
            {
                Skin.Fill(rect, Skin.Alpha(tone, 0.13f));
                Skin.Fill(new Rect(rect.x, rect.y, U(3f), rect.height), tone);
            }
            var right = rect.xMax - U(6f);
            if (StripChip(ref right, rect, "Stop", false)) Say("Scry: " + SelfTest.Stop());
            var pause = SelfTest.Paused ? "Go on" : SelfTest.PauseAsked ? "Pausing" : "Pause";
            if (StripChip(ref right, rect, pause, SelfTest.PauseAsked))
            {
                SelfTest.TogglePause();
                HidePauseCard(false);
            }
            var live = Live();
            var untried = CountOf(live, FeedKind.Untried);
            var failed = CountOf(live, FeedKind.Failed);
            if (untried > 0 && StripChip(ref right, rect, $"● {Numbers.Count(untried)} untried", false, Skin.Immune)) OpenAt(Section.Untried);
            if (failed > 0 && StripChip(ref right, rect, $"● {Numbers.Count(failed)} failed", false, Skin.Warn)) OpenAt(Section.Failed);
            right = PartClock(rect, right, tone);
            var left = rect.x + U(14f);
            var line = SelfTest.Progress ?? "";
            GUI.Label(new Rect(left, rect.y, Mathf.Max(0f, right - left), rect.height - U(4f)), Skin.Fit(Skin.Label, line, right - left), Skin.Label);
            Segments(rect, live);
        }

        /// <summary>The part running's own clock left of the chips: a small bar of its time against its last, and the time in words. Where it stops.</summary>
        private static float PartClock(Rect rect, float right, Color tone)
        {
            var runner = SelfTest.LiveRunner;
            if (SelfTest.Paused || runner?.Current == null) return right;
            var last = SelfTest.LastTimeOf(runner.Current);
            var clock = SelfTestWords.PartClock(SelfTest.PartSeconds, last);
            var width = Skin.Width(Skin.FaintLabel, clock);
            GUI.Label(new Rect(right - U(4f) - width, rect.y, width + U(4f), rect.height - U(4f)), clock, Skin.FaintLabel);
            var bar = new Rect(right - U(10f) - width - U(56f), rect.y + rect.height / 2f - U(4f), U(56f), U(4f));
            if (Event.current.type == EventType.Repaint)
            {
                Skin.Fill(bar, Skin.Alpha(Skin.Text, 0.12f));
                var share = last.HasValue && last.Value > 0 ? Mathf.Clamp01((float)(SelfTest.PartSeconds / last.Value)) : 0f;
                Skin.Fill(new Rect(bar.x, bar.y, bar.width * share, bar.height), tone);
            }
            return bar.x - U(10f);
        }

        /// <summary>A segment a part along the strip's foot, in the run's order.</summary>
        private static void Segments(Rect rect, SelfTestLive live)
        {
            if (live == null || live.InRunOrder.Count == 0 || Event.current.type != EventType.Repaint) return;
            var each = rect.width / live.InRunOrder.Count;
            for (var i = 0; i < live.InRunOrder.Count; i++)
            {
                Skin.Fill(new Rect(rect.x + i * each, rect.yMax - U(4f), Mathf.Max(1f, each - (each > U(3f) ? 1f : 0f)), U(4f)), ColourOf(live.InRunOrder[i].State));
            }
        }

        /// <summary>A section of the card a count on the strip opens it at.</summary>
        private enum Section { None, Failed, Untried }

        private static Section _openSection;

        /// <summary>A count on the strip clicked: the run holds once the part running is done, and the card opens at that count's section.</summary>
        private static void OpenAt(Section section)
        {
            SelfTest.PauseSoon();
            HidePauseCard(false);
            _openSection = section;
        }

        /// <summary>The section the card is to open at, once: none after it is taken.</summary>
        private static Section TakeOpenSection()
        {
            if (!SelfTest.Paused) return Section.None;
            var section = _openSection;
            _openSection = Section.None;
            return section;
        }
    }
}
