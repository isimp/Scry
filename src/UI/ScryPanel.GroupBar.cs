using UnityEngine;

namespace Scry
{
    /// <summary>The switch above a kind's list choosing how it groups (<see cref="ListGrouping"/>).</summary>
    internal static partial class ScryPanel
    {
        /// <summary>Whether the list shows the switch: in a kind's tab offering more than its own grouping, not in the lists of what was learned or looked at lately.</summary>
        private static bool ShowsGroupBar(Explorer explorer) => Grouped(explorer) && !explorer.RecentOnly && !explorer.LearnedOnly && explorer.Lenses.Count > 1;

        /// <summary>
        /// "Group by" and a segment for each way the tab can group, the one in use lit, as the
        /// origin switch's segments are; a click groups the list so. What is left of the list's
        /// area under it.
        /// </summary>
        private static Rect GroupBar(Explorer explorer, Rect inner)
        {
            var barH = U(30f);
            var x = inner.x + U(10f);
            var y = inner.y + U(2f);
            var captionW = Skin.Width(Skin.FaintLabel, ListWords.GroupBy) + U(8f);
            GUI.Label(new Rect(x, y, captionW, U(24f)), ListWords.GroupBy, Skin.FaintLabel);
            x += captionW;
            var kind = explorer.KindFilter ?? Kind.Other;
            foreach (var lens in explorer.Lenses)
            {
                var name = ListGrouping.LensName(lens, kind);
                var width = Skin.Width(Skin.Segment, name) + U(6f);
                var at = new Rect(x, y, width, U(24f));
                var on = explorer.Lens == lens;
                if (GUI.Button(at, name, on ? Skin.SegmentOn : Skin.Segment) && !on)
                {
                    explorer.Lens = lens;
                    ListFromTop();
                }
                if (at.Contains(Event.current.mousePosition)) AskTip("group-by:" + name, ListWords.GroupByTip(name));
                x += width + U(4f);
            }
            CountDrawn(PanelPart.GroupBar);
            return new Rect(inner.x, inner.y + barH, inner.width, inner.height - barH);
        }
    }
}
