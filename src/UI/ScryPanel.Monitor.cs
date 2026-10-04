using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The resource monitor's box (<see cref="Monitor"/>): at the screen's right edge, under
    /// the minimap's height, drawn over everything while its setting is on, the panel open or
    /// not. A title, its lines, and a graph of Scry's own time each frame over the last ten
    /// seconds, a column for each sixth of a second at its most, scaled to the most there.
    /// </summary>
    internal static partial class ScryPanel
    {

        public static void MonitorGUI(IReadOnlyList<string> lines, double[] columns, double graphTop)
        {
            var scale = Scale();
            Skin.Ensure(scale);
            var skin = GUI.skin;
            var was = SwapScale(scale);
            try
            {
                GUI.skin = Skin.Gui;
                var lineH = U(17f);
                var graphH = U(36f);
                var w = U(430f);
                var h = U(30f) + graphH + U(8f) + lines.Count * lineH + U(10f);
                var box = new Rect(Screen.width - w - U(12f), Screen.height * 0.32f, w, h);
                Skin.Box(box, Skin.Alpha(Skin.Backdrop, 0.86f), Skin.Outline);
                GUI.Label(new Rect(box.x + U(10f), box.y + U(6f), w - U(20f), U(20f)), "Scry resource monitor", Skin.Label);

                // The graph, a faint line at its top telling what its top stands for.
                var graph = new Rect(box.x + U(10f), box.y + U(30f), w - U(20f), graphH);
                Skin.Fill(graph, Skin.Light(0.05f));
                if (columns.Length > 0)
                {
                    var cw = graph.width / columns.Length;
                    for (var i = 0; i < columns.Length; i++)
                    {
                        var share = (float)(columns[i] / graphTop);
                        if (share <= 0f) continue;
                        var ch = Mathf.Max(U(1f), graph.height * share);
                        Skin.Fill(new Rect(graph.x + i * cw, graph.yMax - ch, Mathf.Max(U(1f), cw - U(1f)), ch), Skin.Alpha(Skin.Accent, 0.8f));
                    }
                }
                GUI.Label(new Rect(graph.xMax - U(120f), graph.y, U(118f), U(16f)), MonitorWords.Milliseconds(graphTop), Skin.FaintLabel);

                var y = graph.yMax + U(8f);
                foreach (var line in lines)
                {
                    GUI.Label(new Rect(box.x + U(10f), y, w - U(20f), lineH), line, Skin.DimLabel);
                    y += lineH;
                }
                CountDrawn(PanelPart.Monitor);
            }
            finally
            {
                GUI.skin = skin;
                SwapScale(was);
            }
        }
    }
}
