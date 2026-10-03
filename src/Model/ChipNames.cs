using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Chips shown side by side told apart: several prefabs the game shows by one name (four
    /// kinds of Draugr) each get their prefab name after it, unless the prefab is named as it is
    /// shown. Names no other chip shares are left as they are.
    /// </summary>
    internal static class ChipNames
    {
        public static string[] Apart(IReadOnlyList<(string Key, string Shown)> chips)
        {
            var count = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (_, shown) in chips)
            {
                count.TryGetValue(shown ?? "", out var n);
                count[shown ?? ""] = n + 1;
            }

            var names = new string[chips.Count];
            for (var i = 0; i < chips.Count; i++)
            {
                var (key, shown) = chips[i];
                var shared = count[shown ?? ""] > 1;
                names[i] = shared && !string.IsNullOrEmpty(key) && key != shown ? shown + " · " + key : shown;
            }
            return names;
        }
    }
}
