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
            var size = Skin.Tip.CalcSize(content);
            var pointer = ZInput.pointerPosition;
            var x = Mathf.Min(pointer.x + U(18f), Screen.width - size.x - U(4f));
            var y = Mathf.Min(Screen.height - pointer.y + U(18f), Screen.height - size.y - U(4f));
            GUI.Label(new Rect(x, y, size.x, size.y), content, Skin.Tip);
            CountDrawn(PanelPart.MapPointed);
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
                PointedWords();
                if (line == null) return;
                var hideW = Skin.Width(Skin.Chip, SpotWords.Hide) + U(12f);
                var kinds = MapSpots.KindsOffered;
                var allW = kinds >= 2 ? Skin.Width(Skin.ChipOn, SpotWords.AllKindsChip(kinds)) + U(12f) : 0f;
                var key = MapSpots.ShownKey;
                var keepW = key != null && CompendiumKeeper.Book != null ? Skin.Width(Skin.ChipOn, SpotWords.Keep) + U(12f) : 0f;
                var gap = U(6f);
                var buttonsW = hideW + (allW > 0f ? gap + allW : 0f) + (keepW > 0f ? gap + keepW : 0f);
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
                MapSpots.StripDrawn(rect);
                CountDrawn(PanelPart.MapStrip);
            }
            finally
            {
                GUI.skin = skin;
                SwapScale(was);
            }
        }
    }
}
