using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Puts each entry in the group its kind's tab lists it under (<see cref="Groups"/>): an item
    /// by its item type, a creature by its faction, a piece by the build menu and tab it is in, an
    /// effect or sound by what it is for, a projectile by what fires it, a status effect by what
    /// gives it, anything else by what it is there for. Resources have theirs from how they are
    /// gathered.
    /// </summary>
    internal static class Grouping
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Grouping() => WorldCaches.Register(nameof(Grouping), Forget);

        private static readonly HashSet<string> Told = new HashSet<string>();

        /// <summary>Groups the entries, a slice at a time; each step yields how many are done.</summary>
        public static IEnumerable<int> Apply(List<Entry> entries, int slice)
        {
            var byName = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (!EntryKeys.HasOwnNamespace(entry.Kind) && !byName.ContainsKey(entry.Name)) byName[entry.Name] = entry;
            }
            var weather = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                Weather(weather);
            }
            catch (Exception ex)
            {
                Tell("the weather", ex);
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

            Dictionary<string, int> raidRanks;
            try
            {
                raidRanks = RaidRanks(entries);
            }
            catch (Exception ex)
            {
                Tell("the raids' order", ex);
                raidRanks = new Dictionary<string, int>();
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                try
                {
                    if (entry.Kind == Kind.Raid && raidRanks.TryGetValue(entry.Name, out var rank)) entry.GroupRank = rank;
                    var group = Of(entry, byName, menus, weather);
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
            // A projectile another spawns (a cluster bomb's splinters) flies with that one.
            try
            {
                Groups.FollowSpawners(entries, Relations.SpawnedBy, Groups.Projectile(new Shooter[0]).Name);
            }
            catch (Exception ex)
            {
                Tell("projectiles spawned by others", ex);
            }

            // What is left behind goes with what leaves it: a stump with its trees.
            try
            {
                Leftovers.JoinOwnersGroups(entries);
            }
            catch (Exception ex)
            {
                Tell("what is left behind", ex);
            }
            if (Plugin.LogPreviews) Report(entries);
        }

        /// <summary>For the log: how many each group holds, and a sample of what nothing was found to play.</summary>
        private static void Report(List<Entry> entries)
        {
            foreach (var kind in entries.Where(e => e.Group.Length > 0).GroupBy(e => e.Kind))
            {
                var groups = kind.GroupBy(e => e.Group).OrderBy(g => g.First().GroupOrder).Select(g => $"{g.Key} {Numbers.Count(g.Count())}");
                Plugin.Note($"Scry groups its {Kinds.Label(kind.Key).ToLowerInvariant()}: {string.Join(", ", groups)}.");
            }
            var unplayed = entries.Where(e => (e.Kind == Kind.Effect || e.Kind == Kind.Sound) && e.GroupOrder == Groups.Purpose(new string[0], false, false).Order).Select(e => e.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            var unfired = entries.Where(e => e.Kind == Kind.Projectile && e.GroupOrder == Groups.Projectile(new Shooter[0]).Order)
                .Select(e => e.Name + (e.Links.Count > 0 ? " (" + string.Join("/", e.Links.Select(l => l.Group).Distinct()) + ")" : "")).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            if (unfired.Count > 0) Plugin.Note($"Scry found nothing that fires {Numbers.Count(unfired.Count)} projectiles, among them: {string.Join(", ", unfired.Take(100))}.");
            if (unplayed.Count > 0) Plugin.Note($"Scry found nothing that plays {Numbers.Count(unplayed.Count)} effects and sounds, among them: {string.Join(", ", unplayed.Take(150))}.");
        }

        /// <summary>An item's group: by its type, a weapon by its skill, or with what only creatures have.</summary>
        private static Group Item(Entry entry, GameObject prefab)
        {
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            var shared = drop != null ? drop.m_itemData?.m_shared : null;
            // Only creatures have it when one carries it and nothing a player meets gives it:
            // no recipe or station, nothing that drops, holds, sells, spawns or places it.
            var carried = entry.Links.Any(l => l.Group == Relations.CarriedBy);
            var obtainable = entry.Stations.Length > 0 || Knowledge.SourceLines(entry.Name).Count > 0 || entry.FoundIn.Length > 0
                || Knowledge.IsPlacedByWorld(entry.Name) || entry.Links.Any(l => l.Group == Relations.SpawnedBy);
            return Groups.Item(shared?.m_itemType.ToString(), shared?.m_skillType.ToString(), carried, obtainable);
        }

        /// <summary>
        /// Groups again the items only creatures seemed to have that the locations turned out to
        /// hold, once those are read; returns how many moved.
        /// </summary>
        public static int FoundInLocations(IEnumerable<Entry> entries)
        {
            var carried = Groups.CarriedByCreatures.Order;
            var moved = 0;
            foreach (var entry in entries)
            {
                if (entry.Kind != Kind.Item || entry.GroupOrder != carried || entry.FoundIn.Length == 0) continue;
                try
                {
                    var group = Item(entry, entry.Source as GameObject);
                    if (group.Order == carried) continue;
                    entry.Group = group.Name;
                    entry.GroupOrder = group.Order;
                    moved++;
                }
                catch (Exception ex)
                {
                    Tell("an entry", ex);
                }
            }
            return moved;
        }

        /// <summary>
        /// Each raid's rank within its group, in the order the bosses are fought: a boss's fight by
        /// the boss's health, a raid by the strongest boss whose defeat it waits for.
        /// </summary>
        private static Dictionary<string, int> RaidRanks(List<Entry> entries)
        {
            var strengths = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry.Kind != Kind.Raid || !(entry.Source is RandomEvent raid)) continue;
                var boss = Knowledge.BossOfEvent(raid.m_name)?.GetComponent<Character>();
                strengths[entry.Name] = boss != null ? boss.m_health : RaidGrouping.Strength(raid.m_requiredGlobalKeys, Knowledge.BossHealthOf);
            }
            return RaidGrouping.Ranks(strengths);
        }

        private static Group? Of(Entry entry, Dictionary<string, Entry> byName, Dictionary<string, Group> menus, HashSet<string> weather)
        {
            var prefab = entry.Source as GameObject;
            switch (entry.Kind)
            {
                case Kind.Item:
                    return Item(entry, prefab);
                case Kind.Creature:
                    var character = prefab != null ? prefab.GetComponent<Character>() : null;
                    return character != null ? Groups.Creature(character.m_faction.ToString(), character.m_boss) : Groups.Creature(null, false);
                case Kind.Piece:
                    return menus.TryGetValue(entry.Name, out var menu) ? menu : Groups.InNoMenu;
                case Kind.Raid:
                    if (!(entry.Source is RandomEvent raid)) return null;
                    return Groups.Raid(RaidGrouping.Role(raid.m_random, raid.m_standaloneInterval, Knowledge.BossOfEvent(raid.m_name) != null));
                case Kind.Effect:
                case Kind.Sound:
                    var footstep = entry.Links.Any(l => l.Group == Relations.FootstepOf);
                    var animation = entry.Links.Any(l => l.Group == Relations.PlayedByAnimation);
                    // Besides its effect lists: weather shows it, the world places it by itself (cinder
                    // rain, fireflies), a status effect spawns it, or something leaves it when destroyed.
                    var fields = entry.PlayedIn.ToList();
                    if (weather.Contains(entry.Name)) fields.Add("weather");
                    if (Knowledge.IsPlacedByWorld(entry.Name)) fields.Add("ambience");
                    if (entry.Links.Any(l => l.Group == Relations.SpawnedBy && l.Target.StartsWith("se:", StringComparison.Ordinal))) fields.Add("se:spawned");
                    if (entry.LeftBy.Count > 0) fields.Add("left when destroyed");
                    var purpose = Groups.Purpose(fields, footstep, animation);

                    // Found to be played only through a field that spawns or carries it: other.
                    if (purpose.Order == Groups.Purpose(new string[0], false, false).Order && Users(entry).Any()) purpose = Groups.Purpose(new[] { "" }, false, false);
                    return purpose;
                case Kind.Projectile:
                    return Groups.Projectile(Shooters(entry, byName));
                case Kind.StatusEffect:
                    return Groups.StatusEffect(Givers(entry, byName));
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

        /// <summary>What gives a status effect, by the links noted: each giver's kind, and how it gives it.</summary>
        private static IEnumerable<Giver> Givers(Entry entry, Dictionary<string, Entry> byName)
        {
            foreach (var link in entry.Links)
            {
                if (link.Group != Relations.GivenBy) continue;
                var kind = byName.TryGetValue(link.Target, out var giver) ? giver.Kind : Kind.Other;
                if (link.Notes.Count == 0) yield return new Giver(kind, "");
                foreach (var how in link.Notes) yield return new Giver(kind, how);
            }
        }

        /// <summary>What the weather shows, by prefab name: each environment's particle systems and its object (<c>EnvSetup.m_psystems</c>, <c>m_envObject</c>).</summary>
        private static void Weather(HashSet<string> names)
        {
            var environments = EnvMan.instance != null ? EnvMan.instance.m_environments : null;
            if (environments == null) return;
            foreach (var environment in environments)
            {
                if (environment == null) continue;
                if (environment.m_envObject != null) names.Add(environment.m_envObject.name);
                if (environment.m_psystems == null) continue;
                foreach (var system in environment.m_psystems) if (system != null) names.Add(system.name);
            }
        }

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
                var shared = drop != null ? drop.m_itemData?.m_shared : null;
                var skill = shared != null ? shared.m_skillType.ToString() : "";
                var ammo = shared != null && (shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable) ? shared.m_ammoType : "";
                var carried = shooter.Links.Any(l => l.Group == Relations.CarriedBy) && shooter.Stations.Length == 0;
                yield return new Shooter(shooter.Kind, skill, carried, ammo);
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

            var tools = new List<(string Prefab, string Name, bool Main, PieceTable Table)>();
            foreach (var item in items)
            {
                var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                var table = shared?.m_buildPieces;
                if (table == null || table.m_pieces == null) continue;
                var main = item.name == "Hammer";
                var shown = CatalogBuilder.Localize(shared.m_name);
                var entry = (item.name, shown.Length > 0 ? shown : item.name, main, table);
                if (main) tools.Insert(0, entry);
                else tools.Add(entry);
            }

            // Every tool a piece is in is noted for its details and the tool's; the list groups
            // it under the first, the hammer before the rest.
            for (var t = 0; t < tools.Count; t++)
            {
                var (prefab, tool, main, table) = tools[t];
                var pieces = table.m_pieces.Where(p => p != null).Select(p => p.GetComponent<Piece>()).Where(p => p != null).ToList();
                foreach (var piece in pieces)
                {
                    var name = piece.gameObject.name;
                    var index = table.m_categories != null ? table.m_categories.IndexOf(piece.m_category) : -1;
                    var label = index >= 0 && table.m_categoryLabels != null && index < table.m_categoryLabels.Count
                        ? CatalogBuilder.Localize(table.m_categoryLabels[index])
                        : "";
                    if (label.Length == 0) label = CategoryName(piece.m_category);
                    var order = index >= 0 ? index : 500 + (int)piece.m_category;
                    Knowledge.Tools.Add(prefab, tool, name, label, order);
                    if (!menus.ContainsKey(name)) menus[name] = Groups.Piece(tool, t, main, label, order);
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
            // The game's catch-all tab, whose pieces show under every tab.
            if (category == Piece.PieceCategory.All) return "Every tab";
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
