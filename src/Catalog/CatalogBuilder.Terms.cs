using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the search's own terms read of each entry, once every entry is made, linked and
    /// grouped: what it is (is:, <see cref="SearchFlags"/>), as its details tell the same.
    /// </summary>
    internal static partial class CatalogBuilder
    {
        /// <summary>Reads the terms' words into every entry, a slice of entries a step.</summary>
        private static IEnumerable<int> SearchTerms(List<Entry> entries, int slice)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Guard.Each(Feature.SearchTerms, "search terms", entry.Name, () => entry.SetTermWords("is", SearchFlags.Of(FlagFactsOf(entry))));
                if ((i + 1) % slice == 0) yield return i + 1;
            }
        }

        /// <summary>What is: asks of an entry, read from its prefab as its details read it.</summary>
        private static FlagFacts FlagFactsOf(Entry entry)
        {
            var facts = new FlagFacts { Silent = entry.Empty, Unsure = entry.Origin == Origin.Mod && !UnsureWords.IsSureClue(entry.ModClue) };
            if (!(entry.Source is GameObject prefab) || prefab == null || EntryKeys.HasOwnNamespace(entry.Kind)) return facts;

            var character = prefab.GetComponent<Character>();
            if (character != null)
            {
                facts.Boss = character.m_boss;
                facts.Flying = character.m_flying;
                facts.Tameable = prefab.GetComponent<Tameable>() != null;
            }

            var shared = prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
            if (shared != null)
            {
                facts.ItemType = shared.m_itemType.ToString();
                facts.Food = shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f;
                facts.Craftable = _recipes != null && _recipes.ContainsKey(prefab.name);
            }

            if (entry.Kind == Kind.Piece) facts.Buildable = Knowledge.Tools.ToolsOf(prefab.name).Count > 0;
            return facts;
        }
    }
}
