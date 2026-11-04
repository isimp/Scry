using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What a creature's hit does to the character, as the game takes it (<c>Character.RPC_Damage</c>):
    /// scaled first by the world (its enemy damage rate, and the difficulty for the players near),
    /// then each damage type by the character's own modifier of it (<c>HitData.ApplyResistance</c>),
    /// then all of the hit the armour covers together by the character's armour
    /// (<c>HitData.ApplyArmor</c>): a hit more than twice the armour loses the armour, a smaller
    /// one becomes its square over four times the armour. The game's flat damage passes both;
    /// chop and pickaxe are resisted, a player ignoring both, and armour does not cover them.
    /// </summary>
    internal static class HitTaken
    {
        /// <summary>The damage types armour covers (<c>HitData.DamageTypes.ApplyArmor</c>), by the names a hit's figures go by.</summary>
        private static readonly HashSet<string> Armoured = new HashSet<string>(StringComparer.Ordinal)
        {
            "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit",
        };

        /// <summary>The game's flat damage, past every modifier and the armour.</summary>
        private const string Flat = "true";

        /// <summary>A hit after armour, as <c>HitData.ApplyArmor</c> works it out: under twice the armour, its square over four times the armour; past it, less the armour.</summary>
        public static float ArmourCut(float hit, float armour)
        {
            if (armour <= 0f || hit <= 0f) return Math.Max(0f, hit);
            return armour < hit / 2f ? hit - armour : Math.Min(1f, hit / (armour * 4f)) * hit;
        }

        /// <summary>
        /// What a hit by type does to the character: each type scaled by the world's
        /// <paramref name="scale"/> and by the character's <paramref name="factor"/> for it (1 where
        /// it takes it as it comes), the types armour covers then cut together by
        /// <paramref name="armour"/>, the rest added as they are.
        /// </summary>
        public static float Taken(IEnumerable<(string Type, float Amount)> damage, float scale, Func<string, float> factor, float armour)
        {
            var armoured = 0f;
            var past = 0f;
            foreach (var (type, amount) in damage ?? Array.Empty<(string, float)>())
            {
                if (amount <= 0f) continue;
                var scaled = amount * scale;
                if (type == Flat) past += scaled;
                else if (Armoured.Contains(type)) armoured += scaled * factor(type);
                else past += scaled * factor(type);
            }
            return ArmourCut(armoured, armour) + past;
        }

        /// <summary>What a creature's hit does to the character at the least and the most of its roll (<see cref="SkillMath.CreatureHitShare"/>).</summary>
        public static (float Least, float Most) Range(IEnumerable<(string Type, float Amount)> damage, float scale, Func<string, float> factor, float armour)
        {
            var share = SkillMath.CreatureHitShare;
            var hit = damage == null ? new List<(string, float)>() : new List<(string, float)>(damage);
            return (Taken(hit, scale * share.Least, factor, armour), Taken(hit, scale * share.Most, factor, armour));
        }

        /// <summary>How much harder a creature hits with so many stars: half again a star (<c>Attack.GetLevelDamageFactor</c>).</summary>
        public static float StarScale(int stars) => 1f + Math.Max(0, stars) * 0.5f;

        /// <summary>What a damage modifier leaves of a type, by the game's name of it (<c>HitData.ApplyModifier</c>); 1 for one it does not know.</summary>
        public static float Factor(string modifier)
        {
            switch (modifier)
            {
                case "SlightlyResistant": return 0.75f;
                case "Resistant": return 0.5f;
                case "VeryResistant": return 0.25f;
                case "SlightlyWeak": return 1.25f;
                case "Weak": return 1.5f;
                case "VeryWeak": return 2f;
                case "Immune":
                case "Ignore":
                    return 0f;
                default: return 1f;
            }
        }
    }
}
