using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Where a thing shown on the stage stands when its ground is chosen automatically
    /// (<see cref="StageGround.HomeOf"/>), gathered from the catalog: the biomes it spawns or grows
    /// in, of the places that hold it, of what drops or yields it, of the creatures a raid brings,
    /// its tier of progress, and whether it is a ship.
    /// </summary>
    internal static class GroundHomes
    {
        /// <summary>
        /// The biome a thing stands in, in a catalog with its tiers as far as they are worked out;
        /// a kind with no ground of its own where another kind of its family has one
        /// (<see cref="StageGround.HomeOfKind"/>); Meadows for nothing.
        /// </summary>
        public static string Of(Entry entry, EntryCatalog catalog)
        {
            if (entry == null) return "Meadows";
            var tiers = catalog != null ? CompendiumTiers.Of(catalog) : null;
            var kinds = entry.Family?.Kinds.Where(k => k != entry).Select(k => HomeFor(k, catalog, tiers));
            return StageGround.HomeOfKind(HomeFor(entry, catalog, tiers), kinds);
        }

        /// <summary>What a thing's ground is worked out from, by itself.</summary>
        private static StageGround.Home HomeFor(Entry entry, EntryCatalog catalog, IReadOnlyDictionary<string, int> tiers)
        {
            return new StageGround.Home
            {
                Own = entry.Biomes,
                Places = catalog != null ? PlaceBiomes(entry, catalog) : null,
                Givers = entry.GiverBiomes,
                Raid = entry.Kind == Kind.Raid,
                Brought = entry.Kind == Kind.Raid ? entry.TermLinks("spawns").SelectMany(e => e.Biomes).Distinct().ToArray() : null,
                Tier = tiers != null && tiers.TryGetValue(entry.Key, out var tier) ? tier : Tiers.None,
                Floats = entry.Source is GameObject prefab && prefab != null && Floats(prefab),
            };
        }

        /// <summary>The biomes of the places that hold it (<see cref="Places.LabelOf"/>), once the world's places are read.</summary>
        private static string[] PlaceBiomes(Entry entry, EntryCatalog catalog)
        {
            if (entry.Kind == Kind.Location || entry.FoundIn.Length == 0) return null;
            var labels = new HashSet<string>(entry.FoundIn, StringComparer.Ordinal);
            var biomes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var place in catalog.All)
            {
                var label = Places.LabelOf(place);
                if (label != null && labels.Contains(label)) biomes.UnionWith(place.Biomes);
            }
            return biomes.ToArray();
        }

        /// <summary>Whether a prefab floats on the sea as a ship (<c>Ship</c>).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Floats(GameObject prefab) => prefab.GetComponent<Ship>() != null;
    }
}
