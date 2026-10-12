using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What a copy of a place was rolled to, noted while it still sleeps (<see cref="PlaceCopy"/>)
    /// and read with the copy once it stands: its creature spawn points that are there, and for
    /// its floors (<see cref="FloorProbe"/>), every object its rolls switched on or off, the parts
    /// another roll can bring, and the colliders the game's floor rays for what it drops pass
    /// through, known by their own layers, which the copy loses once awake.
    /// </summary>
    internal sealed class PlaceRoll
    {
        public readonly List<Stage.SpawnHere> Spawns = new List<Stage.SpawnHere>();
        public readonly HashSet<Transform> Switched = new HashSet<Transform>();
        public readonly HashSet<Collider> NotSolid = new HashSet<Collider>();
    }
}
