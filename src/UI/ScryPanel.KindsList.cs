using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The kinds list on Valheim's map (<see cref="KindsList"/>): a small window of the kinds kept,
    /// under a heading for each category (<see cref="KeptCategories"/>), a family's under one chip, each
    /// with its mark and how many you saw and the server told of; a click picks a chip's kinds, or a
    /// heading's all, to stand out, the rest faded (<see cref="MapSpots.ShowAlone(IReadOnlyList{string})"/>);
    /// a heading's arrow folds it. An eye on each chip, each heading and the title hides those
    /// kinds' marks, or every mark, while they stay kept (<see cref="CompendiumBook.Hides"/>), grey
    /// while hidden; its foot hides the spots crossed off or the places the server told of, and
    /// shows everything again while anything is hidden.
    /// Its title row drags it, the arrow at its end folds it to the title, the corner at its foot
    /// sizes it, its rows scroll past those it is tall enough for; where it stands, how wide, how
    /// tall and whether folded are kept with the panel's place. Its presses are kept from the map under it.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>Where the list was left, from the map's top left, and how wide; none until moved; how many rows tall, none until sized; and whether it is folded.</summary>
        private static (float X, float Y, float Width)? _kindsKept;
        private static int? _kindsRows;

        /// <summary>Which categories stand folded on the list (<see cref="KeptCategories.Folded"/>).</summary>
        private static int _kindsCats;

        /// <summary>Which categories stand folded on the list, for the self-test.</summary>
        [ForTest]
        public static bool KindCategoryFoldedForTest(KeptCategory category) => KeptCategories.Folded(_kindsCats, category);

        /// <summary>Folds a category or opens it, as its arrow does, for the self-test.</summary>
        [ForTest]
        public static void FoldKindCategoryForTest(KeptCategory category) => FoldKindCategory(category);

        private static void FoldKindCategory(KeptCategory category)
        {
            _kindsCats = KeptCategories.Toggled(_kindsCats, category);
            SaveRects();
        }
        private static bool _kindsFolded;

        /// <summary>What a press on the list holds: nothing, its title moving it, or its corner sizing it; and where the press took hold.</summary>
        private enum KindsHold
        {
            None,
            Moving,
            Sizing,
        }

        private static KindsHold _kindsHold;
        private static Vector2 _kindsGrab;
        private static Vector2 _kindsScroll;

        /// <summary>Whether the kinds list stands folded to its title, for the self-test.</summary>
        [ForTest]
        public static bool KindsFoldedForTest => _kindsFolded;

        /// <summary>Folds the list or opens it, as its arrow does, for the self-test.</summary>
        [ForTest]
        public static void FoldKindsForTest(bool folded) => FoldKinds(folded);

        private static void FoldKinds(bool folded)
        {
            if (_kindsFolded == folded) return;
            _kindsFolded = folded;
            SaveRects();
        }

        /// <summary>The list's place as the panel's file kept it.</summary>
        private static void TakeKindsListPlace(PanelPlace place)
        {
            if (place.KindsList.HasValue) _kindsKept = place.KindsList;
            if (place.KindsRows.HasValue) _kindsRows = place.KindsRows;
            if (place.KindsCats.HasValue) _kindsCats = place.KindsCats.Value;
            if (place.KindsFolded.HasValue) _kindsFolded = place.KindsFolded.Value;
        }

        /// <summary>How bright what is hidden stands on the list: grey, as the game's legend greys an icon it hides.</summary>
        private const float HiddenShare = 0.45f;

        /// <summary>The list drawn over the map: where it was left, else at the top left from a height down.</summary>
        private static void KindsListGUI(Rect map, float top)
        {
            var e = Event.current;
            var start = (U(12f), top - map.y, map.width * KindsList.StartShare);
            var (x, y, width) = KindsList.Place(map.x, map.y, map.width, Screen.width, Screen.height, _kindsKept ?? start, U(1f));
            var chips = MapSpots.KindChips();
            var (pad, gap, chipH, titleH) = (U(KindsList.Pad), U(KindsList.Gap), U(KindsList.ChipHeight), U(KindsList.TitleHeight));
            var eyeW = U(20f);
            var widths = new float[chips.Count];
            for (var i = 0; i < chips.Count; i++) widths[i] = Skin.Width(Skin.Small, SpotWords.KindChip(chips[i].Name, chips[i].Seen, chips[i].Told, chips[i].Crossed)) + U(30f) + eyeW;
            // The foot: the spots crossed off and those the server told of, each where any stand or they are hidden, and the button showing all again.
            var book = CompendiumKeeper.Book;
            var (crossed, told) = (0, 0);
            var kindsOf = new string[chips.Count][];
            for (var i = 0; i < chips.Count; i++)
            {
                crossed += chips[i].Crossed;
                told += chips[i].Told;
                kindsOf[i] = chips[i].Kinds;
            }
            var crossedHidden = Settings.CrossedMarks == CrossedMarks.Hidden;
            var toldHidden = !Settings.MapSquares;
            var hidden = KindsList.HiddenCount(kindsOf, MapSpots.HiddenKind, book?.HidesAll ?? false, crossedHidden, toldHidden);
            var footed = !_kindsFolded && (crossed > 0 || crossedHidden || told > 0 || toldHidden || hidden > 0);
            var footH = footed ? chipH + gap : 0f;
            var bar = U(12f);
            var allowed = KindsList.RowsAllowed(_kindsRows, y + footH, Screen.height, U(1f));
            var groups = MapSpots.KindGroups();
            var laying = new (bool Folded, IReadOnlyList<float> Widths)[groups.Count];
            for (var g = 0; g < groups.Count; g++)
            {
                var these = new float[groups[g].Chips];
                System.Array.Copy(widths, groups[g].First, these, 0, these.Length);
                laying[g] = (KeptCategories.Folded(_kindsCats, groups[g].Category), these);
            }
            var indent = U(18f);
            var laid = KindsList.Laid(laying, width - pad * 2f, gap, indent);
            if (laid.Rows > allowed) laid = KindsList.Laid(laying, width - pad * 2f - bar, gap, indent);
            var rows = laid.Rows;
            var shown = KindsList.RowsShown(rows, allowed);
            var bodyH = _kindsFolded || rows == 0 ? 0f : shown * chipH + (shown - 1) * gap + pad * 2f;
            var box = new Rect(x, y, width, titleH + bodyH + footH);
            var title = new Rect(x, y, width, titleH);
            var fold = new Rect(title.xMax - titleH, y, titleH, titleH);
            var allEye = new Rect(fold.x - titleH + U(4f), y, titleH - U(4f), titleH);
            var grip = new Rect(box.xMax - U(14f), box.yMax - U(14f), U(14f), U(14f));
            HoldKindsList(e, map, box, title, fold, grip, width, footH, allEye);
            Skin.Box(box, Skin.Panel, Skin.Outline);
            GUI.Label(new Rect(x + pad, y, allEye.x - x - pad, titleH), SpotWords.KindsTitle(chips.Count, MapSpots.PickedCount, hidden), Skin.Small);
            string tip = null;
            var allHidden = book?.HidesAll ?? false;
            if (Eye(allEye, allHidden, e.mousePosition)) MapSpots.HideAllKinds(!allHidden);
            if (allEye.Contains(e.mousePosition)) tip = SpotWords.EyeTip(EyeOn.All, allHidden);
            if (GUI.Button(fold, GUIContent.none, GUIStyle.none)) FoldKinds(!_kindsFolded);
            Skin.Icon(new Rect(fold.x + U(6f), fold.y + U(6f), fold.width - U(12f), fold.height - U(12f)), _kindsFolded ? Skin.Down : Skin.Up, fold.Contains(e.mousePosition) ? Skin.Text : Skin.Dim);
            if (!_kindsFolded && rows > 0)
            {
                var body = new Rect(x + pad, y + titleH + pad, width - pad * 2f, bodyH - pad * 2f);
                var content = new Rect(0f, 0f, body.width - (rows > shown ? bar : 0f), rows * chipH + (rows - 1) * gap);
                _kindsScroll = GUI.BeginScrollView(body, _kindsScroll, content);
                var pointer = e.mousePosition;
                for (var g = 0; g < groups.Count; g++)
                {
                    var group = groups[g];
                    var heading = new Rect(0f, laid.Headings[g] * (chipH + gap), content.width, chipH);
                    tip = KindHeading(heading, group, pointer) ?? tip;
                    for (var c = 0; c < laid.Chips[g].Length; c++)
                    {
                        var i = group.First + c;
                        var at = new Rect(laid.Chips[g][c].X, laid.Chips[g][c].Row * (chipH + gap), widths[i], chipH);
                        tip = KindChip(at, chips[i], pointer) ?? tip;
                    }
                }
                GUI.EndScrollView();
            }
            if (footed) tip = KindsFoot(new Rect(x + pad, y + titleH + bodyH, width - pad * 2f - U(14f), chipH), e.mousePosition, crossed, told, hidden) ?? tip;
            if (!_kindsFolded && rows > 0) Skin.Icon(grip, Skin.Resize, _kindsHold == KindsHold.Sizing || grip.Contains(e.mousePosition) ? Skin.Text : Skin.Dim);
            if (tip != null)
            {
                var said = new GUIContent(tip);
                var size = Skin.Tip.CalcSize(said);
                GUI.Label(new Rect(Mathf.Min(e.mousePosition.x + U(14f), Screen.width - size.x - U(4f)), e.mousePosition.y + U(18f), size.x, size.y), said, Skin.Tip);
            }
            MapSpots.KindsStripDrawn(box);
            CountDrawn(PanelPart.MapKindsStrip);
        }

        /// <summary>An eye hiding what it stands for or showing it again: open while shown, struck through in the accent while hidden. True where clicked.</summary>
        private static bool Eye(Rect at, bool hidden, Vector2 pointer)
        {
            var clicked = GUI.Button(at, GUIContent.none, GUIStyle.none);
            var side = Mathf.Min(at.width, at.height) - U(8f);
            var icon = new Rect(at.center.x - side / 2f, at.center.y - side / 2f, side, side);
            Skin.Icon(icon, hidden ? Skin.EyeShut : Skin.Eye, hidden ? Skin.Accent : at.Contains(pointer) ? Skin.Text : Skin.Dim);
            return clicked;
        }

        /// <summary>
        /// The list's foot: the spots crossed off and the places the server told of, each a chip
        /// hiding them or showing them again, grey while hidden, and, while anything is hidden, the
        /// button showing everything again. The tip of what the pointer rests on; none.
        /// </summary>
        private static string KindsFoot(Rect row, Vector2 pointer, int crossed, int told, int hidden)
        {
            string tip = null;
            var x = row.x;
            var gap = U(KindsList.Gap);
            var crossedHidden = Settings.CrossedMarks == CrossedMarks.Hidden;
            if (crossed > 0 || crossedHidden)
            {
                var words = SpotWords.CrossedChip(crossed);
                var at = new Rect(x, row.y, Skin.Width(Skin.Small, words) + U(30f), row.height);
                if (GUI.Button(at, GUIContent.none, Skin.Chip)) Settings.CrossedMarks = crossedHidden ? CrossedMarks.Faded : CrossedMarks.Hidden;
                var cross = Minimap.instance != null ? SpotMarks.CrossPicture(Minimap.instance) : null;
                FootChip(at, cross != null ? cross.texture : null, words, crossedHidden);
                if (at.Contains(pointer)) tip = SpotWords.CrossedChipTip(crossedHidden);
                x = at.xMax + gap;
            }
            var toldHidden = !Settings.MapSquares;
            if (told > 0 || toldHidden)
            {
                var words = SpotWords.ToldChip(toldHidden ? (int?)null : told);
                var at = new Rect(x, row.y, Skin.Width(Skin.Small, words) + U(30f), row.height);
                if (GUI.Button(at, GUIContent.none, Skin.Chip)) Settings.MapSquares = toldHidden;
                FootChip(at, Skin.PointedRing, words, toldHidden);
                if (at.Contains(pointer)) tip = SpotWords.ToldChipTip(toldHidden);
                x = at.xMax + gap;
            }
            if (hidden > 0)
            {
                var w = Skin.Width(Skin.Small, SpotWords.ShowEverything) + U(16f);
                var at = new Rect(Mathf.Max(x, row.xMax - w), row.y, w, row.height);
                if (GUI.Button(at, SpotWords.ShowEverything, Skin.Chip)) MapSpots.ShowEverything();
            }
            return tip;
        }

        /// <summary>A foot chip's picture and words, grey while what it stands for is hidden.</summary>
        private static void FootChip(Rect at, Texture picture, string words, bool hidden)
        {
            var was = GUI.color;
            if (hidden) GUI.color = Skin.Alpha(was, HiddenShare);
            if (picture != null) GUI.DrawTexture(new Rect(at.x + U(6f), at.y + (at.height - U(14f)) / 2f, U(14f), U(14f)), picture);
            GUI.Label(new Rect(at.x + U(24f), at.y, at.width - U(26f), at.height), words, Skin.Small);
            GUI.color = was;
        }

        /// <summary>
        /// A category's heading: its arrow folding it, its shape, its name, how many stand under
        /// it, and its eye hiding every kind under it or showing them again; a click picks all its
        /// kinds or lets them go, lit while all stand out and outlined while some do, grey while all
        /// are hidden. The tip of what the pointer rests on, not its arrow; none.
        /// </summary>
        private static string KindHeading(Rect at, (KeptCategory Category, int Count, string[] Kinds, int First, int Chips) group, Vector2 pointer)
        {
            var picked = MapSpots.IsPicked(group.Kinds);
            var hidden = group.Kinds.Length > 0 && System.Array.TrueForAll(group.Kinds, MapSpots.HiddenKind);
            var arrow = new Rect(at.x, at.y, U(18f), at.height);
            var eye = new Rect(at.xMax - U(22f), at.y, U(22f), at.height);
            if (GUI.Button(arrow, GUIContent.none, GUIStyle.none)) FoldKindCategory(group.Category);
            else if (Eye(eye, hidden, pointer)) MapSpots.HideKinds(group.Kinds, !hidden);
            else if (GUI.Button(at, GUIContent.none, picked ? Skin.ChipOn : GUIStyle.none)) MapSpots.ShowAlone(group.Kinds);
            if (!picked && MapSpots.AnyPicked(group.Kinds)) Skin.Box(at, Skin.Alpha(Skin.Accent, 0f), Skin.Alpha(Skin.Accent, 0.55f));
            else if (!picked && at.Contains(pointer)) Skin.Box(at, Skin.Hover);
            var was = GUI.color;
            if (hidden) GUI.color = Skin.Alpha(was, HiddenShare);
            var folded = KeptCategories.Folded(_kindsCats, group.Category);
            Skin.Icon(new Rect(at.x + U(3f), at.y + (at.height - U(12f)) / 2f, U(12f), U(12f)), folded ? Skin.Down : Skin.Up, picked ? Skin.OnAccent : Skin.Dim);
            GUI.DrawTexture(new Rect(at.x + U(19f), at.y + (at.height - U(16f)) / 2f, U(16f), U(16f)), MapSpots.CategoryPicture(group.Category).texture);
            var style = picked ? Skin.Badge : Skin.Small;
            var name = SpotWords.CategoryName(group.Category);
            GUI.Label(new Rect(at.x + U(38f), at.y, Skin.Width(style, name) + U(2f), at.height), name, style);
            var many = Numbers.Count(group.Count);
            var manyW = Skin.Width(style, many) + U(2f);
            GUI.Label(new Rect(eye.x - manyW - U(2f), at.y, manyW, at.height), many, style);
            GUI.color = was;
            if (eye.Contains(pointer)) return SpotWords.EyeTip(EyeOn.Category, hidden);
            return at.Contains(pointer) && !arrow.Contains(pointer) ? SpotWords.KindHeadingTip(picked, MapSpots.PickedCount > 0) : null;
        }

        /// <summary>
        /// A chip drawn in its mark, lit while its kinds stand out, grey while they are hidden, its
        /// eye at its end hiding them or showing them again; a click picks them or lets them go, or
        /// shows them again where hidden. The tip of what the pointer rests on; none.
        /// </summary>
        private static string KindChip(Rect at, (string Name, int Seen, int Told, Sprite Picture, string[] Kinds, int Crossed) chip, Vector2 pointer)
        {
            var picked = MapSpots.IsPicked(chip.Kinds);
            var hidden = chip.Kinds.Length > 0 && System.Array.TrueForAll(chip.Kinds, MapSpots.HiddenKind);
            var eye = new Rect(at.xMax - U(22f), at.y, U(22f), at.height);
            if (Eye(eye, hidden, pointer)) MapSpots.HideKinds(chip.Kinds, !hidden);
            else if (GUI.Button(at, GUIContent.none, picked && !hidden ? Skin.ChipOn : Skin.Chip))
            {
                if (hidden) MapSpots.HideKinds(chip.Kinds, false);
                else MapSpots.ShowAlone(chip.Kinds);
            }
            var was = GUI.color;
            if (hidden) GUI.color = Skin.Alpha(was, HiddenShare);
            if (chip.Picture != null) GUI.DrawTexture(new Rect(at.x + U(6f), at.y + (at.height - U(16f)) / 2f, U(16f), U(16f)), chip.Picture.texture);
            GUI.Label(new Rect(at.x + U(24f), at.y, eye.x - at.x - U(24f), at.height), SpotWords.KindChip(chip.Name, chip.Seen, chip.Told, chip.Crossed), picked && !hidden ? Skin.Badge : Skin.Small);
            GUI.color = was;
            if (eye.Contains(pointer)) return SpotWords.EyeTip(EyeOn.Kind, hidden);
            if (!at.Contains(pointer)) return null;
            return hidden ? SpotWords.EyeTip(EyeOn.Kind, true) : SpotWords.KindChipTip(picked, MapSpots.PickedCount > 0);
        }

        /// <summary>A press on the title moving the list, but on its eye or its arrow, on its corner sizing it, held until let go, where it is kept.</summary>
        private static void HoldKindsList(Event e, Rect map, Rect box, Rect title, Rect fold, Rect grip, float width, float footH, Rect allEye)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (!_kindsFolded && grip.Contains(e.mousePosition))
                {
                    _kindsHold = KindsHold.Sizing;
                    _kindsGrab = new Vector2(box.xMax - e.mousePosition.x, box.yMax - e.mousePosition.y);
                    e.Use();
                }
                else if (title.Contains(e.mousePosition) && !fold.Contains(e.mousePosition) && !allEye.Contains(e.mousePosition))
                {
                    _kindsHold = KindsHold.Moving;
                    _kindsGrab = e.mousePosition - box.position;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && _kindsHold != KindsHold.None)
            {
                if (_kindsHold == KindsHold.Moving) _kindsKept = (e.mousePosition.x - _kindsGrab.x - map.x, e.mousePosition.y - _kindsGrab.y - map.y, width);
                else
                {
                    _kindsKept = (box.x - map.x, box.y - map.y, e.mousePosition.x + _kindsGrab.x - box.x);
                    // The foot stands under the rows, so the rows reach as far as the pointer less its height.
                    _kindsRows = KindsList.RowsAt(e.mousePosition.y + _kindsGrab.y - footH, box.y, U(1f));
                }
                e.Use();
            }
            else if (e.rawType == EventType.MouseUp && _kindsHold != KindsHold.None)
            {
                _kindsHold = KindsHold.None;
                SaveRects();
                e.Use();
            }
        }
    }
}
