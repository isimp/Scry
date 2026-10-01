using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>One line of what is known, and the prefab it names, if it names one.</summary>
    internal struct Source
    {
        public string Text;
        public string Prefab;

        public Source(string text, string prefab)
        {
            Text = text;
            Prefab = prefab;
        }
    }

    /// <summary>
    /// An altar's offering: the boss it summons (<c>OfferingBowl.m_bossPrefab</c>), the item and
    /// how many of it are offered (<c>m_bossItem</c>, <c>m_bossItems</c>), and where it stands, a
    /// location's name or the altar's own prefab where it is one.
    /// </summary>
    internal struct Summon
    {
        public string Boss;
        public string Item;
        public int Count;
        public string Place;
        public string PlacePrefab;

        public static Summon Of(OfferingBowl bowl, string place, string placePrefab)
        {
            return new Summon
            {
                Boss = bowl.m_bossPrefab != null ? bowl.m_bossPrefab.name : null,
                Item = bowl.m_bossItem != null ? bowl.m_bossItem.gameObject.name : null,
                Count = Math.Max(1, bowl.m_bossItems),
                Place = place,
                PlacePrefab = placePrefab,
            };
        }
    }

    /// <summary>
    /// What the game knows about a prefab beyond its own components: where it spawns or grows,
    /// and which mod added it. Gathered once with the catalog.
    /// </summary>
    internal static partial class Knowledge
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Knowledge() => WorldCaches.Register(nameof(Knowledge), Begin);

        private static readonly Dictionary<string, List<Source>> Where = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, HashSet<string>> BiomesOf = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ModOf = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<Source>> ComesFrom = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly HashSet<string> PlacedByWorld = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, FieldInfo[]> DropTableFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Lines saying what drops or yields an item, or none.</summary>
        public static IReadOnlyList<Source> SourceLines(string item)
        {
            return ComesFrom.TryGetValue(item, out var lines) ? lines : (IReadOnlyList<Source>)Array.Empty<Source>();
        }

        /// <summary>Lines saying where a prefab spawns or grows, or none.</summary>
        public static IReadOnlyList<Source> WhereLines(string prefab)
        {
            return Where.TryGetValue(prefab, out var lines) ? lines : (IReadOnlyList<Source>)Array.Empty<Source>();
        }

        public static string[] Biomes(string prefab)
        {
            return BiomesOf.TryGetValue(prefab, out var set) ? set.ToArray() : new string[0];
        }

        /// <summary>Whether the world places it by itself: its spawn lists (cinder rain, fireflies) or its vegetation.</summary>
        public static bool IsPlacedByWorld(string prefab) => PlacedByWorld.Contains(prefab);

        public static string ModName(string name)
        {
            return ModOf.TryGetValue(name, out var mod) ? mod : "";
        }

        // Read prefab by prefab, then put together in the order the lines were always told in.
        private static readonly Dictionary<string, List<Source>> SpawnPointLines = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<Source>> DropLines = new Dictionary<string, List<Source>>(StringComparer.Ordinal);

        /// <summary>What stations make from what, told on both the item and the station.</summary>
        private static readonly MakerBook Made = new MakerBook();

        /// <summary>The ways an item is made at a station.</summary>
        public static IReadOnlyList<Making> MadeOf(string item) => Made.Of(item);

        /// <summary>What a station makes.</summary>
        public static IReadOnlyList<Making> MadeAt(string station) => Made.At(station);
        private static readonly Dictionary<GameObject, string> ShownNames = new Dictionary<GameObject, string>();

        /// <summary>
        /// The creature whose defeat sets each world key (<c>Character.m_defeatSetGlobalKey</c>): a
        /// boss's, and some others' (a troll sets "KilledTroll"), so a key reads as that creature defeated.
        /// </summary>
        private static readonly Dictionary<string, GameObject> Bosses = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The bosses by the event each names (<c>Character.m_bossEvent</c>): the game has that event
        /// on while the boss's health bar shows (<c>RandEventSystem.GetForcedEvent</c>), for the
        /// fight's music and weather.
        /// </summary>
        private static readonly Dictionary<string, GameObject> BossEvents = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        /// <summary>
        /// The station that takes items past their top quality with the upgrade kits their recipes
        /// name (<c>CraftingStation.m_upgrader</c>), and its name as the game shows it; null when
        /// no prefab has one.
        /// </summary>
        public static string UpgradeStation { get; private set; }

        public static string UpgradeStationName { get; private set; }

        /// <summary>
        /// What turns into each prefab when felled, split or broken open: the trees that fall as a
        /// log, the logs that split into a half, the shells that break into a vein
        /// (<c>TreeBase.SpawnLog</c>, <c>TreeLog.Destroy</c>, <c>Destructible.Destroy</c>).
        /// </summary>
        private static readonly Dictionary<string, List<GameObject>> Turned = new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);

        /// <summary>What turns into a prefab when felled, split or broken open, or none.</summary>
        public static IReadOnlyList<GameObject> TurnedFrom(string prefab)
        {
            return Turned.TryGetValue(prefab, out var from) ? from : (IReadOnlyList<GameObject>)Array.Empty<GameObject>();
        }

        /// <summary>What a prefab turns into when broken, if that is mined (a vein, a rock), or null.</summary>
        public static GameObject MinedInside(GameObject broken)
        {
            if (broken == null) return null;
            return broken.GetComponent<MineRock5>() != null || broken.GetComponent<MineRock>() != null ? broken : null;
        }

        /// <summary>Notes what the prefab turns into, from its own components (not its parts').</summary>
        private static void Turns(GameObject prefab, List<Component> components)
        {
            void Note(GameObject into)
            {
                if (into == null) return;
                if (!Turned.TryGetValue(into.name, out var list)) Turned[into.name] = list = new List<GameObject>();
                if (!list.Contains(prefab)) list.Add(prefab);
            }
            foreach (var component in components)
            {
                if (component == null || component.gameObject != prefab) continue;
                switch (component)
                {
                    case TreeBase tree: Note(tree.m_logPrefab); break;
                    case TreeLog log: Note(log.m_subLogPrefab); break;
                    case Destructible breaks: Note(MinedInside(breaks.m_spawnWhenDestroyed)); break;
                }
            }
        }

        /// <summary>The altars among the registered prefabs, and what each summons.</summary>
        private static readonly List<Summon> Altars = new List<Summon>();

        /// <summary>Every altar known: the registered ones, and those the locations hold once read.</summary>
        public static IEnumerable<Summon> Summons() => Altars.Concat(Locations.Summons);

        /// <summary>The name shown for the creature whose defeat sets a world key, or null.</summary>
        public static string BossOf(string key) => key != null && Bosses.TryGetValue(key, out var boss) ? ShownName(boss) : null;

        /// <summary>The health of the creature whose defeat sets a world key, or 0: how far along the game that key comes.</summary>
        public static float BossHealthOf(string key) => key != null && Bosses.TryGetValue(key, out var boss) && boss.GetComponent<Character>() is Character character ? character.m_health : 0f;

        /// <summary>The boss whose fight an event is, or null.</summary>
        public static GameObject BossOfEvent(string raid) => raid != null && BossEvents.TryGetValue(raid, out var boss) ? boss : null;

        /// <summary>The prefab of the creature whose defeat sets a world key, or null.</summary>
        public static string BossPrefabOf(string key) => key != null && Bosses.TryGetValue(key, out var boss) ? boss.name : null;

        /// <summary>Which build tools build which pieces, on which tab, noted as the list is grouped.</summary>
        public static readonly ToolBook Tools = new ToolBook();

        /// <summary>What each item is used for, noted as the catalog is read.</summary>
        private static readonly UseBook Uses = new UseBook();

        /// <summary>What an item is used for: recipes, pieces, what stations turn it into, what burns it and what eats it.</summary>
        public static IReadOnlyList<UseGroup> UsesOf(string item) => Uses.Of(item);

        /// <summary>Starts reading it all again for the current world.</summary>
        public static void Begin()
        {
            Where.Clear();
            BiomesOf.Clear();
            ModOf.Clear();
            ComesFrom.Clear();
            PlacedByWorld.Clear();
            GiverList.Clear();
            SpawnPointLines.Clear();
            DropLines.Clear();
            Made.Clear();
            ShownNames.Clear();
            Uses.Clear();
            Tools.Clear();
            Bosses.Clear();
            BossEvents.Clear();
            Altars.Clear();
            Turned.Clear();
            SpawnPointsLeft.Clear();
            UpgradeStation = null;
            UpgradeStationName = null;
        }

        /// <summary>
        /// What one prefab tells: the creatures it spawns, what it drops or yields, what it makes
        /// from what, and the status effects it gives. Read from its components, looked through
        /// once for the whole catalog. Each part goes on its own, so one mod's odd component loses
        /// only that part of that prefab, told once for each kind of failure.
        /// </summary>
        public static void Read(GameObject prefab, List<Component> components)
        {
            var started = CatalogTiming.Start();
            try { SpawnPoints(prefab, components); } catch (Exception ex) { Failed("nests and spawn points", prefab, ex); }
            CatalogTiming.Add("spawn points", started);
            started = CatalogTiming.Start();
            try { Drops(prefab, components); } catch (Exception ex) { Failed("drops", prefab, ex); }
            CatalogTiming.Add("drops", started);
            started = CatalogTiming.Start();
            try { Makers(prefab, components); } catch (Exception ex) { Failed("makers", prefab, ex); }
            CatalogTiming.Add("makers", started);
            started = CatalogTiming.Start();
            try { Givers(prefab, components); } catch (Exception ex) { Failed("status effect givers", prefab, ex); }
            CatalogTiming.Add("givers", started);
            started = CatalogTiming.Start();
            try { UsesIn(prefab, components); } catch (Exception ex) { Failed("uses of items", prefab, ex); }
            CatalogTiming.Add("uses", started);
            started = CatalogTiming.Start();
            try { Turns(prefab, components); } catch (Exception ex) { Failed("what things turn into", prefab, ex); }
            CatalogTiming.Add("turns into", started);

            foreach (var component in components)
            {
                if (component is Character boss && !string.IsNullOrEmpty(boss.m_defeatSetGlobalKey) && !Bosses.ContainsKey(boss.m_defeatSetGlobalKey))
                {
                    Bosses[boss.m_defeatSetGlobalKey] = prefab;
                }
                if (component is Character fought && !string.IsNullOrEmpty(fought.m_bossEvent) && !BossEvents.ContainsKey(fought.m_bossEvent))
                {
                    BossEvents[fought.m_bossEvent] = prefab;
                }
                if (component is OfferingBowl bowl)
                {
                    var summon = Summon.Of(bowl, Shown(prefab), prefab.name);
                    if (summon.Boss != null) Altars.Add(summon);
                }
            }
        }

        /// <summary>
        /// What a prefab uses items for: a piece what it is built from, any station or fire the
        /// items it burns (any field of an item named for fuel, which covers mods' stations too),
        /// and a creature what it eats. What stations turn items into is noted with the makers.
        /// </summary>
        private static void UsesIn(GameObject prefab, List<Component> components)
        {
            foreach (var component in components)
            {
                switch (component)
                {
                    case null:
                        continue;
                    case Piece piece when piece.m_enabled && piece.m_resources != null:
                        var near = piece.m_craftingStation != null ? piece.m_craftingStation.gameObject.name : null;
                        foreach (var need in piece.m_resources)
                        {
                            if (need?.m_resItem != null) Uses.Add(need.m_resItem.gameObject.name, UseKind.Builds, prefab.name, need.m_amount, near);
                        }
                        break;
                    case CraftingStation craft when craft.m_upgrader && UpgradeStation == null:
                        UpgradeStation = prefab.name;
                        var shown = CatalogBuilder.Localize(craft.m_name);
                        UpgradeStationName = shown.Length > 0 ? shown : prefab.name;
                        break;
                    case MonsterAI ai when ai.m_consumeItems != null:
                        foreach (var food in ai.m_consumeItems)
                        {
                            if (food != null) Uses.Add(food.gameObject.name, UseKind.EatenBy, prefab.name, 0);
                        }
                        break;
                }

                foreach (var field in FuelFields(component.GetType()))
                {
                    var value = field.GetValue(component);
                    if (value is ItemDrop fuel)
                    {
                        if (fuel != null) Uses.Add(fuel.gameObject.name, UseKind.Fuels, prefab.name, 0);
                    }
                    else if (value is IEnumerable<ItemDrop> fuels)
                    {
                        foreach (var each in fuels) if (each != null) Uses.Add(each.gameObject.name, UseKind.Fuels, prefab.name, 0);
                    }
                }
            }
        }

        private static readonly Dictionary<Type, FieldInfo[]> FuelFieldsByType = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// Whether a field of a component is told as a use or a source already: a list of what
        /// turns into what, or the items it burns. Links leave such fields to the facts.
        /// </summary>
        public static bool IsToldAsUse(Type type, FieldInfo field) =>
            Array.IndexOf(FuelFields(type), field) >= 0 || Array.IndexOf(Conversions(type), field) >= 0;

        /// <summary>Fields holding an item that is burnt, or a list of them: a smelter's, a fire's, a shield generator's, and the like.</summary>
        private static FieldInfo[] FuelFields(Type type)
        {
            if (FuelFieldsByType.TryGetValue(type, out var known)) return known;
            var found = new List<FieldInfo>();
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(flags))
                    {
                        var ft = field.FieldType;
                        var holdsItems = ft == typeof(ItemDrop) || ft == typeof(ItemDrop[]) || ft == typeof(List<ItemDrop>);
                        if (holdsItems && field.Name.IndexOf("fuel", StringComparison.OrdinalIgnoreCase) >= 0) found.Add(field);
                    }
                }
            }
            catch (Exception ex)
            {
                // A mod's type whose fields cannot be read has none here, remembered as such.
                Faults.Skip("reading of a type's fields", type.Name, ex);
                found.Clear();
            }
            known = found.ToArray();
            FuelFieldsByType[type] = known;
            return known;
        }

        /// <summary>The recipes each item goes into, noted once every prefab is read.</summary>
        private static void Recipes()
        {
            var db = ObjectDB.instance;
            if (db == null) return;
            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || !recipe.m_enabled || recipe.m_item == null || recipe.m_resources == null) continue;
                var at = recipe.m_craftingStation != null ? recipe.m_craftingStation.gameObject.name : "hand";
                var maxQuality = recipe.m_item.m_itemData?.m_shared?.m_maxQuality ?? 1;
                foreach (var need in recipe.m_resources)
                {
                    if (need?.m_resItem == null) continue;
                    // An upgrade kit is asked for only at an upgrade station, and only for what can be upgraded.
                    if (!need.m_upgraderResource) Uses.Add(need.m_resItem.gameObject.name, UseKind.Crafts, recipe.m_item.gameObject.name, need.m_amount, at);
                    else if (maxQuality > 1) Uses.Add(need.m_resItem.gameObject.name, UseKind.UpgradesPastTop, recipe.m_item.gameObject.name, need.GetAmount(maxQuality + 1));
                }
            }
        }

        private static void Failed(string what, GameObject prefab, Exception ex)
        {
            Faults.Skip(what, prefab.name, ex);
        }

        /// <summary>
        /// What the world as a whole tells, once every prefab is read, a step at a time: where
        /// creatures spawn and plants grow, what traders sell, and which mod added what. Each
        /// step says what it was.
        /// </summary>
        public static IEnumerable<string> Finish(List<GameObject> prefabs)
        {
            Try("world spawners", WorldSpawners);
            Try("raids", Raids);
            Try("spawn points", TellSpawnPoints);
            Try("nests and spawn points", () => Merge(SpawnPointLines, (prefab, line) => Add(prefab, line.Text, line.Prefab)));
            Try("vegetation", Vegetation);
            yield return "where things live";

            Try("drops", () => Merge(DropLines, From));
            Try("recipes items go into", Recipes);
            Try("traders", Traders);
            yield return "what makes things";

            Try("Jotunn's registry", JotunnMods);
            Try("mods' hooks", ModHooks.Read);
            yield return "which mod added what";

            foreach (var step in BundleMods(prefabs)) yield return step;
        }

        private static void Merge(Dictionary<string, List<Source>> lines, Action<string, Source> add)
        {
            foreach (var pair in lines) foreach (var line in pair.Value) add(pair.Key, line);
        }

        private static void Try(string what, Action act)
        {
            var started = CatalogTiming.Start();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                act();
                if (watch.ElapsedMilliseconds >= 50) Plugin.Note($"Scry read {what} in {watch.ElapsedMilliseconds} ms.");
            }
            catch (Exception ex)
            {
                if (Trouble.IsGameChange(ex)) Faults.Skip(what, "this world", ex);
                else Plugin.Log.LogWarning($"Scry could not read {what}, and leaves it out: {ex.Message}");
            }
            CatalogTiming.Add(what, started);
        }

        /// <summary>
        /// Each of a list on its own, for the world-wide steps: one odd entry (a mod's spawn with
        /// something missing) costs only itself, not the rest of the list after it.
        /// </summary>
        private static void Each<T>(IEnumerable<T> list, string what, Func<T, string> name, Action<T> read) where T : class
        {
            if (list == null) return;
            foreach (var item in list)
            {
                if (item == null) continue;
                try { read(item); }
                catch (Exception ex)
                {
                    string called;
                    try { called = name(item); }
                    catch { called = typeof(T).Name; }
                    Faults.Skip(what, called, ex);
                }
            }
        }

        /// <summary>A line of what something comes from, for one item: each line once.</summary>
        private static void From(string item, Source line)
        {
            if (!ComesFrom.TryGetValue(item, out var lines)) ComesFrom[item] = lines = new List<Source>();
            if (!lines.Exists(l => l.Text == line.Text)) lines.Add(line);
        }

        /// <summary>A line kept to be put together later.</summary>
        private static void Keep(Dictionary<string, List<Source>> lines, GameObject item, string line, string target)
        {
            if (item == null) return;
            if (!lines.TryGetValue(item.name, out var list)) lines[item.name] = list = new List<Source>();
            list.Add(new Source(line, target));
        }
    }
}
