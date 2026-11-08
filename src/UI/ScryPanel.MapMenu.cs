using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The small menu a right click opens on a mark on Valheim's large map (<see cref="MapMenu"/>),
    /// at the pointer and inside the screen, named for the mark; a press outside it puts it away.
    /// What each line does is told by whoever opened it.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The menu drawn over everything else on the map, while it stands open.</summary>
        public static void MapMenuGUI()
        {
            if (!MapSpots.MenuOpen) return;
            var scale = Scale();
            Skin.Ensure(scale);
            var skin = GUI.skin;
            var was = SwapScale(scale);
            try
            {
                GUI.skin = Skin.Gui;
                DrawMapMenu(Event.current);
            }
            finally
            {
                GUI.skin = skin;
                SwapScale(was);
            }
        }

        private static void DrawMapMenu(Event e)
        {
            var items = MapSpots.MenuItems;
            float pad = U(6f), rowH = U(26f), titleH = U(24f);
            var width = Skin.Width(Skin.DimLabel, MapSpots.MenuTitle) + pad * 2f;
            foreach (var (words, _) in items) width = Mathf.Max(width, Skin.Width(Skin.Label, words) + U(24f));
            var height = pad * 2f + titleH + rowH * items.Count;
            var box = new Rect(Mathf.Min(MapSpots.MenuAt.x, Screen.width - width), Mathf.Min(MapSpots.MenuAt.y, Screen.height - height), width, height);
            MapSpots.MenuDrawn(box);
            if (e.type == EventType.MouseDown && !box.Contains(e.mousePosition))
            {
                MapSpots.CloseMenu();
                return;
            }
            Skin.Box(box, Skin.Panel, Skin.Outline);
            GUI.Label(new Rect(box.x + pad, box.y + pad, width - pad * 2f, titleH), MapSpots.MenuTitle, Skin.DimLabel);
            System.Action chosen = null;
            for (var i = 0; i < items.Count; i++)
            {
                var row = new Rect(box.x + U(4f), box.y + pad + titleH + i * rowH, width - U(8f), rowH);
                if (row.Contains(e.mousePosition)) Skin.Box(row, Skin.Hover);
                GUI.Label(new Rect(row.x + U(8f), row.y, row.width - U(12f), rowH), items[i].Words, Skin.Label);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) chosen = items[i].Act;
            }
            if (chosen == null) return;
            MapSpots.CloseMenu();
            chosen();
        }
    }
}
