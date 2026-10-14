using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the game itself keeps of a character, as plain names, for the compendium to catch up
    /// with (<see cref="CompendiumCatchUp"/>): the player's own records (<c>Player.m_knownMaterial</c>,
    /// <c>m_knownRecipes</c>, <c>m_knownStations</c>, <c>m_knownBiome</c>, <c>m_trophies</c>), the guardian power it chose and
    /// what its statistics count (<c>PlayerProfile.m_playerStats</c>). All are the game's name
    /// tokens (<c>$item_wood</c>) but trophies, things picked and the power, which are prefab names.
    /// </summary>
    internal sealed class CompendiumRecords
    {
        /// <summary>Items had in the inventory, or offered by a trader whether bought or not.</summary>
        public readonly HashSet<string> Materials = Names();

        /// <summary>Recipes and build pieces unlocked, by the item's or the piece's name.</summary>
        public readonly HashSet<string> Recipes = Names();

        /// <summary>Crafting stations used.</summary>
        public readonly HashSet<string> Stations = Names();

        /// <summary>The areas entered, each by its biome's name, with a name of its own or a prefix where the world gives one.</summary>
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

        /// <summary>Build pieces placed.</summary>
        public readonly HashSet<string> Placed = Names();

        /// <summary>The character's own keys: what it has done once, a boss's fall among them.</summary>
        public readonly HashSet<string> Uniques = Names();

        /// <summary>The guardian power chosen at a boss stone, by its status effect's prefab; none before one is chosen.</summary>
        public readonly HashSet<string> Powers = Names();

        private static HashSet<string> Names() => new HashSet<string>(StringComparer.Ordinal);
    }
}
