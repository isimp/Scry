using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What Scry has in the world, listed under the "Clear world" button while the mouse is on
    /// it or on the list: each line goes to its entry when its name is clicked and is taken out
    /// of the world by its ×. The button itself still clears everything.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static bool _outOpen;
        private static Rect _outRect;
        private static List<OutRow> _outRows = new List<OutRow>();
        private static int _outRowsFrame = -1;

        /// <summary>How many lines fit in the list; past them the last line says how many more there are.</summary>
        private static int _outFits;
        private static readonly string[] ClearTexts = new string[100];

        private static float OutRowH => U(26f);
        private static float OutTop => U(30f);
        private static float OutCrossW => U(34f);

        /// <summary>The button's text for so many lines out, made once for each count.</summary>
        private static string ClearText(int lines)
        {
            if (lines <= 1 || lines >= ClearTexts.Length) return OutWords.ClearWorld(lines);
            return ClearTexts[lines] ?? (ClearTexts[lines] = OutWords.ClearWorld(lines));
        }

        /// <summary>Takes everything of Scry's out of the world, as the header's button does, and says so.</summary>
        private static void ClearWorld()
        {
            Previews.ClearWorld();
            _outOpen = false;
            Say("Cleared. Nothing from Scry is left in the world.");
        }

        /// <summary>
        /// Opens the list while the mouse is on the button or on the list (with a little room
        /// between them), and works out its lines once a frame while open.
        /// </summary>
        private static void OutHover(Rect button, int lines, Event e, Rect room)
        {
            if (lines == 0)
            {
                _outOpen = false;
                return;
            }

            var zone = button;
            if (_outOpen)
            {
                var left = Mathf.Min(button.x, _outRect.x) - U(6f);
                var right = Mathf.Max(button.xMax, _outRect.xMax) + U(6f);
                zone = new Rect(left, button.y - U(6f), right - left, _outRect.yMax - button.y + U(12f));
            }
            var wasOpen = _outOpen;
            _outOpen = zone.Contains(e.mousePosition);
            if (!_outOpen) return;

            // Once a frame while open, as things end and come: the panel has no layout events
            // (useGUILayout is off), so a refresh tied to them never came.
            if (!wasOpen || _outRowsFrame != Time.frameCount)
            {
                _outRows = Previews.Out();
                _outRowsFrame = Time.frameCount;
            }

            // Under the button, kept inside the panel: as wide as it can be up to its usual width,
            // and as many lines as fit, the last saying how many more are out.
            var width = Mathf.Min(U(320f), room.width);
            var x = Mathf.Clamp(button.xMax - width, room.x, room.xMax - width);
            var top = button.yMax + U(4f);
            var fit = Mathf.Max(1, Mathf.FloorToInt((room.yMax - top - OutTop - U(6f)) / OutRowH));
            _outFits = _outRows.Count > fit ? fit - 1 : _outRows.Count;
            var shownLines = _outRows.Count > fit ? fit : _outRows.Count;
            _outRect = new Rect(x, top, width, OutTop + shownLines * OutRowH + U(6f));
        }

        /// <summary>Before anything under the list is drawn: a click on one of its lines.</summary>
        private static void OutClicks(Explorer explorer, Event e)
        {
            if (!_outOpen || e.type != EventType.MouseDown || !_outRect.Contains(e.mousePosition)) return;
            var index = Mathf.FloorToInt((e.mousePosition.y - _outRect.y - OutTop) / OutRowH);
            if (e.button == 0 && index >= 0 && index < _outFits)
            {
                var row = _outRows[index];
                if (e.mousePosition.x >= _outRect.xMax - OutCrossW)
                {
                    Previews.TakeAway(row);
                    _outRows = Previews.Out();
                    if (_outRows.Count == 0) _outOpen = false;
                }
                else
                {
                    Go(explorer, OutTarget(row));
                }
            }
            e.Use();
        }

        private static string OutTarget(OutRow row) => row.Place == OutPlace.Status ? EntryKeys.For(Kind.StatusEffect, row.Key) : row.Key;

        private static string OutPlaceWord(OutPlace place)
        {
            switch (place)
            {
                case OutPlace.Shown: return "Shown";
                case OutPlace.Pinned: return "Pinned";
                case OutPlace.Sound: return "Sound";
                case OutPlace.Status: return "On you";
                default: return "Playing";
            }
        }

        /// <summary>Drawn last, over what lies below the button.</summary>
        private static void DrawOutList(Explorer explorer)
        {
            if (!_outOpen || _outRows.Count == 0 || Event.current.type != EventType.Repaint) return;
            Skin.Box(_outRect, Skin.Backdrop, Skin.Outline);
            GUI.Label(new Rect(_outRect.x + U(12f), _outRect.y + U(6f), _outRect.width - U(24f), U(20f)),
                "Click a name to go to it, × to take it away", Skin.FaintLabel);

            var mouse = Event.current.mousePosition;
            for (var i = 0; i < _outFits; i++)
            {
                var r = _outRows[i];
                var line = new Rect(_outRect.x + U(4f), _outRect.y + OutTop + i * OutRowH, _outRect.width - U(8f), OutRowH);
                var onCross = line.Contains(mouse) && mouse.x >= _outRect.xMax - OutCrossW;
                if (line.Contains(mouse) && !onCross) Skin.Box(line, Skin.Hover);

                var placeW = U(64f);
                GUI.Label(new Rect(line.x + U(8f), line.y, placeW, line.height), OutPlaceWord(r.Place), Skin.DimLabel);
                var name = OutWords.Row(ShownName(explorer, OutTarget(r), r.Key), r.Count);
                var nameX = line.x + U(8f) + placeW;
                GUI.Label(new Rect(nameX, line.y, line.xMax - OutCrossW - nameX, line.height), name, Skin.Label);

                var cross = new Rect(_outRect.xMax - OutCrossW, line.y, OutCrossW - U(4f), line.height);
                if (onCross) Skin.Box(cross, Skin.Hover);
                GUI.Label(cross, "×", onCross ? Skin.Center : Skin.CenterDim);
            }

            if (_outFits < _outRows.Count)
            {
                var more = _outRows.Count - _outFits;
                GUI.Label(new Rect(_outRect.x + U(12f), _outRect.y + OutTop + _outFits * OutRowH, _outRect.width - U(24f), OutRowH),
                    OutWords.More(more), Skin.FaintLabel);
            }
        }
    }
}
