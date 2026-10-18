using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What a copy of a place was rolled to, noted while it still sleeps (<see cref="PlaceCopy"/>)
    /// and read with the copy once it stands: its creature spawn points that are there, and for
    /// its floors (<see cref="FloorProbe"/>), every object its rolls switched on or off, the parts
    /// another roll can bring, and the colliders the game's floor rays for what it drops pass
    /// through, known by their own layers, which the copy loses once awake. Where wanted, as for
    /// the card of a dungeon's room gone into, the chests and pickups the rolls left there, each
    /// with the chance its page gives it.
    /// </summary>
    internal sealed class PlaceRoll
    {
        public readonly List<Stage.SpawnHere> Spawns = new List<Stage.SpawnHere>();
        public readonly HashSet<Transform> Switched = new HashSet<Transform>();
        public readonly HashSet<Collider> NotSolid = new HashSet<Collider>();

        /// <summary>Whether the chests and pickups left there are noted (<see cref="Loot"/>).</summary>
        public bool WantsLoot;

        /// <summary>
        /// A room of the world's own dungeon, shown by its layout alone: its creatures, chests and
        /// pickups are the world's, which Scry does not read, so none stands in the copy and no
        /// creature is rolled; its chance parts still pick one look each.
        /// </summary>
        public bool LayoutOnly;

        public readonly List<(string Prefab, float Chance)> Loot = new List<(string, float)>();
    }
}
