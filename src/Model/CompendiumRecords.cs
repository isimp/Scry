using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the game itself keeps of a character, as plain names, for the compendium to catch up
    /// with (<see cref="CompendiumCatchUp"/>): the player's own records (<c>Player.m_knownMaterial</c>,
    /// <c>m_knownRecipes</c>, <c>m_knownStations</c>, <c>m_knownBiome</c>, <c>m_trophies</c>), the guardian power it chose and
    /// what its statistics count (<c>PlayerProfile.m_playerStats</c>). All are the game's name
    /// tokens (<c>$item_wood</c>) but trophies, things picked, what was picked from, taken from or caught, and the power, which are prefab names.
    /// </summary>
    internal sealed class CompendiumRecords
    {
        /// <summary>Items had in the inventory, or offered by a trader whether bought or not.</summary>
        public readonly HashSet<string> Materials = Names();

        /// <summary>Recipes and build pieces unlocked, by the item's or the piece's name.</summary>
        public readonly HashSet<string> Recipes = Names();

        /// <summary>Crafting stations used.</summary>
        public readonly HashSet<string> Stations = Names();

        /// <summary>
        /// The areas entered, each by its biome's name as the game showed it, with a name of its own or a
        /// prefix where the world gives one; those a save from before that kept by the biome's name token,
        /// the no-biome among them (<see cref="BiomeWords.NoBiome"/>).
        /// </summary>
        public readonly HashSet<string> Biomes = Names();

        /// <summary>Trophies held, by prefab.</summary>
        public readonly HashSet<string> Trophies = Names();

        /// <summary>Creatures killed, the character credited for each one it hurt.</summary>
        public readonly HashSet<string> Killed = Names();

        /// <summary>Items picked up.</summary>
        public readonly HashSet<string> PickedUp = Names();

        /// <summary>Items crafted.</summary>
        public readonly HashSet<string> Crafted = Names();

        /// <summary>Foods and meads taken.</summary>
        public readonly HashSet<string> Eaten = Names();

        /// <summary>Things picked (berries, mushrooms, flint), by the prefab of the item each gives.</summary>
        public readonly HashSet<string> Picked = Names();

        /// <summary>
        /// Things the character picked from, by prefab: each the one kind of thing that gives an
        /// item picked (<see cref="Picked"/>) by picking, so what gave that item, as the game counts
        /// picks by the item alone.
        /// </summary>
        public readonly HashSet<string> PickedFrom = Names();

        /// <summary>Producers the character took from, by prefab: the one kind of beehive where honey was taken, the one kind of sap collector where sap was, as the game counts those by what was taken alone.</summary>
        public readonly HashSet<string> Collected = Names();

        /// <summary>Items crafted (<see cref="Crafted"/>) whose whole count the game made for another player's taking food off a station this game ran (<see cref="CompendiumBook.CreditedApart"/>): none of the character's own making.</summary>
        public readonly HashSet<string> CraftedApart = Names();

        /// <summary>Cooking stations the character took food off, done or burnt, by prefab, as the game counts that by the kind of station.</summary>
        public readonly HashSet<string> Cooked = Names();

        /// <summary>Fish the character caught, by the prefab of the fish, as the game counts each catch by the fish's own name.</summary>
        public readonly HashSet<string> Caught = Names();

        /// <summary>Build pieces placed.</summary>
        public readonly HashSet<string> Placed = Names();

        /// <summary>The character's own keys: what it has done once, a boss's fall among them.</summary>
        public readonly HashSet<string> Uniques = Names();

        /// <summary>The guardian power chosen at a boss stone, by its status effect's prefab; none before one is chosen.</summary>
        public readonly HashSet<string> Powers = Names();

        /// <summary>Every kind of record, in a fixed order, so two records' sets are walked side by side.</summary>
        private HashSet<string>[] Sets() => new[] { Materials, Recipes, Stations, Biomes, Trophies, Killed, PickedUp, Crafted, Eaten, Picked, PickedFrom, Collected, Cooked, Caught, CraftedApart, Placed, Uniques, Powers };

        /// <summary>Takes every record of another in place of its own.</summary>
        public void CopyFrom(CompendiumRecords other)
        {
            var mine = Sets();
            var theirs = other.Sets();
            for (var i = 0; i < mine.Length; i++)
            {
                mine[i].Clear();
                mine[i].UnionWith(theirs[i]);
            }
        }

        /// <summary>
        /// What these records hold that those before did not, into <paramref name="added"/>, which
        /// is emptied first. False where reading only that would not tell what reading all of them
        /// tells: a biome entered or a power chosen anew changes what the older records say, and a
        /// record gone can take back what it told.
        /// </summary>
        public bool AddedSince(CompendiumRecords before, CompendiumRecords added)
        {
            var now = Sets();
            var was = before.Sets();
            var into = added.Sets();
            for (var i = 0; i < now.Length; i++)
            {
                into[i].Clear();
                foreach (var name in now[i]) if (!was[i].Contains(name)) into[i].Add(name);
                if (was[i].Count + into[i].Count != now[i].Count) return false;
            }
            return added.Biomes.Count == 0 && added.Powers.Count == 0;
        }

        private static HashSet<string> Names() => new HashSet<string>(StringComparer.Ordinal);
    }
}
