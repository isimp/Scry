using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>One attack of an item a creature carries: the item, the name the game shows for it (if any), its animation trigger, and a key to play it by.</summary>
    public sealed class AttackInfo
    {
        public readonly string Item;
        public readonly string Shown;
        public readonly string Trigger;
        public readonly bool Second;
        public readonly object Key;

        public AttackInfo(string item, string shown, string trigger, bool second, object key)
        {
            Item = item ?? "";
            Shown = string.IsNullOrEmpty(shown) ? null : shown;
            Trigger = trigger ?? "";
            Second = second;
            Key = key;
        }
    }

    /// <summary>A chip for an attack: what it is called and which attack it plays.</summary>
    public sealed class AttackChip
    {
        public string Label;
        public object Key;
    }

    /// <summary>
    /// How a creature's attacks are offered. The game gives creatures their attacks as items, many
    /// of them never drawn and named only for its makers ("slap", "fireballattack"), several at
    /// times playing the same animation. So there is one chip per animation the creature attacks
    /// with, called by the game's own name where it has one, else by the item's name made readable
    /// and without the creature's own name in it; a second attack says so, and attacks called
    /// alike are told apart by their item. What it may hold is only what shows in its hand.
    /// </summary>
    public static class AttackChips
    {
        public static List<AttackChip> For(string creature, IEnumerable<AttackInfo> attacks)
        {
            var chosen = new List<(AttackInfo Attack, string Label)>();
            var triggers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attack in attacks)
            {
                if (attack.Trigger.Length > 0 && !triggers.Add(attack.Trigger)) continue;
                var label = attack.Shown ?? Readable(attack.Item, creature);
                if (attack.Second) label += ", second attack";
                chosen.Add((attack, label));
            }

            var chips = new List<AttackChip>();
            foreach (var (attack, label) in chosen)
            {
                var alike = chosen.Count(c => c.Label == label) > 1;
                chips.Add(new AttackChip
                {
                    Label = alike ? $"{label} ({Readable(attack.Item, creature).ToLowerInvariant()})" : label,
                    Key = attack.Key,
                });
            }
            return chips;
        }

        /// <summary>
        /// An item's prefab name as words, leaving out the leading words that name the creature:
        /// "troll_log_swing_v" on a troll is "Log swing v", "trollsnow_punch" on a frost troll
        /// "Punch".
        /// </summary>
        public static string Readable(string item, string creature)
        {
            var own = Words(creature).Select(w => w.ToLowerInvariant()).ToList();
            var words = (item ?? "").Split(new[] { '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            while (words.Count > 1 && own.Any(o => words[0].ToLowerInvariant().Contains(o)))
            {
                words.RemoveAt(0);
            }
            var text = string.Join(" ", words.SelectMany(Words)).ToLowerInvariant();
            return text.Length > 0 ? char.ToUpperInvariant(text[0]) + text.Substring(1) : item ?? "";
        }

        /// <summary>The weapons that are choices to hold: those that show, one of each shown alike.</summary>
        public static List<string> Holdable(IEnumerable<(string Item, string Shown, bool Drawn)> weapons)
        {
            var held = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var weapon in weapons)
            {
                if (!weapon.Drawn || !seen.Add(weapon.Shown ?? weapon.Item)) continue;
                held.Add(weapon.Item);
            }
            return held;
        }

        /// <summary>A name split at its capitals and underscores: "TrollFrost" is "Troll", "Frost".</summary>
        private static IEnumerable<string> Words(string name)
        {
            var words = new List<string>();
            foreach (var part in (name ?? "").Split(new[] { '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var start = 0;
                for (var i = 1; i < part.Length; i++)
                {
                    if (char.IsUpper(part[i]) && char.IsLower(part[i - 1]))
                    {
                        words.Add(part.Substring(start, i - start));
                        start = i;
                    }
                }
                words.Add(part.Substring(start));
            }
            return words;
        }
    }
}
