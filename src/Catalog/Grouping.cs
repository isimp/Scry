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
            var byName = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry.Kind != Kind.StatusEffect && !byName.ContainsKey(entry.Name)) byName[entry.Name] = entry;
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
                    var group = Of(entry, byName, menus);
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
            if (Plugin.LogPreviews) Report(entries);
        }

        /// <summary>For the log: how many each group holds, and a sample of what nothing was found to play.</summary>
        private static void Report(List<Entry> entries)
        {
            foreach (var kind in entries.Where(e => e.Group.Length > 0).GroupBy(e => e.Kind))
            {
                var groups = kind.GroupBy(e => e.Group).OrderBy(g => g.First().GroupOrder).Select(g => $"{g.Key} {g.Count()}");
                Plugin.Note($"Scry groups its {Kinds.Label(kind.Key).ToLowerInvariant()}: {string.Join(", ", groups)}.");
            }
            var unplayed = entries.Where(e => (e.Kind == Kind.Effect || e.Kind == Kind.Sound) && e.GroupOrder == Groups.Purpose(new string[0], false, false).Order).Select(e => e.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            if (unplayed.Count > 0) Plugin.Note($"Scry found nothing that plays {unplayed.Count} effects and sounds, among them: {string.Join(", ", unplayed.Take(150))}.");
        }

        private static Group? Of(Entry entry, Dictionary<string, Entry> byName, Dictionary<string, Group> menus)
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
                    var footstep = entry.Links.Any(l => l.Group == Relations.FootstepOf);
                    var animation = entry.Links.Any(l => l.Group == Relations.PlayedByAnimation);
                    var purpose = Groups.Purpose(entry.PlayedIn, footstep, animation);

                    // Found to be played only through a field that spawns or carries it: other.
                    if (purpose.Order == Groups.Purpose(new string[0], false, false).Order && Users(entry).Any()) purpose = Groups.Purpose(new[] { "" }, false, false);
                    return purpose;
                case Kind.Projectile:
                    return Groups.Projectile(Shooters(entry, byName));
                default:
                    return null;
            }
        }

        /// <summary>
        /// What plays an effect or sound: what its effect lists name, and what points at it in
        /// other ways (the footsteps of a creature, an animation's events, a field that spawns or
        /// shoots it, a hand that carries it), as the links noted.
        /// </summary>
        private static IEnumerable<string> Users(Entry entry)
        {
            foreach (var user in entry.UsedBy) yield return user;
            foreach (var link in entry.Links)
            {
                if (UsedByLinks.Contains(link.Group)) yield return link.Target;
            }
        }

        private static readonly HashSet<string> UsedByLinks = new HashSet<string>(StringComparer.Ordinal)
        {
            Relations.FootstepOf, Relations.PlayedByAnimation, Relations.SpawnedBy, Relations.CarriedBy, LinkBook.ShotFrom,
        };

        /// <summary>
        /// What fires a projectile, by the links noted: what shoots it or spawns it, each with its
        /// kind, its weapon skill, and whether only creatures carry it (carried by one, and made by
        /// no recipe or at no station).
        /// </summary>
        private static IEnumerable<Shooter> Shooters(Entry entry, Dictionary<string, Entry> byName)
        {
            foreach (var link in entry.Links)
            {
                if (link.Group != LinkBook.ShotFrom && link.Group != Relations.SpawnedBy) continue;
                if (!byName.TryGetValue(link.Target, out var shooter)) continue;
                var prefab = shooter.Source as GameObject;
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                var skill = drop != null && drop.m_itemData?.m_shared != null ? drop.m_itemData.m_shared.m_skillType.ToString() : "";
                var carried = shooter.Links.Any(l => l.Group == Relations.CarriedBy) && shooter.Stations.Length == 0;
                yield return new Shooter(shooter.Kind, skill, carried);
            }
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
                        : "";
                    if (label.Length == 0) label = CategoryName(piece.m_category);
                    menus[name] = Groups.Piece(tool, t, main, label, index >= 0 ? index : 500 + (int)piece.m_category, tabCount);
                }
            }
            return menus;
        }

        /// <summary>
        /// A build tab by its name. A tab a mod adds through Jotunn is a number the game's enum does
        /// not name; Jotunn adds its names to what Enum.GetNames and GetValues give for the enum,
        /// so they are read from there, not from the value itself.
        /// </summary>
        private static string CategoryName(Piece.PieceCategory category)
        {
            if (_categoryNames == null)
            {
                _categoryNames = new Dictionary<Piece.PieceCategory, string>();
                try
                {
                    var names = Enum.GetNames(typeof(Piece.PieceCategory));
                    var values = Enum.GetValues(typeof(Piece.PieceCategory));
                    for (var i = 0; i < names.Length && i < values.Length; i++)
                    {
                        var value = (Piece.PieceCategory)values.GetValue(i);
                        if (!_categoryNames.ContainsKey(value)) _categoryNames[value] = names[i];
                    }
                }
                catch (Exception ex)
                {
                    Tell("the build tabs' names", ex);
                }
            }
            var name = _categoryNames.TryGetValue(category, out var known) ? known : category.ToString();
            return name.IndexOf(' ') >= 0 ? name : Naming.FieldLabel(name);
        }

        private static Dictionary<Piece.PieceCategory, string> _categoryNames;

        /// <summary>Forgets the build tabs' names, which mods may add to in the next world.</summary>
        public static void Forget() => _categoryNames = null;

        private static void Tell(string what, Exception ex)
        {
            if (Told.Add(what + "|" + ex.GetType().Name + "|" + ex.Message))
            {
                Plugin.Log.LogWarning($"Scry could not group {what}, and lists it ungrouped (said once): {ex.Message}");
            }
        }
    }
}
