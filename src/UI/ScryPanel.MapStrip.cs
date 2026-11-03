using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Show on map's strip over Valheim's large map (<see cref="MapSpots"/>): a notice as the
    /// panel's look it, on the panel's own colour, at the map's top left, naming the kind, how many
    /// the character saw and the nearest, with Hide; for a place of several kinds met, a button
    /// after it showing all of them, lit while they are. Drawn over everything while the map shows
    /// a kind, the panel open or not, as the resource monitor is.
    /// </summary>
    internal static partial class ScryPanel
    {
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
            if (line == null || map == null) return;
            var scale = Scale();
            Skin.Ensure(scale);
            var skin = GUI.skin;
            var was = SwapScale(scale);
            try
            {
                GUI.skin = Skin.Gui;
                var chipW = Skin.Width(Skin.Chip, SpotWords.Hide) + U(12f);
                var kinds = MapSpots.KindsOffered;
                var allW = kinds >= 2 ? Skin.Width(Skin.ChipOn, SpotWords.AllKindsChip(kinds)) + U(12f) + U(6f) : 0f;
                var width = Mathf.Min(map.Value.width - U(24f), U(14f) + Skin.Width(Skin.Small, line) + U(8f) + allW + chipW + U(8f));
                var rect = new Rect(map.Value.x + U(12f), map.Value.y + U(12f), width, U(32f));
                Skin.Box(rect, Skin.Panel, Skin.Outline);
                // The words and Hide in the strip's own way, the button for all kinds after Hide.
                var strip = new Rect(rect.x, rect.y, rect.width - allW, rect.height);
                if (NoticeStrip(strip, Skin.Accent, line, Skin.Small, false, SpotWords.Hide, false, false) == StripClick.Chip) MapSpots.Hide();
                if (kinds >= 2) AllKindsButton(new Rect(strip.xMax - U(4f), rect.y + (rect.height - U(22f)) / 2f, allW - U(6f), U(22f)), kinds);
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
