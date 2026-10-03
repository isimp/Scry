using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    internal sealed partial class Facts
    {
        // ----- Projectiles -----

        /// <summary>
        /// A projectile's own numbers: what a hit does and over how wide, how it flies (how long,
        /// how it falls and slows, whether it bounces or stays), whether it can be blocked or
        /// dodged, and the status effect it puts on what it hits. An attack that fires it (a bow,
        /// a staff, a creature's throw) gives it the attack's damage, knockback, blocking and
        /// status effect instead (<c>Projectile.Setup</c>); its speed always comes from what fires it.
        /// </summary>
        private void Flight(Projectile projectile)
        {
            Description = ProjectileWords.Description(_entry != null && _entry.Links.Any(l => l.Group == LinkBook.ShotFrom));
            var onHit = !string.IsNullOrEmpty(projectile.m_statusEffect) && ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(projectile.m_statusEffect.GetStableHashCode()) : null;
            AddAll(ProjectileWords.Pairs(new ProjectileFacts
            {
                Damage = DamageFigures(projectile.m_damage),
                SpawnDealsTheDamage = projectile.m_spawnOnHit != null && projectile.m_onlySpawnedProjectilesDealDamage,
                AreaOfEffect = projectile.m_aoe,
                Knockback = projectile.m_attackForce,
                Blockable = projectile.m_blockable,
                Dodgeable = projectile.m_dodgeable,
                OnHit = onHit != null ? EffectName(onHit) : null,
                OnHitKey = onHit != null ? EntryKeys.For(Kind.StatusEffect, onHit.name) : null,
                FliesFor = projectile.m_ttl,
                Gravity = projectile.m_gravity,
                Drag = projectile.m_drag,
                Bounces = projectile.m_bounce,
                MaxBounces = projectile.m_maxBounces,
                StaysAfterHit = projectile.m_stayAfterHitStatic || projectile.m_stayAfterHitDynamic,
                StaysFor = projectile.m_stayTTL,
                LeavesTheWeapon = projectile.m_respawnItemOnHit,
                SpawnsOnHit = projectile.m_spawnOnHit != null,
                SpawnOnHitChance = projectile.m_spawnOnHitChance,
            }));
        }
    }
}
