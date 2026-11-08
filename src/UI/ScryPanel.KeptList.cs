using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character keeps on Valheim's map (<see cref="CompendiumBook.Kept"/>), listed under
    /// the title row's "On the map" button, as Clear previews lists what is in the world: the
    /// list opens on a click and a click outside puts it away (<see cref="ClickBox"/>); each line
    /// its kind's mark, its name and how many stand, a click on the name opening its page and its
    /// × taking it off the map, under a heading for each category (<see cref="KeptCategories"/>), its
    /// arrow folding it and its × taking the whole category off; "Take all off" at its foot. Every
    /// kind of dungeon kept is the dungeons' heading, its types under it to read and open; taken off it
    /// stays at the top, faint, with Keep on the map to put it back. More lines than fit under the button
    /// scroll, by the wheel or the thumb at its right edge (<see cref="RowScroll"/>).
    /// </summary>
    internal static partial class ScryPanel
    {
        private static readonly ClickBox KeptClick = new ClickBox();
        private static Rect _keptButton, _keptRect;
        private static readonly List<KeptLine> KeptRows = new List<KeptLine>();

        /// <summary>What the lines were worked out for, which categories stand folded (<see cref="KeptCategories.Folded"/>), and each type of dungeon's name and counts by the key it opens.</summary>
        private static (CompendiumBook, int, int, int, int) _keptRowsFor;
        private static int _keptCats;
        private static readonly Dictionary<string, (string Name, int Seen, int Told)> KeptTypes = new Dictionary<string, (string, int, int)>(System.StringComparer.Ordinal);

        /// <summary>How many kept lines stand under the button, the rest scrolled to, and the line standing first.</summary>
        private static int _keptFits;
        private static int _keptFirst;

        /// <summary>Whether a press holds the thumb, and how far down it the press took hold.</summary>
        private static bool _keptThumbHeld;
        private static float _keptThumbGrab;

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
        public static bool KeptDungeonsOffForTest => KeptClick.Open && _dungeonsOff && KeptRows.Count > 0 && KeptRows[0].Key == CompendiumBook.Dungeons;

        /// <summary>The first line of a look in the list, among all it holds, for the self-test; -1 for none.</summary>
        [ForTest]
        public static int KeptLineForTest(KeptLineLook look, KeptCategory category) => KeptRows.FindIndex(line => line.Look == look && line.Category == category);

        /// <summary>Folds a category of the list or opens it, as its arrow does, for the self-test.</summary>
        [ForTest]
        public static void FoldKeptCategoryForTest(KeptCategory category) => FoldKeptCategory(category);

        /// <summary>Takes a category off the map, as its heading's × does, for the self-test.</summary>
        [ForTest]
        public static void TakeCategoryOffForTest(KeptCategory category) => TakeCategoryOffMap(category);

        /// <summary>How many lines the list stands at once, for the self-test.</summary>
        [ForTest]
        public static int KeptShownForTest => _keptFits;

        /// <summary>The line standing first in the list, for the self-test.</summary>
        [ForTest]
        public static int KeptFirstForTest => _keptFirst;

        /// <summary>How many lines the list holds, for the self-test.</summary>
        [ForTest]
        public static int KeptLinesForTest => KeptRows.Count;

        /// <summary>A turn of the wheel over the list, for the self-test.</summary>
        [ForTest]
        public static void WheelKeptForTest(float wheel) => WheelKept(wheel);

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
                KeptClick.DownAt(zone.Contains(e.mousePosition) || (KeptClick.Open && _pickFrom == PickFrom.List && _pickRect.Contains(e.mousePosition)));
            }
            if (!KeptClick.Open)
            {
                if (_pickFrom == PickFrom.List) PutPickerAway();
                _keptFirst = 0;
                _keptThumbHeld = false;
                return;
            }
            MapKept.Refresh();
            _dungeonsOff = !book.Keeps(CompendiumBook.Dungeons);
            KeptRowsWorkedOut(book);
            var top = button.yMax + U(4f);
            var capped = (MapKept.AllDots > SeenOnMap.MostKept ? 1 : 0) + (book.Kept.Count == 0 ? 1 : 0);
            var fit = Mathf.Max(1, Mathf.FloorToInt((room.yMax - top - KeptTop - KeptFootH - U(6f)) / KeptRowH) - capped);
            _keptFits = Mathf.Min(KeptRows.Count, fit);
            _keptFirst = RowScroll.Clamp(_keptFirst, KeptRows.Count, _keptFits);
            var lines = _keptFits + capped;
            var width = Mathf.Min(U(400f), room.width);
            var x = Mathf.Clamp(button.xMax - width, room.x, room.xMax - width);
            _keptRect = new Rect(x, button.yMax + U(4f), width, KeptTop + lines * KeptRowH + KeptFootH);
            PickerLayout(book, room);
        }

        /// <summary>Before anything under the list is drawn: a click on a line's mark, name or ×, on Take all off, or in the mark's picker.</summary>
        private static void KeptClicks(Explorer explorer, Event e)
        {
            var book = CompendiumKeeper.Book;
            if (!KeptClick.Open || book == null) return;
            if (KeptScrolled(e) || e.type != EventType.MouseDown) return;
            if (_pickFrom == PickFrom.List && _pickRect.width > 0f && _pickRect.Contains(e.mousePosition))
            {
                if (e.button == 0 && PickerChoiceAt(e.mousePosition) is int chosen) PickMark(_picking, chosen);
                e.Use();
                return;
            }
            if (!_keptRect.Contains(e.mousePosition)) return;
            var picking = _pickFrom == PickFrom.List ? PutPickerAway() : null;
            if (e.button == 0)
            {
                var index = KeptRowAt(e.mousePosition.y);
                if (e.mousePosition.y >= _keptRect.yMax - KeptFootH)
                {
                    // Only on Take all off itself, not on the words beside it.
                    if (book.Kept.Count > 0 && e.mousePosition.x >= _keptRect.xMax - U(120f)) TakeAllOffMap();
                }
                else if (index >= 0)
                {
                    var line = KeptRows[index];
                    var onCross = e.mousePosition.x >= _keptRect.xMax - U(34f);
                    if (line.Look == KeptLineLook.Heading) HeadingClicked(line, e.mousePosition, onCross);
                    else if (line.Look == KeptLineLook.Type) OpenKept(explorer, line.Key);
                    else if (Pickable(index) && MarkBox(_keptRect, KeptRowTop(index)).Contains(e.mousePosition))
                    {
                        // A line's mark opens its picker, or puts it away again.
                        if (picking != line.Key) OpenPicker(line.Key);
                    }
                    else if (onCross) KeepOnMap(line.Key, false);
                    else OpenKept(explorer, MapKept.KindsIn(line.Key)[0]);
                }
            }
            e.Use();
        }

        /// <summary>A click on a heading: Keep on the map where the dungeons were taken off, its × taking its category off, elsewhere folding it where it holds lines.</summary>
        private static void HeadingClicked(KeptLine line, Vector2 at, bool onCross)
        {
            var off = line.Category == KeptCategory.Dungeons && _dungeonsOff;
            if (off && KeepAgain(_keptRect).Contains(at)) KeepOnMap(CompendiumBook.Dungeons, true);
            else if (!off && onCross && line.Keeps > 0) TakeCategoryOffMap(line.Category);
            else if (line.Under > 0) FoldKeptCategory(line.Category);
        }

        private static void OpenKept(Explorer explorer, string key)
        {
            KeptClick.Close();
            Go(explorer, key);
        }

        private static void FoldKeptCategory(KeptCategory category)
        {
            _keptCats = KeptCategories.Toggled(_keptCats, category);
            SaveRects();
        }

        /// <summary>The categories folded as the panel's file kept them.</summary>
        private static void TakeKeptListPlace(PanelPlace place)
        {
            if (place.KeptCats.HasValue) _keptCats = place.KeptCats.Value;
        }

        /// <summary>Every kind of a category taken off the map, kept at once: its heading's ×.</summary>
        private static void TakeCategoryOffMap(KeptCategory category)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            foreach (var kept in book.Kept.ToList()) if (MapKept.CategoryOf(kept) == category) book.Keep(kept, false);
            CompendiumKeeper.Save(now: true);
        }

        /// <summary>The list's lines, worked out again only as what is kept, what stands, what is folded or the catalog changes; a family's types of dungeon as one (<see cref="KindsList.Group"/>).</summary>
        private static void KeptRowsWorkedOut(CompendiumBook book)
        {
            var now = (book, book.KeptVersion, MapKept.Worked, _keptCats, WorldCatalog.Current?.Regroups ?? -1);
            if (now == _keptRowsFor) return;
            _keptRowsFor = now;
            KeptTypes.Clear();
            var keys = MapKept.DungeonCounts.Keys.OrderBy(key => key, System.StringComparer.Ordinal).ToList();
            var under = keys.Select(key => WorldCatalog.Find(key)?.Family is Family family && family.Kinds.Count > 1 ? family.Head.Key : key).ToList();
            var types = new List<string>();
            foreach (var (_, members) in KindsList.Group(under))
            {
                var first = keys[members[0]];
                var (seen, told) = (0, 0);
                foreach (var member in members)
                {
                    seen += MapKept.DungeonCounts[keys[member]].Seen;
                    told += MapKept.DungeonCounts[keys[member]].Told;
                }
                var entry = WorldCatalog.Find(first);
                KeptTypes[first] = (entry != null ? MapSpots.FamilyNameOf(entry) : MapKept.NameOf(first), seen, told);
                types.Add(first);
            }
            KeptRows.Clear();
            KeptRows.AddRange(KeptLine.Of(book.Kept, types, MapKept.IsDungeon, _keptCats));
        }

        /// <summary>The line standing at a height, among all the list holds; -1 where none does.</summary>
        private static int KeptRowAt(float y)
        {
            var shown = Mathf.FloorToInt((y - _keptRect.y - KeptTop) / KeptRowH);
            return shown >= 0 && shown < _keptFits ? shown + _keptFirst : -1;
        }

        /// <summary>Where a line standing in the list starts.</summary>
        private static float KeptRowTop(int row) => _keptRect.y + KeptTop + (row - _keptFirst) * KeptRowH;

        /// <summary>The track the thumb runs on, beside the lines; narrow where it is drawn, as the panel's other scrollbars are.</summary>
        private static Rect KeptTrack => new Rect(_keptRect.xMax - U(10f), _keptRect.y + KeptTop, U(8f), _keptFits * KeptRowH);

        /// <summary>The thumb's least length, as the panel's other scrollbars keep it long enough to catch.</summary>
        private static float KeptThumbLeast => U(32f);

        private static bool KeptScrolls => KeptRows.Count > _keptFits;

        /// <summary>The first line moved by the wheel; a picker open from a line is put away, as its line moves from under it.</summary>
        private static void WheelKept(float wheel)
        {
            var first = RowScroll.Wheeled(_keptFirst, wheel, KeptRows.Count, _keptFits);
            if (first == _keptFirst) return;
            _keptFirst = first;
            if (_pickFrom == PickFrom.List) PutPickerAway();
        }

        /// <summary>The wheel over the list, and a press on its track or thumb held until let go; whether the event was theirs.</summary>
        private static bool KeptScrolled(Event e)
        {
            var track = KeptTrack;
            if (e.type == EventType.ScrollWheel)
            {
                if (!_keptRect.Contains(e.mousePosition) || (_pickFrom == PickFrom.List && _pickRect.Contains(e.mousePosition))) return false;
                WheelKept(e.delta.y);
                e.Use();
                return true;
            }
            if (_keptThumbHeld && e.type == EventType.MouseDrag)
            {
                _keptFirst = RowScroll.FirstAt(e.mousePosition.y - _keptThumbGrab, track.y, track.height, KeptRows.Count, _keptFits, KeptThumbLeast);
                e.Use();
                return true;
            }
            if (_keptThumbHeld && e.rawType == EventType.MouseUp)
            {
                _keptThumbHeld = false;
                e.Use();
                return true;
            }
            if (e.type != EventType.MouseDown || e.button != 0 || !KeptScrolls || !track.Contains(e.mousePosition)) return false;
            if (_pickFrom == PickFrom.List) PutPickerAway();
            // On the thumb it holds where it was taken; on the track the thumb comes under the pointer.
            var (y, height) = RowScroll.Thumb(track.y, track.height, _keptFirst, KeptRows.Count, _keptFits, KeptThumbLeast);
            var onThumb = e.mousePosition.y >= y && e.mousePosition.y < y + height;
            _keptThumbGrab = onThumb ? e.mousePosition.y - y : height / 2f;
            _keptFirst = RowScroll.FirstAt(e.mousePosition.y - _keptThumbGrab, track.y, track.height, KeptRows.Count, _keptFits, KeptThumbLeast);
            _keptThumbHeld = true;
            e.Use();
            return true;
        }

        /// <summary>The track and its thumb, in the panel's scrollbar look, where more lines are kept than stand.</summary>
        private static void DrawKeptThumb(Vector2 mouse)
        {
            if (!KeptScrolls) return;
            var track = KeptTrack;
            Skin.Gui.verticalScrollbar.Draw(track, GUIContent.none, false, false, false, false);
            var (y, height) = RowScroll.Thumb(track.y, track.height, _keptFirst, KeptRows.Count, _keptFits, KeptThumbLeast);
            var thumb = new Rect(track.x, y, track.width, height);
            Skin.Gui.verticalScrollbarThumb.Draw(thumb, GUIContent.none, thumb.Contains(mouse), _keptThumbHeld, false, false);
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
            return new Rect(list.xMax - U(14f) - width, KeptRowTop(0) + (KeptRowH - U(22f)) / 2f, width, U(22f));
        }

        /// <summary>A kept line's name: every kind of dungeon, a place's kinds shown at once, or a kind's own.</summary>
        private static string KeptName(string kept)
        {
            if (kept == CompendiumBook.Dungeons) return SpotWords.DungeonsRow;
            var kinds = MapKept.KindsIn(kept);
            if (kinds.Length < 2) return MapKept.NameOf(kept);
            var first = WorldCatalog.Find(kinds[0]);
            var family = first != null ? MapSpots.FamilyNameOf(first) : MapKept.NameOf(kinds[0]);
            return SpotWords.AllKindsName(family, kinds.Length);
        }

        /// <summary>Where a line's × stands, and whether the pointer is on it, not on the thumb beside it.</summary>
        private static bool OnKeptCross(Rect line, Rect cross, Vector2 mouse) => line.Contains(mouse) && mouse.x >= cross.x && !(KeptScrolls && mouse.x >= KeptTrack.x);

        /// <summary>A category's heading: its arrow where it holds lines, its shape, name and how many, and its ×; the dungeons taken off faint, with Keep on the map.</summary>
        private static void DrawKeptHeading(KeptLine heading, Rect r, float y, Vector2 mouse)
        {
            var dungeons = heading.Category == KeptCategory.Dungeons;
            var off = dungeons && _dungeonsOff;
            var line = new Rect(r.x + U(4f), y, r.width - U(8f), KeptRowH);
            var cross = new Rect(r.xMax - U(34f), y, U(30f), KeptRowH);
            var onCross = OnKeptCross(line, cross, mouse) && heading.Keeps > 0 && !off;
            if (line.Contains(mouse) && !onCross && heading.Under > 0) Skin.Box(line, Skin.Hover);
            if (heading.Under > 0) Skin.Icon(new Rect(r.x + U(7f), y + (KeptRowH - U(12f)) / 2f, U(12f), U(12f)), KeptCategories.Folded(_keptCats, heading.Category) ? Skin.Down : Skin.Up, Skin.Dim);
            var picture = new Rect(r.x + U(23f), y + (KeptRowH - U(16f)) / 2f, U(16f), U(16f));
            if (off) GUI.color = Skin.Alpha(Skin.MarkShown, 0.4f);
            GUI.DrawTexture(picture, MapSpots.CategoryPicture(heading.Category).texture);
            GUI.color = Skin.MarkShown;
            var name = dungeons ? SpotWords.DungeonsRow : SpotWords.CategoryName(heading.Category);
            if (off)
            {
                var again = KeepAgain(r);
                GUI.Label(new Rect(picture.xMax + U(8f), y, again.x - picture.xMax - U(14f), KeptRowH), name, Skin.DimLabel);
                Skin.Chip.Draw(again, new GUIContent(SpotWords.Keep), again.Contains(mouse), false, false, false);
                return;
            }
            string count;
            if (dungeons && KeptTypes.Count > 0)
            {
                var (seen, squares, told) = MapKept.CountOf(CompendiumBook.Dungeons);
                count = SpotWords.DungeonsCount(seen, squares, told);
            }
            else count = SpotWords.CategoryKinds(heading.Keeps);
            var countW = Skin.Width(Skin.DimRight, count) + U(4f);
            GUI.Label(new Rect(picture.xMax + U(8f), y, r.width - U(84f) - countW, KeptRowH), Skin.Fit(Skin.Label, name, r.width - U(84f) - countW), Skin.Label);
            GUI.Label(new Rect(r.xMax - U(40f) - countW, y, countW, KeptRowH), count, Skin.DimRight);
            if (heading.Keeps == 0) return;
            if (onCross) Skin.Box(cross, Skin.Hover);
            GUI.Label(cross, "×", onCross ? Skin.Center : Skin.CenterDim);
        }

        /// <summary>A type of dungeon kept with every kind: its disc, name and how many, lit under the pointer as a click opens its page; no × of its own, as they are kept as one.</summary>
        private static void DrawKeptType(KeptLine type, Rect r, float y, Vector2 mouse)
        {
            if (!KeptTypes.TryGetValue(type.Key, out var told)) return;
            var line = new Rect(r.x + U(4f), y, r.width - U(8f), KeptRowH);
            if (line.Contains(mouse)) Skin.Box(line, Skin.Hover);
            GUI.DrawTexture(new Rect(r.x + U(24f), y + (KeptRowH - U(14f)) / 2f, U(14f), U(14f)), MapSpots.CategoryPicture(KeptCategory.Dungeons).texture);
            var count = SpotWords.KeptCount(told.Seen, 0, told.Told);
            var countW = Skin.Width(Skin.DimRight, count) + U(4f);
            GUI.Label(new Rect(r.x + U(48f), y, r.width - U(84f) - countW, KeptRowH), Skin.Fit(Skin.Label, told.Name, r.width - U(84f) - countW), Skin.Label);
            GUI.Label(new Rect(r.xMax - U(40f) - countW, y, countW, KeptRowH), count, Skin.DimRight);
        }

        /// <summary>A kept kind's line, lit under the pointer, or its × alone where the pointer is on it, as Clear previews lights them; its mark as it stands on the map, as a legend, lit where a click opens its picker.</summary>
        private static void DrawKeptOwn(string kept, Rect r, float y, Vector2 mouse)
        {
            var line = new Rect(r.x + U(4f), y, r.width - U(8f), KeptRowH);
            var cross = new Rect(r.xMax - U(34f), y, U(30f), KeptRowH);
            var onCross = OnKeptCross(line, cross, mouse);
            if (line.Contains(mouse) && !onCross) Skin.Box(line, Skin.Hover);
            var mark = new Rect(r.x + U(26f), y + (KeptRowH - U(12f)) / 2f, U(12f), U(12f));
            var shown = MapKept.MarkOf(CompendiumKeeper.Book, kept);
            if (shown == KeptMarks.Dungeons) Skin.Icon(mark, Skin.Circle, Skin.Accent);
            else
            {
                var around = MarkBox(r, y);
                if (PickingFrom(kept, PickFrom.List)) Skin.Box(around, Skin.Alpha(Skin.Accent, 0.16f), Skin.Accent);
                else if (around.Contains(mouse)) Skin.Box(around, Skin.Hover, Skin.Alpha(Skin.Text, 0.18f));
                GUI.DrawTexture(new Rect(mark.x - U(2f), mark.y - U(2f), mark.width + U(4f), mark.height + U(4f)), MapSpots.MarkPicture(shown, kept).texture);
            }
            var (seen, squares, told) = MapKept.CountOf(kept);
            var count = SpotWords.KeptCount(seen, squares, told);
            var countW = Skin.Width(Skin.DimRight, count) + U(4f);
            GUI.Label(new Rect(mark.xMax + U(10f), y, r.width - U(84f) - countW, KeptRowH), Skin.Fit(Skin.Label, KeptName(kept), r.width - U(84f) - countW), Skin.Label);
            GUI.Label(new Rect(r.xMax - U(40f) - countW, y, countW, KeptRowH), count, Skin.DimRight);
            if (onCross) Skin.Box(cross, Skin.Hover);
            GUI.Label(cross, "×", onCross ? Skin.Center : Skin.CenterDim);
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
            for (var row = _keptFirst; row < _keptFirst + _keptFits; row++)
            {
                var kept = KeptRows[row];
                if (kept.Look == KeptLineLook.Heading) DrawKeptHeading(kept, r, y, mouse);
                else if (kept.Look == KeptLineLook.Type) DrawKeptType(kept, r, y, mouse);
                else DrawKeptOwn(kept.Key, r, y, mouse);
                y += KeptRowH;
            }
            DrawKeptThumb(mouse);
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
            // Resting on a mark that can be picked, the foot says what its click does.
            var onMark = MarkPointedRow(mouse) >= 0;
            GUI.Label(new Rect(r.x + U(12f), y + U(6f), r.width - U(120f), U(20f)), Skin.Fit(Skin.FaintLabel, onMark ? KeptMarkWords.MarkHint : SpotWords.KeptFoot, r.width - U(120f)), Skin.FaintLabel);
            if (CompendiumKeeper.Book?.Kept.Count is null or 0) return;
            // Take all off lit under the pointer, as a line's × is.
            var all = new Rect(r.xMax - U(120f), y + U(4f), U(116f), U(24f));
            var onAll = all.Contains(mouse);
            if (onAll) Skin.Box(all, Skin.Hover);
            var allW = Skin.Width(Skin.Label, SpotWords.TakeAllOff);
            GUI.Label(new Rect(all.xMax - U(8f) - allW, all.y + U(2f), allW, U(20f)), SpotWords.TakeAllOff, onAll ? Skin.Label : Skin.DimLabel);
            if (_pickFrom == PickFrom.List) DrawMarkPicker();
        }
    }
}
