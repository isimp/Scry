using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Every effect list each sound and effect is part of, noted while the catalog is read: who
    /// plays the list, what for, and what else it plays. Read by the panel as
    /// <see cref="PlaysIn"/> rows.
    /// </summary>
    internal static class EffectLinks
    {
        private static readonly Dictionary<string, List<EffectUse>> Uses = new Dictionary<string, List<EffectUse>>(StringComparer.Ordinal);
        private static readonly HashSet<(EffectList, string)> Noted = new HashSet<(EffectList, string)>();
        private static readonly Dictionary<string, List<PlaysInRow>> Rows = new Dictionary<string, List<PlaysInRow>>(StringComparer.Ordinal);

        public static void Clear()
        {
            Uses.Clear();
            Noted.Clear();
            Rows.Clear();
        }

        /// <summary>Notes one list: its player as shown and where clicking it goes, and what it is for.</summary>
        public static void Note(EffectList list, string owner, string ownerKey, string label)
        {
            if (list?.m_effectPrefabs == null || !Noted.Add((list, owner))) return;

            var members = new List<string>();
            foreach (var data in list.m_effectPrefabs)
            {
                if (data != null && data.m_enabled && data.m_prefab != null) members.Add(data.m_prefab.name);
            }
            if (members.Count == 0) return;

            var use = new EffectUse(owner, ownerKey, label, members, list);
            foreach (var member in use.Members)
            {
                if (!Uses.TryGetValue(member, out var uses))
                {
                    uses = new List<EffectUse>();
                    Uses[member] = uses;
                }
                uses.Add(use);
            }
        }

        /// <summary>The lists a prefab plays in, as rows, worked out once.</summary>
        public static List<PlaysInRow> For(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return new List<PlaysInRow>();
            if (Rows.TryGetValue(prefabName, out var rows)) return rows;
            rows = Uses.TryGetValue(prefabName, out var uses) ? PlaysIn.Rows(uses) : new List<PlaysInRow>();
            Rows[prefabName] = rows;
            return rows;
        }
    }
}
