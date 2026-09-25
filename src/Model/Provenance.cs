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

        public bool Recorded => _original != null;

        public Origin Of(string name)
        {
            if (_original == null) return Origin.Unknown;
            return _original.Contains(name) ? Origin.Vanilla : Origin.Mod;
        }

        /// <summary>The origin of an effect, from the prefabs that use it.</summary>
        public Origin OfEffect(IEnumerable<string> usedBy)
        {
            if (_original == null) return Origin.Unknown;

            var any = false;
            foreach (var user in usedBy)
            {
                any = true;
                if (user == Interface || _original.Contains(user)) return Origin.Vanilla;
            }
            return any ? Origin.Mod : Origin.Unknown;
        }
    }
}
