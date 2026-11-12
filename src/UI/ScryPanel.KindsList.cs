using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The kinds list on Valheim's map (<see cref="KindsList"/>): a small window of the kinds kept,
    /// under a heading for each category (<see cref="KeptCategories"/>), a family's in one row, each
    /// with its mark and how many you saw and the server told of, in as many columns as its width
    /// holds; a click picks a row's kinds, or a heading's all, to stand out, the rest faded
    /// (<see cref="MapSpots.ShowAlone(IReadOnlyList{string})"/>); a heading's arrow folds it. An eye,
    /// on the row or heading pointed at and on whatever is hidden, and one in the title hide those
    /// kinds' marks, or every mark, while they stay kept (<see cref="CompendiumBook.Hides"/>), grey
    /// while hidden; a pill in the title says how much is hidden and shows it all again; its foot's
    /// switches hide the spots crossed off or the places the server told of. Folded, it says under
    /// its title what stands out and what is hidden.
    /// Its title row drags it, the arrow at its end folds it, the corner at its foot
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
            var groups = MapSpots.KindGroups();
            var (pad, gap, lineH, titleH) = (U(KindsList.Pad), U(KindsList.Gap), U(KindsList.LineHeight), U(KindsList.TitleHeight));
            var book = CompendiumKeeper.Book;
            var kindsOf = new string[chips.Count][];
            for (var i = 0; i < chips.Count; i++) kindsOf[i] = chips[i].Kinds;
            var crossedHidden = Settings.CrossedMarks == CrossedMarks.Hidden;
            var toldHidden = !Settings.MapSquares;
            var allHidden = book?.HidesAll ?? false;
            var hidden = KindsList.HiddenCount(kindsOf, MapSpots.HiddenKind, allHidden, crossedHidden, toldHidden);
            var inner = width - pad * 2f;
            // Folded, a line or two in words under the title; open, the rows, then the foot's switches.
            var words = _kindsFolded ? FoldedWords(chips, allHidden, crossedHidden, toldHidden) : null;
            var wordH = U(18f);
            var foldedH = words != null ? words.Count * wordH + pad : 0f;
            var footInner = inner - U(14f);
            var foot = _kindsFolded ? new List<(FootPiece Piece, float Width)>() : FootPieces();
            var footFlow = KindsList.Flow(foot.ConvertAll(f => f.Width), footInner, U(8f));
            var footRows = KindsList.Rows(footFlow);
            var footLineH = U(22f);
            var footH = footRows > 0 ? footRows * footLineH + U(10f) : 0f;
            var bar = U(12f);
            var allowed = KindsList.RowsAllowed(_kindsRows, y + footH, Screen.height, U(1f));
            var laying = new (bool Folded, int Kinds)[groups.Count];
            for (var g = 0; g < groups.Count; g++) laying[g] = (KeptCategories.Folded(_kindsCats, groups[g].Category), groups[g].Chips);
            // As many columns as the width holds, fewer where the scroll bar takes its room.
            var colGap = U(10f);
            var least = U(KindsList.ColumnLeast);
            var columns = KindsList.Columns(inner, colGap, least);
            var laid = KindsList.LaidRows(laying, columns);
            var scrolls = laid.Lines > allowed;
            if (scrolls)
            {
                columns = KindsList.Columns(inner - bar, colGap, least);
                laid = KindsList.LaidRows(laying, columns);
            }
            var lines = laid.Lines;
            var shown = KindsList.RowsShown(lines, allowed);
            var bodyH = _kindsFolded || lines == 0 ? 0f : shown * lineH + (shown - 1) * gap + pad * 2f;
            var box = new Rect(x, y, width, titleH + (_kindsFolded ? foldedH : bodyH + footH));
            var title = new Rect(x, y, width, titleH);
            var fold = new Rect(title.xMax - titleH, y, titleH, titleH);
            var allEye = new Rect(fold.x - titleH + U(4f), y, titleH - U(4f), titleH);
            // While anything is hidden, a pill before the title's eye says how much and shows it all again.
            var pillWords = hidden > 0 ? SpotWords.HiddenPill(hidden) : null;
            var pillW = pillWords != null ? Skin.Width(Skin.Badge, pillWords) + U(14f) : 0f;
            var pill = new Rect(allEye.x - pillW - U(2f), y + (titleH - U(18f)) / 2f, pillW, U(18f));
            var grip = new Rect(box.xMax - U(14f), box.yMax - U(14f), U(14f), U(14f));
            HoldKindsList(e, map, box, title, fold, grip, width, footH, allEye, pill);
            Skin.Box(box, Skin.Panel, Skin.Outline);
            string tip = null;
            KindsTitle(new Rect(x + pad, y, (pillW > 0f ? pill.x : allEye.x) - x - pad - U(4f), titleH), chips.Count);
            if (pillWords != null)
            {
                if (GUI.Button(pill, GUIContent.none, GUIStyle.none)) MapSpots.ShowEverything();
                Skin.PillBox(pill, pill.Contains(e.mousePosition) ? Skin.Alpha(Skin.Accent, 0.85f) : Skin.Accent);
                GUI.Label(pill, pillWords, Skin.Badge);
                if (pill.Contains(e.mousePosition)) tip = SpotWords.HiddenPillTip;
            }
            if (Eye(allEye, allHidden, e.mousePosition)) MapSpots.HideAllKinds(!allHidden);
            if (allEye.Contains(e.mousePosition)) tip = SpotWords.EyeTip(EyeOn.All, allHidden);
            if (GUI.Button(fold, GUIContent.none, GUIStyle.none)) FoldKinds(!_kindsFolded);
            Skin.Icon(new Rect(fold.x + U(6f), fold.y + U(6f), fold.width - U(12f), fold.height - U(12f)), _kindsFolded ? Skin.Down : Skin.Up, fold.Contains(e.mousePosition) ? Skin.Text : Skin.Dim);
            if (words != null) for (var i = 0; i < words.Count; i++) FoldedLine(new Rect(x + pad + U(2f), y + titleH + i * wordH, inner - U(4f), wordH), words[i]);
            if (!_kindsFolded && lines > 0)
            {
                var body = new Rect(x + pad, y + titleH + pad, inner, bodyH - pad * 2f);
                var content = new Rect(0f, 0f, inner - (scrolls ? bar : 0f), lines * lineH + (lines - 1) * gap);
                var colW = (content.width - (columns - 1) * colGap) / columns;
                _kindsScroll = GUI.BeginScrollView(body, _kindsScroll, content);
                var pointer = e.mousePosition;
                for (var g = 0; g < groups.Count; g++)
                {
                    var group = groups[g];
                    var heading = new Rect(0f, laid.Headings[g] * (lineH + gap), content.width, lineH);
                    tip = KindHeading(heading, group, pointer) ?? tip;
                    for (var c = 0; c < laid.Rows[g].Length; c++)
                    {
                        var (column, line) = laid.Rows[g][c];
                        var at = new Rect(column * (colW + colGap), line * (lineH + gap), colW, lineH);
                        tip = KindRow(at, chips[group.First + c], pointer) ?? tip;
                    }
                }
                GUI.EndScrollView();
            }
            if (footRows > 0) tip = KindsFoot(new Rect(x, y + titleH + bodyH, width, footH), foot, footFlow, e.mousePosition) ?? tip;
            if (!_kindsFolded && lines > 0) Skin.Icon(grip, Skin.Resize, _kindsHold == KindsHold.Sizing || grip.Contains(e.mousePosition) ? Skin.Text : Skin.Dim);
            if (tip != null)
            {
                var said = new GUIContent(tip);
                var size = Skin.Tip.CalcSize(said);
                GUI.Label(new Rect(Mathf.Min(e.mousePosition.x + U(14f), Screen.width - size.x - U(4f)), e.mousePosition.y + U(18f), size.x, size.y), said, Skin.Tip);
            }
            MapSpots.KindsStripDrawn(box);
            CountDrawn(PanelPart.MapKindsStrip);
        }

        /// <summary>The title's name and how many kinds the list holds, fainter; the shorter name where the whole does not fit.</summary>
        private static void KindsTitle(Rect at, int kinds)
        {
            var count = Numbers.Count(kinds);
            var countW = Skin.Width(Skin.FaintLabel, count) + U(2f);
            var name = Skin.Width(Skin.Small, SpotWords.KindsTitle) + U(6f) + countW <= at.width ? SpotWords.KindsTitle : SpotWords.KindsTitleShort;
            var nameW = Mathf.Max(0f, Mathf.Min(at.width - countW - U(6f), Skin.Width(Skin.Small, name) + U(2f)));
            GUI.Label(new Rect(at.x, at.y, nameW, at.height), name, Skin.Small);
            GUI.Label(new Rect(at.x + nameW + U(6f), at.y, countW, at.height), count, Skin.FaintLabel);
        }

        /// <summary>What the folded list says: the kinds picked by name, or that every kind stands alike, and what is hidden.</summary>
        private static List<(string Label, string Words)> FoldedWords(IReadOnlyList<(string Name, int Seen, int Told, Sprite Picture, string[] Kinds, int Crossed)> chips, bool all, bool crossedHidden, bool toldHidden)
        {
            var picked = new List<string>();
            var gone = new List<string>();
            foreach (var chip in chips)
            {
                if (chip.Kinds.Length > 0 && System.Array.TrueForAll(chip.Kinds, MapSpots.HiddenKind)) gone.Add(chip.Name);
                else if (MapSpots.IsPicked(chip.Kinds)) picked.Add(chip.Name);
            }
            return KindsList.FoldedLines(picked, gone, all, crossedHidden, toldHidden);
        }

        /// <summary>A line of the folded list: its label, amber for what is picked and grey for what is hidden, then its words, cut short where long.</summary>
        private static void FoldedLine(Rect at, (string Label, string Words) line)
        {
            var x = at.x;
            var was = GUI.color;
            if (line.Label != null)
            {
                var labelW = Skin.Width(Skin.FaintLabel, line.Label) + U(6f);
                GUI.color = line.Label == SpotWords.FoldedPicked ? Skin.Accent : Skin.Alpha(Color.white, HiddenShare + 0.15f);
                GUI.Label(new Rect(x, at.y, labelW, at.height), line.Label, Skin.FaintLabel);
                GUI.color = was;
                x += labelW;
            }
            if (line.Label == SpotWords.FoldedHidden) GUI.color = Skin.Alpha(was, HiddenShare + 0.15f);
            GUI.Label(new Rect(x, at.y, at.xMax - x, at.height), line.Words, line.Label == null ? Skin.FaintLabel : Skin.Small);
            GUI.color = was;
        }

        /// <summary>An eye hiding what it stands for or showing it again: open while shown, struck through in the accent while hidden, half lit where some under it are. True where clicked.</summary>
        private static bool Eye(Rect at, bool hidden, Vector2 pointer, bool some = false)
        {
            var clicked = GUI.Button(at, GUIContent.none, GUIStyle.none);
            var side = Mathf.Min(at.width, at.height) - U(8f);
            var icon = new Rect(at.center.x - side / 2f, at.center.y - side / 2f, side, side);
            Skin.Icon(icon, hidden ? Skin.EyeShut : some ? Skin.EyeHalf : Skin.Eye, hidden || some ? Skin.Accent : at.Contains(pointer) ? Skin.Text : Skin.Dim);
            return clicked;
        }

        /// <summary>What stands on the list's foot: its label, and the switches for the spots crossed off and the places told.</summary>
        private enum FootPiece
        {
            Label,
            Crossed,
            Told,
        }

        /// <summary>The foot's pieces with their widths: a switch with its words after it.</summary>
        private static List<(FootPiece Piece, float Width)> FootPieces()
        {
            var switchW = U(20f) + U(4f);
            return new List<(FootPiece Piece, float Width)>
            {
                (FootPiece.Label, Skin.Width(Skin.FaintLabel, SpotWords.FootLabel) + U(2f)),
                (FootPiece.Crossed, switchW + Skin.Width(Skin.FaintLabel, SpotWords.FootCrossed) + U(2f)),
                (FootPiece.Told, switchW + Skin.Width(Skin.FaintLabel, SpotWords.FootTold) + U(2f)),
            };
        }

        /// <summary>
        /// The list's foot, set apart as settings: a sunken well inside the list's edge, its label and two
        /// small switches of the settings card's own look, lit while the spots crossed off or the
        /// places the server told of stand, which a click hides or shows again. The tip of what the pointer rests on; none.
        /// </summary>
        private static string KindsFoot(Rect strip, List<(FootPiece Piece, float Width)> foot, (float X, int Row)[] flow, Vector2 pointer)
        {
            // Inside the list's rounded edge, as a well of its own.
            Skin.Box(new Rect(strip.x + U(4f), strip.y + U(1f), strip.width - U(8f), strip.height - U(5f)), Skin.Sunken);
            string tip = null;
            var lineH = U(22f);
            var from = new Vector2(strip.x + U(KindsList.Pad) + U(3f), strip.y + U(3f));
            for (var i = 0; i < foot.Count; i++)
            {
                var (piece, w) = foot[i];
                var at = new Rect(from.x + flow[i].X, from.y + flow[i].Row * lineH, w, lineH);
                if (piece == FootPiece.Label)
                {
                    GUI.Label(at, SpotWords.FootLabel, Skin.FaintLabel);
                    continue;
                }
                var crossed = piece == FootPiece.Crossed;
                var on = crossed ? Settings.CrossedMarks != CrossedMarks.Hidden : Settings.MapSquares;
                if (GUI.Button(at, GUIContent.none, GUIStyle.none))
                {
                    if (crossed) Settings.CrossedMarks = on ? CrossedMarks.Hidden : CrossedMarks.Faded;
                    else Settings.MapSquares = !on;
                }
                var track = new Rect(at.x, at.y + (at.height - U(11f)) / 2f, U(20f), U(11f));
                Skin.PillBox(track, on ? Skin.Accent : Skin.Raised);
                Skin.Icon(new Rect(on ? track.xMax - U(9f) : track.x + U(2f), track.y + U(2f), U(7f), U(7f)), Skin.Circle, on ? Skin.OnAccent : Skin.Faint);
                var was = GUI.color;
                if (at.Contains(pointer)) GUI.color = Skin.Alpha(was, 1f);
                else if (!on) GUI.color = Skin.Alpha(was, HiddenShare + 0.25f);
                GUI.Label(new Rect(track.xMax + U(4f), at.y, at.xMax - track.xMax - U(4f), at.height), crossed ? SpotWords.FootCrossed : SpotWords.FootTold, Skin.FaintLabel);
                GUI.color = was;
                if (at.Contains(pointer)) tip = crossed ? SpotWords.CrossedChipTip(!on) : SpotWords.ToldChipTip(!on);
            }
            return tip;
        }

        /// <summary>
        /// A category's heading: its arrow folding it, its shape, its name, folded how many under it
        /// are hidden, how many stand under it, and, pointed at or with any under it hidden, its eye
        /// hiding every kind under it or showing them again, half lit while some are; a click picks all
        /// its kinds or lets them go, lit while all stand out and outlined while some do, grey while
        /// all are hidden. The tip of what the pointer rests on, not its arrow; none.
        /// </summary>
        private static string KindHeading(Rect at, (KeptCategory Category, int Count, string[] Kinds, int First, int Chips) group, Vector2 pointer)
        {
            var picked = MapSpots.IsPicked(group.Kinds);
            var hiddenUnder = 0;
            foreach (var kind in group.Kinds) if (MapSpots.HiddenKind(kind)) hiddenUnder++;
            var hidden = group.Kinds.Length > 0 && hiddenUnder == group.Kinds.Length;
            var some = !hidden && hiddenUnder > 0;
            var hover = at.Contains(pointer);
            var arrow = new Rect(at.x, at.y, U(18f), at.height);
            var eye = new Rect(at.xMax - U(22f), at.y, U(22f), at.height);
            var eyeShown = hover || hidden || some;
            if (Event.current.type == EventType.Repaint)
            {
                if (picked) Skin.ChipOn.Draw(at, GUIContent.none, false, false, false, false);
                else if (MapSpots.AnyPicked(group.Kinds)) Skin.Box(at, Skin.Alpha(Skin.Accent, 0f), Skin.Alpha(Skin.Accent, 0.55f));
                else if (hover) Skin.Box(at, Skin.Hover);
            }
            if (GUI.Button(arrow, GUIContent.none, GUIStyle.none)) FoldKindCategory(group.Category);
            else if (eyeShown && Eye(eye, hidden, pointer, some)) MapSpots.HideKinds(group.Kinds, !hidden);
            else if (GUI.Button(at, GUIContent.none, GUIStyle.none))
            {
                if (hidden) MapSpots.HideKinds(group.Kinds, false);
                else MapSpots.ShowAlone(group.Kinds);
            }
            var was = GUI.color;
            if (hidden) GUI.color = Skin.Alpha(was, HiddenShare);
            var folded = KeptCategories.Folded(_kindsCats, group.Category);
            Skin.Icon(new Rect(at.x + U(3f), at.y + (at.height - U(12f)) / 2f, U(12f), U(12f)), folded ? Skin.Down : Skin.Up, picked ? Skin.OnAccent : Skin.Dim);
            GUI.DrawTexture(new Rect(at.x + U(19f), at.y + (at.height - U(16f)) / 2f, U(16f), U(16f)), MapSpots.CategoryPicture(group.Category).texture);
            var style = picked ? Skin.Badge : Skin.Small;
            var name = SpotWords.CategoryName(group.Category);
            GUI.Label(new Rect(at.x + U(38f), at.y, Skin.Width(style, name) + U(2f), at.height), name, style);
            var many = Numbers.Count(group.Count);
            var manyStyle = picked ? Skin.Badge : Skin.FaintLabel;
            var manyW = Skin.Width(manyStyle, many) + U(2f);
            var manyAt = new Rect(eye.x - manyW - U(2f), at.y, manyW, at.height);
            GUI.Label(manyAt, many, manyStyle);
            // Folded, how many under it are hidden, as none of them shows.
            if (folded && hiddenUnder > 0 && !hidden)
            {
                var badge = SpotWords.HiddenBadge(hiddenUnder);
                var badgeW = Skin.Width(Skin.Badge, badge) + U(10f);
                var badgeAt = new Rect(manyAt.x - badgeW - U(6f), at.y + (at.height - U(15f)) / 2f, badgeW, U(15f));
                Skin.Box(badgeAt, Skin.Alpha(Skin.Accent, 0f), Skin.Alpha(Skin.Accent, 0.5f));
                GUI.color = Skin.Accent;
                GUI.Label(badgeAt, badge, Skin.SmallCenter);
            }
            GUI.color = was;
            if (eyeShown && eye.Contains(pointer)) return SpotWords.EyeTip(EyeOn.Category, hidden);
            return hover && !arrow.Contains(pointer) ? (hidden ? SpotWords.EyeTip(EyeOn.Category, true) : SpotWords.KindHeadingTip(picked, MapSpots.PickedCount > 0)) : null;
        }

        /// <summary>
        /// A kind's row: its mark, its name cut short where long, how many the server told of beside
        /// a hollow ring and how many you saw, right-aligned, and, pointed at or while hidden, its
        /// eye hiding it or showing it again; a bar at its start while it stands out, grey while
        /// hidden. A click picks it or lets it go, or shows it again where hidden. The tip of what the pointer rests on; none.
        /// </summary>
        private static string KindRow(Rect at, (string Name, int Seen, int Told, Sprite Picture, string[] Kinds, int Crossed) chip, Vector2 pointer)
        {
            var picked = MapSpots.IsPicked(chip.Kinds);
            var hidden = chip.Kinds.Length > 0 && System.Array.TrueForAll(chip.Kinds, MapSpots.HiddenKind);
            var hover = at.Contains(pointer);
            var eye = new Rect(at.xMax - U(22f), at.y, U(22f), at.height);
            if (Event.current.type == EventType.Repaint && hover) Skin.Box(at, Skin.Hover);
            if ((hover || hidden) && Eye(eye, hidden, pointer)) MapSpots.HideKinds(chip.Kinds, !hidden);
            else if (GUI.Button(at, GUIContent.none, GUIStyle.none))
            {
                if (hidden) MapSpots.HideKinds(chip.Kinds, false);
                else MapSpots.ShowAlone(chip.Kinds);
            }
            if (picked && !hidden) Skin.Fill(new Rect(at.x + U(4f), at.y + U(5f), U(3f), at.height - U(10f)), Skin.Accent);
            var was = GUI.color;
            if (hidden) GUI.color = Skin.Alpha(was, HiddenShare);
            if (chip.Picture != null) GUI.DrawTexture(new Rect(at.x + U(12f), at.y + (at.height - U(15f)) / 2f, U(15f), U(15f)), chip.Picture.texture);
            // The counts right-aligned before the eye: those told of beside a hollow ring, then those seen.
            var right = eye.x - U(2f);
            var seen = Numbers.Count(chip.Seen);
            var seenW = Skin.Width(Skin.FaintLabel, seen) + U(2f);
            right -= seenW;
            if (chip.Seen > 0 || chip.Told == 0) GUI.Label(new Rect(right, at.y, seenW, at.height), seen, Skin.FaintLabel);
            if (chip.Told > 0)
            {
                var told = Numbers.Count(chip.Told);
                var toldW = Skin.Width(Skin.FaintLabel, told) + U(2f);
                right -= toldW + U(6f);
                GUI.Label(new Rect(right, at.y, toldW, at.height), told, Skin.FaintLabel);
                right -= U(11f);
                Skin.Icon(new Rect(right, at.y + (at.height - U(10f)) / 2f, U(10f), U(10f)), Skin.PointedRing, Skin.Faint);
            }
            var nameX = at.x + U(32f);
            GUI.Label(new Rect(nameX, at.y, Mathf.Max(0f, right - nameX - U(6f)), at.height), chip.Name, Skin.Small);
            GUI.color = was;
            if ((hover || hidden) && eye.Contains(pointer)) return SpotWords.EyeTip(EyeOn.Kind, hidden);
            return hover ? SpotWords.KindRowTip(chip.Name, chip.Seen, chip.Told, chip.Crossed, picked, MapSpots.PickedCount > 0, hidden) : null;
        }

        /// <summary>A press on the title moving the list, but on its pill, its eye or its arrow, on its corner sizing it, held until let go, where it is kept.</summary>
        private static void HoldKindsList(Event e, Rect map, Rect box, Rect title, Rect fold, Rect grip, float width, float footH, Rect allEye, Rect pill)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (!_kindsFolded && grip.Contains(e.mousePosition))
                {
                    _kindsHold = KindsHold.Sizing;
                    _kindsGrab = new Vector2(box.xMax - e.mousePosition.x, box.yMax - e.mousePosition.y);
                    e.Use();
                }
                else if (title.Contains(e.mousePosition) && !fold.Contains(e.mousePosition) && !allEye.Contains(e.mousePosition) && !pill.Contains(e.mousePosition))
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
