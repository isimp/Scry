using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>A way a kind's tab groups its list: its own (creatures by faction, items by type), by biome, or by what taming allows.</summary>
    internal enum GroupLens
    {
        Own,
        Biome,
        Taming,
    }

    /// <summary>The group a thing is listed under in a way of grouping, its group's place among the others, and how many biomes more it has where grouped by biome.</summary>
    internal struct ListGroup
    {
        public string Name;
        public int Order;
        public int More;
    }

    /// <summary>
    /// The other ways a kind's tab can group its list (<see cref="GroupLens"/>), one at a time, each
    /// thing under one heading: creatures by biome or by what taming allows, items by biome; by
    /// biome under the first biome players reach, the rest counted on its row.
    /// </summary>
    internal static class ListGrouping
    {
        public const string Ridden = "Ridden";
        public const string TamedAndBred = "Tamed and bred";
        public const string Tamed = "Tamed";
        public const string Summoned = "Summoned";
        public const string NeverTamed = "Never tamed";
        public const string NoBiome = "No biome";
        public const string NotMetInABiome = "Not met in a biome yet";

        private static readonly GroupLens[] CreatureLenses = { GroupLens.Own, GroupLens.Biome, GroupLens.Taming };
        private static readonly GroupLens[] ItemLenses = { GroupLens.Own, GroupLens.Biome };
        private static readonly GroupLens[] OwnOnly = { GroupLens.Own };

        /// <summary>The ways a kind's tab can group, its own first; its own alone for a kind with no other.</summary>
        public static IReadOnlyList<GroupLens> Lenses(Kind kind) =>
            kind == Kind.Creature ? CreatureLenses : kind == Kind.Item ? ItemLenses : (IReadOnlyList<GroupLens>)OwnOnly;

        /// <summary>A way of grouping as its switch names it.</summary>
        public static string LensName(GroupLens lens, Kind kind)
        {
            switch (lens)
            {
                case GroupLens.Biome: return "Biome";
                case GroupLens.Taming: return "Taming";
                default: return kind == Kind.Creature ? "Faction" : kind == Kind.Item ? "Type" : "Group";
            }
        }

        /// <summary>
        /// A creature by what taming allows, the most first: ridden (it takes a saddle), tamed and
        /// bred, tamed, summoned (of the players' faction, the player aside), else never tamed.
        /// </summary>
        public static ListGroup Taming(Entry entry)
        {
            if (Has(entry.Deeds, Deed.Ridden)) return new ListGroup { Name = Ridden, Order = 1 };
            if (Has(entry.Deeds, Deed.Tamed)) return Has(entry.Deeds, Deed.Born) ? new ListGroup { Name = TamedAndBred, Order = 2 } : new ListGroup { Name = Tamed, Order = 3 };
            if (entry.Group == Groups.PlayersGroup && Array.IndexOf(entry.Components, "Player") < 0) return new ListGroup { Name = Summoned, Order = 4 };
            return new ListGroup { Name = NeverTamed, Order = 5 };
        }

        /// <summary>
        /// A thing by its biomes: under the first players reach, by its shown name, the others
        /// counted; none under <paramref name="none"/>, last.
        /// </summary>
        public static ListGroup Biome(IReadOnlyList<string> biomes, Func<string, string> shown, string none)
        {
            var first = -1;
            string firstBiome = null;
            foreach (var biome in biomes)
            {
                var rank = LocationWords.BiomeRank(biome);
                if (rank < 0) rank = LocationWords.BiomeCount;
                if (firstBiome == null || rank < first)
                {
                    first = rank;
                    firstBiome = biome;
                }
            }
            if (firstBiome == null) return new ListGroup { Name = none, Order = LocationWords.BiomeCount + 2 };
            return new ListGroup { Name = shown(firstBiome), Order = first + 1, More = biomes.Count - 1 };
        }

        /// <summary>Whether a group starts folded away: what can never be tamed.</summary>
        public static bool FoldedAtFirst(GroupLens lens, string group) => lens == GroupLens.Taming && group == NeverTamed;

        /// <summary>A row's note of the biomes more it has than the one it is grouped under: "+2 biomes".</summary>
        public static string MoreBiomes(int more) => more <= 0 ? null : "+" + Numbers.Count(more) + (more == 1 ? " biome" : " biomes");

        private static bool Has(Deed[] deeds, Deed deed) => Array.IndexOf(deeds, deed) >= 0;
    }
}
