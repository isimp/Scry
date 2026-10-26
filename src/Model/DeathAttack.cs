using System.Collections.Generic;

namespace Scry
{
    /// <summary>One thing a creature's death makes (<c>Character.m_deathEffects</c>), as far as it hurts what stands near.</summary>
    internal sealed class DeathPart
    {
        public string Prefab;

        /// <summary>What its area hit deals, by type (<c>Aoe.m_damage</c>); none for a thing without one.</summary>
        public (string Type, float Amount)[] Damage = System.Array.Empty<(string, float)>();

        /// <summary>What it deals to anything but a player (<c>HitData.DamageTypes.m_nonPlayer</c>).</summary>
        public float NonPlayer;

        /// <summary>The status effect it puts on what it hits, by name; null for none.</summary>
        public string Effect;

        public float Radius;

        /// <summary>It is a creature of its own, as the next phase of a boss is.</summary>
        public bool IsCreature;
    }

    /// <summary>
    /// What a creature's death does to what stands near, as one of its attacks: the first thing its
    /// death makes that hits all around with damage or an effect (<c>Aoe</c>), as the Writhan's and
    /// the lava blob's explosions do. A creature its death makes (a boss's next phase) is that
    /// creature's, never this one's attack.
    /// </summary>
    internal static class DeathAttack
    {
        public static DeathPart Pick(IEnumerable<DeathPart> parts)
        {
            foreach (var part in parts)
            {
                if (part == null || part.IsCreature) continue;
                if (part.NonPlayer > 0f || !string.IsNullOrEmpty(part.Effect)) return part;
                foreach (var (_, amount) in part.Damage) if (amount > 0f) return part;
            }
            return null;
        }
    }
}
