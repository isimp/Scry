using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The chains a world's prefabs make, read once a world (again once the locations add
    /// altars): a creature's parents, its egg or young and what that grows into; a seed, what it
    /// is planted as, what that grows into and what is picked from it; a boss's offering, its
    /// altar, the boss, its trophy and the power the trophy gives.
    /// </summary>
    internal static class Chains
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Chains() => WorldCaches.Register(nameof(Chains), Forget);

        private static readonly ChainBook Book = new ChainBook();

        /// <summary>The catalog the chains were read for, and how many altars were known then.</summary>
        private static EntryCatalog _readFor;
        private static int _altars = -1;

        /// <summary>The chains a prefab or key is a step of.</summary>
        public static List<Chain> Of(string key)
        {
            var catalog = WorldCatalog.Current;
            if (catalog == null) return new List<Chain>();
            var altars = Knowledge.Summons().Count();
            if (!ReferenceEquals(catalog, _readFor) || altars != _altars)
            {
                _readFor = catalog;
                _altars = altars;
                Read(catalog);
            }
            return Book.Of(key);
        }

        public static void Forget()
        {
            Book.Clear();
            _readFor = null;
            _altars = -1;
        }

        private static void Read(EntryCatalog catalog)
        {
            Book.Clear();
            var prefabs = catalog.All.Select(e => e.Source as GameObject).Where(p => p != null).ToList();

            // A creature's parents to what their young grows into; an egg nothing lays from itself.
            foreach (var parent in prefabs.Where(p => p.GetComponent<Procreation>() != null)) Book.Add(ChainWords.Breeding, ChainBook.Walk(parent.name, Bred));
            foreach (var egg in prefabs.Where(p => p.GetComponent<EggGrow>() != null && Book.Of(p.name).Count == 0)) Book.Add(ChainWords.Breeding, ChainBook.Walk(egg.name, Bred));

            // A seed to the crop: what is planted, what it grows into and what is picked from it,
            // led by the seed it is planted with.
            foreach (var planted in prefabs.Where(p => p.GetComponent<Plant>() != null))
            {
                var steps = ChainBook.Walk(planted.name, Grown);
                var seeds = planted.GetComponent<Piece>().OrNull()?.m_resources?.Where(r => r?.m_resItem != null).Select(r => r.m_resItem.gameObject.name).Distinct().ToArray();
                if (seeds != null && seeds.Length > 0) steps.Insert(0, seeds);
                Book.Add(ChainWords.Planting, steps);
            }

            // An offering to the power: the altar it is offered at, the boss, the trophy it drops and what that gives.
            foreach (var summon in Knowledge.Summons())
            {
                if (summon.Item == null || summon.Boss == null) continue;
                var steps = new List<string[]> { new[] { summon.Item } };
                if (!string.IsNullOrEmpty(summon.PlacePrefab)) steps.Add(new[] { summon.PlacePrefab });
                steps.Add(new[] { summon.Boss });
                var boss = GamePrefabs.Named(summon.Boss);
                var drops = boss != null ? boss.GetComponent<CharacterDrop>().OrNull()?.m_drops : null;
                var trophy = drops?.Select(d => d?.m_prefab).FirstOrDefault(p => p != null && Knowledge.PowerOf(p.name).Power != null);
                if (trophy != null)
                {
                    steps.Add(new[] { trophy.name });
                    steps.Add(new[] { EntryKeys.For(Kind.StatusEffect, Knowledge.PowerOf(trophy.name).Power) });
                }
                Book.Add(ChainWords.Summoning, steps);
            }
        }

        /// <summary>What a creature's parents have, an egg hatches into, or a young one grows into.</summary>
        private static IReadOnlyList<string> Bred(string name)
        {
            var prefab = GamePrefabs.Named(name);
            if (prefab == null) return null;
            var offspring = prefab.GetComponent<Procreation>().OrNull()?.m_offspring;
            if (offspring != null) return new[] { offspring.name };
            var hatches = prefab.GetComponent<EggGrow>().OrNull()?.m_grownPrefab;
            if (hatches != null) return new[] { hatches.name };
            var grow = prefab.GetComponent<Growup>();
            return grow != null ? Knowledge.GrownOf(grow).Where(g => g != null).Select(g => g.name).ToList() : null;
        }

        /// <summary>What a planted thing grows into, or what is picked from a grown one.</summary>
        private static IReadOnlyList<string> Grown(string name)
        {
            var prefab = GamePrefabs.Named(name);
            if (prefab == null) return null;
            var plant = prefab.GetComponent<Plant>();
            if (plant != null) return plant.m_grownPrefabs?.Where(g => g != null).Select(g => g.name).ToList();
            var picked = prefab.GetComponent<Pickable>().OrNull()?.m_itemPrefab;
            return picked != null ? new[] { picked.name } : null;
        }
    }
}
