using System.Collections.Generic;

namespace Scry
{
    /// <summary>What the probe saw an animator play: for each attack trigger, and for each of the game's own actions.</summary>
    internal sealed class Probed
    {
        public readonly Dictionary<string, IReadOnlyList<string>> Triggers = new Dictionary<string, IReadOnlyList<string>>();
        public readonly Dictionary<string, IReadOnlyList<string>> Actions = new Dictionary<string, IReadOnlyList<string>>();

        /// <summary>The clips it plays when left alone, over several seeds, since it picks among them at random.</summary>
        public readonly HashSet<string> Idle = new HashSet<string>();

        /// <summary>Still being watched: what is here so far is only part.</summary>
        public bool Busy;

        /// <summary>The clips it moves, crouches, blocks and jumps in under each stance, by the stance's number (<see cref="HeldClips"/>); only from a sweep of its stances.</summary>
        public readonly Dictionary<int, HashSet<string>> Stances = new Dictionary<int, HashSet<string>>();

        /// <summary>The clips each switch an item names plays (drawing a bow, reloading), by the switch.</summary>
        public readonly Dictionary<string, HashSet<string>> Holds = new Dictionary<string, HashSet<string>>();

        /// <summary>The clips each run of the sweep saw, whatever the stance (<see cref="ClipGroups.MoveVisit"/> and the others).</summary>
        public readonly Dictionary<string, HashSet<string>> Visits = new Dictionary<string, HashSet<string>>();

        /// <summary>The clips each switch or trigger of a known use leads to (<see cref="ClipGroups.OfSwitch"/>), by its name.</summary>
        public readonly Dictionary<string, HashSet<string>> Switched = new Dictionary<string, HashSet<string>>();
    }
}
