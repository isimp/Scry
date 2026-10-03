using System;

namespace Scry
{
    /// <summary>
    /// The game's own console command that spawns an entry, or gives an item, for pasting. It is
    /// the vanilla <c>spawn</c> command: name, amount, level, and <c>p</c> to pick the spawned items
    /// up. The game reads the level as a creature's level (up to 9, one star per level above 1)
    /// or an item's quality (up to 4). Using it needs cheats, which the game only allows the host.
    /// </summary>
    internal static class SpawnCommand
    {
        public const int MaxCreatureLevel = 9;
        public const int MaxItemQuality = 4;

        /// <summary>The command, or null for what the spawn command cannot make.</summary>
        public static string For(Entry entry, int amount, int level, bool give)
        {
            if (entry == null || !entry.Registered || entry.Kind == Kind.StatusEffect) return null;

            amount = Math.Max(1, amount);
            var isItem = entry.Kind == Kind.Item;
            var maxLevel = isItem ? MaxItemQuality : entry.Kind == Kind.Creature ? MaxCreatureLevel : 1;
            level = Math.Max(1, Math.Min(maxLevel, level));

            if (isItem && give) return $"spawn {entry.Name} {Numbers.Count(amount)} {Numbers.Count(level)} p";
            if (amount == 1 && level == 1) return "spawn " + entry.Name;
            return $"spawn {entry.Name} {Numbers.Count(amount)} {Numbers.Count(level)}";
        }
    }
}
