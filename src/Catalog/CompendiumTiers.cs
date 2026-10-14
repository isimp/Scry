using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Each entry's milestone tier (<see cref="TierPass"/>), from what Scry knows of how things are
    /// come by: the biomes a thing lives in; the places that hold it (a room the dungeons it is
    /// built into); whatever drops, yields, grows, makes or bears it, and a trader with the boss
    /// whose fall it waits for (its defeat key); each recipe, piece cost and station's making a
    /// way of its own (<see cref="TierWays"/>); the spawners and raids that bring a creature, a
    /// raid no sooner than what it waits for; the owners of what is played or left behind; the
    /// givers of a status effect. Gathered on the main thread, worked out on a worker, again
    /// whenever the catalog's groups change, as when the world's places are read.
    /// </summary>
    internal static class CompendiumTiers
    {
        static CompendiumTiers() => WorldCaches.Register(nameof(CompendiumTiers), Forget);


        private static EntryCatalog _for;
        private static int _forRegroups = -1;
        private static Task<Dictionary<string, int>> _working;
        private static Dictionary<string, int> _tiers;

        /// <summary>Counts up each time new tiers are in, so what uses them can tell.</summary>
        public static int Version { get; private set; }

        /// <summary>The tiers worked out for a catalog as it is now; the ones before, or none, while they are being worked out.</summary>
        public static IReadOnlyDictionary<string, int> Of(EntryCatalog catalog)
        {
            if (catalog == null) return null;
            if (_working != null && _working.IsCompleted)
            {
                var working = _working;
                _working = null;
                if (working.Status == TaskStatus.RanToCompletion)
                {
                    _tiers = working.Result;
                    Version++;
                }
                else Faults.Tell(Feature.CompendiumTiers, "working out the tiers", working.Exception?.GetBaseException());
            }
            if (_working == null && (!ReferenceEquals(catalog, _for) || catalog.Regroups != _forRegroups))
            {
                _for = catalog;
                _forRegroups = catalog.Regroups;
                var pass = Gather(catalog);
                _working = Task.Run(pass.Work);
            }
            return ReferenceEquals(catalog, _for) ? _tiers : null;
        }

        /// <summary>The ways to come by each entry of a catalog, read from what Scry knows of it.</summary>
        public static TierPass Gather(EntryCatalog catalog)
        {
            var pass = new TierPass();
            var places = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
            foreach (var entry in catalog.All)
            {
                var label = Places.LabelOf(entry);
                if (label == null) continue;
                if (!places.TryGetValue(label, out var list)) places[label] = list = new List<Entry>();
                list.Add(entry);
            }
            var keyTiers = DefeatKeys(catalog);
            string KeyOf(string name) => catalog.Find(name)?.Key;

            foreach (var entry in catalog.All)
            {
                var key = entry.Key;
                if (entry.Kind == Kind.Biome) pass.Add(WayFrom.Biome, key, Tiers.OfBiome(entry.Name));
                if (entry.Kind == Kind.Raid && entry.Source is RandomEvent raid) TierWays.Raid(pass, key, entry.Biomes.Select(Tiers.OfBiome), Ready(raid), keyTiers, KeyOf, entry.TermLinks("spawns").Select(b => b.Key));
                else foreach (var biome in entry.Biomes) pass.Add(WayFrom.Biome, key, Tiers.OfBiome(biome));
                foreach (var label in entry.FoundIn)
                {
                    if (label == null || !places.TryGetValue(label, out var holders)) continue;
                    foreach (var place in holders) if (place != entry) pass.Add(WayFrom.Place, key, 0, place.Key);
                }
                foreach (var owner in entry.UsedBy) pass.Add(WayFrom.Played, key, 0, owner);
                foreach (var owner in entry.LeftBy) pass.Add(WayFrom.LeftBehind, key, 0, owner);
                foreach (var link in entry.Links)
                {
                    // A boss stone gives its power only for the trophy it takes.
                    if (link.Group == Relations.GivenBy) TierWays.Given(pass, key, link.Target, Knowledge.TrophiesFor(link.Target, entry.Name), KeyOf);
                }
                foreach (var brought in entry.TermLinks("spawns")) pass.Add(WayFrom.Spawned, brought.Key, 0, key);

                if (EntryKeys.HasOwnNamespace(entry.Kind)) continue;
                foreach (var source in Knowledge.SourceLines(entry.Name))
                {
                    var record = source.Record;
                    var giver = record != null ? catalog.Find(record.Giver) : null;
                    if (giver == null || giver == entry) continue;
                    var tier = 0;
                    if (record.Way == SourceWay.Sold && record.Key.Length > 0 && !keyTiers.TryGetValue(record.Key, out tier)) continue;
                    pass.Add(WayFrom.Source, key, tier, giver.Key);
                }
                // What stations turn into it, each item they take a way of its own where any one will do.
                foreach (var making in Knowledge.MadeOf(entry.Name))
                {
                    foreach (var way in TierWays.Of(making)) TierWays.Making(pass, way, KeyOf, WayFrom.Station);
                }
            }

            // Each recipe and piece cost a way of its own: all it takes to make it once and its station; a piece with each tool that builds it.
            foreach (var way in Knowledge.CraftWays)
            {
                if (way.Built) TierWays.Building(pass, way, Knowledge.Tools.ToolsOf(way.Target).Select(u => u.Tool), KeyOf);
                else TierWays.Making(pass, way, KeyOf);
            }
            return pass;
        }

        /// <summary>The ways a raid becomes ready, read from what it waits for.</summary>
        private static List<RaidReady> Ready(RandomEvent raid) => TierWays.Ready(Knowledge.ByEachPlayer(raid), raid.m_requiredGlobalKeys,
            raid.m_altRequiredKnownItems?.Where(i => i != null).Select(i => i.gameObject.name), raid.m_altRequiredPlayerKeysAny, raid.m_altRequiredPlayerKeysAll);

        /// <summary>Each tier's boss as a character's records know it, the first tier first: its name as kills are counted, its defeat key, its trophy (what it drops named Trophy).</summary>
        public static List<BossMarks> Bosses(EntryCatalog catalog)
        {
            var bosses = new List<BossMarks>();
            for (var tier = 1; tier <= Tiers.Last; tier++)
            {
                var boss = catalog.Find(Tiers.BossOf(tier));
                var character = boss?.Source is GameObject prefab ? prefab.GetComponent<Character>().OrNull() : null;
                string trophy = null;
                if (boss != null)
                {
                    foreach (var drop in boss.TermLinks("drops"))
                    {
                        if (drop.Name.StartsWith("Trophy", StringComparison.Ordinal)) trophy = drop.Name;
                    }
                }
                bosses.Add(character != null ? new BossMarks(character.m_name, character.m_defeatSetGlobalKey, trophy) : new BossMarks(null, null, trophy));
            }
            return bosses;
        }

        /// <summary>The tier each boss's defeat key closes, read from the bosses' own creatures.</summary>
        public static Dictionary<string, int> DefeatKeys(EntryCatalog catalog)
        {
            var keys = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var tier = 1; tier <= Tiers.Last; tier++)
            {
                var boss = catalog.Find(Tiers.BossOf(tier));
                var key = boss?.Source is GameObject prefab ? prefab.GetComponent<Character>().OrNull()?.m_defeatSetGlobalKey : null;
                if (!string.IsNullOrEmpty(key) && !keys.ContainsKey(key)) keys[key] = tier;
            }
            return keys;
        }

        private static void Forget()
        {
            _for = null;
            _forRegroups = -1;
            _working = null;
            _tiers = null;
        }
    }
}
