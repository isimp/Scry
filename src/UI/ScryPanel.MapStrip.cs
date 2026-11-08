using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Show on map's strip over Valheim's large map (<see cref="MapSpots"/>): a notice as the
    /// panel's look it, on the panel's own colour, at the map's top left, naming the kind, how many
    /// the character saw and the nearest; under the words its buttons, Hide, for a place of several
    /// kinds met one showing all of them, lit while they are, and Keep on the map. No wider than
    /// its words or its buttons, and short of the map's top middle, where other mods put their
    /// own, the words scrolling where they do not fit. Drawn over everything while the map shows a
    /// kind, the panel open or not, as the resource monitor is.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>How much of the map's width the strip takes at most, so it stays short of the map's top middle.</summary>
        private const float StripShare = 0.38f;

        /// <summary>What the pointer rests on on the map: a ring round a dot, and its words beside the pointer, kept on the screen.</summary>
        private static void PointedWords()
        {
            var pointed = MapSpots.Pointed;
            if (pointed == null) return;
            var (words, ring) = pointed.Value;
            if (ring is Rect at)
            {
                var pad = U(5f);
                Skin.Icon(new Rect(at.x - pad, at.y - pad, at.width + pad * 2f, at.height + pad * 2f), Skin.PointedRing, Color.white);
            }
            var content = new GUIContent(words);
            // As wide as its longest line and a point more, so no line wraps at its own width, and as tall as it then stands.
            var width = Mathf.Ceil(Skin.Tip.CalcSize(content).x) + 1f;
            var size = new Vector2(width, Skin.Tip.CalcHeight(content, width));
            var pointer = ZInput.pointerPosition;
            var x = Mathf.Min(pointer.x + U(18f), Screen.width - size.x - U(4f));
            var y = Mathf.Min(Screen.height - pointer.y + U(18f), Screen.height - size.y - U(4f));
            GUI.Label(new Rect(x, y, size.x, size.y), content, Skin.Tip);
            CountDrawn(PanelPart.MapPointed);
        }

        /// <summary>The most rows of kinds the kinds strip takes; the kinds past them counted on its last chip.</summary>
        private const int KindsRows = 2;

        /// <summary>
        /// The kinds kept that stand on the map, at its top left from a height down: each a chip in
        /// its mark with how many stand, wrapping to a second row, the rest counted; a click shows
        /// that kind alone, lit, and again shows them all (<see cref="MapSpots.ShowAlone"/>); the
        /// pointer on a chip says what its click does. Its clicks are kept from the map under it.
        /// </summary>
        private static void KindsStrip(Rect map, float top)
        {
            var kinds = MapSpots.KindsStanding();
            var rowH = U(26f);
            var gap = U(4f);
            var pad = U(6f);
            var left = map.x + U(12f);
            var right = map.xMax - U(12f);
            var x = left + pad;
            var row = 0;
            var chips = new List<(Rect At, int Index)>();
            var more = 0;
            for (var i = 0; i < kinds.Count; i++)
            {
                var width = Skin.Width(Skin.Small, SpotWords.KindChip(kinds[i].Name, kinds[i].Seen, kinds[i].Told)) + U(30f);
                if (x + width > right - pad && x > left + pad)
                {
                    if (++row >= KindsRows)
                    {
                        more = kinds.Count - i;
                        break;
                    }
                    x = left + pad;
                }
                chips.Add((new Rect(x, top + pad + row * (rowH + gap), width, rowH), i));
                x += width + gap;
            }
            var rows = Mathf.Min(row + 1, KindsRows);
            var widest = chips.Count > 0 ? chips.Max(c => c.At.xMax) : left + pad;
            var moreText = more > 0 ? SpotWords.KindsMore(more) : null;
            var box = new Rect(left, top, Mathf.Min(right, widest + pad) - left, rows * rowH + (rows - 1) * gap + pad * 2f + (moreText != null ? U(18f) : 0f));
            Skin.Box(box, Skin.Panel, Skin.Outline);
            var pointer = Event.current.mousePosition;
            string tip = null;
            foreach (var (at, index) in chips)
            {
                var (kind, name, seen, told, picture) = kinds[index];
                var alone = MapSpots.IsPicked(kind);
                if (GUI.Button(at, GUIContent.none, alone ? Skin.ChipOn : Skin.Chip)) MapSpots.ShowAlone(kind);
                if (picture != null) GUI.DrawTexture(new Rect(at.x + U(6f), at.y + (at.height - U(16f)) / 2f, U(16f), U(16f)), picture.texture);
                GUI.Label(new Rect(at.x + U(24f), at.y, at.width - U(26f), at.height), SpotWords.KindChip(name, seen, told), alone ? Skin.Badge : Skin.Small);
                if (at.Contains(pointer)) tip = SpotWords.KindChipTip(alone, MapSpots.PickedCount > 0);
            }
            if (moreText != null) GUI.Label(new Rect(left + pad, box.yMax - pad - U(18f), box.width - pad * 2f, U(18f)), moreText, Skin.Small);
            if (tip != null)
            {
                var content = new GUIContent(tip);
                var size = Skin.Tip.CalcSize(content);
                GUI.Label(new Rect(Mathf.Min(pointer.x + U(14f), Screen.width - size.x - U(4f)), pointer.y + U(18f), size.x, size.y), content, Skin.Tip);
            }
            MapSpots.KindsStripDrawn(box);
            CountDrawn(PanelPart.MapKindsStrip);
        }

        /// <summary>The strip's button keeping the kind shown on the map every time it opens, lit while it is kept.</summary>
        private static void KeepButton(Rect at, string key)
        {
            var kept = CompendiumKeeper.Book?.Keeps(key) ?? false;
            if (GUI.Button(at, SpotWords.Keep, kept ? Skin.ChipOn : Skin.Chip)) KeepOnMap(key, !kept);
            CountDrawn(PanelPart.MapKeep);
        }

        /// <summary>The strip's button showing all of a place's kinds at once, lit while it does, or the page's alone again.</summary>
        private static void AllKindsButton(Rect at, int kinds)
        {
            var on = MapSpots.AllKinds;
            if (GUI.Button(at, SpotWords.AllKindsChip(kinds), on ? Skin.ChipOn : Skin.Chip)) MapSpots.ShowAllKinds(!on);
            CountDrawn(PanelPart.MapAllKinds);
        }

        public static void MapStripGUI()
        {
            var line = MapSpots.Strip;
            var map = MapSpots.MapOnScreen();
            if (map == null) return;
            var scale = Scale();
            Skin.Ensure(scale);
            var skin = GUI.skin;
            var was = SwapScale(scale);
            try
            {
                GUI.skin = Skin.Gui;
                // A press in the mark's picker, or one putting it away, before the strip's buttons take it.
                PickerClicks(Event.current, PickFrom.Strip);
                // The kinds strip under Show on map's strip, or in its place where none stands.
                if (MapSpots.KindsStripShown) KindsStrip(map.Value, map.Value.y + U(12f) + (line != null ? U(28f) * 2f + U(2f) + U(6f) : 0f));
                PointedWords();
                if (line == null) return;
                var hideW = Skin.Width(Skin.Chip, SpotWords.Hide) + U(12f);
                var kinds = MapSpots.KindsOffered;
                var allW = kinds >= 2 ? Skin.Width(Skin.ChipOn, SpotWords.AllKindsChip(kinds)) + U(12f) : 0f;
                var key = MapSpots.ShownKey;
                var keepW = key != null && CompendiumKeeper.Book != null ? Skin.Width(Skin.ChipOn, SpotWords.Keep) + U(12f) : 0f;
                // The kept kind's mark beside its Keep on the map, opening its picker.
                var pickable = key != null ? CompendiumKeeper.Book?.PickableMark(key) : null;
                var markW = keepW > 0f && pickable != null ? U(34f) : 0f;
                var gap = U(6f);
                var buttonsW = hideW + (allW > 0f ? gap + allW : 0f) + (keepW > 0f ? gap + keepW : 0f) + (markW > 0f ? gap + markW : 0f);
                var wordsW = Skin.Width(Skin.Small, line);
                // Both rows start past the kind's mark, where one is drawn.
                var lead = U(14f) + (MapSpots.ShownPicture != null ? U(18f) : 0f);
                var most = Mathf.Max(lead + buttonsW + U(10f), map.Value.width * StripShare);
                var width = Mathf.Min(map.Value.width - U(24f), most, lead + Mathf.Max(wordsW, buttonsW) + U(12f));
                var rowH = U(28f);
                var rect = new Rect(map.Value.x + U(12f), map.Value.y + U(12f), width, rowH * 2f + U(2f));
                // The notices' look over both rows: the panel under a tint of the accent, its bar at the left.
                Skin.Box(rect, Skin.Panel, Skin.Outline);
                Skin.Fill(rect, Skin.Alpha(Skin.Accent, 0.13f));
                Skin.Fill(new Rect(rect.x, rect.y, U(3f), rect.height), Skin.Accent);
                var left = rect.x + U(14f);
                // The kind's mark before its name, as it stands on the map.
                if (MapSpots.ShownPicture is Sprite mark)
                {
                    GUI.DrawTexture(new Rect(left - U(2f), rect.y + U(2f) + (rowH - U(16f)) / 2f, U(16f), U(16f)), mark.texture);
                    left += U(18f);
                }
                Ticker(new Rect(left, rect.y + U(2f), rect.xMax - left - U(10f), rowH), line, Skin.Small);
                var y = rect.y + rowH + (rowH - U(22f)) / 2f - U(2f);
                if (GUI.Button(new Rect(left, y, hideW, U(22f)), SpotWords.Hide, Skin.Chip)) MapSpots.Hide();
                var x = left + hideW + gap;
                if (allW > 0f)
                {
                    AllKindsButton(new Rect(x, y, allW, U(22f)), kinds);
                    x += allW + gap;
                }
                if (keepW > 0f) KeepButton(new Rect(x, y, keepW, U(22f)), key);
                if (markW > 0f && pickable is int picked)
                {
                    var piece = new Rect(x + keepW + gap, y, markW, U(22f));
                    var lit = PickingFrom(key, PickFrom.Strip) || piece.Contains(Event.current.mousePosition);
                    Skin.Box(piece, lit ? Skin.RaisedHover : Skin.Raised, lit ? Skin.Accent : Skin.Alpha(Skin.Text, 0.12f));
                    MarkPiece(piece, key, picked, PickFrom.Strip);
                    CountDrawn(PanelPart.MapKeepMark);
                }
                MapSpots.StripDrawn(rect);
                CountDrawn(PanelPart.MapStrip);
                // Its picker under the mark, inside the map, its clicks kept from the map too.
                AnchoredPicker(PickFrom.Strip, new Rect(map.Value.x + U(8f), map.Value.y + U(8f), map.Value.width - U(16f), map.Value.height - U(16f)));
                MapSpots.PickerDrawn(MarkPickerShownForTest && _pickFrom == PickFrom.Strip ? _pickRect : Rect.zero);
            }
            finally
            {
                GUI.skin = skin;
                SwapScale(was);
            }
        }
    }
}
