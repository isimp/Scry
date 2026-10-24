using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Which of a creature's attack items an animation trigger starts. The game starts an attack
    /// by setting the trigger its item names (<c>Attack.Start</c>), which every player's game
    /// receives for the animation (<c>ZSyncAnimation.RPC_SetTrigger</c>), a chain's later steps by
    /// that trigger numbered (<see cref="ClipAttacks.ComboSteps"/>); so a trigger received tells
    /// which attack was seen begun, where only one of the creature's items goes by it.
    /// </summary>
    internal static class AttackTriggers
    {
        /// <summary>Each trigger's item, by trigger, from each item's attack animation and chain; a trigger two items share names none.</summary>
        public static Dictionary<string, string> Map(IEnumerable<(string Item, string Animation, int ChainLevels)> attacks)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (item, animation, chainLevels) in attacks)
            {
                if (string.IsNullOrEmpty(item) || string.IsNullOrEmpty(animation)) continue;
                Put(map, animation, item);
                foreach (var (_, trigger) in ClipAttacks.ComboSteps(animation, chainLevels, _ => true)) Put(map, trigger, item);
            }
            return map;
        }

        private static void Put(Dictionary<string, string> map, string trigger, string item)
        {
            if (!map.TryGetValue(trigger, out var held)) map[trigger] = item;
            else if (held != item) map[trigger] = null;
        }

        /// <summary>The item whose attack a trigger starts; none for a trigger that is no attack's or that two items share.</summary>
        public static string ItemFor(IReadOnlyDictionary<string, string> map, string trigger) =>
            map != null && trigger != null && map.TryGetValue(trigger, out var item) ? item : null;
    }
}
