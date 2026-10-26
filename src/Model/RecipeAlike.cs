using System;
using System.Collections.Generic;
using System.Text;

namespace Scry
{
    /// <summary>
    /// Tells a recipe already shown on a page from one of its own: the game's recipe list can hold
    /// the same recipe several times over, as a mod registering one again leaves it, and each
    /// would be a row saying the same. Alike are those of one title (station, level, amount),
    /// station, ingredients with their amounts, and upgrade kits.
    /// </summary>
    internal sealed class RecipeAlike
    {
        private readonly HashSet<string> _shown = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Whether a recipe by its key (<see cref="Key"/>) is the first of its kind on the page; false for one alike shown before.</summary>
        public bool First(string key) => _shown.Add(key);

        /// <summary>A recipe's key: all a row shows of it, each part kept apart so none runs into the next.</summary>
        public static string Key(string title, string station, IEnumerable<(string Prefab, string Amount)> items, IEnumerable<(string Prefab, string Amount)> kits)
        {
            var key = new StringBuilder();
            // Each part ends at a bar, a bar or backslash of its own escaped, and none apart from empty.
            void Part(string text) => key.Append(text == null ? "\\0" : text.Replace("\\", "\\\\").Replace("|", "\\|")).Append('|');
            Part(title);
            Part(station);
            foreach (var (prefab, amount) in items)
            {
                Part(prefab);
                Part(amount);
            }
            key.Append('#');
            foreach (var (prefab, amount) in kits)
            {
                Part(prefab);
                Part(amount);
            }
            return key.ToString();
        }
    }
}
