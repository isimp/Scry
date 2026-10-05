using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What a page says around its facts, in words: the parts of it that could not be read, and
    /// a value of the game's shown as it is, a choice by its name.
    /// </summary>
    internal static class FactWords
    {
        /// <summary>A part of the facts, as a failure to read it names it.</summary>
        public static string Part(string part) => $"the {part} details";

        /// <summary>A fact told as a note under what it qualifies: "Drops with stars: twice as many with 1 star".</summary>
        public static string Note(string label, string value) => $"{label}: {value}";

        /// <summary>A part of the facts among the timings, while previews are logged.</summary>
        public static string PartTiming(string part) => "facts " + part;

        /// <summary>The parts that could not be read, told at the end of the facts.</summary>
        public static string NotShown(IEnumerable<string> parts) => $"the {Naming.Commas(parts)} details could not be read (the log says why)";

        /// <summary>A value as text, a whole number and a fraction with thousands; null for a choice the game has no name for (a mod's own numbered one).</summary>
        public static string Value(object value)
        {
            switch (value)
            {
                case float f: return Numbers.Amount(f);
                case double d: return Numbers.Amount(d);
                case int i: return Numbers.Count(i);
                case long l: return Numbers.Count(l);
                case bool b: return b ? "yes" : "no";
                case Enum e: return Choice(e)?.ToLowerInvariant();
                default: return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// A choice by its name, "OneHandedWeapon" as "One handed weapon". Null when the value has
        /// no name, which is how a mod's own categories and factions show up, as bare numbers.
        /// Flags that combine several names are joined.
        /// </summary>
        public static string Choice(Enum value)
        {
            var text = value.ToString();
            if (text.Length > 0 && (char.IsDigit(text[0]) || text[0] == '-')) return null;
            return Naming.Commas(text.Split(FlagSeparator, StringSplitOptions.None).Select(Naming.FieldLabel));
        }

        /// <summary>How .NET writes a set of flags: "Fire, Frost".</summary>
        private static readonly string[] FlagSeparator = { ", " };
    }
}
