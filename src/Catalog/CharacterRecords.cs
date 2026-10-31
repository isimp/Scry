using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// Reads what the game keeps of the character being played into <see cref="CompendiumRecords"/>:
    /// the player's own sets, private to it (<c>Player.m_knownMaterial</c>, <c>m_knownRecipes</c>,
    /// <c>m_knownStations</c>, <c>m_knownBiome</c>, <c>m_trophies</c>, <c>m_uniques</c>, the guardian power chosen), and the statistics its
    /// profile counts (<c>PlayerProfile.m_playerStats</c>, the first set, which counts everything).
    /// </summary>
    internal static class CharacterRecords
    {
        static CharacterRecords() => WorldCaches.Register(nameof(CharacterRecords), Forget);

        private static AccessTools.FieldRef<Player, HashSet<string>> _materials;
        private static AccessTools.FieldRef<Player, HashSet<string>> _recipes;
        private static AccessTools.FieldRef<Player, Dictionary<string, int>> _stations;
        private static AccessTools.FieldRef<Player, HashSet<string>> _biomes;
        private static AccessTools.FieldRef<Player, HashSet<string>> _trophies;
        private static AccessTools.FieldRef<Player, HashSet<string>> _uniques;

        /// <summary>How long the character has played, in seconds, as the game counts its time in and out of its base.</summary>
        public static float Played(PlayerProfile profile)
        {
            var stats = profile?.m_playerStats != null && profile.m_playerStats.Length > 0 ? profile.m_playerStats[0] : null;
            if (stats?.m_stats == null) return float.MaxValue;
            stats.m_stats.TryGetValue(PlayerStatType.TimeInBase, out var inside);
            stats.m_stats.TryGetValue(PlayerStatType.TimeOutOfBase, out var outside);
            return inside + outside;
        }

        /// <summary>How many records there are in all, the power chosen one of them; it grows whenever any of them does, but a power chosen in place of another (<see cref="Power"/>).</summary>
        public static int Size(Player player, PlayerProfile profile)
        {
            Bind();
            var stats = profile.m_playerStats[0];
            return _materials(player).Count + _recipes(player).Count + _stations(player).Count + _biomes(player).Count + _trophies(player).Count + _uniques(player).Count
                + stats.m_enemyStats[0].Count + stats.m_itemPickupStats.Count + stats.m_itemCraftStats.Count + stats.m_foodEatenStats.Count
                + stats.m_pickableStats.Count + stats.m_piecesPlacedStats.Count
                + Harvests(profile)
                + (Power(player).Length > 0 ? 1 : 0);
        }

        /// <summary>Puts every record into <paramref name="into"/>, in place of what it held.</summary>
        public static void Read(Player player, PlayerProfile profile, CompendiumRecords into)
        {
            Bind();
            var stats = profile.m_playerStats[0];
            Copy(_materials(player), into.Materials);
            Copy(_recipes(player), into.Recipes);
            Copy(_stations(player).Keys, into.Stations);
            Copy(_biomes(player), into.Biomes);
            Copy(_trophies(player), into.Trophies);
            Copy(_uniques(player), into.Uniques);
            Copy(stats.m_enemyStats[0].Keys, into.Killed);
            Copy(stats.m_itemPickupStats.Keys, into.PickedUp);
            Copy(stats.m_itemCraftStats.Keys, into.Crafted);
            Copy(stats.m_foodEatenStats.Keys, into.Eaten);
            Copy(stats.m_pickableStats.Keys, into.Picked);
            // Before the game's prefabs are read, nothing is told of what was picked from, taken from or caught.
            var ready = Kinds();
            Copy(ready ? stats.m_pickableStats.Keys.SelectMany(item => _pickedFrom.TryGetValue(item, out var givers) && givers.Count == 1 ? givers : Enumerable.Empty<string>()) : Enumerable.Empty<string>(), into.PickedFrom);
            Copy(ready ? Sole(_hives, Taken(stats, PlayerStatType.BeesHarvested)).Concat(Sole(_taps, Taken(stats, PlayerStatType.SapHarvested))) : Enumerable.Empty<string>(), into.Collected);
            Copy(ready ? stats.m_pickableStats.Keys.SelectMany(name => _fish.TryGetValue(name, out var fish) ? fish : Enumerable.Empty<string>()) : Enumerable.Empty<string>(), into.Caught);
            Copy(stats.m_piecesPlacedStats.Keys, into.Placed);
            var power = Power(player);
            Copy(power.Length > 0 ? new[] { power } : Array.Empty<string>(), into.Powers);
        }

        /// <summary>
        /// What the game counted of an entry for the character playing, by the names it goes by:
        /// a creature's kills, a piece's builds, an item's meals, crafts and pickups by its name,
        /// and its picks by its prefab, as the game counts each; none outside a game.
        /// </summary>
        public static CharacterCounts Counts(Entry entry)
        {
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null || entry == null) return null;
            var stats = profile.m_playerStats[0];
            int Sum(Dictionary<string, float> counted)
            {
                var sum = 0f;
                foreach (var token in entry.Tokens.Distinct()) if (!string.IsNullOrEmpty(token) && counted.TryGetValue(token, out var count)) sum += count;
                return (int)sum;
            }
            var counts = new CharacterCounts();
            switch (entry.Kind)
            {
                case Kind.Creature:
                    counts.Killed = Sum(stats.m_enemyStats[0]);
                    // Counted again by how it was dealt, where every hit was of one kind.
                    int By(KillModifiers kind) => (int)kind < stats.m_enemyStats.Length && stats.m_enemyStats[(int)kind] != null ? Sum(stats.m_enemyStats[(int)kind]) : 0;
                    counts.Melee = By(KillModifiers.Melee);
                    counts.Ranged = By(KillModifiers.Ranged);
                    counts.Magic = By(KillModifiers.Magic);
                    counts.Unarmed = By(KillModifiers.Unarmed);
                    break;
                case Kind.Piece:
                    counts.Built = Sum(stats.m_piecesPlacedStats);
                    break;
                case Kind.Item:
                    counts.Eaten = Sum(stats.m_foodEatenStats);
                    counts.Made = Sum(stats.m_itemCraftStats);
                    counts.PickedUp = Sum(stats.m_itemPickupStats);
                    counts.Picked = stats.m_pickableStats.TryGetValue(entry.Name, out var picked) ? (int)picked : 0;
                    break;
            }
            return counts;
        }

        /// <summary>The guardian power the character chose at a boss stone, by its status effect's prefab; empty before one is chosen.</summary>
        public static string Power(Player player) => player.GetGuardianPowerName() ?? "";

        /// <summary>Whether the game lists a recipe for the character, by the token it lists it by (<c>Player.m_knownRecipes</c>), read as it stands now.</summary>
        public static bool RecipeListed(Player player, string token)
        {
            if (player == null || string.IsNullOrEmpty(token)) return false;
            Bind();
            return _recipes(player).Contains(token);
        }

        private static void Bind()
        {
            if (_trophies != null) return;
            _materials = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial");
            _recipes = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownRecipes");
            _stations = AccessTools.FieldRefAccess<Player, Dictionary<string, int>>("m_knownStations");
            _biomes = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownBiome");
            _trophies = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_trophies");
            _uniques = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_uniques");
        }

        /// <summary>How many of honey and sap the character ever took, none, one or both, as the game counts them: a first take grows the records.</summary>
        public static int Harvests(PlayerProfile profile)
        {
            var stats = profile.m_playerStats[0];
            return (Taken(stats, PlayerStatType.BeesHarvested) ? 1 : 0) + (Taken(stats, PlayerStatType.SapHarvested) ? 1 : 0);
        }

        /// <summary>Whether the character took honey or sap, as the game counts it.</summary>
        private static bool Taken(PlayerProfile.PlayerStats stats, PlayerStatType what) => stats.m_stats != null && stats.m_stats.TryGetValue(what, out var count) && count > 0f;

        /// <summary>The one kind of producer of a sort, where the game has only one and the character took from it; none otherwise, as the game counts what was taken, not from which.</summary>
        private static IEnumerable<string> Sole(List<string> kinds, bool taken) => taken && kinds.Count == 1 ? kinds : Enumerable.Empty<string>();

        private static Dictionary<string, List<string>> _pickedFrom;
        private static Dictionary<string, List<string>> _fish;
        private static List<string> _hives;
        private static List<string> _taps;

        /// <summary>
        /// The game's prefabs by what the records count them by, worked out once: what gives each
        /// item by picking (<c>Pickable.m_itemPrefab</c>, as <c>Pickable.Interact</c> counts a pick by
        /// the item), the fish going by each name (<c>Fish.m_name</c>, as a catch is counted), and
        /// the kinds of beehive and sap collector. False while the game has no prefabs to read.
        /// </summary>
        private static bool Kinds()
        {
            if (_pickedFrom != null) return true;
            if (ZNetScene.instance == null) return false;
            var pickedFrom = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var fish = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var hives = new List<string>();
            var taps = new List<string>();
            void Add(Dictionary<string, List<string>> by, string key, string prefab)
            {
                if (string.IsNullOrEmpty(key)) return;
                if (!by.TryGetValue(key, out var list)) by[key] = list = new List<string>();
                if (!list.Contains(prefab)) list.Add(prefab);
            }
            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;
                var pickable = prefab.GetComponent<Pickable>();
                if (pickable != null && pickable.m_itemPrefab != null) Add(pickedFrom, pickable.m_itemPrefab.name, prefab.name);
                var swimmer = prefab.GetComponent<Fish>();
                if (swimmer != null) Add(fish, swimmer.m_name, prefab.name);
                if (prefab.GetComponent<Beehive>() != null) hives.Add(prefab.name);
                if (prefab.GetComponent<SapCollector>() != null) taps.Add(prefab.name);
            }
            (_pickedFrom, _fish, _hives, _taps) = (pickedFrom, fish, hives, taps);
            return true;
        }

        /// <summary>The game's prefabs as read for the records, read again in the next world, where mods may bring others.</summary>
        private static void Forget()
        {
            _pickedFrom = null;
            _fish = null;
            _hives = null;
            _taps = null;
        }

        private static void Copy(IEnumerable<string> from, HashSet<string> into)
        {
            into.Clear();
            foreach (var name in from) into.Add(name);
        }
    }
}
