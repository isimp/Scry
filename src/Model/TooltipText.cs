using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Scry
{
    /// <summary>
    /// An item's tooltip as plain lines, and which of them a mod added: a line whose label (what
    /// stands before its colon, or the whole line where it has none) the game's own tooltip lacks.
    /// A mod that only colours or adds to the game's own lines changes none of their labels, so
    /// those stay the game's.
    /// </summary>
    internal static class TooltipText
    {
        /// <summary>Rich text's tags, as the game's tooltips colour and size their figures.</summary>
        private static readonly Regex Tag = new Regex(@"</?(color|b|i|size|u|s|sup|sub|mark|alpha|font|nobr|br)(=[^>]*)?>", RegexOptions.IgnoreCase);

        /// <summary>A tooltip's lines without their markup, each trimmed, a run of blank lines as one and none at either end.</summary>
        public static List<string> Lines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            foreach (var raw in Tag.Replace(text, "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 && (lines.Count == 0 || lines[lines.Count - 1].Length == 0)) continue;
                lines.Add(line);
            }
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        /// <summary>The tooltip with its mods, line by line, each marked where a mod added it; nothing marked without the game's own.</summary>
        public static List<(string Line, bool FromMods)> Marked(string withMods, string gameOwn)
        {
            var own = gameOwn == null ? null : new HashSet<string>(Lines(gameOwn).Select(Label), StringComparer.Ordinal);
            return Lines(withMods).Select(l => (l, own != null && l.Length > 0 && !own.Contains(Label(l)))).ToList();
        }

        /// <summary>Whether a mod added any of these lines.</summary>
        public static bool AnyFromMods(IEnumerable<(string Line, bool FromMods)> lines) => lines.Any(l => l.FromMods);

        /// <summary>A line's label as plain words: what stands before its colon, else the whole line.</summary>
        private static string Label(string line)
        {
            var colon = line.IndexOf(':');
            return ConfigSections.Plain(colon > 0 ? line.Substring(0, colon) : line);
        }
    }

    /// <summary>How an item's tooltip is told on its page.</summary>
    internal static class TooltipWords
    {
        public const string Heading = "TOOLTIP IN THE GAME";

        public const string Quality = "Quality";

        /// <summary>The qualities the tooltip can be shown at, one to the item's most.</summary>
        public static List<string> Qualities(int most)
        {
            var names = new List<string>();
            for (var q = 1; q <= most; q++) names.Add(Numbers.Count(q));
            return names;
        }

        /// <summary>The legend under the card: what its tint means, leading into the mods hooking the tooltip where any are named.</summary>
        public static string Legend(bool modsNamed) => modsNamed ? "Tinted lines come from the mods hooking the tooltip:" : "Tinted lines come from mods hooking the tooltip.";
    }
}
