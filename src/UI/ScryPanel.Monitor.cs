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
        /// <summary>How many times the monitor has been drawn, for the self-test.</summary>
        public static int MonitorsDrawn { get; private set; }

        public static void MonitorGUI()
        {
            Monitor.Refresh();
            var scale = Scale();
            Skin.Ensure(scale);
            var was = _s;
            var skin = GUI.skin;
            try
            {
                _s = scale;
                GUI.skin = Skin.Gui;
                var lineH = U(17f);
                var graphH = U(36f);
                var w = U(430f);
                var h = U(30f) + graphH + U(8f) + Monitor.Lines.Count * lineH + U(10f);
                var box = new Rect(Screen.width - w - U(12f), Screen.height * 0.32f, w, h);
                Skin.Box(box, new Color(Skin.Backdrop.r, Skin.Backdrop.g, Skin.Backdrop.b, 0.86f), Skin.Outline);
                GUI.Label(new Rect(box.x + U(10f), box.y + U(6f), w - U(20f), U(20f)), "Scry resource monitor", Skin.Label);

                // The graph, a faint line at its top telling what its top stands for.
                var graph = new Rect(box.x + U(10f), box.y + U(30f), w - U(20f), graphH);
                Skin.Fill(graph, new Color(1f, 1f, 1f, 0.05f));
                var columns = Monitor.Graph;
                if (columns.Length > 0)
                {
                    var cw = graph.width / columns.Length;
                    for (var i = 0; i < columns.Length; i++)
                    {
                        var share = (float)(columns[i] / Monitor.GraphTop);
                        if (share <= 0f) continue;
                        var ch = Mathf.Max(U(1f), graph.height * share);
                        Skin.Fill(new Rect(graph.x + i * cw, graph.yMax - ch, Mathf.Max(U(1f), cw - U(1f)), ch), new Color(Skin.Accent.r, Skin.Accent.g, Skin.Accent.b, 0.8f));
                    }
                }
                GUI.Label(new Rect(graph.xMax - U(120f), graph.y, U(118f), U(16f)), MonitorWords.Ms(Monitor.GraphTop) + " ms", Skin.FaintLabel);

                var y = graph.yMax + U(8f);
                foreach (var line in Monitor.Lines)
                {
                    GUI.Label(new Rect(box.x + U(10f), y, w - U(20f), lineH), line, Skin.DimLabel);
                    y += lineH;
                }
                MonitorsDrawn++;
            }
            finally
            {
                GUI.skin = skin;
                _s = was;
            }
        }
    }
}
