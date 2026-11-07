using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The picker a kept kind's mark is chosen in (<see cref="KeptMarks"/>), opened by a click on
    /// the mark of its line in the On the map list and drawn over the list under that line: the
    /// colours, the shapes, the game's map pictures and the kind's own picture, each drawn as it
    /// would stand on the map, the one it stands in lit, those another kept kind stands in dotted.
    /// The line under the choices says what the one pointed at is and who stands in it already.
    /// A click picks at once and keeps the pick with the character; a click outside puts it away.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The kept line whose mark is being picked; none while the picker is put away.</summary>
        private static string _picking;
        private static Rect _pickRect;

        /// <summary>Each choice's place and the mark it gives, worked out with the picker's place.</summary>
        private static readonly List<(Rect Cell, int Mark, bool Colour)> Choices = new List<(Rect, int, bool)>();

        /// <summary>The picker's words and where each stands.</summary>
        private static readonly List<(Rect At, string Words, GUIStyle Style)> PickerWords = new List<(Rect, string, GUIStyle)>();

        /// <summary>The marks other kept lines stand in, to dot those choices.</summary>
        private static readonly HashSet<int> OthersMarks = new HashSet<int>();

        private static Rect _pickNote;
        private static string _pickHeadingFor, _pickHeading;

        private static float PickCell => U(28f);

        /// <summary>Whether the mark's picker stands open, for the self-test.</summary>
        [Diagnostic]
        public static bool MarkPickerShownForTest => KeptClick.Open && _picking != null && _pickRect.width > 0f;

        /// <summary>The marks the picker offers as it stands, for the self-test.</summary>
        [Diagnostic]
        public static List<int> MarkChoicesForTest => Choices.Select(c => c.Mark).ToList();

        /// <summary>Opens the picker of a kept line's mark, as a click on its mark does, or puts it away; for the self-test.</summary>
        [ForTest]
        public static void OpenMarkPickerForTest(string kept) => _picking = kept;

        /// <summary>Puts the picker away; the line it stood open for, none where it was away.</summary>
        private static string PutPickerAway()
        {
            var was = _picking;
            _picking = null;
            _pickRect = Rect.zero;
            return was;
        }

        /// <summary>Opens the picker for a kept line's mark, laid out under it as the list is next drawn.</summary>
        private static void OpenPicker(string kept) => _picking = kept;

        /// <summary>Gives a kept line the mark picked, kept at once: the picker's click, and the self-test.</summary>
        public static void PickMark(string kept, int mark)
        {
            var book = CompendiumKeeper.Book;
            if (book == null || !book.Pick(kept, mark)) return;
            CompendiumKeeper.Save(now: true);
        }

        /// <summary>Where a line's mark stands, lit under the pointer; the box a click opens its picker in.</summary>
        private static Rect MarkBox(Rect list, float lineY) => new Rect(list.x + U(7f), lineY + U(2f), U(22f), KeptRowH - U(4f));

        /// <summary>The right edge of the lines' marks, left of which a click is on the mark.</summary>
        private static float MarkRight(Rect list) => list.x + U(29f);

        /// <summary>Whether a shown line's mark can be picked: a kept kind's, never every kind of dungeon's, or a line taken off.</summary>
        private static bool Pickable(int row)
        {
            if (row < 0 || row >= KeptRows.Count || (_dungeonsOff && row == 0)) return false;
            var kept = KeptRows[row];
            return MapKept.MarkOf(CompendiumKeeper.Book, kept) != KeptMarks.Dungeons;
        }

        /// <summary>The line whose pickable mark the pointer rests on; -1 for none.</summary>
        private static int MarkPointedRow(Vector2 mouse)
        {
            var row = Mathf.FloorToInt((mouse.y - _keptRect.y - KeptTop) / KeptRowH);
            if (row < 0 || row >= _keptFits || !Pickable(row)) return -1;
            return MarkBox(_keptRect, _keptRect.y + KeptTop + row * KeptRowH).Contains(mouse) ? row : -1;
        }

        /// <summary>
        /// The picker's place under the line picked, or over it where the panel ends first, and
        /// its choices and words; put away where its line is no longer listed or shown.
        /// </summary>
        private static void PickerLayout(CompendiumBook book, Rect room)
        {
            _pickRect = Rect.zero;
            Choices.Clear();
            PickerWords.Clear();
            if (_picking == null) return;
            var row = KeptRows.IndexOf(_picking);
            if (row < 0 || row >= _keptFits || !Pickable(row) || !(book.MarkOf(_picking) is int current))
            {
                _picking = null;
                return;
            }
            OthersMarks.Clear();
            foreach (var other in book.Kept) if (other != _picking && book.MarkOf(other) is int theirs) OthersMarks.Add(theirs);
            var colour = KeptMarks.ColourOf(current);
            var pictures = Enumerable.Range(0, KeptMarks.Pictures).Where(p => KeptMarkPictures.Game(p) != null).ToList();
            var ownName = KeptMarkPictures.OwnNameOf(_picking);
            float cell = PickCell, pad = U(10f), label = U(18f), heading = U(22f), note = U(22f);
            var height = pad + heading + (label + cell) * (pictures.Count > 0 ? 3 : 2) + label + (ownName != null ? cell : U(20f)) + note + pad;
            var width = Mathf.Min(U(340f), room.width);
            var lineTop = _keptRect.y + KeptTop + row * KeptRowH;
            var top = lineTop + KeptRowH + U(2f);
            if (top + height > room.yMax) top = Mathf.Max(room.y, lineTop - height - U(2f));
            var x = Mathf.Clamp(_keptRect.x + U(8f), room.x, room.xMax - width);
            _pickRect = new Rect(x, top, width, height);

            var y = top + pad;
            if (_pickHeadingFor != _picking)
            {
                _pickHeadingFor = _picking;
                _pickHeading = KeptMarkWords.Heading(KeptName(_picking));
            }
            PickerWords.Add((new Rect(x + pad, y, width - 2f * pad, heading), _pickHeading, Skin.FaintLabel));
            y += heading;
            void Row(string words, IEnumerable<(int Mark, bool Colour)> marks)
            {
                PickerWords.Add((new Rect(x + pad, y, width - 2f * pad, label), words, Skin.DimLabel));
                y += label;
                var left = x + pad - U(3f);
                foreach (var (mark, isColour) in marks)
                {
                    Choices.Add((new Rect(left, y, cell, cell), mark, isColour));
                    left += cell + U(2f);
                }
                y += cell;
            }
            // A colour keeps the shape or picture the mark stands in.
            Row(KeptMarkWords.ColourRow, Enumerable.Range(0, KeptMarks.Colours).Select(c => (current - colour + c, true)));
            Row(KeptMarkWords.ShapeRow, Enumerable.Range(0, KeptMarks.Shapes).Select(s => (KeptMarks.Plain(colour, (MarkShape)s), false)));
            if (pictures.Count > 0) Row(KeptMarkWords.PictureRow, pictures.Select(p => (KeptMarks.Pictured(colour, p), false)));
            if (ownName != null) Row(KeptMarkWords.OwnRow, new[] { (KeptMarks.Own(colour), false) });
            else
            {
                PickerWords.Add((new Rect(x + pad, y, width - 2f * pad, label), KeptMarkWords.OwnRow, Skin.DimLabel));
                y += label;
                PickerWords.Add((new Rect(x + pad, y, width - 2f * pad, U(20f)), KeptMarkWords.NoOwn, Skin.FaintLabel));
                y += U(20f);
            }
            _pickNote = new Rect(x + pad, y + U(2f), width - 2f * pad, note - U(2f));
        }

        /// <summary>The mark the choice under the pointer gives; none off every choice.</summary>
        private static int? PickerChoiceAt(Vector2 at)
        {
            foreach (var (cell, mark, _) in Choices) if (cell.Contains(at)) return mark;
            return null;
        }

        /// <summary>Drawn over the list: the box, its words, each choice as it would stand, and what the one pointed at is.</summary>
        private static void DrawMarkPicker()
        {
            if (_picking == null || _pickRect.width <= 0f || !(CompendiumKeeper.Book?.MarkOf(_picking) is int current)) return;
            Skin.Box(_pickRect, Skin.Backdrop, Skin.Alpha(Skin.Text, 0.16f));
            foreach (var (at, words, style) in PickerWords) GUI.Label(at, words, style);
            var mouse = Event.current.mousePosition;
            int? pointed = null;
            foreach (var (cell, mark, isColour) in Choices)
            {
                var on = isColour ? KeptMarks.ColourOf(mark) == KeptMarks.ColourOf(current) : mark == current;
                var under = cell.Contains(mouse);
                if (under) pointed = mark;
                if (on) Skin.Box(cell, Skin.Alpha(Skin.Accent, 0.16f), Skin.Accent);
                else if (under) Skin.Box(cell, Skin.Hover, Skin.Alpha(Skin.Text, 0.18f));
                // A colour as its round swatch; the rest as the mark would stand on the map.
                var size = isColour ? U(18f) : U(24f);
                var picture = isColour ? MapSpots.Picture(Skin.MarkColour(mark)) : MapSpots.MarkPicture(mark, _picking);
                GUI.DrawTexture(new Rect(cell.center.x - size / 2f, cell.center.y - size / 2f, size, size), picture.texture);
                if (OthersMarks.Contains(mark)) Skin.Icon(new Rect(cell.xMax - U(7f), cell.yMax - U(7f), U(5f), U(5f)), Skin.Circle, Skin.Faint);
            }
            var own = Choices.FirstOrDefault(c => KeptMarks.IsOwn(c.Mark));
            if (own.Cell.width > 0f)
            {
                var name = KeptMarkPictures.OwnNameOf(_picking);
                GUI.Label(new Rect(own.Cell.xMax + U(8f), own.Cell.y, _pickRect.xMax - own.Cell.xMax - U(18f), own.Cell.height), name, Skin.DimLabel);
            }
            var shown = pointed ?? current;
            var holders = CompendiumKeeper.Book.Kept.Where(k => k != _picking && CompendiumKeeper.Book.MarkOf(k) == shown).Select(KeptName).ToList();
            var said = KeptMarkWords.Said(shown, KeptMarkPictures.OwnNameOf(_picking), holders);
            GUI.Label(_pickNote, Skin.Fit(Skin.FaintLabel, said, _pickNote.width), Skin.FaintLabel);
        }
    }
}
