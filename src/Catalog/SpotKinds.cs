using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What each of the game's prefabs counts as for the spots a character's sight keeps
    /// (<see cref="SeenSpots"/>), worked out once a prefab from its parts: whether it is worth
    /// finding again (<see cref="SeenOnMap.Findable"/>: picked, mined or tapped, giving something besides
    /// wood and stone, not a crop grown from planting), not while it carries a beacon a Wishbone finds; and the kind it is kept
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

        /// <summary>What a sapling or a seed grows into once planted (<c>Plant.m_grownPrefabs</c>), and what the world places wild by its vegetation rules (<c>ZoneSystem.m_vegetation</c>), by name.</summary>
        private static HashSet<string> _grown, _wild;

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
            if (pickable != null) return SeenOnMap.Findable(false, Gathered.Picked, pickable.m_itemPrefab != null ? new[] { pickable.m_itemPrefab.name } : System.Array.Empty<string>(), Grown(prefab.name) && !Wild(prefab.name));
            if (prefab.GetComponent<ResourceRoot>() != null) return SeenOnMap.Findable(false, Gathered.Tapped, TappedFrom(prefab.name));
            var deposit = DepositOf(prefab);
            return deposit != null && SeenOnMap.Findable(false, Gathered.Mined, deposit.m_drops.Where(d => d.m_item != null).Select(d => d.m_item.name));
        }

        /// <summary>What the taps built on a root make of it: each tap that must stand on that root (<c>SapCollector.m_mustConnectTo</c>), by what it makes (<c>SapCollector.m_spawnItem</c>).</summary>
        private static IEnumerable<string> TappedFrom(string root)
        {
            foreach (var prefab in ZNetScene.instance != null ? ZNetScene.instance.m_prefabs : new List<GameObject>())
            {
                var tap = prefab != null ? prefab.GetComponent<SapCollector>() : null;
                if (tap != null && tap.m_mustConnectTo != null && tap.m_mustConnectTo.name == root && tap.m_spawnItem != null) yield return tap.m_spawnItem.gameObject.name;
            }
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
            _grown = null;
            _wild = null;
            Gardens.Clear();
        }

        /// <summary>Whether a prefab grows both wild and from planting, as a seed carrot, by its hash: its things are kept only off tilled ground (<see cref="InAGarden"/>).</summary>
        private static readonly Dictionary<int, bool> Gardens = new Dictionary<int, bool>();

        public static bool GrowsInGardens(int prefab)
        {
            if (Gardens.TryGetValue(prefab, out var grows)) return grows;
            var source = GamePrefabs.Hashed(prefab);
            grows = source != null && Grown(source.name) && Wild(source.name);
            Gardens[prefab] = grows;
            return grows;
        }

        /// <summary>Whether a thing of such a kind stands in a garden, on tilled ground where the game grows a planted one (<c>Heightmap.IsCultivated</c>).</summary>
        public static bool InAGarden(bool growsInGardens, Vector3 at)
        {
            if (!growsInGardens) return false;
            var ground = Heightmap.FindHeightmap(at);
            return SeenOnMap.InAGarden(true, ground != null && ground.IsCultivated(at));
        }

        /// <summary>Whether the world places a prefab wild by its vegetation rules.</summary>
        private static bool Wild(string name)
        {
            if (_wild == null)
            {
                // Read once the world's rules are in, not before.
                var zones = ZoneSystem.instance;
                if (zones == null || zones.m_vegetation == null) return false;
                _wild = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (var rule in zones.m_vegetation) if (rule != null && rule.m_enable && rule.m_prefab != null) _wild.Add(rule.m_prefab.name);
            }
            return _wild.Contains(name);
        }

        /// <summary>Whether a prefab is what a sapling or a seed grows into once planted, a crop.</summary>
        private static bool Grown(string name)
        {
            if (_grown == null)
            {
                _grown = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (var prefab in ZNetScene.instance != null ? ZNetScene.instance.m_prefabs : new List<GameObject>())
                {
                    var plant = prefab != null ? prefab.GetComponent<Plant>() : null;
                    if (plant == null || plant.m_grownPrefabs == null) continue;
                    foreach (var grown in plant.m_grownPrefabs)
                    {
                        if (grown != null) _grown.Add(grown.name);
                    }
                }
            }
            return _grown.Contains(name);
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
