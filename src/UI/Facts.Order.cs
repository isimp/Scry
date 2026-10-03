using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What content lists are ordered by, read from the prefabs (<see cref="ContentOrder"/>): a
    /// creature's toughness, how hard something is to gather, and which row of a place's page a
    /// part belongs in.
    /// </summary>
    internal sealed partial class Facts
    {
        /// <summary>A creature as creatures are ordered by: a boss or not, and its health; null for what is no creature.</summary>
        private static Foe? FoeOf(GameObject prefab)
        {
            var character = prefab != null ? prefab.GetComponent<Character>() : null;
            return character != null ? new Foe(character.m_boss, character.m_health) : (Foe?)null;
        }

        private static Foe? FoeOf(string prefab) => FoeOf(Looks.Prefab(prefab));

        /// <summary>
        /// How hard it is to gather: the tool tier it needs and its health (a piece's, for what is
        /// mined a piece at a time); a shell breaking into a vein as the vein, at the higher tier
        /// of the two. Null for what is not felled, mined or broken.
        /// </summary>
        private static (int Tier, float Health)? ToGather(GameObject prefab)
        {
            if (prefab == null) return null;
            var tree = prefab.GetComponent<TreeBase>();
            if (tree != null) return (tree.m_minToolTier, tree.m_health);
            var log = prefab.GetComponent<TreeLog>();
            if (log != null) return (log.m_minToolTier, log.m_health);
            var rock = prefab.GetComponent<MineRock>();
            if (rock != null) return (rock.m_minToolTier, rock.m_health);
            var vein = prefab.GetComponent<MineRock5>();
            if (vein != null) return (vein.m_minToolTier, vein.m_health);
            var breaks = prefab.GetComponent<Destructible>();
            if (breaks == null) return null;
            var inside = Knowledge.MinedInside(breaks.m_spawnWhenDestroyed);
            var inner = inside != null && inside != prefab ? ToGather(inside) : null;
            return inner != null ? (Math.Max(breaks.m_minToolTier, inner.Value.Tier), inner.Value.Health) : (breaks.m_minToolTier, breaks.m_health);
        }

        /// <summary>What a place's part has, as its components tell, for the row it is told in (<see cref="PlaceParts.RoleOf"/>).</summary>
        private static PartTraits TraitsOf(GameObject prefab)
        {
            if (prefab == null) return default;
            bool Has<T>() where T : Component => prefab.GetComponentInChildren<T>(true) != null;
            return new PartTraits
            {
                Container = Has<Container>(),
                Pickup = Has<Pickable>() || Has<PickableItem>() || prefab.GetComponent<ItemDrop>() != null,
                Gathered = Has<MineRock>() || Has<MineRock5>() || Has<TreeBase>() || Has<TreeLog>(),
                Breaks = Has<Destructible>() || Has<DropOnDestroyed>(),
                Built = Has<Piece>() || Has<WearNTear>(),
                Used = Has<CraftingStation>() || Has<Smelter>() || Has<Fermenter>() || Has<CookingStation>() || Has<Beehive>() || Has<SapCollector>()
                       || Has<Bed>() || Has<Fireplace>() || Has<OfferingBowl>() || Has<ItemStand>() || Has<Vegvisir>() || Has<RuneStone>()
                       || Has<Teleport>() || Has<TeleportWorld>() || Has<Trader>() || Has<Turret>() || Has<ShieldGenerator>() || Has<Incinerator>(),
                Spawns = Has<SpawnArea>() || Has<CreatureSpawner>() || Has<Character>(),
            };
        }

        /// <summary>
        /// The parts of a place told in one row, in that row's order: chests and pickups rarest
        /// first, what is gathered hardest first, the rest and building pieces as given.
        /// </summary>
        private static List<T> InRow<T>(IEnumerable<T> parts, Func<T, string> prefab, Func<T, double> chance, PartRole role)
        {
            var mine = parts.Where(p => PlaceParts.RoleOf(TraitsOf(Looks.Prefab(prefab(p)))) == role);
            switch (role)
            {
                case PartRole.Loot: return ContentOrder.RarestFirst(mine, chance);
                case PartRole.Gather: return ContentOrder.HardestFirst(mine, p => ToGather(Looks.Prefab(prefab(p))));
                default: return mine.ToList();
            }
        }
    }
}
