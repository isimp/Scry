using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Show on map's strip over Valheim's large map (<see cref="MapSpots"/>): a notice as the
    /// panel's look it, on the panel's own colour, at the map's top left, naming the kind, how many
    /// the character saw and the nearest, with Hide. Drawn over everything while the map shows a
    /// kind, the panel open or not, as the resource monitor is.
    /// </summary>
    internal static partial class ScryPanel
    {
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
                var width = Mathf.Min(map.Value.width - U(24f), U(14f) + Skin.Width(Skin.Small, line) + U(8f) + chipW + U(8f));
                var rect = new Rect(map.Value.x + U(12f), map.Value.y + U(12f), width, U(32f));
                Skin.Box(rect, Skin.Panel, Skin.Outline);
                if (NoticeStrip(rect, Skin.Accent, line, Skin.Small, false, SpotWords.Hide, false, false) == StripClick.Chip) MapSpots.Hide();
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
