using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What a creature's weapons are called and which of them are choices to hold. The game gives
    /// creatures their attacks as items, many of them never drawn and named only for its makers
    /// ("slap", "fireballattack"); where the game has no name of its own for one, it is called by
    /// its prefab name made readable and without the creature's own name in it, and what it may
    /// hold is only what shows in its hand.
    /// </summary>
    internal static class WeaponChoices
    {
        /// <summary>What a prefab's name is split into words at.</summary>
        private static readonly char[] NameParts = { '_', ' ', '-' };

        /// <summary>
        /// An item's prefab name as words, leaving out the leading words that name the creature:
        /// "troll_log_swing_v" on a troll is "Log swing v", "trollsnow_punch" on a frost troll
        /// "Punch".
        /// </summary>
        public static string Readable(string item, string creature)
        {
            var own = Words(creature).Select(w => w.ToLowerInvariant()).ToList();
            var words = (item ?? "").Split(NameParts, StringSplitOptions.RemoveEmptyEntries).ToList();
            while (words.Count > 1 && own.Any(o => words[0].IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                words.RemoveAt(0);
            }
            var text = string.Join(" ", words.SelectMany(Words)).ToLowerInvariant();
            return text.Length > 0 ? Naming.Capital(text) : item ?? "";
        }

        /// <summary>The weapons that are choices to hold: those that show, one of each shown alike.</summary>
        public static List<string> Holdable(IEnumerable<(string Item, string Shown, bool Drawn)> weapons)
        {
            var held = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var weapon in weapons)
            {
                if (!weapon.Drawn || !seen.Add(weapon.Shown ?? weapon.Item)) continue;
                held.Add(weapon.Item);
            }
            return held;
        }

        /// <summary>A name split at its capitals and underscores: "TrollFrost" is "Troll", "Frost".</summary>
        private static IEnumerable<string> Words(string name)
        {
            var words = new List<string>();
            foreach (var part in (name ?? "").Split(NameParts, StringSplitOptions.RemoveEmptyEntries))
            {
                var start = 0;
                for (var i = 1; i < part.Length; i++)
                {
                    if (char.IsUpper(part[i]) && char.IsLower(part[i - 1]))
                    {
                        words.Add(part.Substring(start, i - start));
                        start = i;
                    }
                }
                words.Add(part.Substring(start));
            }
            return words;
        }
    }
}
