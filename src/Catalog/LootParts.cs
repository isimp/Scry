using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Which parts of a place are its chests and pickups (<see cref="Facts.IsLoot"/>), by their
    /// prefab's name, each told once for the world: a dungeon's rooms are copied over and over,
    /// and each copy's parts are looked at as it is made.
    /// </summary>
    internal static class LootParts
    {
        private static readonly Dictionary<string, bool> Known = new Dictionary<string, bool>(StringComparer.Ordinal);

        static LootParts() => WorldCaches.Register(nameof(LootParts), Forget);

        public static bool Is(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return false;
            if (!Known.TryGetValue(prefab, out var loot)) Known[prefab] = loot = Facts.IsLoot(prefab);
            return loot;
        }

        private static void Forget() => Known.Clear();
    }
}
