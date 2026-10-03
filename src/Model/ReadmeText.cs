using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Scry
{
    /// <summary>
    /// A mod's readme (Markdown, often with some HTML) as plain text for its page: headings and
    /// paragraphs, list items as bullets, table rows as their cells, code as it is; pictures,
    /// badges, markup, link targets and comments left out. A long one stops at a paragraph.
    /// </summary>
    public static class ReadmeText
    {
        private enum Line { Blank, Paragraph, Heading, Item, Row, Code }

        private static readonly Regex Comment = new Regex("<!--.*?-->", RegexOptions.Singleline);
        private static readonly Regex Fence = new Regex(@"^\s*(```|~~~)");
        private static readonly Regex LinkTarget = new Regex(@"^\s*\[[^\]]+\]:\s*\S+");
        private static readonly Regex TableRule = new Regex(@"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$");
        private static readonly Regex Rule = new Regex(@"^\s*([-*_])(\s*\1){2,}\s*$");
        private static readonly Regex Heading = new Regex(@"^\s{0,3}#{1,6}\s+(.*?)\s*#*\s*$");
        private static readonly Regex Quote = new Regex(@"^\s*>\s?");
        private static readonly Regex Item = new Regex(@"^\s*([-*+]|\d+[.)])\s+");
        private static readonly Regex Code = new Regex("`([^`]*)`");
        private static readonly Regex Escaped = new Regex(@"\\([\\`*_{}\[\]()#+\-.!|>])");
        private static readonly Regex Picture = new Regex(@"!\[[^\]]*\]\([^)]*\)");
        private static readonly Regex Link = new Regex(@"\[([^\]]*)\]\([^)]*\)");
        private static readonly Regex Reference = new Regex(@"\[([^\]]+)\]\[[^\]]*\]");
        private static readonly Regex Tag = new Regex(@"</?[A-Za-z][^>]*>");
        private static readonly Regex Strong = new Regex(@"(\*\*|__)(.+?)\1");
        private static readonly Regex StarEmphasis = new Regex(@"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])");
        private static readonly Regex LineEmphasis = new Regex(@"(?<![\w_])_(?!\s)(.+?)(?<!\s)_(?![\w_])");
        private static readonly Regex Entity = new Regex(@"&(#[0-9]{1,6}|#[xX][0-9a-fA-F]{1,6}|[a-zA-Z]{2,8});");

        /// <summary>Marks taken out of reach of the markup while it is read, and put back after.</summary>
        private const int Shift = 0xE000;

        public static string Plain(string markdown, int limit)
        {
            if (string.IsNullOrEmpty(markdown)) return "";
            var source = Comment.Replace(markdown.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n'), "");

            var lines = new List<(Line Kind, string Text)>();
            var inCode = false;
            foreach (var raw in source.Split('\n'))
            {
                if (Fence.IsMatch(raw))
                {
                    inCode = !inCode;
                    continue;
                }
                if (inCode)
                {
                    lines.Add((Line.Code, Hide(raw.TrimEnd())));
                    continue;
                }
                if (LinkTarget.IsMatch(raw) || TableRule.IsMatch(raw)) continue;
                if (Rule.IsMatch(raw))
                {
                    lines.Add((Line.Blank, ""));
                    continue;
                }

                var line = raw;
                while (Quote.IsMatch(line)) line = Quote.Replace(line, "", 1);
                var kind = Line.Paragraph;
                var heading = Heading.Match(line);
                if (heading.Success)
                {
                    kind = Line.Heading;
                    line = heading.Groups[1].Value;
                }
                else if (Item.IsMatch(line))
                {
                    kind = Line.Item;
                    line = Item.Replace(line, "", 1);
                }
                else if (line.TrimStart().StartsWith("|", System.StringComparison.Ordinal))
                {
                    kind = Line.Row;
                }

                line = Inline(line);
                if (kind == Line.Row)
                {
                    var cells = new List<string>();
                    foreach (var cell in line.Split('|')) if (cell.Trim().Length > 0) cells.Add(cell.Trim());
                    line = string.Join(" \u00b7 ", cells.ToArray());
                }
                line = line.Trim();
                if (line.Length == 0)
                {
                    lines.Add((Line.Blank, ""));
                    continue;
                }
                if (kind == Line.Item) line = "\u2022 " + line;
                lines.Add((kind, line));
            }

            // Lines go together as the Markdown puts them: a paragraph's lines into one (and a
            // line straight after an item into the item), a heading on its own, items, rows and
            // code a line each, blank lines between blocks.
            var text = new StringBuilder();
            var last = Line.Blank;
            foreach (var (kind, line) in lines)
            {
                if (kind == Line.Blank)
                {
                    last = Line.Blank;
                    continue;
                }
                var goesOn = text.Length > 0 && kind == Line.Paragraph && (last == Line.Paragraph || last == Line.Item);
                if (goesOn) text.Append(' ');
                else if (text.Length > 0) text.Append(last == Line.Blank || kind == Line.Heading || kind != last ? "\n\n" : "\n");
                text.Append(line);
                if (!goesOn) last = kind;
            }

            // Bold and emphasis last, as they may span the lines of a paragraph or an item.
            var joined = Strong.Replace(text.ToString(), "$2");
            joined = StarEmphasis.Replace(joined, "$1");
            joined = LineEmphasis.Replace(joined, "$1");
            return Shorten(Restore(joined), limit);
        }

        private static string Inline(string line)
        {
            line = Escaped.Replace(line, m => ((char)(Shift + m.Groups[1].Value[0])).ToString());
            line = Code.Replace(line, m => Hide(m.Groups[1].Value));
            line = Picture.Replace(line, "");
            line = Link.Replace(line, "$1");
            line = Reference.Replace(line, "$1");
            line = Tag.Replace(line, "");
            return Entity.Replace(line, m => Hide(Unescape(m.Groups[1].Value)));
        }

        /// <summary>What an entity stands for: the few a readme uses by name, any by its number; one not known stays as written.</summary>
        private static string Unescape(string entity)
        {
            if (entity[0] == '#')
            {
                var hex = entity.Length > 1 && (entity[1] == 'x' || entity[1] == 'X');
                if ((hex ? Stored.TryHex(entity.Substring(2), out var code) : Stored.TryCount(entity.Substring(1), out code))
                    && code > 0 && code < 0x110000 && (code < 0xD800 || code > 0xDFFF))
                {
                    return char.ConvertFromUtf32(code);
                }
                return "&" + entity + ";";
            }
            switch (entity)
            {
                case "lt": return "<";
                case "gt": return ">";
                case "amp": return "&";
                case "quot": return "\"";
                case "apos": return "'";
                case "nbsp": return " ";
                default: return "&" + entity + ";";
            }
        }

        /// <summary>Code's marks out of the markup's reach.</summary>
        private static string Hide(string code)
        {
            var hidden = new StringBuilder(code.Length);
            foreach (var c in code) hidden.Append(c < 128 && !char.IsLetterOrDigit(c) && c != ' ' ? (char)(Shift + c) : c);
            return hidden.ToString();
        }

        private static string Restore(string text)
        {
            var restored = new StringBuilder(text.Length);
            foreach (var c in text) restored.Append(c >= Shift && c < Shift + 128 ? (char)(c - Shift) : c);
            return restored.ToString();
        }

        /// <summary>At most so many characters: up to the last paragraph that fits, else the last word, and an ellipsis.</summary>
        private static string Shorten(string text, int limit)
        {
            if (text.Length <= limit) return text;
            var head = text.Substring(0, limit);
            var paragraph = head.LastIndexOf("\n\n", System.StringComparison.Ordinal);
            if (paragraph > 0) return head.Substring(0, paragraph + 2) + "\u2026";
            var word = head.LastIndexOf(' ');
            return (word > 0 ? head.Substring(0, word + 1) : head) + "\u2026";
        }
    }
}
