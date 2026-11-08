using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The kinds list on Valheim's map (<see cref="KindsList"/>): a small window of the kinds kept,
    /// under a heading for each category (<see cref="KeptCategories"/>), a family's under one chip, each
    /// with its mark and how many you saw and the server told of; a click picks a chip's kinds, or a
    /// heading's all, to stand out, the rest faded (<see cref="MapSpots.ShowAlone(IReadOnlyList{string})"/>);
    /// a heading's arrow folds it.
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

        /// <summary>The list drawn over the map: where it was left, else at the top left from a height down.</summary>
        private static void KindsListGUI(Rect map, float top)
        {
            var e = Event.current;
            var start = (U(12f), top - map.y, map.width * KindsList.StartShare);
            var (x, y, width) = KindsList.Place(map.x, map.y, map.width, Screen.width, Screen.height, _kindsKept ?? start, U(1f));
            var chips = MapSpots.KindChips();
            var (pad, gap, chipH, titleH) = (U(KindsList.Pad), U(KindsList.Gap), U(KindsList.ChipHeight), U(KindsList.TitleHeight));
            var widths = new float[chips.Count];
            for (var i = 0; i < chips.Count; i++) widths[i] = Skin.Width(Skin.Small, SpotWords.KindChip(chips[i].Name, chips[i].Seen, chips[i].Told)) + U(30f);
            var bar = U(12f);
            var allowed = KindsList.RowsAllowed(_kindsRows, y, Screen.height, U(1f));
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
            var box = new Rect(x, y, width, titleH + bodyH);
            var title = new Rect(x, y, width, titleH);
            var fold = new Rect(title.xMax - titleH, y, titleH, titleH);
            var grip = new Rect(box.xMax - U(14f), box.yMax - U(14f), U(14f), U(14f));
            HoldKindsList(e, map, box, title, fold, grip, width);
            Skin.Box(box, Skin.Panel, Skin.Outline);
            GUI.Label(new Rect(x + pad, y, width - titleH - pad, titleH), SpotWords.KindsTitle(chips.Count, MapSpots.PickedCount), Skin.Small);
            if (GUI.Button(fold, GUIContent.none, GUIStyle.none)) FoldKinds(!_kindsFolded);
            Skin.Icon(new Rect(fold.x + U(6f), fold.y + U(6f), fold.width - U(12f), fold.height - U(12f)), _kindsFolded ? Skin.Down : Skin.Up, fold.Contains(e.mousePosition) ? Skin.Text : Skin.Dim);
            string tip = null;
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
                    if (KindHeading(heading, group, pointer)) tip = SpotWords.KindHeadingTip(MapSpots.IsPicked(group.Kinds), MapSpots.PickedCount > 0);
                    for (var c = 0; c < laid.Chips[g].Length; c++)
                    {
                        var i = group.First + c;
                        var at = new Rect(laid.Chips[g][c].X, laid.Chips[g][c].Row * (chipH + gap), widths[i], chipH);
                        if (KindChip(at, chips[i], pointer)) tip = SpotWords.KindChipTip(MapSpots.IsPicked(chips[i].Kinds), MapSpots.PickedCount > 0);
                    }
                }
                GUI.EndScrollView();
                Skin.Icon(grip, Skin.Resize, _kindsHold == KindsHold.Sizing || grip.Contains(e.mousePosition) ? Skin.Text : Skin.Dim);
            }
            if (tip != null)
            {
                var said = new GUIContent(tip);
                var size = Skin.Tip.CalcSize(said);
                GUI.Label(new Rect(Mathf.Min(e.mousePosition.x + U(14f), Screen.width - size.x - U(4f)), e.mousePosition.y + U(18f), size.x, size.y), said, Skin.Tip);
            }
            MapSpots.KindsStripDrawn(box);
            CountDrawn(PanelPart.MapKindsStrip);
        }

        /// <summary>
        /// A category's heading: its arrow folding it, its shape, its name and how many stand under
        /// it; a click picks all its kinds or lets them go, lit while all stand out and outlined while
        /// some do. True where the pointer rests on it, not on its arrow.
        /// </summary>
        private static bool KindHeading(Rect at, (KeptCategory Category, int Count, string[] Kinds, int First, int Chips) group, Vector2 pointer)
        {
            var picked = MapSpots.IsPicked(group.Kinds);
            var arrow = new Rect(at.x, at.y, U(18f), at.height);
            if (GUI.Button(arrow, GUIContent.none, GUIStyle.none)) FoldKindCategory(group.Category);
            else if (GUI.Button(at, GUIContent.none, picked ? Skin.ChipOn : GUIStyle.none)) MapSpots.ShowAlone(group.Kinds);
            if (!picked && MapSpots.AnyPicked(group.Kinds)) Skin.Box(at, Skin.Alpha(Skin.Accent, 0f), Skin.Alpha(Skin.Accent, 0.55f));
            else if (!picked && at.Contains(pointer)) Skin.Box(at, Skin.Hover);
            var folded = KeptCategories.Folded(_kindsCats, group.Category);
            Skin.Icon(new Rect(at.x + U(3f), at.y + (at.height - U(12f)) / 2f, U(12f), U(12f)), folded ? Skin.Down : Skin.Up, picked ? Skin.OnAccent : Skin.Dim);
            GUI.DrawTexture(new Rect(at.x + U(19f), at.y + (at.height - U(16f)) / 2f, U(16f), U(16f)), MapSpots.CategoryPicture(group.Category).texture);
            var style = picked ? Skin.Badge : Skin.Small;
            var name = SpotWords.CategoryName(group.Category);
            GUI.Label(new Rect(at.x + U(38f), at.y, Skin.Width(style, name) + U(2f), at.height), name, style);
            var many = Numbers.Count(group.Count);
            var manyW = Skin.Width(style, many) + U(2f);
            GUI.Label(new Rect(at.xMax - manyW - U(6f), at.y, manyW, at.height), many, style);
            return at.Contains(pointer) && !arrow.Contains(pointer);
        }

        /// <summary>A chip drawn in its mark, lit while its kinds stand out; a click picks them or lets them go. True where the pointer rests on it.</summary>
        private static bool KindChip(Rect at, (string Name, int Seen, int Told, Sprite Picture, string[] Kinds) chip, Vector2 pointer)
        {
            var picked = MapSpots.IsPicked(chip.Kinds);
            if (GUI.Button(at, GUIContent.none, picked ? Skin.ChipOn : Skin.Chip)) MapSpots.ShowAlone(chip.Kinds);
            if (chip.Picture != null) GUI.DrawTexture(new Rect(at.x + U(6f), at.y + (at.height - U(16f)) / 2f, U(16f), U(16f)), chip.Picture.texture);
            GUI.Label(new Rect(at.x + U(24f), at.y, at.width - U(26f), at.height), SpotWords.KindChip(chip.Name, chip.Seen, chip.Told), picked ? Skin.Badge : Skin.Small);
            return at.Contains(pointer);
        }

        /// <summary>A press on the title moving the list, on its corner sizing it, held until let go, where it is kept.</summary>
        private static void HoldKindsList(Event e, Rect map, Rect box, Rect title, Rect fold, Rect grip, float width)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (!_kindsFolded && grip.Contains(e.mousePosition))
                {
                    _kindsHold = KindsHold.Sizing;
                    _kindsGrab = new Vector2(box.xMax - e.mousePosition.x, box.yMax - e.mousePosition.y);
                    e.Use();
                }
                else if (title.Contains(e.mousePosition) && !fold.Contains(e.mousePosition))
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
                    _kindsRows = KindsList.RowsAt(e.mousePosition.y + _kindsGrab.y, box.y, U(1f));
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
