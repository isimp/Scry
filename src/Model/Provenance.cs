using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Tells the game's own prefabs from those a mod added. The names the scene holds when it
    /// wakes are the game's; mods register theirs afterwards. An effect only reached through other
    /// prefabs' effect lists belongs to the game when any game prefab uses it.
    /// </summary>
    public sealed class Provenance
    {
        /// <summary>Stands for the game's own interface as the user of an effect.</summary>
        public const string Interface = "(interface)";

        private HashSet<string> _original;

        /// <summary>Records the names present before any mod added its own.</summary>
        public void RecordOriginal(IEnumerable<string> names)
        {
            _original = new HashSet<string>(names, StringComparer.Ordinal);
        }

        /// <summary>Adds names the game itself adds a step later, from lists of its own.</summary>
        public void AddOriginal(IEnumerable<string> names)
        {
            if (_original == null) _original = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names) _original.Add(name);
        }

        public Origin Of(string name)
        {
            if (_original == null) return Origin.Unknown;
            return _original.Contains(name) ? Origin.Vanilla : Origin.Mod;
        }

        /// <summary>
        /// The origin of something from the origins of what uses it: the game's when any user is
        /// the game's, a mod's when every user is known and none is, otherwise not claimed.
        /// </summary>
        public static Origin Combine(IEnumerable<Origin> users)
        {
            var any = false;
            var unknown = false;
            foreach (var origin in users)
            {
                if (origin == Origin.Vanilla) return Origin.Vanilla;
                if (origin == Origin.Unknown) unknown = true;
                any = true;
            }
            return any && !unknown ? Origin.Mod : Origin.Unknown;
        }
    }
}
