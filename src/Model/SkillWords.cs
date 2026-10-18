using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// How a skill is named. One the game has goes by its name in the game's list. One a mod adds
    /// is only a number there, and its mod names it under the game's own word for a skill
    /// ("$skill_" and that number), which is where the game's skill screen looks too.
    /// </summary>
    internal static class SkillWords
    {
        public const string ModSkill = "a skill a mod adds";

        /// <summary>A skill's name from the game's name for it (or its number), or null for none.</summary>
        public static string Name(string skill, Func<string, string> localize)
        {
            if (string.IsNullOrEmpty(skill)) return null;
            if (!char.IsDigit(skill[0]) && skill[0] != '-') return Naming.FieldLabel(skill);
            var named = localize?.Invoke("$skill_" + skill);
            return string.IsNullOrEmpty(named) ? ModSkill : named;
        }

        /// <summary>The faint label of what something comes to at the character's own skill, by the level the game works with: "at Swords 34".</summary>
        public static string At(string skill, float level) => $"at {skill} {Numbers.Count((long)Math.Floor(level))}";

        /// <summary>A figure at the character's skill, on a line of its own under the game's: "at Blocking 20: 15"; null for none.</summary>
        public static string Yours(string at, string value) => value == null ? null : $"{at}: {value}";

        /// <summary>A range rounded to whole numbers, as the game's tooltip tells a hit's: "25–41", one number where they meet.</summary>
        public static string Range(float least, float most) => Numbers.CountRange((long)Math.Round(least), (long)Math.Round(most));

        /// <summary>Damage by type rolled between two shares of it, the biggest first: "25–41 slash, 5–8 fire"; null for none.</summary>
        public static string DamageRange(IEnumerable<(string Type, float Amount)> damage, float least, float most)
        {
            var parts = damage.Where(d => d.Amount > 0f).OrderByDescending(d => d.Amount).Select(d => $"{Range(d.Amount * least, d.Amount * most)} {d.Type}").ToList();
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>
        /// A weapon attack's line at the character's skill, under its line in the attacks table
        /// (<see cref="CombatWords.WeaponAttackColumns"/>): what it deals and knocks back, rolled
        /// between the skill's two shares, and what it costs; its stagger, the attack's own
        /// multiplier, is left blank, as the skill leaves it.
        /// </summary>
        public static string[] AttackLine(string at, IEnumerable<(string Type, float Amount)> damage, float knockback, (float Least, float Most) share, string costs) =>
            new[] { at, DamageRange(damage, share.Least, share.Most) ?? None, knockback > 0f ? Range(knockback * share.Least, knockback * share.Most) : None, "", costs ?? None };

        /// <summary>
        /// A weapon attack's lines at the character's skill, one for each step of its combo as
        /// the table has them (<see cref="CombatWords.WeaponCombo"/>): the last step's damage
        /// times its multiple, its knockback 1.2 times where that is more than 1.
        /// </summary>
        public static List<string[]> AttackLines(string at, int steps, IList<(string Type, float Amount)> damage, float knockback, float lastMultiplier, (float Least, float Most) share, string costs)
        {
            var lines = new List<string[]>();
            for (var step = 0; step < Math.Max(1, steps); step++)
            {
                var times = step == steps - 1 ? lastMultiplier : 1f;
                var push = times > 1f ? 1.2f : 1f;
                lines.Add(AttackLine(at, damage.Select(d => (d.Type, d.Amount * times)), knockback * push, share, costs));
            }
            return lines;
        }

        /// <summary>An axe's hits on trees at the skill trees take, a line for each step of its combo: what it deals, the rest blank, a tree being pushed by nothing.</summary>
        public static List<string[]> TreeLines(string at, int steps, IList<(string Type, float Amount)> damage, float lastMultiplier, (float Least, float Most) share)
        {
            var lines = new List<string[]>();
            for (var step = 0; step < Math.Max(1, steps); step++)
            {
                var times = step == steps - 1 ? lastMultiplier : 1f;
                lines.Add(new[] { at, DamageRange(damage.Select(d => (d.Type, d.Amount * times)), share.Least, share.Most) ?? None, "", "", "" });
            }
            return lines;
        }

        /// <summary>The label of an axe's hits on trees, which take the skill the axe names for them: "on trees, at Wood Cutting 12".</summary>
        public static string OnTrees(string at) => "on trees, " + at;

        /// <summary>What the faint lines at the character's skill say on hover.</summary>
        public static string YoursTip(string skill) =>
            $"What it comes to at your {skill} level now, with what effects add to the level. What you wear and other effects change it further.";

        private const string None = "–";
    }
}
