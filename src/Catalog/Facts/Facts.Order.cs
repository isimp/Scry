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

        private static Foe? FoeOf(string prefab) => FoeOf(GamePrefabs.Item(prefab));

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

        /// <summary>Whether a place's part, by its prefab's name, is told among its chests and pickups (<see cref="PartRole.Loot"/>).</summary>
        public static bool IsLoot(string prefab) => PlaceParts.RoleOf(TraitsOf(GamePrefabs.Item(prefab))) == PartRole.Loot;

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
        /// The parts of a place by the row each is told in (<see cref="PlaceParts.RoleOf"/>), in
        /// the order of <see cref="PlaceParts.Roles"/>, each part's components read once.
        /// </summary>
        private static List<T>[] ByRow<T>(IEnumerable<T> parts, Func<T, string> prefab)
        {
            var rows = new List<T>[PlaceParts.Roles.Length];
            for (var r = 0; r < rows.Length; r++) rows[r] = new List<T>();
            foreach (var part in parts)
            {
                var role = PlaceParts.RoleOf(TraitsOf(GamePrefabs.Item(prefab(part))));
                for (var r = 0; r < rows.Length; r++)
                {
                    if (PlaceParts.Roles[r] == role) rows[r].Add(part);
                }
            }
            return rows;
        }

        /// <summary>
        /// The parts of a place told in one row, in that row's order: chests and pickups by what
        /// they can give (<see cref="ContentOrder.GiversFirst"/>), what is gathered hardest first,
        /// the rest and building pieces as given.
        /// </summary>
        /// <param name="mine">The place's parts told in the row (<see cref="ByRow{T}"/>).</param>
        /// <param name="prefab">Each part's prefab.</param>
        /// <param name="chance">How likely each is there, or how widespread among a dungeon's rooms.</param>
        /// <param name="role">The row's kind of part.</param>
        /// <param name="here">The place and all it holds, for what only it gives.</param>
        private static List<T> InRow<T>(List<T> mine, Func<T, string> prefab, Func<T, double> chance, PartRole role, ICollection<string> here)
        {
            switch (role)
            {
                case PartRole.Loot: return ContentOrder.GiversFirst(mine, chance, p => Gives(prefab(p), here).Only, p => Gives(prefab(p), here).Worth, p => Gives(prefab(p), here).Kinds);
                case PartRole.Gather: return ContentOrder.HardestFirst(mine, p => ToGather(GamePrefabs.Item(prefab(p))));
                default: return mine;
            }
        }
    }
}
