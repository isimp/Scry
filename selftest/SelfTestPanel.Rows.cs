using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Scry.ScryPanel;

namespace Scry
{
    /// <summary>
    /// The rows the run's card is made of: the tiles by area with their legend and the part
    /// picked under them; a part's box with its name, time, Run again and its checks, what was
    /// checked on the left and what was found on the right; a row of where to stand with what it
    /// would show, Try where that thing is near; and two wrapped columns side by side. Each lays
    /// itself out the same whether measuring or drawing and returns where it ends.
    /// </summary>
    internal static partial class SelfTestPanel
    {
        private static string _picked;

        private const float TileSize = 11f;
        private const float TileGap = 3f;

        /// <summary>Every part as a tile by area, the areas' names in a column wide enough for the longest, then the legend and the part picked.</summary>
        private static float TilesIn(SelfTestOutcome outcome, float x, float y, float width)
        {
            var live = Live();
            if (live == null) return y;
            var labelW = U(12f) + live.Areas.Max(a => Skin.Width(Skin.DimLabel, a.Area));
            var step = U(TileSize + TileGap);
            var perRow = Mathf.Max(1, Mathf.FloorToInt((width - labelW) / step));
            foreach (var (area, tiles) in live.Areas)
            {
                if (_drawing) GUI.Label(new Rect(x, y - U(4f), labelW, U(20f)), area, Skin.DimLabel);
                for (var i = 0; i < tiles.Count && _drawing; i++)
                {
                    var (name, state) = tiles[i];
                    var tile = new Rect(x + labelW + i % perRow * step, y + i / perRow * step, U(TileSize), U(TileSize));
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (name == _picked) Skin.Fill(new Rect(tile.x - U(2f), tile.y - U(2f), tile.width + U(4f), tile.height + U(4f)), Skin.Text);
                        Skin.Fill(tile, ColourOf(state));
                    }
                    if (GUI.Button(tile, GUIContent.none, GUIStyle.none)) _picked = name == _picked ? null : name;
                }
                y += Mathf.Max(U(20f), Mathf.CeilToInt(tiles.Count / (float)perRow) * step + U(6f));
            }
            y = Legend(x + labelW, y, width - labelW);
            return Picked(outcome, x, y + U(4f), width);
        }

        private static readonly (TileState State, string Word)[] LegendOf =
        {
            (TileState.Pass, "passed"), (TileState.Fail, "failed"), (TileState.Untried, "untried rows"), (TileState.Busy, "not judged"), (TileState.Skipped, "skipped"), (TileState.Running, "running"),
        };

        private static float Legend(float x, float y, float width)
        {
            var left = x;
            foreach (var (state, word) in LegendOf)
            {
                if (state == TileState.Running && !SelfTest.Running) continue;
                var w = Skin.Width(Skin.FaintLabel, word);
                if (_drawing)
                {
                    if (Event.current.type == EventType.Repaint) Skin.Fill(new Rect(left, y + U(6f), U(9f), U(9f)), ColourOf(state));
                    GUI.Label(new Rect(left + U(13f), y, w + U(4f), U(20f)), word, Skin.FaintLabel);
                }
                left += w + U(30f);
            }
            return y + U(22f);
        }

        /// <summary>The part picked from a tile, its box as the failed ones have, or a line asking for a pick.</summary>
        private static float Picked(SelfTestOutcome outcome, float x, float y, float width)
        {
            var report = _picked == null ? null : SelfTest.LiveRunner?.Reports.FirstOrDefault(r => r.Name == _picked);
            if (report == null)
            {
                var hint = _picked == null ? "Pick a tile to open its part here." : _picked + ": still to come.";
                if (_drawing) GUI.Label(new Rect(x, y, width, U(20f)), Skin.Fit(Skin.FaintLabel, hint, width), Skin.FaintLabel);
                return y + U(22f);
            }
            var lines = report.Lines.Where(l => l.StartsWith("  FAIL ", System.StringComparison.Ordinal) || l.StartsWith("  BUSY ", System.StringComparison.Ordinal) || l.StartsWith("  untried ", System.StringComparison.Ordinal))
                .Select(l => Split(l.Substring(l.IndexOf(' ', 2) + 1))).ToList();
            if (lines.Count == 0) lines.Add((report.Result == Result.Skip ? "skipped" : "it passed every check", ""));
            return PartBox(x, y, width, report.Name, report.Seconds, lines, report.Result == Result.Fail);
        }

        /// <summary>The dim wrapped text in the warning's colour, for what a failed check found; made again whenever the panel makes its styles again, at another size.</summary>
        private static GUIStyle WarnWrap
        {
            get
            {
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

        private static (string What, string Found) Split(string line)
        {
            var at = line.IndexOf(": ", System.StringComparison.Ordinal);
            return at < 0 ? (line, "") : (line.Substring(0, at), line.Substring(at + 2));
        }

        /// <summary>A part's box: its name, its time and Run again once the run is done, then each check, what was checked against what was found.</summary>
        private static float PartBox(float x, float y, float width, string part, double? seconds, List<(string What, string Found)> checks, bool failed)
        {
            var top = y;
            var inner = width - U(20f);
            var chipW = SelfTest.Running ? 0f : Skin.Width(Skin.Chip, "Run again") + U(14f);
            var time = seconds.HasValue ? SelfTestWords.Duration(seconds.Value) : "";
            var timeW = Skin.Width(Skin.FaintLabel, time);
            var nameW = inner - chipW - timeW - U(16f);
            var nameH = Skin.Height(Skin.Wrap, part, nameW);
            var height = U(8f) + nameH + U(4f);
            foreach (var (what, found) in checks) height += RowHeight(inner - U(12f), what, found);
            height += U(6f);
            if (_drawing)
            {
                Skin.Box(new Rect(x, top, width, height), Skin.Sunken);
                GUI.Label(new Rect(x + U(10f), top + U(6f), nameW, nameH), part, Skin.Wrap);
                GUI.Label(new Rect(x + U(10f) + nameW + U(8f), top + U(6f), timeW + U(4f), U(22f)), time, Skin.FaintLabel);
                if (chipW > 0f)
                {
                    var chip = new Rect(x + width - U(10f) - chipW, top + U(5f), chipW, U(24f));
                    if (GUI.Button(chip, "Run again", Skin.Fitted(Skin.Chip, chip))) SayStarted(SelfTest.StartParts(new[] { part }));
                }
            }
            y = top + U(8f) + nameH + U(4f);
            foreach (var (what, found) in checks) y = TwoColumns(x + U(22f), y, inner - U(12f), what, found, Skin.DimWrap, failed ? WarnWrap : Skin.DimWrap);
            return top + height + U(6f);
        }

        private static float RowHeight(float width, string left, string right)
        {
            var leftW = right.Length == 0 ? width : width * 0.6f;
            var rightW = width - leftW - U(10f);
            return Mathf.Max(Skin.Height(Skin.DimWrap, left, leftW), right.Length == 0 ? 0f : Skin.Height(Skin.DimWrap, right, rightW)) + U(2f);
        }

        /// <summary>Two wrapped columns side by side, the left one alone where the right says nothing.</summary>
        private static float TwoColumns(float x, float y, float width, string left, string right, GUIStyle leftStyle, GUIStyle rightStyle)
        {
            var leftW = right.Length == 0 ? width : width * 0.6f;
            var rightW = width - leftW - U(10f);
            var height = RowHeight(width, left, right);
            if (_drawing)
            {
                GUI.Label(new Rect(x, y, leftW, height), left, leftStyle);
                if (right.Length > 0) GUI.Label(new Rect(x + leftW + U(10f), y, rightW, height), right, rightStyle);
            }
            return y + height;
        }

        /// <summary>A row of where to stand: the place on the left, what it would show in the middle, and where that thing is near now, so and Try.</summary>
        private static float StandRow(float x, float y, float width, SelfTestOutcome.UntriedGroup group, bool done)
        {
            var near = done && SelfTest.IsNear(group.Need);
            var tryW = near ? Skin.Width(Skin.Chip, "Try") + U(14f) : 0f;
            var nearW = near ? Skin.Width(Skin.SmallWrap, "near now") + U(8f) : 0f;
            var whereW = Mathf.Min(U(240f), width * 0.36f);
            var whatW = width - whereW - nearW - tryW - U(16f);
            var where = group.Where.StartsWith("stand ", System.StringComparison.Ordinal) ? group.Where.Substring(6) : group.Where;
            var whats = string.Join(" · ", group.Whats);
            var height = Mathf.Max(Skin.Height(Skin.Wrap, where, whereW), Skin.Height(Skin.DimWrap, whats, whatW)) + U(6f);
            if (_drawing)
            {
                if (Event.current.type == EventType.Repaint) Skin.Fill(new Rect(x, y, width, 1f), Skin.Outline);
                GUI.Label(new Rect(x, y + U(3f), whereW, height), where, Skin.Wrap);
                GUI.Label(new Rect(x + whereW + U(8f), y + U(3f), whatW, height), whats, Skin.DimWrap);
                if (near)
                {
                    Skin.LabelIn(new Rect(x + width - tryW - nearW, y + U(3f), nearW, U(22f)), "near now", Skin.Small, Skin.Immune);
                    var chip = new Rect(x + width - tryW + U(4f), y + U(2f), tryW - U(4f), U(24f));
                    if (GUI.Button(chip, "Try", Skin.Fitted(Skin.Chip, chip))) SayStarted(SelfTest.StartParts(group.Parts));
                }
            }
            return y + height;
        }
    }
}
