using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Attacks seen begun on a game that does not run their creature, whose trigger several of its
    /// weapons share (<see cref="SwingsPending"/>): told by the weapon in the creature's hand once
    /// its data caught up, or by what the attack put into the world beside it. What an attack puts
    /// there is read from its weapon's projectile, as <c>Attack.FireProjectileBurst</c> makes it at
    /// the caster: an area that every game sees wake (an <c>Aoe</c> with a network view of its
    /// own, as a Dvergr mage's shield), or the creatures a summon places round its caster
    /// (<c>SpawnAbility</c> aimed at the caster, within its spawn radius, as a Dvergr mage's Mistiles).
    /// </summary>
    internal static partial class CompendiumSight
    {
        /// <summary>How near its caster an area an attack makes wakes, in metres: the caster's attack origin, on its own body.</summary>
        private const float AreaNear = 3f;

        /// <summary>How much farther than a summon's own spawn radius its creatures are looked for, in metres, as the ground they are snapped to shifts them.</summary>
        private const float SummonSlack = 1f;

        private static readonly SwingsPending Swings = new SwingsPending();

        /// <summary>The creatures whose attacks wait, by the number each was begun under.</summary>
        private static readonly Dictionary<long, Character> SwingCreatures = new Dictionary<long, Character>();

        /// <summary>What each creature prefab's weapons put into the world beside it, worked out once each.</summary>
        private static readonly Dictionary<string, (string Item, string Made, float Near)[]> Makes = new Dictionary<string, (string, string, float)[]>(StringComparer.Ordinal);

        private static readonly Func<long, string> HandOf = id => SwingCreatures.TryGetValue(id, out var creature) && creature != null ? CompendiumKeeper.WeaponOf(creature as Humanoid).OrNull()?.name : null;

        /// <summary>An attack begun whose trigger several weapons share, on a game not running its creature: kept to be told.</summary>
        private static void BeginSwing(Character creature, string key, IReadOnlyList<string> candidates, float away)
        {
            var id = (long)creature.GetInstanceID();
            SwingCreatures[id] = creature;
            var at = creature.transform.position;
            Swings.Begin(id, key, candidates, MakesOf(key), new Vec3(at.x, at.y, at.z), away, Time.unscaledTime);
        }

        /// <summary>The attacks the hand tells now, each found out; called each frame while any wait.</summary>
        private static void TickSwings()
        {
            foreach (var (creature, item, away) in Swings.Tick(HandOf, Time.unscaledTime)) SwingTold(creature, item, away, IsBoss(creature));
            if (Swings.Count == 0) SwingCreatures.Clear();
        }

        /// <summary>A thing woke while attacks wait: the one it tells, if any, found out.</summary>
        private static void SwingMade(ZNetView thing)
        {
            Guard.Run(Feature.CompendiumSeen, "telling an attack by what it made", () =>
            {
                var at = thing.transform.position;
                var told = Swings.Woke(Utils.GetPrefabName(thing.gameObject), new Vec3(at.x, at.y, at.z), Time.unscaledTime);
                if (told.HasValue) SwingTold(told.Value.Creature, told.Value.Item, told.Value.Away, IsBoss(told.Value.Creature));
                if (Swings.Count == 0) SwingCreatures.Clear();
            });
        }

        private static bool IsBoss(string creature) => GamePrefabs.Named(creature).OrNull()?.GetComponent<Character>().OrNull()?.IsBoss() ?? false;

        /// <summary>
        /// What a creature prefab's weapons put into the world beside it: each weapon's attack
        /// projectile that is an area with a network view of its own and no flight, near its caster
        /// (<see cref="AreaNear"/>); each creature a summon aimed at its caster places, within the
        /// summon's spawn radius and <see cref="SummonSlack"/>.
        /// </summary>
        private static (string Item, string Made, float Near)[] MakesOf(string prefabName)
        {
            if (Makes.TryGetValue(prefabName, out var made)) return made;
            var found = new List<(string, string, float)>();
            var prefab = GamePrefabs.Named(prefabName);
            if (prefab != null)
            {
                foreach (var item in Relations.CarriedItems(prefab))
                {
                    var shared = item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
                    if (shared == null) continue;
                    foreach (var attack in new[] { shared.m_attack, shared.m_secondaryAttack })
                    {
                        var thrown = attack?.m_attackProjectile;
                        if (thrown == null) continue;
                        if (thrown.GetComponent<ZNetView>() != null && thrown.GetComponent<Aoe>() != null && thrown.GetComponent<Projectile>() == null) found.Add((item.name, thrown.name, AreaNear));
                        var summon = thrown.GetComponent<SpawnAbility>();
                        if (summon == null || summon.m_targetType != SpawnAbility.TargetType.Caster || summon.m_spawnPrefab == null) continue;
                        foreach (var spawned in summon.m_spawnPrefab)
                        {
                            if (spawned != null) found.Add((item.name, spawned.name, summon.m_spawnRadius + SummonSlack));
                        }
                    }
                }
            }
            return Makes[prefabName] = found.ToArray();
        }

        /// <summary>The weapons a creature prefab's trigger may start (<see cref="AttackTriggers.Candidates"/>); for the self-test.</summary>
        [ForTest]
        public static IReadOnlyList<string> TriggerCandidates(string prefabName, string trigger) => AttackTriggers.Candidates(TriggersOf(prefabName), trigger);

        /// <summary>Every trigger of a creature prefab with the weapons it may start; for the self-test.</summary>
        [ForTest]
        public static IReadOnlyDictionary<string, string[]> TriggerMap(string prefabName) => TriggersOf(prefabName);

        /// <summary>What a creature prefab's weapons put into the world beside it (<see cref="MakesOf"/>); for the self-test.</summary>
        [ForTest]
        public static IReadOnlyList<(string Item, string Made, float Near)> MadeBy(string prefabName) => MakesOf(prefabName);

        /// <summary>Lets the attacks waiting and what is known of what weapons make go, with the world.</summary>
        private static void ForgetSwings()
        {
            Swings.Clear();
            SwingCreatures.Clear();
            Makes.Clear();
        }
    }
}
