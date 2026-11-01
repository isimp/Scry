using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The trees, logs, rocks, deposits and bushes of a catalog that are twins (<see cref="Twins"/>):
    /// going by one name, their variant's number aside, with the same health, tool tier, damage
    /// modifiers, what they give and what they break into, read from the game's prefabs once a catalog.
    /// </summary>
    internal static class ResourceTwins
    {
        static ResourceTwins() => WorldCaches.Register(nameof(ResourceTwins), Forget);

        private static EntryCatalog _of;
        private static Func<string, IReadOnlyList<string>> _twins;

        /// <summary>Each resource's twins in a catalog, worked out the first time it is asked.</summary>
        public static Func<string, IReadOnlyList<string>> For(EntryCatalog catalog)
        {
            if (catalog == null) return null;
            if (ReferenceEquals(catalog, _of)) return _twins;
            _of = catalog;
            _twins = Twins.Of(catalog.All.Where(e => e.Kind == Kind.Resource)
                .Select(e => (e.Key, e.ShownName, Signature: SignatureOf(GamePrefabs.Named(e.Name))))
                .Where(t => t.Signature != 0L));
            return _twins;
        }

        /// <summary>The catalog's twins, read again for the next world's catalog.</summary>
        private static void Forget()
        {
            _of = null;
            _twins = null;
        }

        /// <summary>A number standing for a prefab's values that a page of it tells as found out: alike only where they are; none for a prefab of none of the kinds.</summary>
        private static long SignatureOf(GameObject prefab)
        {
            if (prefab == null) return 0L;
            var sign = new Signature();
            var tree = prefab.GetComponent<TreeBase>();
            if (tree != null) sign.Kind(1).Add(tree.m_health).Add(tree.m_minToolTier).Add(tree.m_damageModifiers).Add(tree.m_dropWhenDestroyed);
            var log = prefab.GetComponent<TreeLog>();
            if (log != null) sign.Kind(2).Add(log.m_health).Add(log.m_minToolTier).Add(log.m_damages).Add(log.m_dropWhenDestroyed);
            var rock = prefab.GetComponent<MineRock>();
            if (rock != null) sign.Kind(3).Add(rock.m_health).Add(rock.m_minToolTier).Add(rock.m_damageModifiers).Add(rock.m_dropItems);
            var vein = prefab.GetComponent<MineRock5>();
            if (vein != null) sign.Kind(4).Add(vein.m_health).Add(vein.m_minToolTier).Add(vein.m_damageModifiers).Add(vein.m_dropItems);
            var breaks = prefab.GetComponent<Destructible>();
            if (breaks != null) sign.Kind(5).Add(breaks.m_health).Add(breaks.m_minToolTier).Add(breaks.m_damages).Add(breaks.m_spawnWhenDestroyed.OrNull()?.name);
            var dropping = prefab.GetComponent<DropOnDestroyed>();
            if (dropping != null) sign.Kind(6).Add(dropping.m_dropWhenDestroyed);
            var pickable = prefab.GetComponent<Pickable>();
            if (pickable != null) sign.Kind(7).Add(pickable.m_itemPrefab.OrNull()?.name).Add(pickable.m_amount).Add(pickable.m_respawnTimeMinutes).Add(pickable.m_extraDrops);
            return sign.Kinds == 0 ? 0L : sign.Value;
        }

        /// <summary>A running number over values added, alike for alike values in the same order.</summary>
        private sealed class Signature
        {
            public long Value = 1469598103934665603L;
            public int Kinds;

            public Signature Add(int value)
            {
                Value = unchecked((Value ^ value) * 1099511628211L);
                return this;
            }

            public Signature Add(float value) => Add(BitConverter.ToInt32(BitConverter.GetBytes(value), 0));

            public Signature Add(string value) => Add(string.IsNullOrEmpty(value) ? 0 : value.GetStableHashCode());

            /// <summary>A kind of part read, by its number, as the start of its values.</summary>
            public Signature Kind(int kind)
            {
                Kinds++;
                return Add(kind);
            }

            public Signature Add(HitData.DamageModifiers mods)
            {
                foreach (var cell in Facts.Cells(mods)) Add(cell.Value);
                return this;
            }

            public Signature Add(DropTable table)
            {
                if (table == null) return Add(0);
                Add(table.m_dropMin).Add(table.m_dropMax).Add(table.m_dropChance).Add(table.m_oneOfEach ? 1 : 0);
                foreach (var drop in table.m_drops ?? new List<DropTable.DropData>()) Add(drop.m_item.OrNull()?.name).Add(drop.m_stackMin).Add(drop.m_stackMax).Add(drop.m_weight);
                return this;
            }
        }
    }
}
