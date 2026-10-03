using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>An attack played whole, each part where the game plays it: where it comes from, strikes and hits, and what it throws.</summary>
    internal static partial class Previews
    {
        /// <summary>An attack played whole: what plays as it begins, as it strikes, and where it hits.</summary>
        private sealed class WholeAttack
        {
            public EffectList Begin;
            public EffectList Trigger;
            public EffectList Hit;
        }

        private static readonly Dictionary<Attack, WholeAttack> Wholes = new Dictionary<Attack, WholeAttack>();
        private static readonly Dictionary<Attack, ItemDrop.ItemData.SharedData> WeaponOf = new Dictionary<Attack, ItemDrop.ItemData.SharedData>();

        /// <summary>
        /// All an attack plays, as the game plays it, made once per attack: as it begins, the
        /// weapon's and the attack's start and trail; as it strikes, their trigger; where it hits,
        /// their hit, or where there is no hit, their hit on the ground. What is thrown or shot hits
        /// with its own effects where it lands, so such an attack has no hit of its own
        /// (<c>Attack.ProjectileAttackTriggered</c> plays only the trigger).
        /// </summary>
        public static void AttackParts(Attack attack, out EffectList begin, out EffectList trigger, out EffectList hit)
        {
            if (!Wholes.TryGetValue(attack, out var whole))
            {
                WeaponOf.TryGetValue(attack, out var weapon);
                var owners = weapon != null ? new object[] { weapon, attack } : new object[] { attack };

                EffectList Join(params string[] fields)
                {
                    var data = new List<EffectList.EffectData>();
                    foreach (var owner in owners)
                    {
                        foreach (var field in CatalogBuilder.EffectFields(owner.GetType()))
                        {
                            if (System.Array.IndexOf(fields, field.Name) < 0 || !(TypeFields.Value(field, owner) is EffectList list) || list.m_effectPrefabs == null) continue;
                            data.AddRange(list.m_effectPrefabs.Where(d => d != null && d.m_enabled && d.m_prefab != null));
                        }
                    }
                    return new EffectList { m_effectPrefabs = data.ToArray() };
                }

                whole = new WholeAttack { Begin = Join("m_startEffect", "m_holdStartEffect", "m_trailStartEffect"), Trigger = Join("m_triggerEffect"), Hit = Join("m_hitEffect") };

                // Thrown, it hits where it lands; an attack with no hit of its own (a ground
                // slam) lands on the ground.
                if (attack.m_attackType == Attack.AttackType.Projectile) whole.Hit = new EffectList { m_effectPrefabs = System.Array.Empty<EffectList.EffectData>() };
                else if (whole.Hit.m_effectPrefabs.Length == 0)
                {
                    whole.Hit = Join("m_hitTerrainEffect");
                    OnGround.Add(whole.Hit);
                }
                Wholes[attack] = whole;
            }
            begin = whole.Begin;
            trigger = whole.Trigger;
            hit = whole.Hit;
        }

        /// <summary>
        /// Where an attack comes from on a copy, as <c>Attack.GetAttackOrigin</c> finds it: its
        /// origin joint in the body (the part the game calls "Visual", not an old model kept
        /// switched off beside it), else the copy itself.
        /// </summary>
        public static Transform AttackOrigin(GameObject copy, Attack attack)
        {
            var t = copy.transform;
            if (string.IsNullOrEmpty(attack.m_attackOriginJoint)) return t;
            var body = t.Find("Visual");
            var joint = Utils.FindChild(body != null ? body : t, attack.m_attackOriginJoint);
            return joint != null ? joint : t;
        }

        /// <summary>The hits of attacks that land on the ground there.</summary>
        private static readonly HashSet<EffectList> OnGround = new HashSet<EffectList>();

        /// <summary>
        /// Where an attack strikes from a copy, as <c>Attack.GetProjectileSpawnPoint</c> places its
        /// reach: from its origin joint or the body, up by its height, out by its range, aside by
        /// its offset; on the ground under that for what hits the ground. The game measures these
        /// in metres whatever the creature's own scale (a frost troll's is four), so they are only
        /// made bigger or smaller as the copy is shown so.
        /// </summary>
        public static Vector3 StrikePoint(GameObject copy, Attack attack, bool ground)
        {
            var point = IsMelee(attack) ? MeleeHitPoint(copy, attack) : ReachPoint(copy, attack);
            if (ground) point.y = copy.transform.position.y;
            return point;
        }

        /// <summary>Whether an attack is a swing, which hits along its sweep, rather than an area, a throw or nothing.</summary>
        public static bool IsMelee(Attack attack) =>
            attack.m_attackType == Attack.AttackType.Horizontal || attack.m_attackType == Attack.AttackType.Vertical;

        /// <summary>
        /// Where an area attack's middle is, and where a throw lets go (<c>Attack.DoAreaAttack</c>,
        /// <c>GetProjectileSpawnPoint</c>): from its origin joint, up by its height, out by its
        /// range, aside by its offset.
        /// </summary>
        public static Vector3 ReachPoint(GameObject copy, Attack attack)
        {
            var t = copy.transform;
            var size = SizeOf(copy);
            return AttackOrigin(copy, attack).position + t.up * attack.m_attackHeight * size + t.forward * attack.m_attackRange * size + t.right * attack.m_attackOffset * size;
        }

        /// <summary>
        /// Where a swing hits one it swings at (<c>Attack.DoMeleeAttack</c>): it sweeps out from
        /// its origin joint, up by its height and aside by its offset, as far as its range, and hits
        /// the first thing there. The creature swings once one is within its weapon's attack
        /// distance of it (<c>MonsterAI</c>, <c>m_aiAttackRange</c>), so that is taken to be where
        /// the one it hits stands, never beyond the sweep.
        /// </summary>
        private static Vector3 MeleeHitPoint(GameObject copy, Attack attack)
        {
            var t = copy.transform;
            var size = SizeOf(copy);
            var start = AttackOrigin(copy, attack).position + Vector3.up * attack.m_attackHeight * size + t.right * attack.m_attackOffset * size;
            var stands = WeaponOf.TryGetValue(attack, out var weapon) ? weapon.m_aiAttackRange : attack.m_attackRange;
            var ahead = Vector3.Dot(start - t.position, t.forward);
            var reach = Mathf.Clamp(stands * size - ahead, 0f, attack.m_attackRange * size);
            return start + t.forward * reach;
        }

        public static bool LandsOnGround(EffectList list) => OnGround.Contains(list);

        /// <summary>Throws or shoots a projectile copy on the stage or in the world, flying as <see cref="Flight"/> flies it.</summary>
        public static GameObject Launch(GameObject prefab, Vector3 start, Vector3 velocity, bool onStage, float size = 1f)
        {
            var copy = Ghost.Make(prefab, null, start, velocity.sqrMagnitude > 0.001f ? Quaternion.LookRotation(velocity) : Quaternion.identity);
            if (copy == null) return null;
            Ghost.Magnify(copy, size);

            // Shown so many times its size, it flies as far again in the same time: its speed
            // and its fall both so many times more.
            var projectile = prefab.GetComponentInChildren<Projectile>(true);
            var flight = copy.AddComponent<Flight>();
            flight.Velocity = velocity * size;
            flight.Gravity = (projectile != null ? projectile.m_gravity : 0f) * size;
            flight.Size = size;
            flight.Lifetime = projectile != null && projectile.m_ttl > 0f ? Mathf.Min(projectile.m_ttl, 8f) : 4f;
            flight.Burst = projectile?.m_hitEffects;
            flight.OnStage = onStage;
            if (onStage) Stage.Adopt(copy, flight.Lifetime + 1f);
            else Remember(copy, flight.Lifetime + 1f);
            return copy;
        }
    }
}
