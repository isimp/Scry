using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Which of a creature's attack items an animation trigger starts. The game starts an attack
    /// by setting the trigger its item names (<c>Attack.Start</c>), which every player's game
    /// receives for the animation (<c>ZSyncAnimation.RPC_SetTrigger</c>), a chain's later steps by
    /// that trigger numbered (<see cref="ClipAttacks.ComboSteps"/>); so a trigger received tells
    /// which attack was seen begun, where only one of the creature's weapons goes by it. Only
    /// weapons take part, as the game attacks with nothing else (<c>Humanoid.EquipBestWeapon</c>
    /// skips every item that is no weapon). A trigger several weapons share is told by the one
    /// the creature holds, as the game equips the weapon it attacks with before the attack
    /// begins and every game reads it from the creature's data (<c>VisEquipment.SetRightItem</c>).
    /// </summary>
    internal static class AttackTriggers
    {
        /// <summary>The weapons each trigger starts, by trigger, from each weapon's attack animation and chain.</summary>
        public static Dictionary<string, string[]> Map(IEnumerable<(string Item, string Animation, int ChainLevels, bool Weapon)> attacks)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var (item, animation, chainLevels, weapon) in attacks)
            {
                if (!weapon || string.IsNullOrEmpty(item) || string.IsNullOrEmpty(animation)) continue;
                Put(map, animation, item);
                foreach (var (_, trigger) in ClipAttacks.ComboSteps(animation, chainLevels, _ => true)) Put(map, trigger, item);
            }
            var done = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var each in map) done[each.Key] = each.Value.ToArray();
            return done;
        }

        /// <summary>The weapons each trigger starts, every item taken as a weapon.</summary>
        public static Dictionary<string, string[]> Map(IEnumerable<(string Item, string Animation, int ChainLevels)> attacks)
        {
            var all = new List<(string, string, int, bool)>();
            foreach (var (item, animation, chainLevels) in attacks) all.Add((item, animation, chainLevels, true));
            return Map(all);
        }

        private static void Put(Dictionary<string, List<string>> map, string trigger, string item)
        {
            if (!map.TryGetValue(trigger, out var items)) map[trigger] = items = new List<string>();
            if (!items.Contains(item)) items.Add(item);
        }

        /// <summary>The weapons a trigger may start, in the order the creature carries them; none for a trigger that is no attack's.</summary>
        public static IReadOnlyList<string> Candidates(IReadOnlyDictionary<string, string[]> map, string trigger) =>
            map != null && trigger != null && map.TryGetValue(trigger, out var items) ? items : Array.Empty<string>();

        /// <summary>
        /// The weapon whose attack a trigger starts: the one weapon going by it, else the one among
        /// them the creature holds (<paramref name="held"/>); none for a trigger that is no attack's
        /// or a hand holding none of them.
        /// </summary>
        public static string ItemFor(IReadOnlyDictionary<string, string[]> map, string trigger, string held = null) => Resolve(Candidates(map, trigger), held);

        /// <summary>The one weapon of several a hand tells (<see cref="ItemFor"/>).</summary>
        public static string Resolve(IReadOnlyList<string> candidates, string held)
        {
            if (candidates == null || candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];
            if (held == null) return null;
            foreach (var item in candidates) if (item == held) return item;
            return null;
        }
    }
}
