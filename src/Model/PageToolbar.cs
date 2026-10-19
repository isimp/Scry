using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Where the toolbar under a page's name puts its buttons: what the entry does as one group
    /// from the left, its pieces joined by a seam, and its sliders' icons as one group at the
    /// right end of the first line. Where the first line has no room left of the icons, the
    /// actions wrap as a group onto the lines under it, each from the left; the icons never move.
    /// An action wider than a line stands alone at a line's start. A faint caption may follow
    /// the actions a gap away (a status effect's "only the look"), wrapping as they do, or stand
    /// where they would with none. Places are from the left edge.
    /// </summary>
    internal static class PageToolbar
    {
        /// <summary>Where the icons begin on the first line, where the caption stands (its line -1 for none), and how many lines the toolbar takes: none with nothing to show.</summary>
        internal struct Laid
        {
            public float IconsX;
            public int Lines;
            public float CaptionX;
            public int CaptionLine;
        }

        /// <summary>
        /// Lays out actions of the widths given, and so many icons each as wide as <paramref name="icon"/>,
        /// across a width, a seam between joined pieces and a gap between the groups, then a
        /// caption as wide as <paramref name="caption"/> (none at 0); each action's place and line
        /// goes into <paramref name="into"/>, emptied first.
        /// </summary>
        public static Laid Lay(float width, IReadOnlyList<float> actions, int icons, float icon, float seam, float gap, List<(float X, int Line)> into, float caption = 0f)
        {
            into.Clear();
            var iconsW = icons > 0 ? icons * icon + (icons - 1) * seam : 0f;
            var room = width - (icons > 0 ? iconsW + gap : 0f);
            var x = 0f;
            var line = 0;
            foreach (var w in actions)
            {
                if (x > 0f && x + w > room)
                {
                    line++;
                    x = 0f;
                    room = width;
                }
                into.Add((x, line));
                x += w + seam;
            }
            var captionX = 0f;
            var captionLine = -1;
            if (caption > 0f)
            {
                captionX = x > 0f ? x - seam + gap : 0f;
                if (x > 0f && captionX + caption > room)
                {
                    line++;
                    captionX = 0f;
                }
                captionLine = line;
            }
            var lines = actions.Count == 0 && icons == 0 && caption <= 0f ? 0 : line + 1;
            return new Laid { IconsX = width - iconsW, Lines = lines, CaptionX = captionX, CaptionLine = captionLine };
        }
    }
}
