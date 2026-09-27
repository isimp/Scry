using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// Where things are found in the world's locations and dungeon rooms, once they are read (on
    /// request, since the game keeps them in asset bundles loaded only while it builds a zone).
    /// A location goes by its name in words, a room by its kind of dungeon.
    /// </summary>
    public static class Places
    {
        /// <summary>
        /// A location's name in words, without the number of its variant: "WoodHouse10" is a wood
        /// house, "Crypt2" a crypt. A word in capitals ("DN") stays so. A name with no words in it
        /// (a mod's, all digits) keeps its own, so no place goes unnamed.
        /// </summary>
        public static string LocationLabel(string prefab) => OrOwn(InWords(prefab), prefab);

        /// <summary>The rooms of a kind of dungeon (the game's <c>Room.Theme</c>, by name; a mod's may be only a number).</summary>
        public static string RoomLabel(string theme) => OrOwn(InWords(theme), theme) + " rooms";

        private static string OrOwn(string words, string own) => words.Length > 0 ? words : (own ?? "").Trim();

        private static string InWords(string name)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            void End()
            {
                var text = word.ToString().TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (text.Length > 0) words.Add(text);
                word.Clear();
            }

            var source = name ?? "";
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (c == '_' || c == ' ' || c == '-') { End(); continue; }
                var startsWord = char.IsUpper(c) && word.Length > 0
                                 && (!char.IsUpper(source[i - 1]) || (i + 1 < source.Length && char.IsLower(source[i + 1])));
                var afterDigit = word.Length > 0 && char.IsDigit(source[i - 1]) && !char.IsDigit(c);
                if (startsWord || afterDigit) End();
                word.Append(c);
            }
            End();

            var shown = new StringBuilder();
            foreach (var w in words)
            {
                var capitals = w.Length > 1 && w.All(char.IsUpper);
                if (shown.Length == 0) shown.Append(capitals ? w : char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant());
                else shown.Append(' ').Append(capitals ? w : w.ToLowerInvariant());
            }
            return shown.ToString();
        }

        /// <summary>
        /// Puts what was found where on the catalog's entries (by prefab name; a status effect is
        /// no prefab), each place once, in order. An effect or sound nothing was found to play, or a
        /// projectile nothing was found to fire, goes under "In locations" when one names it.
        /// </summary>
        public static void Apply(IEnumerable<Entry> catalog, IDictionary<string, HashSet<string>> found)
        {
            var unplayed = Groups.Purpose(new string[0], false, false).Order;
            var unfired = Groups.Projectile(new Shooter[0]).Order;
            foreach (var entry in catalog)
            {
                if (entry.Kind == Kind.StatusEffect || !found.TryGetValue(entry.Name, out var places) || places.Count == 0) continue;
                entry.FoundIn = places.Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

                Group? group = null;
                if ((entry.Kind == Kind.Effect || entry.Kind == Kind.Sound) && entry.GroupOrder == unplayed) group = Groups.EffectsInLocations;
                else if (entry.Kind == Kind.Projectile && entry.GroupOrder == unfired) group = Groups.ProjectilesInLocations;
                if (group == null) continue;
                entry.Group = group.Value.Name;
                entry.GroupOrder = group.Value.Order;
            }
        }
    }
}
