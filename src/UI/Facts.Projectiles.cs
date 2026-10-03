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
            var shot = _entry != null && _entry.Links.Any(l => l.Group == LinkBook.ShotFrom);
            Description = shot
                ? "Fired by an attack, it takes that attack's damage, knockback, blocking and status effect in place of its own below, and the attack's speed."
                : "Its speed comes from whatever launches it.";

            var spawnsTheDamage = projectile.m_spawnOnHit != null && projectile.m_onlySpawnedProjectilesDealDamage;
            var damage = Damages(projectile.m_damage);
            if (spawnsTheDamage) Add("Own damage", "none; what it spawns on hit deals the damage");
            else if (damage.Length > 0) Add("Own damage", damage);
            if (projectile.m_aoe > 0f) Add("Hits", $"everything within {Number(projectile.m_aoe)} m of where it lands");
            if (projectile.m_attackForce > 0f) Add("Knockback", Number(projectile.m_attackForce));

            var can = new List<string>();
            if (projectile.m_blockable) can.Add("blocked");
            if (projectile.m_dodgeable) can.Add("dodged");
            Add("Can be", can.Count > 0 ? string.Join(" or ", can) : "neither blocked nor dodged");

            if (!string.IsNullOrEmpty(projectile.m_statusEffect) && ObjectDB.instance != null)
            {
                var effect = ObjectDB.instance.GetStatusEffect(projectile.m_statusEffect.GetStableHashCode());
                if (effect != null) Add("On hit", EffectName(effect), "se:" + effect.name);
            }

            if (projectile.m_ttl > 0f) Add("Flies for", Naming.Duration(projectile.m_ttl));
            if (projectile.m_gravity != 0f) Add("Falls", $"{Number(projectile.m_gravity)} m/s²");
            else Add("Falls", "no, it flies straight");
            if (projectile.m_drag > 0f) Add("Slows", $"drag {Number(projectile.m_drag)}");
            if (projectile.m_bounce) Add("Bounces", projectile.m_maxBounces < 99 ? $"up to {projectile.m_maxBounces} times" : "yes");
            if (projectile.m_stayAfterHitStatic || projectile.m_stayAfterHitDynamic)
            {
                Add("After a hit", $"stays where it struck for {Naming.Duration(projectile.m_stayTTL)}");
            }
            if (projectile.m_respawnItemOnHit) Add("Leaves", "the weapon that threw it, where it lands");
            if (projectile.m_spawnOnHit != null && projectile.m_spawnOnHitChance < 1f)
            {
                Add("Spawns on hit", DropWords.Share(projectile.m_spawnOnHitChance) + " of the time");
            }
        }
    }
}
