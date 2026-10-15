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
        private static AccessTools.FieldRef<Player, HashSet<string>> _materials;
        private static AccessTools.FieldRef<Player, HashSet<string>> _recipes;
        private static AccessTools.FieldRef<Player, Dictionary<string, int>> _stations;
        private static AccessTools.FieldRef<Player, HashSet<string>> _biomes;
        private static AccessTools.FieldRef<Player, HashSet<string>> _trophies;
        private static AccessTools.FieldRef<Player, HashSet<string>> _uniques;

        /// <summary>How many records there are in all, the power chosen one of them; it grows whenever any of them does, but a power chosen in place of another (<see cref="Power"/>).</summary>
        public static int Size(Player player, PlayerProfile profile)
        {
            Bind();
            var stats = profile.m_playerStats[0];
            return _materials(player).Count + _recipes(player).Count + _stations(player).Count + _biomes(player).Count + _trophies(player).Count + _uniques(player).Count
                + stats.m_enemyStats[0].Count + stats.m_itemPickupStats.Count + stats.m_itemCraftStats.Count + stats.m_foodEatenStats.Count
                + stats.m_pickableStats.Count + stats.m_piecesPlacedStats.Count
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

        private static void Copy(IEnumerable<string> from, HashSet<string> into)
        {
            into.Clear();
            foreach (var name in from) into.Add(name);
        }
    }
}
