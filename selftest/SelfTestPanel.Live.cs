using UnityEngine;
using static Scry.ScryPanel;

namespace Scry
{
    /// <summary>
    /// The run live: the strip with its line, Pause (or Go on) and Stop, and a segment a part
    /// along its foot; the card, held or done, with a tile a part by area, the part picked or
    /// under the mouse, and the feed of what needs a look, newest first. What is drawn is worked
    /// out again only as the run moves on, so drawing allocates nothing a frame.
    /// </summary>
    internal static partial class SelfTestPanel
    {
        private static SelfTestLive _live;
        private static (ScenarioRunner Runner, int Done, string Current, int Marked) _liveKey;
        private static string _picked;
        private static Vector2 _liveScroll;

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

        private static void LiveStrip(Rect rect)
        {
            var tone = Skin.Accent;
            var stopW = Skin.Width(Skin.Chip, "Stop") + U(12f);
            var stopArea = new Rect(rect.xMax - stopW - U(8f), rect.y, stopW + U(8f), rect.height);
            var lineArea = new Rect(rect.x, rect.y, rect.width - stopArea.width, rect.height);
            var pause = SelfTest.Paused ? "Go on" : SelfTest.PauseAsked ? "Pausing" : "Pause";
            var pauseTip = SelfTest.Paused ? "Carry the self-test on from the next part" : "Hold the self-test once the part running is done, to look at it or move";
            if (NoticeStrip(lineArea, tone, SelfTest.Progress, Skin.Label, false, pause, SelfTest.PauseAsked, false, chipTip: pauseTip, lineTip: SelfTest.ProgressTip) == StripClick.Chip)
            {
                SelfTest.TogglePause();
                HidePauseCard(false);
            }
            Skin.Fill(stopArea, Skin.Alpha(tone, 0.13f));
            var chipH = U(24f);
            var stop = new Rect(stopArea.x, stopArea.y + (stopArea.height - chipH) / 2f, stopW, chipH);
            if (GUI.Button(stop, "Stop", Skin.Fitted(Skin.Chip, stop))) Say("Scry: " + SelfTest.Stop());

            // A segment a part along the foot, in the run's order.
            var live = Live();
            if (live == null || live.InRunOrder.Count == 0 || Event.current.type != EventType.Repaint) return;
            var each = rect.width / live.InRunOrder.Count;
            for (var i = 0; i < live.InRunOrder.Count; i++)
            {
                Skin.Fill(new Rect(rect.x + i * each, rect.yMax - U(4f), Mathf.Max(1f, each - (each > U(3f) ? 1f : 0f)), U(4f)), ColourOf(live.InRunOrder[i].State));
            }
        }

        private const float LabelWidth = 120f;
        private const float TileSize = 11f;
        private const float TileGap = 3f;

        /// <summary>How many tiles stand on a row in a width.</summary>
        private static int PerRow(float width) => Mathf.Max(1, Mathf.FloorToInt((width - U(LabelWidth) + U(TileGap)) / U(TileSize + TileGap)));

        /// <summary>How tall an area's tiles stand.</summary>
        private static float RowsHeight(int tiles, float width) => Mathf.Max(U(20f), Mathf.CeilToInt(tiles / (float)PerRow(width)) * U(TileSize + TileGap) + U(6f));

        /// <summary>The lines of the part picked: its checks needing a look, or that it went well.</summary>
        private static string PickedText(SelfTestLive live)
        {
            if (_picked == null) return "Pick a tile to read its part here.";
            var lines = new System.Collections.Generic.List<string> { _picked };
            foreach (var entry in live.Feed) if (entry.Part == _picked) lines.Add("  " + SelfTestLive.Word(entry.Kind) + ": " + entry.Text);
            if (lines.Count == 1) lines.Add(live.InRunOrder.Find(t => t.Name == _picked).State == TileState.ToCome ? "  still to come" : "  nothing needed a look");
            return string.Join("\n", lines);
        }

        private static void LiveCard(Rect rect)
        {
            var live = Live();
            var width = CardWidth(rect);
            var done = !SelfTest.Running;
            var headline = done ? SelfTest.LastHeadline ?? "" : SelfTest.Progress ?? "";
            var headlineH = Skin.Height(Skin.Wrap, headline, width);
            var picked = live != null ? PickedText(live) : "";
            var pickedH = Skin.Height(Skin.DimWrap, picked, width);
            var height = U(44f) + headlineH + U(10f) + pickedH + U(12f);
            if (live != null)
            {
                foreach (var area in live.Areas) height += RowsHeight(area.Tiles.Count, width);
                height += U(28f);
                foreach (var entry in live.Feed) height += Skin.Height(FeedStyle(entry), FeedText(entry), width);
            }
            var lines = done ? SelfTest.LastSummary : new System.Collections.Generic.List<string>();
            var advice = done ? SelfTest.LastAdvice ?? "" : "";
            var adviceH = done ? Skin.Height(Skin.DimWrap, advice, width) : 0f;
            height += lines.Count * U(22f) + U(10f) + adviceH + U(12f);

            var card = BeginCard(rect, ref _liveScroll, height, "Self-test");
            var x = card.X;
            var y = card.Y;
            GUI.Label(new Rect(x, y, width, headlineH), headline, Skin.Wrap);
            y += headlineH + U(10f);
            if (live != null) y = Tiles(live, x, y, width);
            GUI.Label(new Rect(x, y, width, pickedH), picked, Skin.DimWrap);
            y += pickedH + U(12f);
            if (live != null) y = Feed(live, x, y, width);
            var inFailed = false;
            foreach (var line in lines)
            {
                if (!line.StartsWith(" ", System.StringComparison.Ordinal)) inFailed = line.StartsWith("Failed", System.StringComparison.Ordinal);
                GUI.Label(new Rect(x, y, width, U(22f)), line, inFailed ? Skin.WarnLabel : Skin.Label);
                y += U(22f);
            }
            y += U(10f);
            if (done) GUI.Label(new Rect(x, y, width, adviceH), advice, Skin.DimWrap);
            if (EndCard(rect, out var close))
            {
                if (SelfTest.Running) HidePauseCard(true);
                else DetailsAway();
            }
            if (SelfTest.Running)
            {
                if (CardButton(rect, close, "Go on", out _))
                {
                    SelfTest.TogglePause();
                    HidePauseCard(false);
                }
            }
            else if (CardButton(rect, close, "Copy summary", out _))
            {
                GUIUtility.systemCopyBuffer = SelfTest.LastText;
                Say("Copied the self-test's summary.");
            }
        }

        /// <summary>A row of tiles a part for each area, its name before them; a click picks a tile's part.</summary>
        private static float Tiles(SelfTestLive live, float x, float y, float width)
        {
            var perRow = PerRow(width);
            var step = U(TileSize + TileGap);
            foreach (var area in live.Areas)
            {
                GUI.Label(new Rect(x, y - U(4f), U(LabelWidth) - U(6f), U(20f)), area.Area, Skin.DimLabel);
                for (var i = 0; i < area.Tiles.Count; i++)
                {
                    var (name, state) = area.Tiles[i];
                    var tile = new Rect(x + U(LabelWidth) + i % perRow * step, y + i / perRow * step, U(TileSize), U(TileSize));
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (name == _picked) Skin.Fill(new Rect(tile.x - U(2f), tile.y - U(2f), tile.width + U(4f), tile.height + U(4f)), Skin.Text);
                        Skin.Fill(tile, ColourOf(state));
                    }
                    if (GUI.Button(tile, GUIContent.none, GUIStyle.none)) _picked = name == _picked ? null : name;
                }
                y += RowsHeight(area.Tiles.Count, width);
            }
            return y + U(4f);
        }

        /// <summary>The dim wrapped text in the warning's colour, for a failure in the feed; made once.</summary>
        private static GUIStyle WarnWrap
        {
            get
            {
                // Made again whenever the panel makes its styles again, at another size.
                if (_warnWrap == null || _warnWrapOf != Skin.DimWrap)
                {
                    _warnWrapOf = Skin.DimWrap;
                    _warnWrap = new GUIStyle(Skin.DimWrap) { normal = { textColor = Skin.Warn } };
                }
                return _warnWrap;
            }
        }

        private static GUIStyle _warnWrap;
        private static GUIStyle _warnWrapOf;

        /// <summary>The style a feed line is drawn and measured in.</summary>
        private static GUIStyle FeedStyle(FeedEntry entry) => entry.Kind == FeedKind.Failed ? WarnWrap : Skin.DimWrap;

        private static string FeedText(FeedEntry entry) => $"{SelfTestLive.Word(entry.Kind)} · {entry.Part}: {entry.Text}";

        /// <summary>Every line that needs a look, newest first, a failure in the warning's colour.</summary>
        private static float Feed(SelfTestLive live, float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, width, U(22f)), live.Feed.Count == 0 ? "Nothing needed a look so far." : Naming.Counted("Needs a look", live.Feed.Count), Skin.Label);
            y += U(28f);
            foreach (var entry in live.Feed)
            {
                var text = FeedText(entry);
                var h = Skin.Height(FeedStyle(entry), text, width);
                GUI.Label(new Rect(x, y, width, h), text, FeedStyle(entry));
                y += h;
            }
            return y;
        }
    }
}
