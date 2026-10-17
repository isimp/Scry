using System;
using System.Collections.Generic;
using System.Text;

namespace Scry
{
    /// <summary>
    /// The game's language tables, read as <c>Localization.LoadCSV</c> reads them for the language
    /// played in, its rows split by <c>Localization.DoQuoteLineSplit</c>, but in every language
    /// they have at once. A table's first line names the
    /// languages, from its second column on; each row after it is a word, its key first
    /// ("biome_meadows") and its text in each language under that language's column. A cell in
    /// quotes may hold commas, line breaks and doubled quotes; an empty cell is the second
    /// column's text, the English; a row short of a language has no text for it. The game loads its tables in turn, so a later table's text
    /// of a word replaces an earlier one's in the languages the later one has.
    /// </summary>
    internal static class LanguageTable
    {
        /// <summary>The text of each word asked for, by its key and then by the language, in every language the tables have it in.</summary>
        public static Dictionary<string, Dictionary<string, string>> Read(IEnumerable<string> tables, ICollection<string> keys)
        {
            var words = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var wanted = new HashSet<string>(keys, StringComparer.Ordinal);
            foreach (var table in tables)
            {
                if (!string.IsNullOrEmpty(table)) ReadOne(table, wanted, words);
            }
            return words;
        }

        private static readonly char[] LineBreaks = { '\r', '\n' };

        private static void ReadOne(string table, HashSet<string> wanted, Dictionary<string, Dictionary<string, string>> words)
        {
            // The first line split at each comma, as the game splits it, each name without its quotes.
            var lineEnd = table.IndexOfAny(LineBreaks);
            var header = (lineEnd < 0 ? table : table.Substring(0, lineEnd)).Split(',');
            var languages = new List<(string Language, int Column)>();
            for (var column = 1; column < header.Length; column++)
            {
                var language = Unquoted(header[column]);
                if (!languages.Exists(l => l.Language == language)) languages.Add((language, column));
            }
            if (lineEnd < 0) return;

            // Each row's cells, kept only once its key is one asked for; quotes are followed all the
            // same. A line break left of the first line's ends an empty row, which names no word.
            var cells = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            var keeping = true;
            for (var at = lineEnd + 1; at <= table.Length; at++)
            {
                if (at == table.Length)
                {
                    EndRow(ref keeping, cells, cell, languages, wanted, words);
                    break;
                }
                var c = table[at];
                if (c == '"')
                {
                    if (quoted && at + 1 < table.Length && table[at + 1] == '"')
                    {
                        at++;
                        if (keeping) cell.Append('"');
                    }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted)
                {
                    if (!keeping) continue;
                    cells.Add(cell.ToString());
                    cell.Length = 0;
                    if (cells.Count == 1) keeping = wanted.Contains(cells[0]);
                }
                else if (c == '\n' && !quoted) EndRow(ref keeping, cells, cell, languages, wanted, words);
                else if (keeping) cell.Append(c);
            }
        }

        private static void EndRow(ref bool keeping, List<string> cells, StringBuilder cell, List<(string Language, int Column)> languages,
            HashSet<string> wanted, Dictionary<string, Dictionary<string, string>> words)
        {
            if (keeping)
            {
                cells.Add(cell.ToString());
                var key = cells[0];
                if (wanted.Contains(key))
                {
                    if (!words.TryGetValue(key, out var texts)) words[key] = texts = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var (language, column) in languages)
                    {
                        if (cells.Count <= column) continue;
                        var text = cells[column].Trim();
                        texts[language] = text.Length > 0 ? text : cells[1];
                    }
                }
            }
            cells.Clear();
            cell.Length = 0;
            keeping = true;
        }

        /// <summary>A name without the one quote before and the one after it, as the game reads the languages' names.</summary>
        private static string Unquoted(string name)
        {
            if (name.StartsWith("\"", StringComparison.Ordinal))
            {
                name = name.Substring(1);
                if (name.EndsWith("\"", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 1);
            }
            return name;
        }
    }
}
