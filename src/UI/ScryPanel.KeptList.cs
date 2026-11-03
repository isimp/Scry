using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character keeps on Valheim's map (<see cref="CompendiumBook.Kept"/>), listed under
    /// the title row's "On the map" button, as Clear previews lists what is in the world: the
    /// list opens on a click and a click outside puts it away (<see cref="ClickBox"/>); each line
    /// its kind's mark, its name and how many stand, a click on the name opening its page and its
    /// × taking it off the map; "Take all off" at its foot. Every kind of dungeon taken off stays
    /// at its top, faint, with Keep on the map to put it back.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static readonly ClickBox KeptClick = new ClickBox();
        private static Rect _keptButton, _keptRect;
        private static readonly List<string> KeptRows = new List<string>();

        /// <summary>How many kept lines fit under the button, the rest told by the last line.</summary>
        private static int _keptFits;

        /// <summary>Whether every kind of dungeon is taken off, its line standing first to keep them again.</summary>
        private static bool _dungeonsOff;

        private static float KeptRowH => U(26f);
        private static float KeptTop => U(30f);
        private static float KeptFootH => U(30f);

        /// <summary>Whether the list stands open, for the self-test.</summary>
        [Diagnostic]
        public static bool KeptListOpenForTest => KeptClick.Open;

        /// <summary>Whether the list stands every kind of dungeon first, taken off, to be kept again, for the self-test.</summary>
        [Diagnostic]
        public static bool KeptDungeonsOffForTest => KeptClick.Open && _dungeonsOff && KeptRows.Count > 0 && KeptRows[0] == CompendiumBook.Dungeons;

        /// <summary>Opens the list or puts it away, as a click on its button does; for the self-test.</summary>
        [ForTest]
        public static void OpenKeptListForTest(bool open)
        {
            if (open) KeptClick.Show();
            else KeptClick.Close();
        }

        /// <summary>
        /// The button left of <paramref name="right"/> in the title row, once the character's book
        /// is read, and its list's place under it; where the row goes on, else
        /// <paramref name="right"/> where it is left out for room or in the compact view.
        /// </summary>
        private static float KeptFront(float right, float y, float height, float leftmost, Rect room, Event e)
        {
            var book = CompendiumKeeper.Book;
            if (book == null || _compact)
            {
                KeptClick.Close();
                _keptButton = Rect.zero;
                return right;
            }
            var text = SpotWords.KeptButton(book.Kept.Count);
            var width = Skin.Width(Skin.Button, text) + U(10f);
            var button = new Rect(right - U(8f) - width, y, width, height);
            if (button.x < leftmost)
            {
                KeptClick.Close();
                _keptButton = Rect.zero;
                return right;
            }
            _keptButton = button;
            if (GUI.Button(button, text, KeptClick.Open ? Skin.On : Skin.Button)) KeptClick.Pressed();
            CountDrawn(PanelPart.KeptButton);
            KeptLayout(book, button, room, e);
            return button.x;
        }

        /// <summary>The list's lines and its place under the button, kept inside the panel; put away by a press outside it and the button.</summary>
        private static void KeptLayout(CompendiumBook book, Rect button, Rect room, Event e)
        {
            if (e.rawType == EventType.MouseDown)
            {
                var zone = KeptClick.Open ? Rect.MinMaxRect(Mathf.Min(button.x, _keptRect.x), button.y, Mathf.Max(button.xMax, _keptRect.xMax), Mathf.Max(button.yMax, _keptRect.yMax)) : button;
                KeptClick.DownAt(zone.Contains(e.mousePosition));
            }
            if (!KeptClick.Open) return;
            MapKept.Refresh();
            KeptRows.Clear();
            _dungeonsOff = !book.Keeps(CompendiumBook.Dungeons);
            if (_dungeonsOff) KeptRows.Add(CompendiumBook.Dungeons);
            KeptRows.AddRange(book.Kept);
            var top = button.yMax + U(4f);
            var capped = (MapKept.AllDots > SeenOnMap.MostKept ? 1 : 0) + (book.Kept.Count == 0 ? 1 : 0);
            var fit = Mathf.Max(1, Mathf.FloorToInt((room.yMax - top - KeptTop - KeptFootH - U(6f)) / KeptRowH) - capped);
            _keptFits = KeptRows.Count > fit ? fit - 1 : KeptRows.Count;
            var lines = (KeptRows.Count > fit ? fit : KeptRows.Count) + capped;
            var width = Mathf.Min(U(400f), room.width);
            var x = Mathf.Clamp(button.xMax - width, room.x, room.xMax - width);
            _keptRect = new Rect(x, button.yMax + U(4f), width, KeptTop + lines * KeptRowH + KeptFootH);
        }

        /// <summary>Before anything under the list is drawn: a click on a line's name or its ×, or on Take all off.</summary>
        private static void KeptClicks(Explorer explorer, Event e)
        {
            var book = CompendiumKeeper.Book;
            if (!KeptClick.Open || book == null || e.type != EventType.MouseDown || !_keptRect.Contains(e.mousePosition)) return;
            if (e.button == 0)
            {
                var index = Mathf.FloorToInt((e.mousePosition.y - _keptRect.y - KeptTop) / KeptRowH);
                if (e.mousePosition.y >= _keptRect.yMax - KeptFootH)
                {
                    // Only on Take all off itself, not on the words beside it.
                    if (book.Kept.Count > 0 && e.mousePosition.x >= _keptRect.xMax - U(120f)) TakeAllOffMap();
                }
                else if (index >= 0 && index < _keptFits)
                {
                    if (_dungeonsOff && index == 0)
                    {
                        if (KeepAgain(_keptRect).Contains(e.mousePosition)) KeepOnMap(CompendiumBook.Dungeons, true);
                    }
                    else if (e.mousePosition.x >= _keptRect.xMax - U(34f)) KeepOnMap(KeptRows[index], false);
                    else if (KeptRows[index] != CompendiumBook.Dungeons)
                    {
                        KeptClick.Close();
                        Go(explorer, MapKept.KindsIn(KeptRows[index])[0]);
                    }
                }
            }
            e.Use();
        }

        /// <summary>Keeps a kind on the map or takes it off, for the character played, kept at once: the list's ×, the strip's button, and the self-test.</summary>
        public static void KeepOnMap(string kind, bool on)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            book.Keep(kind, on);
            CompendiumKeeper.Save(now: true);
        }

        /// <summary>Takes every kind off the map, the dungeons too, kept at once: the list's foot.</summary>
        public static void TakeAllOffMap()
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            book.TakeAllOff();
            CompendiumKeeper.Save(now: true);
        }

        /// <summary>Where the first line's Keep on the map stands while every kind of dungeon is taken off.</summary>
        private static Rect KeepAgain(Rect list)
        {
            var width = Skin.Width(Skin.Chip, SpotWords.Keep) + U(12f);
            return new Rect(list.xMax - U(10f) - width, list.y + KeptTop + (KeptRowH - U(22f)) / 2f, width, U(22f));
        }

        /// <summary>A kept line's name: every kind of dungeon, a place's kinds shown at once, or a kind's own.</summary>
        private static string KeptName(string kept)
        {
            if (kept == CompendiumBook.Dungeons) return SpotWords.DungeonsRow;
            var kinds = MapKept.KindsIn(kept);
            if (kinds.Length < 2) return MapKept.NameOf(kept);
            var family = WorldCatalog.Find(kinds[0])?.Family?.Name ?? MapKept.NameOf(kinds[0]);
            return SpotWords.AllKindsName(family, kinds.Length);
        }

        /// <summary>Drawn last, over what lies below the button: the heading, each kept kind's line, a line where too many marks stand, and the foot.</summary>
        private static void DrawKeptList()
        {
            if (!KeptClick.Open || _keptButton.width <= 0f) return;
            CountDrawn(PanelPart.KeptList);
            if (Event.current.type != EventType.Repaint) return;
            var r = _keptRect;
            Skin.Box(r, Skin.Backdrop, Skin.Outline);
            GUI.Label(new Rect(r.x + U(12f), r.y + U(6f), r.width - U(24f), U(20f)), SpotWords.KeptHeading, Skin.FaintLabel);
            var mouse = Event.current.mousePosition;
            var y = r.y + KeptTop;
            for (var row = 0; row < _keptFits; row++)
            {
                var kept = KeptRows[row];
                var line = new Rect(r.x + U(4f), y, r.width - U(8f), KeptRowH);
                if (_dungeonsOff && row == 0)
                {
                    // Taken off: faint, no count, Keep on the map putting it back.
                    var again = KeepAgain(r);
                    Skin.Icon(new Rect(r.x + U(12f), y + (KeptRowH - U(12f)) / 2f, U(12f), U(12f)), Skin.Circle, Skin.Alpha(Skin.Accent, 0.3f));
                    GUI.Label(new Rect(r.x + U(34f), y, again.x - r.x - U(40f), KeptRowH), SpotWords.DungeonsRow, Skin.DimLabel);
                    Skin.Chip.Draw(again, new GUIContent(SpotWords.Keep), again.Contains(mouse), false, false, false);
                    y += KeptRowH;
                    continue;
                }
                // The line lit under the pointer, or its × alone where the pointer is on it, as Clear previews lights them.
                var cross = new Rect(r.xMax - U(34f), y, U(30f), KeptRowH);
                var onCross = line.Contains(mouse) && mouse.x >= cross.x;
                if (line.Contains(mouse) && !onCross) Skin.Box(line, Skin.Hover);
                var dungeons = kept == CompendiumBook.Dungeons;
                var mark = new Rect(r.x + U(12f), y + (KeptRowH - U(12f)) / 2f, U(12f), U(12f));
                var colour = dungeons || MapKept.IsDungeon(MapKept.KindsIn(kept)[0]) ? Skin.Accent : Skin.MapDot(MapKept.PlaceOf(MapKept.KindsIn(kept)[0]) != null ? Kind.Location : WorldCatalog.Find(kept)?.Kind ?? Kind.Resource);
                Skin.Icon(mark, Skin.Circle, colour);
                var (seen, squares) = MapKept.CountOf(kept);
                var count = dungeons ? SpotWords.DungeonsCount(seen, squares) : SpotWords.KeptCount(seen, squares);
                var countW = Skin.Width(Skin.DimRight, count) + U(4f);
                GUI.Label(new Rect(mark.xMax + U(10f), y, r.width - U(70f) - countW, KeptRowH), Skin.Fit(Skin.Label, KeptName(kept), r.width - U(70f) - countW), Skin.Label);
                GUI.Label(new Rect(r.xMax - U(40f) - countW, y, countW, KeptRowH), count, Skin.DimRight);
                if (onCross) Skin.Box(cross, Skin.Hover);
                GUI.Label(cross, "×", onCross ? Skin.Center : Skin.CenterDim);
                y += KeptRowH;
            }
            if (KeptRows.Count > _keptFits)
            {
                GUI.Label(new Rect(r.x + U(12f), y, r.width - U(24f), KeptRowH), SpotWords.KeptMore(KeptRows.Count - _keptFits), Skin.DimLabel);
                y += KeptRowH;
            }
            if (CompendiumKeeper.Book is CompendiumBook book && book.Kept.Count == 0)
            {
                GUI.Label(new Rect(r.x + U(12f), y, r.width - U(24f), KeptRowH), SpotWords.NothingKept, Skin.DimLabel);
                y += KeptRowH;
            }
            if (MapKept.AllDots > SeenOnMap.MostKept)
            {
                GUI.Label(new Rect(r.x + U(12f), y, r.width - U(24f), KeptRowH), SpotWords.Capped(SeenOnMap.MostKept, MapKept.AllDots), Skin.DimLabel);
                y += KeptRowH;
            }
            Skin.Fill(new Rect(r.x + U(8f), y + U(2f), r.width - U(16f), 1f), Skin.Alpha(Skin.Text, 0.06f));
            GUI.Label(new Rect(r.x + U(12f), y + U(6f), r.width - U(120f), U(20f)), SpotWords.KeptFoot, Skin.FaintLabel);
            if (CompendiumKeeper.Book?.Kept.Count is null or 0) return;
            // Take all off lit under the pointer, as a line's × is.
            var all = new Rect(r.xMax - U(120f), y + U(4f), U(116f), U(24f));
            var onAll = all.Contains(mouse);
            if (onAll) Skin.Box(all, Skin.Hover);
            var allW = Skin.Width(Skin.Label, SpotWords.TakeAllOff);
            GUI.Label(new Rect(all.xMax - U(8f) - allW, all.y + U(2f), allW, U(20f)), SpotWords.TakeAllOff, onAll ? Skin.Label : Skin.DimLabel);
        }
    }
}
