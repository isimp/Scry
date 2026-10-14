using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Each entry's milestone tier (<see cref="TierPass"/>), from what Scry knows of how things are
    /// come by: the biomes a thing lives in; the places that hold it (a room the dungeons it is
    /// built into); whatever drops, yields, grows, makes or bears it, and a trader with the boss
    /// whose fall it waits for (its defeat key); each recipe's ingredients with its station; the
    /// spawners and raids that bring a creature; the owners of what is played or left behind; the
    /// givers of a status effect. Gathered on the main thread, worked out on a worker, again
    /// whenever the catalog's groups change, as when the world's places are read.
    /// </summary>
    internal static class CompendiumTiers
    {
        static CompendiumTiers() => WorldCaches.Register(nameof(CompendiumTiers), Forget);

        private static readonly UseKind[] Recipes = { UseKind.Crafts, UseKind.Builds, UseKind.TurnsInto };

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
            var recipes = new Dictionary<(string Target, string Place), List<string>>();

            foreach (var entry in catalog.All)
            {
                var key = entry.Key;
                if (entry.Kind == Kind.Biome) pass.Add(key, Tiers.OfBiome(entry.Name));
                foreach (var biome in entry.Biomes) pass.Add(key, Tiers.OfBiome(biome));
                foreach (var label in entry.FoundIn)
                {
                    if (label == null || !places.TryGetValue(label, out var holders)) continue;
                    foreach (var place in holders) if (place != entry) pass.Add(key, 0, place.Key);
                }
                foreach (var owner in entry.UsedBy) pass.Add(key, 0, owner);
                foreach (var owner in entry.LeftBy) pass.Add(key, 0, owner);
                foreach (var link in entry.Links)
                {
                    if (link.Group == Relations.GivenBy) pass.Add(key, 0, link.Target);
                }
                foreach (var brought in entry.TermLinks("spawns")) pass.Add(brought.Key, 0, key);

                if (EntryKeys.HasOwnNamespace(entry.Kind)) continue;
                foreach (var source in Knowledge.SourceLines(entry.Name))
                {
                    var record = source.Record;
                    var giver = record != null ? catalog.Find(record.Giver) : null;
                    if (giver == null || giver == entry) continue;
                    var tier = 0;
                    if (record.Way == SourceWay.Sold && record.Key.Length > 0 && !keyTiers.TryGetValue(record.Key, out tier)) continue;
                    pass.Add(key, tier, giver.Key);
                }
                foreach (var use in Knowledge.UsesOf(entry.Name))
                {
                    if (Array.IndexOf(Recipes, use.Kind) < 0) continue;
                    foreach (var (target, _) in use.Targets)
                    {
                        if (!recipes.TryGetValue((target, use.Place), out var needs)) recipes[(target, use.Place)] = needs = new List<string>();
                        needs.Add(key);
                    }
                }
            }

            // A recipe needs all its ingredients and its station, where that is a thing of its own.
            foreach (var recipe in recipes)
            {
                var station = recipe.Key.Place != null ? catalog.Find(recipe.Key.Place) : null;
                if (station != null) recipe.Value.Add(station.Key);
                pass.Add(recipe.Key.Target, 0, recipe.Value.ToArray());
            }
            return pass;
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
