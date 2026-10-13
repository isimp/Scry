using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// Which sections of the mods' settings are about an item, by their names: a section named
    /// after it, whole or as one of the parts its name is made of ("Backpack: Arctic Sherpa Pack",
    /// "2 - Satchel"), by the name the game shows or its prefab's. Names are compared as plain
    /// words, case, spaces and punctuation aside. A section holding the name among other words
    /// ("Wood farm") is not the item's, nor is a name too short to tell by.
    /// </summary>
    internal static class ConfigSections
    {
        /// <summary>The fewest letters a name is told by.</summary>
        private const int Shortest = 3;

        /// <summary>What splits a section's name into parts: a kind before a colon, a number before a dash, brackets and bars.</summary>
        private static readonly string[] Splits = { ":", " - ", " – ", "|", "/", "(", ")", "[", "]", "," };

        /// <summary>A name as plain words: lower case letters and digits, one space between words.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var plain = new StringBuilder(text.Length);
            var gap = false;
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (gap && plain.Length > 0) plain.Append(' ');
                    plain.Append(char.ToLowerInvariant(c));
                    gap = false;
                }
                else gap = true;
            }
            return plain.ToString();
        }

        /// <summary>The names a section goes by: its whole name and each of its parts, as plain words.</summary>
        public static List<string> NamesOf(string section)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(section)) return names;
            foreach (var name in new[] { section }.Concat(section.Split(Splits, StringSplitOptions.RemoveEmptyEntries)).Select(Plain))
            {
                if (name.Length > 0 && !names.Contains(name)) names.Add(name);
            }
            return names;
        }

        /// <summary>Whether a section is about the item of this shown name or prefab.</summary>
        public static bool About(string section, string shownName, string prefab)
        {
            var names = NamesOf(section);
            return ItemNames(shownName, prefab).Any(names.Contains);
        }

        /// <summary>The names an item is told by in a section's name, as plain words: those long enough to tell by.</summary>
        public static IEnumerable<string> ItemNames(string shownName, string prefab) =>
            new[] { Plain(shownName), Plain(prefab) }.Where(n => n.Count(char.IsLetterOrDigit) >= Shortest).Distinct();
    }

    /// <summary>How a mod's settings for an item are told: as its config holds them.</summary>
    internal static class ModSettingWords
    {
        /// <summary>The most of a value shown; a list of every prefab a mod touches is cut short.</summary>
        public const int LongestValue = 120;

        public static readonly string[] Columns = { "Setting", "Value" };

        /// <summary>A row's title: the mod, and the section its settings are under.</summary>
        public static string Title(string mod, string section) => $"In the settings of {mod}, under {section}";

        /// <summary>A setting's value as its config file writes it, cut short past <see cref="LongestValue"/>.</summary>
        public static string Value(string value)
        {
            if (string.IsNullOrEmpty(value)) return "(empty)";
            return value.Length <= LongestValue ? value : value.Substring(0, LongestValue) + "…";
        }
    }
}
