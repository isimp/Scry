using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Puts each entry in the group its kind's tab lists it under (<see cref="Groups"/>): an item
    /// by its item type, a creature by its faction, a piece by the build menu and tab it is in, an
    /// effect or sound by what plays it. Resources have theirs from how they are gathered.
    /// </summary>
    internal static class Grouping
    {
        private static readonly HashSet<string> Told = new HashSet<string>();

        /// <summary>Groups the entries, a slice at a time; each step yields how many are done.</summary>
        public static IEnumerable<int> Apply(List<Entry> entries, int slice)
        {
            var kinds = new Dictionary<string, Kind>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry.Kind != Kind.StatusEffect && !kinds.ContainsKey(entry.Name)) kinds[entry.Name] = entry.Kind;
            }
            Dictionary<string, Group> menus;
            try
            {
                menus = Menus();
            }
            catch (Exception ex)
            {
                Tell("the build menus", ex);
                menus = new Dictionary<string, Group>();
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                try
                {
                    var group = Of(entry, kinds, menus);
                    if (group.HasValue)
                    {
                        entry.Group = group.Value.Name;
                        entry.GroupOrder = group.Value.Order;
                    }
                }
                catch (Exception ex)
                {
                    Tell("an entry", ex);
                }
                if ((i + 1) % slice == 0) yield return i + 1;
            }
        }

        private static Group? Of(Entry entry, Dictionary<string, Kind> kinds, Dictionary<string, Group> menus)
        {
            var prefab = entry.Source as GameObject;
            switch (entry.Kind)
            {
                case Kind.Item:
                    var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                    var shared = drop != null ? drop.m_itemData?.m_shared : null;
                    return Groups.Item(shared?.m_itemType.ToString());
                case Kind.Creature:
                    var character = prefab != null ? prefab.GetComponent<Character>() : null;
                    return character != null ? Groups.Creature(character.m_faction.ToString(), character.m_boss) : Groups.Creature(null, false);
                case Kind.Piece:
                    return menus.TryGetValue(entry.Name, out var menu) ? menu : Groups.InNoMenu;
                case Kind.Effect:
                case Kind.Sound:
                    return Groups.ByUsers(entry.UsedBy.Select(user => KindOf(user, kinds)));
                default:
                    return null;
            }
        }

        /// <summary>The kind of what plays an effect, by the name it is noted under: a prefab by its name, a status effect as "status effect" and its name.</summary>
        private static Kind KindOf(string user, Dictionary<string, Kind> kinds)
        {
            if (user == null) return Kind.Other;
            if (user.StartsWith("status effect ", StringComparison.Ordinal)) return Kind.StatusEffect;
            return kinds.TryGetValue(user, out var kind) ? kind : Kind.Other;
        }

        /// <summary>
        /// Each piece's place in the build menus, by prefab name: the tool that holds it (the
        /// hammer first, then the others as the game lists its items) and the tab it is on, named
        /// as the game names it (<c>PieceTable.m_categories</c> and <c>m_categoryLabels</c>).
        /// </summary>
        private static Dictionary<string, Group> Menus()
        {
            var menus = new Dictionary<string, Group>(StringComparer.Ordinal);
            var items = ObjectDB.instance != null ? ObjectDB.instance.m_items : null;
            if (items == null) return menus;

            var tools = new List<(string Name, bool Main, PieceTable Table)>();
            foreach (var item in items)
            {
                var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                var table = shared?.m_buildPieces;
                if (table == null || table.m_pieces == null) continue;
                var main = item.name == "Hammer";
                var entry = (CatalogBuilder.Localize(shared.m_name), main, table);
                if (main) tools.Insert(0, entry);
                else tools.Add(entry);
            }

            for (var t = 0; t < tools.Count; t++)
            {
                var (tool, main, table) = tools[t];
                var pieces = table.m_pieces.Where(p => p != null).Select(p => p.GetComponent<Piece>()).Where(p => p != null).ToList();
                var tabCount = pieces.Select(p => p.m_category).Distinct().Count();
                foreach (var piece in pieces)
                {
                    var name = piece.gameObject.name;
                    if (menus.ContainsKey(name)) continue;
                    var index = table.m_categories != null ? table.m_categories.IndexOf(piece.m_category) : -1;
                    var label = index >= 0 && table.m_categoryLabels != null && index < table.m_categoryLabels.Count
                        ? CatalogBuilder.Localize(table.m_categoryLabels[index])
                        : Naming.FieldLabel(piece.m_category.ToString());
                    menus[name] = Groups.Piece(tool, t, main, label, index >= 0 ? index : 500 + (int)piece.m_category, tabCount);
                }
            }
            return menus;
        }

        private static void Tell(string what, Exception ex)
        {
            if (Told.Add(what + "|" + ex.GetType().Name + "|" + ex.Message))
            {
                Plugin.Log.LogWarning($"Scry could not group {what}, and lists it ungrouped (said once): {ex.Message}");
            }
        }
    }
}
