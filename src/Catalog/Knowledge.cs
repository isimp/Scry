using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>One line of what is known, the prefab it names, if it names one, and why Scry is not sure of it, if it is not.</summary>
    internal struct Source
    {
        public string Text;
        public string Prefab;
        public string Unsure;

        /// <summary>How often it gives the thing, from 0 to 1, for telling the surest first (<see cref="ContentOrder.SurestFirst{T}"/>).</summary>
        public double Chance;

        /// <summary>The figures the line was told from, where it tells a way something gives a thing; null for other lines.</summary>
        public SourceFacts Record;

        public Source(string text, string prefab, string unsure = null, double chance = 1.0)
        {
            Text = text;
            Prefab = prefab;
            Unsure = unsure;
            Chance = chance;
        }
    }

    /// <summary>
    /// An altar's offering: the boss it summons (<c>OfferingBowl.m_bossPrefab</c>), the item and
    /// how many of it are offered (<c>m_bossItem</c>, <c>m_bossItems</c>), or for an altar that
    /// takes them on item stands (<c>m_useItemStands</c>) the stands' item and how many stands,
    /// and where it stands, a location's name or the altar's own prefab where it is one.
    /// </summary>
    internal struct Summon
    {
        public string Boss;
        public string Item;
        public int Count;

        /// <summary>Whether the items are set on its item stands rather than offered at it.</summary>
        public bool OnStands;

        public string Place;
        public string PlacePrefab;

        public static Summon Of(OfferingBowl bowl, string place, string placePrefab)
        {
            var summon = new Summon
            {
                Boss = bowl.m_bossPrefab != null ? bowl.m_bossPrefab.name : null,
                Item = bowl.m_bossItem != null ? bowl.m_bossItem.gameObject.name : null,
                Count = Math.Max(1, bowl.m_bossItems),
                Place = place,
                PlacePrefab = placePrefab,
            };

            // An altar of item stands (Moder's eggs) ignores what it would be offered: every stand
            // whose name starts with its prefix within its range must hold something
            // (OfferingBowl.Interact, FindItemStands), the stands taking the item they support.
            if (bowl.m_useItemStands)
            {
                var stands = bowl.transform.root.GetComponentsInChildren<ItemStand>(true)
                    .Where(s => s != null && s.gameObject.name.StartsWith(bowl.m_itemStandPrefix ?? "", StringComparison.Ordinal)
                                && Vector3.Distance(s.transform.position, bowl.transform.position) <= bowl.m_itemstandMaxRange)
                    .ToList();
                var item = stands.SelectMany(s => s.m_supportedItems ?? new List<ItemDrop>()).FirstOrDefault(i => i != null);
                if (stands.Count > 0)
                {
                    summon.OnStands = true;
                    summon.Count = stands.Count;
                    if (item != null) summon.Item = item.gameObject.name;
                }
            }
            return summon;
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

        private static readonly Dictionary<string, List<Source>> ComesFrom = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
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
            return BiomesOf.TryGetValue(prefab, out var set) ? set.ToArray() : Array.Empty<string>();
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
            ForgetSpawns();
            ForgetMods();
            ComesFrom.Clear();
            SpawnPointLines.Clear();
            DropLines.Clear();
            Made.Clear();
            Uses.Clear();
            Tools.Clear();
            Bosses.Clear();
            BossEvents.Clear();
            Altars.Clear();
            Turned.Clear();
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
            Guard.Each(Feature.WhereCreaturesSpawn, "nests and spawn points", prefab.name, () => SpawnPoints(prefab, components));
            CatalogTiming.Add("spawn points", started);
            started = CatalogTiming.Start();
            Guard.Each(Feature.Drops, "drops", prefab.name, () => Drops(prefab, components));
            CatalogTiming.Add("drops", started);
            started = CatalogTiming.Start();
            Guard.Each(Feature.Makers, "makers", prefab.name, () => Makers(prefab, components));
            CatalogTiming.Add("makers", started);
            started = CatalogTiming.Start();
            Guard.Each(Feature.EffectGivers, "status effect givers", prefab.name, () => Givers(prefab, components));
            CatalogTiming.Add("givers", started);
            started = CatalogTiming.Start();
            Guard.Each(Feature.ItemUses, "uses of items", prefab.name, () => UsesIn(prefab, components));
            Guard.Each(Feature.ModAttribution, "which mod added what", prefab.name, () => Scripts(prefab, components));
            Guard.Each(Feature.HowTameCreaturesBreed, "breeding", prefab.name, () => Breeding(prefab, components));
            Guard.Each(Feature.BaitsKeysAndPowers, "baits, keys and powers", prefab.name, () => BaitsKeysAndPowers(prefab, components));
            CatalogTiming.Add("uses", started);
            started = CatalogTiming.Start();
            Guard.Each(Feature.TurnsInto, "what things turn into", prefab.name, () => Turns(prefab, components));
            CatalogTiming.Add("turns into", started);

            foreach (var component in components)
            {
                if (component is Character boss && !string.IsNullOrEmpty(boss.m_defeatSetGlobalKey) && !Bosses.ContainsKey(boss.m_defeatSetGlobalKey))
                {
                    Bosses[boss.m_defeatSetGlobalKey] = prefab;
                }
                // A boss's fight only where its event is on: the game finds none switched off (RandEventSystem.GetEvent).
                if (component is Character fought && !string.IsNullOrEmpty(fought.m_bossEvent) && !BossEvents.ContainsKey(fought.m_bossEvent)
                    && RandEventSystem.instance != null && RandEventSystem.instance.m_events.Exists(e => e != null && e.m_name == fought.m_bossEvent && e.m_enabled))
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
                    var value = TypeFields.Value(field, component);
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
        private static FieldInfo[] FuelFields(Type type) => TypeFields.Matching(FuelFieldsByType, type, field =>
        {
            var ft = field.FieldType;
            var holdsItems = ft == typeof(ItemDrop) || ft == typeof(ItemDrop[]) || ft == typeof(List<ItemDrop>);
            return holdsItems && field.Name.IndexOf("fuel", StringComparison.OrdinalIgnoreCase) >= 0;
        });

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

        /// <summary>
        /// What the world as a whole tells, once every prefab is read, a step at a time: where
        /// creatures spawn and plants grow, what traders sell, and which mod added what. Each
        /// step says what it was.
        /// </summary>
        public static IEnumerable<string> Finish(List<GameObject> prefabs)
        {
            Guard.Read(Feature.WhereCreaturesSpawn, "world spawners", WorldSpawners);
            Guard.Read(Feature.Raids, "raids", Raids);
            Guard.Read(Feature.WhereCreaturesSpawn, "spawn points", TellSpawnPoints);
            Guard.Read(Feature.WhereCreaturesSpawn, "nests and spawn points", () => Merge(SpawnPointLines, (prefab, line) => Add(prefab, line.Text, line.Prefab, line.Chance)));
            Guard.Read(Feature.WhereThingsGrow, "vegetation", Vegetation);
            yield return "where things live";

            Guard.Read(Feature.Drops, "drops", () => Merge(DropLines, From));
            Guard.Read(Feature.ItemUses, "recipes items go into", Recipes);
            Guard.Read(Feature.WhatTradersSellAndWhen, "traders", Traders);
            yield return "what makes things";

            Guard.Read(Feature.ModAttribution, "Jotunn's registry", JotunnMods);
            Guard.Read(Feature.ModAttribution, "mods' scripts", ScriptMods);
            Guard.Read(Feature.ModHooks, "mods' hooks", ModHooks.Read);
            yield return "which mod added what";

            foreach (var step in BundleMods(prefabs)) yield return step;
        }

        private static void Merge(Dictionary<string, List<Source>> lines, Action<string, Source> add)
        {
            foreach (var pair in lines) foreach (var line in pair.Value) add(pair.Key, line);
        }

        /// <summary>
        /// Each of a list on its own, for the world-wide steps: one odd entry (a mod's spawn with
        /// something missing) costs only itself, not the rest of the list after it.
        /// </summary>
        private static void Each<T>(IEnumerable<T> list, Feature feature, string what, Func<T, string> name, Action<T> read) where T : class
        {
            if (list == null) return;
            foreach (var item in list)
            {
                if (item == null) continue;
                Guard.Each(feature, what, () => name(item), () => read(item));
            }
        }

        /// <summary>A line of what something comes from, for one item: each line once.</summary>
        private static void From(string item, Source line)
        {
            if (!ComesFrom.TryGetValue(item, out var lines)) ComesFrom[item] = lines = new List<Source>();
            if (!lines.Exists(l => l.Text == line.Text)) lines.Add(line);
        }

        /// <summary>A line kept to be put together later, with how often it gives the thing.</summary>
        private static void Keep(Dictionary<string, List<Source>> lines, GameObject item, string line, string target, double chance = 1.0)
        {
            if (item == null) return;
            if (!lines.TryGetValue(item.name, out var list)) lines[item.name] = list = new List<Source>();
            list.Add(new Source(line, target, chance: chance));
        }

        /// <summary>A way something gives a thing, kept to be put together later.</summary>
        private static void Keep(Dictionary<string, List<Source>> lines, GameObject item, SourceFacts facts)
        {
            if (item == null) return;
            if (!lines.TryGetValue(item.name, out var list)) lines[item.name] = list = new List<Source>();
            list.Add(Told(facts));
        }

        /// <summary>A way something gives a thing as its line, going to what gives it, with how sure it is (<see cref="SourceWords"/>).</summary>
        private static Source Told(SourceFacts facts) =>
            new Source(SourceWords.Line(facts, BossOf), facts.Giver, chance: SourceWords.Sureness(facts)) { Record = facts };
    }
}
