using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The picker a kept kind's mark is chosen in (<see cref="KeptMarks"/>), opened by a click on
    /// the mark of its line in the On the map list, drawn over the list under that line, or on the
    /// mark beside a lit Keep on the map, on a kind's page or on the map's strip, drawn under it:
    /// the colours, the shapes, the game's map pictures and the kind's own picture, each drawn as
    /// it would stand on the map, the one it stands in lit, those another kept kind stands in
    /// dotted. The line under the choices says what the one pointed at is and who stands in it
    /// already. A click picks at once and keeps the pick with the character; a click outside puts
    /// it away, as a click on the mark that opened it does.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>What a picker is opened from: a line of the On the map list, the mark beside a page's Keep on the map, or the map strip's.</summary>
        private enum PickFrom
        {
            List,
            Page,
            Strip,
        }

        /// <summary>The kept line whose mark is being picked, and what opened it; none while the picker is put away.</summary>
        private static string _picking;
        private static PickFrom _pickFrom;
        private static Rect _pickRect;

        /// <summary>
        /// Where the mark that opened the picker stands on the screen, a page's or the strip's, and
        /// the frame it was last drawn in: the picker goes with it, and away once it is not drawn.
        /// </summary>
        private static Rect _pickAnchor;
        private static int _pickAnchorFrame = -10;

        /// <summary>The line a press put the picker away from, and what it stood open from, so a press on the mark that opened it leaves it put away.</summary>
        private static string _pickBeforePress;
        private static PickFrom _pickFromBeforePress;

        /// <summary>The mark beside Keep on the map last drawn on a page and on the strip: its line, where it stands on the screen, and the frame.</summary>
        private static (string Kept, Rect At, int Frame) _pagePiece, _stripPiece;

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
        public static bool MarkPickerShownForTest => _picking != null && _pickRect.width > 0f && (_pickFrom != PickFrom.List || KeptClick.Open);

        /// <summary>The choice the own picture's name was last drawn beside.</summary>
        private static Rect _ownNamedBeside;

        /// <summary>Whether the own picture's name stands beside its own cell, the picker's last, not beside another choice, for the self-test.</summary>
        [Diagnostic]
        public static bool OwnNamedBesideItsPictureForTest => Choices.Count > 0 && _ownNamedBeside == Choices[Choices.Count - 1].Cell;

        /// <summary>What the picker stands open from, the list, a page or the strip; none while put away, for the self-test.</summary>
        [Diagnostic]
        public static string MarkPickerFromForTest => _picking == null ? null : _pickFrom.ToString();

        /// <summary>Where the mark that opened a page's or the strip's picker stands on the screen, and the picker's top there, for the self-test.</summary>
        [Diagnostic]
        public static (Rect Anchor, float PickerTop, float PickerHeight) MarkPickerPlaceForTest => (_pickAnchor, _pickTopOnScreen, _pickRect.height);

        /// <summary>The line the mark beside Keep on the map stands for, on the page or on the strip, drawn this frame or the one before; none where none is, for the self-test.</summary>
        [Diagnostic]
        public static string MarkPieceForTest(bool strip)
        {
            var piece = strip ? _stripPiece : _pagePiece;
            return Time.frameCount - piece.Frame <= 1 ? piece.Kept : null;
        }

        /// <summary>Presses the mark beside Keep on the map, on the page or on the strip, as a click on it does; for the self-test.</summary>
        [ForTest]
        public static void PressMarkPieceForTest(bool strip)
        {
            var piece = strip ? _stripPiece : _pagePiece;
            if (piece.Kept != null) PressMarkPiece(piece.Kept, strip ? PickFrom.Strip : PickFrom.Page, piece.At);
        }

        /// <summary>The marks the picker offers as it stands, for the self-test.</summary>
        [Diagnostic]
        public static List<int> MarkChoicesForTest => Choices.Select(c => c.Mark).ToList();

        /// <summary>Opens the picker of a kept line's mark, as a click on its mark does, or puts it away; for the self-test.</summary>
        [ForTest]
        public static void OpenMarkPickerForTest(string kept)
        {
            if (kept == null) PutPickerAway();
            else OpenPicker(kept);
        }

        /// <summary>Puts the picker away; the line it stood open for, none where it was away.</summary>
        private static string PutPickerAway()
        {
            var was = _picking;
            _picking = null;
            _pickRect = Rect.zero;
            return was;
        }

        /// <summary>Opens the picker for a kept line's mark, laid out under it as the list is next drawn.</summary>
        private static void OpenPicker(string kept)
        {
            _picking = kept;
            _pickFrom = PickFrom.List;
        }

        /// <summary>
        /// The mark beside a lit Keep on the map pressed, on a page or on the strip: opens the
        /// picker under it, or leaves it put away where the press put away the one it had opened.
        /// </summary>
        private static void PressMarkPiece(string kept, PickFrom from, Rect onScreen)
        {
            if (_pickBeforePress == kept && _pickFromBeforePress == from) return;
            _picking = kept;
            _pickFrom = from;
            _pickAnchor = onScreen;
            _pickAnchorFrame = Time.frameCount;
        }

        /// <summary>
        /// Before anything under a page's or the strip's picker takes the press: a press in it
        /// picks the choice under it, one anywhere else puts it away, noting it for the mark that
        /// opened it.
        /// </summary>
        private static void PickerClicks(Event e, PickFrom from)
        {
            if (e.type != EventType.MouseDown) return;
            if (_pickFromBeforePress == from) _pickBeforePress = null;
            if (_picking == null || _pickFrom != from) return;
            if (_pickRect.width > 0f && _pickRect.Contains(e.mousePosition))
            {
                if (e.button == 0 && PickerChoiceAt(e.mousePosition) is int chosen) PickMark(_picking, chosen);
                e.Use();
                return;
            }
            _pickBeforePress = _picking;
            _pickFromBeforePress = from;
            PutPickerAway();
        }

        /// <summary>
        /// The mark beside a lit Keep on the map, on a page or on the strip, drawn in a box already
        /// placed, its tip saying a click picks the look; pressed, it opens the picker under it.
        /// </summary>
        private static void MarkPiece(Rect at, string kept, int mark, PickFrom from)
        {
            var onScreen = OnScreen(at);
            if (Event.current.type == EventType.Repaint)
            {
                if (from == PickFrom.Strip) _stripPiece = (kept, onScreen, Time.frameCount);
                else _pagePiece = (kept, onScreen, Time.frameCount);
                if (_picking == kept && _pickFrom == from)
                {
                    _pickAnchor = onScreen;
                    _pickAnchorFrame = Time.frameCount;
                }
            }
            var size = U(18f);
            GUI.DrawTexture(new Rect(at.center.x - size / 2f, at.center.y - size / 2f, size, size), MapSpots.MarkPicture(mark, kept).texture);
            if (at.Contains(Event.current.mousePosition)) AskTip("mark:" + kept, KeptMarkWords.PieceTip(KeptName(kept)));
            if (GUI.Button(at, GUIContent.none, GUIStyle.none)) PressMarkPiece(kept, from, onScreen);
        }

        /// <summary>Whether a page's or the strip's picker stands open from a line's mark, to light that mark.</summary>
        private static bool PickingFrom(string kept, PickFrom from) => _picking == kept && _pickFrom == from;

        /// <summary>
        /// A page's or the strip's picker, over everything else: laid out under the mark that
        /// opened it, inside the room, and drawn; put away where that mark no longer stands or its
        /// line is no longer kept.
        /// </summary>
        private static void AnchoredPicker(PickFrom from, Rect room)
        {
            if (_picking == null || _pickFrom != from) return;
            var book = CompendiumKeeper.Book;
            if (book == null || Time.frameCount - _pickAnchorFrame > 2 || book.PickableMark(_picking) == null)
            {
                PutPickerAway();
                return;
            }
            LayPicker(book, Here(_pickAnchor), room);
            _pickTopOnScreen = OnScreen(_pickRect).y;
            DrawMarkPicker();
        }

        /// <summary>Where a page's or the strip's picker last stood on the screen, its top, for the self-test.</summary>
        private static float _pickTopOnScreen;

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
        private static bool Pickable(int row) =>
            row >= 0 && row < KeptRows.Count && !(_dungeonsOff && row == 0) && CompendiumKeeper.Book?.PickableMark(KeptRows[row]) != null;

        /// <summary>The line whose pickable mark the pointer rests on; -1 for none.</summary>
        private static int MarkPointedRow(Vector2 mouse)
        {
            var row = Mathf.FloorToInt((mouse.y - _keptRect.y - KeptTop) / KeptRowH);
            if (row < 0 || row >= _keptFits || !Pickable(row)) return -1;
            return MarkBox(_keptRect, _keptRect.y + KeptTop + row * KeptRowH).Contains(mouse) ? row : -1;
        }

        /// <summary>
        /// The list's picker under the line picked, or over it where the panel ends first; put
        /// away where its line is no longer listed or shown.
        /// </summary>
        private static void PickerLayout(CompendiumBook book, Rect room)
        {
            if (_pickFrom != PickFrom.List) return;
            _pickRect = Rect.zero;
            Choices.Clear();
            PickerWords.Clear();
            if (_picking == null) return;
            var row = KeptRows.IndexOf(_picking);
            if (row < 0 || row >= _keptFits || !Pickable(row))
            {
                PutPickerAway();
                return;
            }
            var lineTop = _keptRect.y + KeptTop + row * KeptRowH;
            LayPicker(book, new Rect(_keptRect.x + U(8f), lineTop, U(22f), KeptRowH), room);
        }

        /// <summary>The picker's place under what opened it (<see cref="MarkPickerPlace"/>), and its choices and words.</summary>
        private static void LayPicker(CompendiumBook book, Rect anchor, Rect room)
        {
            _pickRect = Rect.zero;
            Choices.Clear();
            PickerWords.Clear();
            if (!(book.MarkOf(_picking) is int current)) return;
            OthersMarks.Clear();
            foreach (var other in book.Kept) if (other != _picking && book.MarkOf(other) is int theirs) OthersMarks.Add(theirs);
            var colour = KeptMarks.ColourOf(current);
            var pictures = Enumerable.Range(0, KeptMarks.Pictures).Where(p => KeptMarkPictures.Game(p) != null).ToList();
            var ownName = KeptMarkPictures.OwnNameOf(_picking);
            float cell = PickCell, pad = U(10f), label = U(18f), heading = U(22f), note = U(22f);
            var height = pad + heading + (label + cell) * (pictures.Count > 0 ? 3 : 2) + label + (ownName != null ? cell : U(20f)) + note + pad;
            var width = Mathf.Min(U(340f), room.width);
            var (x, top) = MarkPickerPlace.Of(anchor.x, anchor.y, anchor.yMax, width, height, room.x, room.y, room.xMax, room.yMax, U(2f));
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
            // The own picture's cell, not a colour swatch, which holds an own mark too while the mark is one.
            var own = Choices.FirstOrDefault(c => !c.Colour && KeptMarks.IsOwn(c.Mark));
            if (Event.current.type == EventType.Repaint) _ownNamedBeside = own.Cell;
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
