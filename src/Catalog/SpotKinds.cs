using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What each of the game's prefabs counts as for the spots a character's sight keeps
    /// (<see cref="SeenSpots"/>), worked out once a prefab from its parts: whether it is worth
    /// finding again (<see cref="SeenOnMap.Findable"/>: picked or mined, giving something besides
    /// wood and stone), not while it carries a beacon a Wishbone finds; and the kind it is kept
    /// by, a deposit's broken form by the whole deposit that becomes it when first struck
    /// (<c>Destructible.m_spawnWhenDestroyed</c>), so a spot keeps its kind as the deposit breaks.
    /// </summary>
    internal static class SpotKinds
    {
        /// <summary>Leaving a world forgets what is worked out here (<see cref="WorldCaches"/>): another server's mods may give a deposit other drops.</summary>
        static SpotKinds() => WorldCaches.Register(nameof(SpotKinds), Forget);

        /// <summary>The kind each prefab is kept by, by its hash; none for one not worth finding again.</summary>
        private static readonly Dictionary<int, string> Kinds = new Dictionary<int, string>();

        /// <summary>The whole deposit each broken form comes of, by the broken form's name.</summary>
        private static Dictionary<string, string> _wholeOf;

        /// <summary>The kind a prefab's things are kept by as spots; none for one not worth finding again, or one a Wishbone finds.</summary>
        public static string Of(int prefab)
        {
            if (Kinds.TryGetValue(prefab, out var kind)) return kind;
            if (ZNetScene.instance == null) return null;
            // Taken as none first, so a prefab whose parts cannot be read is not read again each time one is destroyed.
            Kinds[prefab] = null;
            var source = GamePrefabs.Hashed(prefab);
            kind = source != null && Findable(source) ? WholeOf(source.name) : null;
            Kinds[prefab] = kind;
            return kind;
        }

        /// <summary>Whether a thing gone is only replaced where it stands: a whole deposit struck, becoming its broken form of the same kind.</summary>
        public static bool Replaced(int prefab)
        {
            var source = GamePrefabs.Hashed(prefab);
            var broken = source != null ? source.GetComponent<Destructible>().OrNull()?.m_spawnWhenDestroyed : null;
            return broken != null && WholeOf(broken.name) == source.name;
        }

        /// <summary>Whether a prefab is worth finding again and carries no beacon a Wishbone finds.</summary>
        private static bool Findable(GameObject prefab)
        {
            if (!SeenOnMap.Records(prefab.GetComponentInChildren<Beacon>(true) != null)) return false;
            var pickable = prefab.GetComponent<Pickable>();
            if (pickable != null) return SeenOnMap.Findable(false, Gathered.Picked, pickable.m_itemPrefab != null ? new[] { pickable.m_itemPrefab.name } : System.Array.Empty<string>());
            var deposit = DepositOf(prefab);
            return deposit != null && SeenOnMap.Findable(false, Gathered.Mined, deposit.m_drops.Where(d => d.m_item != null).Select(d => d.m_item.name));
        }

        /// <summary>What a deposit gives when mined: its own drops, those of the broken form a whole deposit becomes, or what one only a pickaxe breaks drops; none for anything else.</summary>
        private static DropTable DepositOf(GameObject prefab)
        {
            var mine5 = prefab.GetComponent<MineRock5>();
            if (mine5 != null) return mine5.m_dropItems;
            var mine = prefab.GetComponent<MineRock>();
            if (mine != null) return mine.m_dropItems;
            var breaks = prefab.GetComponent<Destructible>();
            if (breaks == null) return null;
            var broken = breaks.m_spawnWhenDestroyed;
            if (broken != null) return broken.GetComponent<Destructible>() == null ? DepositOf(broken) : null;
            // A tin or obsidian deposit breaks whole under the pickaxe alone and drops what it gives.
            var dropping = prefab.GetComponent<DropOnDestroyed>();
            return dropping != null && OnlyAPickaxeBreaks(breaks.m_damages) ? dropping.m_dropWhenDestroyed : null;
        }

        private static bool OnlyAPickaxeBreaks(HitData.DamageModifiers mods) =>
            SeenOnMap.OnlyAPickaxeBreaks((Degree)(int)mods.m_pickaxe, new[]
            {
                (Degree)(int)mods.m_blunt, (Degree)(int)mods.m_slash, (Degree)(int)mods.m_pierce, (Degree)(int)mods.m_chop,
                (Degree)(int)mods.m_fire, (Degree)(int)mods.m_frost, (Degree)(int)mods.m_lightning, (Degree)(int)mods.m_poison, (Degree)(int)mods.m_spirit,
            });

        private static void Forget()
        {
            Kinds.Clear();
            _wholeOf = null;
        }

        /// <summary>The kind a prefab is kept by: the whole deposit a broken form comes of, else its own name.</summary>
        private static string WholeOf(string name)
        {
            if (_wholeOf == null)
            {
                _wholeOf = new Dictionary<string, string>(System.StringComparer.Ordinal);
                foreach (var prefab in ZNetScene.instance != null ? ZNetScene.instance.m_prefabs : new List<GameObject>())
                {
                    var broken = prefab != null ? prefab.GetComponent<Destructible>().OrNull()?.m_spawnWhenDestroyed : null;
                    if (broken != null && broken != prefab && !_wholeOf.ContainsKey(broken.name)) _wholeOf[broken.name] = prefab.name;
                }
            }
            return _wholeOf.TryGetValue(name, out var whole) ? whole : name;
        }
    }
}
