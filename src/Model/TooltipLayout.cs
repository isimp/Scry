using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a line of an item's tooltip is, as its card lays it out.</summary>
    internal enum TooltipPartKind
    {
        /// <summary>A line of the item's own description, which leads the tooltip.</summary>
        Description,

        /// <summary>A label beside its value: "Weight: 5.0".</summary>
        Row,

        /// <summary>A line ending in a colon, heading the lines under it: "Recycle Yield:".</summary>
        Heading,

        /// <summary>Any other line, told whole.</summary>
        Text,

        /// <summary>A blank line, between groups of lines.</summary>
        Divider,
    }

    /// <summary>One line of an item's tooltip as its card lays it out.</summary>
    internal readonly struct TooltipPart
    {
        public readonly TooltipPartKind Kind;

        /// <summary>A row's label; "" for the other kinds.</summary>
        public readonly string Label;

        /// <summary>A row's value, a heading without its colon, or the line itself.</summary>
        public readonly string Value;

        /// <summary>What a row's value has in brackets after it, told softer: "(Total:-10%)"; "" for none.</summary>
        public readonly string Extra;

        /// <summary>Whether a mod added the line; a divider is a mod's when it stands within a run of a mod's lines.</summary>
        public readonly bool FromMods;

        public TooltipPart(TooltipPartKind kind, string label, string value, string extra, bool fromMods)
        {
            Kind = kind;
            Label = label;
            Value = value;
            Extra = extra;
            FromMods = fromMods;
        }
    }

    /// <summary>
    /// An item's tooltip laid out as a card (<see cref="TooltipText"/> gives its lines): the item's
    /// description first, as the game puts it there; a line whose short label stands before a colon
    /// as that label beside its value, a bracketed aside after a figure told apart from it; a line
    /// ending in a colon as a heading; any other line whole; a blank line as a divider. The lines a
    /// mod added stay marked, and a divider within a run of them goes with them, so each run can
    /// be shown as one block.
    /// </summary>
    internal static class TooltipLayout
    {
        /// <summary>The longest a label is, in characters: a colon further in ends a sentence's clause, not a label.</summary>
        private const int LongestLabel = 40;

        /// <summary>The tooltip's lines as the card's parts, with the item's description to tell its lead by.</summary>
        public static List<TooltipPart> Parts(IReadOnlyList<(string Line, bool FromMods)> lines, string description)
        {
            var parts = new List<TooltipPart>();
            var first = 0;
            var lead = TooltipText.Lines(description);
            if (Leads(lines, lead))
            {
                for (; first < lead.Count; first++) parts.Add(new TooltipPart(TooltipPartKind.Description, "", lines[first].Line, "", lines[first].FromMods));
                while (first < lines.Count && lines[first].Line.Length == 0) first++;
            }
            for (var i = first; i < lines.Count; i++)
            {
                var (line, fromMods) = lines[i];
                if (line.Length > 0)
                {
                    parts.Add(Of(line, fromMods));
                    continue;
                }
                var withinMods = i > 0 && lines[i - 1].FromMods && i + 1 < lines.Count && lines[i + 1].FromMods;
                parts.Add(new TooltipPart(TooltipPartKind.Divider, "", "", "", withinMods));
            }
            return parts;
        }

        /// <summary>Each run of parts a mod added, first and last, a divider within a run counted in it.</summary>
        public static List<(int First, int Last)> ModRuns(IReadOnlyList<TooltipPart> parts)
        {
            var runs = new List<(int, int)>();
            for (var i = 0; i < parts.Count; i++)
            {
                if (!parts[i].FromMods) continue;
                var last = i;
                while (last + 1 < parts.Count && parts[last + 1].FromMods) last++;
                runs.Add((i, last));
                i = last;
            }
            return runs;
        }

        /// <summary>Whether the tooltip begins with every line of the description.</summary>
        private static bool Leads(IReadOnlyList<(string Line, bool FromMods)> lines, List<string> lead)
        {
            if (lines.Count < lead.Count) return false;
            for (var i = 0; i < lead.Count; i++)
            {
                if (lines[i].Line != lead[i]) return false;
            }
            return true;
        }

        private static TooltipPart Of(string line, bool fromMods)
        {
            var colon = line.IndexOf(':');
            if (colon == line.Length - 1 && colon > 0) return new TooltipPart(TooltipPartKind.Heading, "", line.Substring(0, colon).TrimEnd(), "", fromMods);
            if (colon > 0 && colon <= LongestLabel && !line.Substring(colon + 1).StartsWith("//", System.StringComparison.Ordinal))
            {
                var value = line.Substring(colon + 1).Trim();
                if (value.Length > 0)
                {
                    var (figure, extra) = Aside(value);
                    return new TooltipPart(TooltipPartKind.Row, line.Substring(0, colon).TrimEnd(), figure, extra, fromMods);
                }
            }
            return new TooltipPart(TooltipPartKind.Text, "", line, "", fromMods);
        }

        /// <summary>A value and the bracketed aside closing it, which must follow something and a space; the value whole and "" without one.</summary>
        private static (string Value, string Extra) Aside(string value)
        {
            if (value[value.Length - 1] != ')') return (value, "");
            var depth = 0;
            for (var i = value.Length - 1; i >= 0; i--)
            {
                if (value[i] == ')') depth++;
                else if (value[i] == '(' && --depth == 0)
                {
                    if (i < 2 || value[i - 1] != ' ') return (value, "");
                    return (value.Substring(0, i).TrimEnd(), value.Substring(i));
                }
            }
            return (value, "");
        }
    }
}
