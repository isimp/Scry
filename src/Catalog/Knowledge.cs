using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the game knows about a prefab beyond its own components: where it spawns or grows,
    /// and which mod added it. Gathered once with the catalog.
    /// </summary>
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

    internal static class Knowledge
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

        /// <summary>The altars among the registered prefabs, and what each summons.</summary>
        private static readonly List<Summon> Altars = new List<Summon>();

        /// <summary>Every altar known: the registered ones, and those the locations hold once read.</summary>
        public static IEnumerable<Summon> Summons() => Altars.Concat(Locations.Summons);

        /// <summary>The name shown for the creature whose defeat sets a world key, or null.</summary>
        public static string BossOf(string key) => key != null && Bosses.TryGetValue(key, out var boss) ? ShownName(boss) : null;

        /// <summary>The prefab of the creature whose defeat sets a world key, or null.</summary>
        public static string BossPrefabOf(string key) => key != null && Bosses.TryGetValue(key, out var boss) ? boss.name : null;

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
            Bosses.Clear();
            Altars.Clear();
            SpawnPointsLeft.Clear();
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

            foreach (var component in components)
            {
                if (component is Character boss && !string.IsNullOrEmpty(boss.m_defeatSetGlobalKey) && !Bosses.ContainsKey(boss.m_defeatSetGlobalKey))
                {
                    Bosses[boss.m_defeatSetGlobalKey] = prefab;
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

        // ----- Where things live -----

        private static void Add(string prefab, string line, string target = null)
        {
            if (string.IsNullOrEmpty(prefab)) return;
            if (!Where.TryGetValue(prefab, out var lines)) Where[prefab] = lines = new List<Source>();
            if (!lines.Exists(l => l.Text == line)) lines.Add(new Source(line, target));
        }

        private static void AddBiomes(string prefab, Heightmap.Biome biome)
        {
            if (!BiomesOf.TryGetValue(prefab, out var set)) BiomesOf[prefab] = set = new HashSet<string>();
            foreach (var single in Singles(biome)) set.Add(single.ToString());
        }

        /// <summary>The single biomes in a set of flags.</summary>
        private static IEnumerable<Heightmap.Biome> Singles(Heightmap.Biome biome)
        {
            foreach (Heightmap.Biome value in Enum.GetValues(typeof(Heightmap.Biome)))
            {
                var bits = (int)value;
                if (bits == 0 || (bits & (bits - 1)) != 0) continue;
                if ((biome & value) != 0) yield return value;
            }
        }

        /// <summary>One biome, by its enum name, as the game shows it: "BlackForest" as "Black Forest".</summary>
        public static string BiomeName(string biome)
        {
            var shown = CatalogBuilder.Localize("$biome_" + (biome ?? "").ToLowerInvariant());
            return shown.Length > 0 ? shown : Naming.FieldLabel(biome ?? "");
        }

        /// <summary>Biomes by the names the game shows, joined.</summary>
        public static string BiomeNames(Heightmap.Biome biome)
        {
            var all = Singles(biome).ToList();
            var every = Enum.GetValues(typeof(Heightmap.Biome)).Cast<Heightmap.Biome>().Count(b => { var i = (int)b; return i != 0 && (i & (i - 1)) == 0; });
            if (all.Count == 0) return "no biome";
            if (all.Count >= every) return "every biome";

            return string.Join(", ", all.Select(b => BiomeName(b.ToString())));
        }

        private static AccessTools.FieldRef<List<SpawnSystem>> _spawnSystems;
        private static bool _spawnSystemsTried;

        /// <summary>
        /// The running spawn systems, from the game's private list of them. Reached when first
        /// needed: if an update renames the list, only where creatures spawn is lost.
        /// </summary>
        private static List<SpawnSystem> SpawnSystems()
        {
            if (!_spawnSystemsTried)
            {
                _spawnSystemsTried = true;
                var field = AccessTools.Field(typeof(SpawnSystem), "m_instances");
                if (field != null) _spawnSystems = AccessTools.StaticFieldRefAccess<List<SpawnSystem>>(field);
            }
            return _spawnSystems?.Invoke();
        }

        /// <summary>The world's spawn lists, as the running spawn systems use them.</summary>
        private static void WorldSpawners()
        {
            var lists = new HashSet<SpawnSystemList>();
            foreach (var system in SpawnSystems() ?? new List<SpawnSystem>())
            {
                if (system == null) continue;
                foreach (var list in system.m_spawnLists) if (list != null) lists.Add(list);
            }
            if (lists.Count == 0)
            {
                foreach (var list in Resources.FindObjectsOfTypeAll<SpawnSystemList>())
                {
                    if (list != null && list.gameObject.scene.IsValid()) lists.Add(list);
                }
            }

            foreach (var list in lists)
            {
                Each(list.m_spawners, "world spawns", d => d.m_name.Length > 0 ? d.m_name : d.m_prefab != null ? d.m_prefab.name : "a spawn", data =>
                {
                    if (data.m_prefab == null || !data.m_enabled) return;
                    var name = data.m_prefab.name;
                    AddBiomes(name, data.m_biome);
                    PlacedByWorld.Add(name);

                    var spawn = new SpawnFacts
                    {
                        Biomes = BiomeNames(data.m_biome), AtNight = data.m_spawnAtNight, AtDay = data.m_spawnAtDay,
                        MinLevel = data.m_minLevel, MaxLevel = data.m_maxLevel, GroupMin = data.m_groupSizeMin, GroupMax = data.m_groupSizeMax,
                        InForest = data.m_inForest, OutsideForest = data.m_outsideForest,
                        Weather = (data.m_requiredEnvironments ?? new List<string>()).Where(e => !string.IsNullOrEmpty(e)).Select(Naming.FieldLabel).ToArray(),
                        Keys = new[] { data.m_requiredGlobalKey },
                    };
                    Add(name, SpawnWords.Line("Spawns in", spawn, BossOf), BossPrefabOf(data.m_requiredGlobalKey));
                });
            }
        }

        private static void Raids()
        {
            var events = RandEventSystem.instance?.m_events;
            if (events == null) return;

            // A world set to pick raids by each player's own progress checks other keys for them.
            var byPlayer = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.PlayerEvents);
            Each(events, "raids", r => r.m_name, raid =>
            {
                if (raid.m_spawn == null || !raid.m_enabled) return;
                var shown = CatalogBuilder.Localize(raid.m_startMessage);
                var start = $"Comes in the raid \"{(shown.Length > 0 ? shown : raid.m_name)}\"";
                int Count<T>(List<T> keys) => keys?.Count ?? 0;
                var perPlayer = byPlayer && (Count(raid.m_altRequiredPlayerKeysAny) > 0 || Count(raid.m_altRequiredPlayerKeysAll) > 0 || Count(raid.m_altRequiredKnownItems) > 0
                                             || Count(raid.m_altRequiredNotKnownItems) > 0 || Count(raid.m_altNotRequiredPlayerKeys) > 0);
                var facts = new SpawnFacts
                {
                    Biomes = raid.m_biome != 0 ? BiomeNames(raid.m_biome) : "",
                    Keys = perPlayer || raid.m_requiredGlobalKeys == null ? new string[0] : raid.m_requiredGlobalKeys.ToArray(),
                    NotKeys = perPlayer || raid.m_notRequiredGlobalKeys == null ? new string[0] : raid.m_notRequiredGlobalKeys.ToArray(),
                };
                var line = SpawnWords.Line(facts.Biomes.Length > 0 ? start + ", in" : start, facts, BossOf);
                if (perPlayer) line += ", for a player whose own progress calls for it";
                foreach (var data in raid.m_spawn)
                {
                    if (data?.m_prefab == null) continue;
                    Add(data.m_prefab.name, line, BossPrefabOf(facts.Keys.FirstOrDefault()));
                }
            });
        }

        /// <summary>Spawn points read, told once every prefab is read and each boss is known by its key.</summary>
        private static readonly List<(GameObject Point, GameObject Creature, SpawnFacts Spawn)> SpawnPointsLeft = new List<(GameObject, GameObject, SpawnFacts)>();

        private static void TellSpawnPoints()
        {
            foreach (var (point, creature, spawn) in SpawnPointsLeft)
            {
                Keep(SpawnPointLines, creature, SpawnWords.Line($"In dungeons or locations, from the spawn point {Shown(point)}", spawn, BossOf), point.name);
            }
            SpawnPointsLeft.Clear();
        }

        private static void SpawnPoints(GameObject prefab, List<Component> components)
        {
            foreach (var component in components)
            {
                if (!(component is SpawnArea area) || area.m_prefabs == null) continue;
                foreach (var data in area.m_prefabs)
                {
                    if (data?.m_prefab == null) continue;
                    Keep(SpawnPointLines, data.m_prefab, $"Comes from {Shown(prefab)}, {SpawnWords.Stars(data.m_minLevel, data.m_maxLevel)}", prefab.name);
                }
            }
            foreach (var component in components)
            {
                if (!(component is CreatureSpawner point) || point.m_creaturePrefab == null) continue;
                // Told as a world spawn is, by the same words, once every boss is known for its key.
                var spawn = new SpawnFacts
                {
                    AtNight = point.m_spawnAtNight, AtDay = point.m_spawnAtDay, MinLevel = point.m_minLevel, MaxLevel = point.m_maxLevel,
                    Keys = new[] { point.m_requiredGlobalKey }, NotKeys = new[] { point.m_blockingGlobalKey },
                };
                SpawnPointsLeft.Add((prefab, point.m_creaturePrefab, spawn));
            }
        }

        private static void Vegetation()
        {
            var vegetation = ZoneSystem.instance?.m_vegetation;
            if (vegetation == null) return;

            Each(vegetation, "vegetation", v => v.m_name, veg =>
            {
                if (veg.m_prefab == null || !veg.m_enable) return;
                var name = veg.m_prefab.name;
                AddBiomes(name, veg.m_biome);
                PlacedByWorld.Add(name);

                var line = "Grows in " + BiomeNames(veg.m_biome);
                if (veg.m_minAltitude > -1000f || veg.m_maxAltitude < 1000f)
                {
                    line += veg.m_maxAltitude < 1000f
                        ? $", {DropWords.Range(Mathf.RoundToInt(veg.m_minAltitude), Mathf.RoundToInt(veg.m_maxAltitude))} m up"
                        : $", from {Mathf.RoundToInt(veg.m_minAltitude)} m up";
                }
                if (veg.m_minOceanDepth > 0f || veg.m_maxOceanDepth > 0f) line += ", in the sea";
                if (veg.m_inForest) line += ", in forests";
                var group = SpawnWords.Group(veg.m_groupSizeMin, veg.m_groupSizeMax);
                if (group != null) line += ", " + group;
                Add(name, line);
            });
        }

        /// <summary>
        /// What gives each item: creatures that drop it, plants and bushes it is picked from, and
        /// anything with a drop table (rocks, trees, containers) that can yield it.
        /// </summary>
        private static void Drops(GameObject prefab, List<Component> components)
        {
            foreach (var component in components)
            {
                if (component == null) continue;

                if (component is CharacterDrop drops)
                {
                    if (drops.m_drops == null) continue;
                    foreach (var drop in drops.m_drops)
                    {
                        if (drop?.m_prefab == null) continue;
                        var amount = DropWords.CreatureAmount(drop.m_amountMin, drop.m_amountMax, drop.m_onePerPlayer);
                        var chance = drop.m_chance < 1f ? $" ({Mathf.RoundToInt(drop.m_chance * 100f)}%)" : "";
                        Keep(DropLines, drop.m_prefab, $"Dropped by {Shown(prefab)}, {amount}{chance}", prefab.name);
                    }
                    continue;
                }

                if (component is Pickable pickable)
                {
                    Keep(DropLines, pickable.m_itemPrefab, $"Picked from {Shown(prefab)}", prefab.name);
                }

                // A sapling or seedling tells what it grows into; what grows tells where from.
                if (component is Plant plant && plant.m_grownPrefabs != null)
                {
                    foreach (var grown in plant.m_grownPrefabs) Keep(DropLines, grown, $"Grows from {Shown(prefab)}", prefab.name);
                }

                var tables = DropTables(component.GetType());
                if (tables.Length == 0) continue;
                string shown = null;
                foreach (var field in tables)
                {
                    if (!(field.GetValue(component) is DropTable table) || table.m_drops == null) continue;
                    shown = shown ?? $"{FromVerb(component)} {Shown(prefab)}";
                    foreach (var data in table.m_drops) Keep(DropLines, data.m_item, shown, prefab.name);
                }
            }
        }

        /// <summary>
        /// What else makes an item: every list on a prefab whose entries turn one item into
        /// another (smelters, kilns, fermenters, cooking stations, and any mod's that works the
        /// same way), and what traders sell. Traders stand in locations, so only those loaded are
        /// found.
        /// </summary>
        private static void Makers(GameObject prefab, List<Component> components)
        {
            foreach (var component in components)
            {
                if (component == null) continue;
                foreach (var field in Conversions(component.GetType()))
                {
                    if (!(field.GetValue(component) is System.Collections.IEnumerable list)) continue;
                    foreach (var conversion in list)
                    {
                        if (conversion == null) continue;
                        var type = conversion.GetType();
                        var from = type.GetField("m_from")?.GetValue(conversion) as ItemDrop;
                        var to = type.GetField("m_to")?.GetValue(conversion) as ItemDrop;
                        if (from == null || to == null) continue;
                        // A fermenter's batch makes several (m_producedItems); a smelter's one.
                        var makes = type.GetField("m_producedItems")?.GetValue(conversion) is int produced && produced > 1 ? produced : 1;
                        var making = new Making { Station = prefab.name, Output = to.gameObject.name, Makes = makes };
                        making.Inputs.Add((from.gameObject.name, 1));
                        Made.Add(making);
                        Uses.Add(from.gameObject.name, UseKind.TurnsInto, to.gameObject.name, 0, prefab.name);
                    }
                }

                // Producers make an item by themselves over time, up to what they hold.
                if (component is Beehive hive && hive.m_honeyItem != null)
                {
                    var biomes = hive.m_biome != 0 ? $", in {BiomeNames(hive.m_biome)}" : "";
                    Keep(DropLines, hive.m_honeyItem.gameObject, $"Made by {Shown(prefab)}, one every {Naming.Duration(hive.m_secPerUnit)}, holding up to {hive.m_maxHoney}{biomes}", prefab.name);
                }
                if (component is SapCollector tap && tap.m_spawnItem != null)
                {
                    Keep(DropLines, tap.m_spawnItem.gameObject, $"Made by {Shown(prefab)}, one every {Naming.Duration(tap.m_secPerUnit)}, holding up to {tap.m_maxLevel}", prefab.name);
                }

                // The obliterator: each conversion takes all or any one of its items, and whatever
                // is left over becomes its default result (Incinerator.Incinerate).
                if (component is Incinerator incinerator && incinerator.m_conversions != null)
                {
                    foreach (var conversion in incinerator.m_conversions)
                    {
                        if (conversion?.m_result == null || conversion.m_requirements == null) continue;
                        var making = new Making
                        {
                            Station = prefab.name, Output = conversion.m_result.gameObject.name,
                            Makes = Math.Max(1, conversion.m_resultAmount), AnyOne = conversion.m_requireOnlyOneIngredient,
                        };
                        foreach (var need in conversion.m_requirements)
                        {
                            if (need?.m_resItem == null) continue;
                            making.Inputs.Add((need.m_resItem.gameObject.name, Math.Max(1, need.m_amount)));
                            Uses.Add(need.m_resItem.gameObject.name, UseKind.TurnsInto, making.Output, 0, prefab.name);
                        }
                        Made.Add(making);
                    }
                }
            }
        }

        /// <summary>
        /// How an item comes out of a drop table, in the words the other side's facts use: a tree
        /// "When felled", a rock "Each piece drops", a chest "Holds", a bush "Also", anything
        /// broken "When broken"; a mod's own part, plainly.
        /// </summary>
        private static string FromVerb(Component component)
        {
            switch (component)
            {
                case TreeBase _: return "Felled from";
                case TreeLog _: return "Chopped from";
                case MineRock _:
                case MineRock5 _: return "Mined from";
                case Container _: return "Found in";
                case Pickable _: return "Also picked from";
                case DropOnDestroyed _: return "Broken out of";
                default: return "Comes out of";
            }
        }

        /// <summary>What traders sell. They stand in locations, so only those loaded are found.</summary>
        private static void Traders()
        {
            Each(Resources.FindObjectsOfTypeAll<Trader>(), "traders", t => t.name, trader =>
            {
                if (trader.m_items == null) return;
                var name = CatalogBuilder.Localize(trader.m_name);
                if (name.Length == 0) name = trader.gameObject.name;
                // The trader's own prefab, where it is one, for the line to go to; a copy standing in a location is named after it.
                var self = trader.transform.root.gameObject.name.Replace("(Clone)", "").Trim();
                foreach (var trade in trader.m_items)
                {
                    if (trade?.m_prefab == null) continue;
                    var stack = trade.m_stack > 1 ? $"{trade.m_stack} for " : "";
                    var key = string.IsNullOrEmpty(trade.m_requiredGlobalKey) ? "" : ", " + SpawnWords.Once(trade.m_requiredGlobalKey, BossOf);
                    From(trade.m_prefab.gameObject.name, new Source($"Sold by {name}, {stack}{trade.m_price} coins{key}", self));
                }
            });
        }

        private static readonly Dictionary<Type, FieldInfo[]> ConversionFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Fields holding a list or array of entries with an m_from and an m_to item.</summary>
        private static FieldInfo[] Conversions(Type type)
        {
            if (ConversionFields.TryGetValue(type, out var known)) return known;
            var found = new List<FieldInfo>();
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(flags))
                    {
                        var element = field.FieldType.IsArray ? field.FieldType.GetElementType()
                            : field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>) ? field.FieldType.GetGenericArguments()[0] : null;
                        if (element == null) continue;
                        if (element.GetField("m_from")?.FieldType == typeof(ItemDrop) && element.GetField("m_to")?.FieldType == typeof(ItemDrop)) found.Add(field);
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
            ConversionFields[type] = known;
            return known;
        }

        private static readonly List<(string Prefab, string Effect, string How)> GiverList = new List<(string, string, string)>();
        private static readonly Dictionary<Type, FieldInfo[]> EffectRefFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Each prefab that gives a status effect, which one, and how (worn, eaten, a set, an attack).</summary>
        public static IReadOnlyList<(string Prefab, string Effect, string How)> Givers() => GiverList;

        /// <summary>
        /// What gives each status effect: any item, attack or part of a prefab that names it,
        /// whether by reference (eating, wearing, a set, an attack, a guardian power) or by name
        /// (areas of effect, and the like).
        /// </summary>
        private static void Givers(GameObject prefab, List<Component> components)
        {
            void Note(object owner)
            {
                foreach (var field in EffectRefs(owner.GetType()))
                {
                    string name = null;
                    var value = field.GetValue(owner);
                    if (value is StatusEffect effect && effect != null) name = effect.name;
                    else if (value is string text && text.Length > 0) name = text;
                    if (name == null) continue;

                    var how = Naming.FieldLabel(field.Name).ToLowerInvariant()
                        .Replace("status effect", "").Replace(" se", "").Trim();
                    GiverList.Add((prefab.name, name, how));
                }
            }

            foreach (var component in components)
            {
                if (component == null) continue;
                Note(component);
                var shared = (component as ItemDrop)?.m_itemData?.m_shared;
                if (shared == null) continue;
                Note(shared);
                if (shared.m_attack != null) Note(shared.m_attack);
                if (shared.m_secondaryAttack != null) Note(shared.m_secondaryAttack);
            }
        }

        /// <summary>Fields holding a status effect, or a status effect's name.</summary>
        private static FieldInfo[] EffectRefs(Type type)
        {
            if (EffectRefFields.TryGetValue(type, out var known)) return known;
            var found = new List<FieldInfo>();
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(flags))
                    {
                        if (typeof(StatusEffect).IsAssignableFrom(field.FieldType)) found.Add(field);
                        else if (field.FieldType == typeof(string) && field.Name.IndexOf("statuseffect", StringComparison.OrdinalIgnoreCase) >= 0) found.Add(field);
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
            EffectRefFields[type] = known;
            return known;
        }

        private static string ItemName(GameObject item)
        {
            var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            var name = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
            return name.Length > 0 ? name : item != null ? item.name : "";
        }

        private static FieldInfo[] DropTables(Type type)
        {
            if (DropTableFields.TryGetValue(type, out var known)) return known;
            var found = new List<FieldInfo>();
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(flags)) if (field.FieldType == typeof(DropTable)) found.Add(field);
                }
            }
            catch (Exception ex)
            {
                // A mod's type whose fields cannot be read has none here, remembered as such.
                Faults.Skip("reading of a type's fields", type.Name, ex);
                found.Clear();
            }
            known = found.ToArray();
            DropTableFields[type] = known;
            return known;
        }

        /// <summary>A prefab as the lines name it: its shown name and its prefab name. Worked out once per prefab.</summary>
        private static string Shown(GameObject prefab)
        {
            if (ShownNames.TryGetValue(prefab, out var known)) return known;
            known = ShownOf(prefab);
            ShownNames[prefab] = known;
            return known;
        }

        /// <summary>A creature's name as the game shows it, alone, else the prefab's.</summary>
        private static string ShownName(GameObject prefab)
        {
            var shown = CatalogBuilder.Localize(prefab.GetComponent<Character>()?.m_name);
            return shown.Length > 0 ? shown : prefab.name;
        }

        private static string ShownOf(GameObject prefab)
        {
            string token = null;
            var character = prefab.GetComponent<Character>();
            if (character != null) token = character.m_name;
            var piece = prefab.GetComponent<Piece>();
            if (token == null && piece != null) token = piece.m_name;
            var item = prefab.GetComponent<ItemDrop>();
            if (token == null && item != null) token = item.m_itemData?.m_shared?.m_name;
            var fish = prefab.GetComponent<Fish>();
            if (token == null && fish != null) token = fish.m_name;
            var hover = prefab.GetComponent<HoverText>();
            if (token == null && hover != null) token = hover.m_text;

            var shown = CatalogBuilder.Localize(token);
            return shown.Length > 0 && shown != prefab.name ? $"{shown} ({prefab.name})" : prefab.name;
        }

        // ----- Which mod -----

        /// <summary>Jotunn's plugin id, as it loads (Jotunn 2.30: "Loading [Jotunn 2.30.2] (com.jotunn.jotunn)").</summary>
        private const string JotunnGuid = "com.jotunn.jotunn";

        private static readonly Dictionary<(Type, string), PropertyInfo> Properties = new Dictionary<(Type, string), PropertyInfo>();

        /// <summary>
        /// Jotunn keeps a registry of what each mod built on it added, with the mod. Read by name
        /// from Jotunn's own assembly, so Scry needs no reference to Jotunn and does nothing when it
        /// is not installed. Looking the type up among all assemblies instead made the log fill
        /// with the load errors of mods whose types cannot all be loaded.
        /// </summary>
        private static void JotunnMods()
        {
            if (!Chainloader.PluginInfos.TryGetValue(JotunnGuid, out var jotunn) || jotunn?.Instance == null) return;
            var registry = jotunn.Instance.GetType().Assembly.GetType("Jotunn.Utils.ModRegistry", false);
            if (registry == null) return;

            PropertyInfo Property(Type type, string name)
            {
                if (!Properties.TryGetValue((type, name), out var property)) Properties[(type, name)] = property = AccessTools.Property(type, name);
                return property;
            }

            void Read(string method, string property)
            {
                var get = AccessTools.Method(registry, method, new Type[0]);
                if (get == null) return;
                if (!(get.Invoke(null, null) is System.Collections.IEnumerable entities)) return;

                foreach (var entity in entities)
                {
                    if (entity == null) continue;
                    var mod = Property(entity.GetType(), "SourceMod")?.GetValue(entity) as BepInEx.BepInPlugin;
                    var thing = Property(entity.GetType(), property)?.GetValue(entity) as UnityEngine.Object;
                    if (mod == null || thing == null) continue;
                    ModOf[thing.name] = mod.Name;
                }
            }

            Read("GetPrefabs", "Prefab");
            Read("GetItems", "ItemPrefab");
            Read("GetPieces", "PiecePrefab");
            Read("GetStatusEffects", "StatusEffect");
        }

        /// <summary>
        /// For the rest: a mod's prefabs usually come out of an asset bundle it ships, either as a
        /// file in its folder or embedded in its assembly. The bundle holding a prefab of the same
        /// name is found among those loaded, and the mod shipping a bundle of that name named.
        /// A prefab made in code, or a bundle named unlike anything the mod ships, stays unnamed.
        /// </summary>
        private static IEnumerable<string> BundleMods(List<GameObject> prefabs)
        {
            var started = CatalogTiming.Start();
            var wanted = new HashSet<string>(prefabs.Where(p => p != null && !ModOf.ContainsKey(p.name) && Origins.Prefabs.Of(p.name) == Origin.Mod)
                .Select(p => p.name.ToLowerInvariant()));
            if (wanted.Count == 0) yield break;

            // A bundle, or a mod's folder, at a time: looking through them all took some 150 ms.
            var bundleOf = new Dictionary<string, string>();
            List<AssetBundle> bundles;
            try
            {
                bundles = AssetBundle.GetAllLoadedAssetBundles().ToList();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not read asset bundles: {ex.Message}");
                yield break;
            }
            foreach (var bundle in bundles)
            {
                try
                {
                    if (bundle == null || bundle.isStreamedSceneAssetBundle) continue;
                    foreach (var path in bundle.GetAllAssetNames())
                    {
                        var file = Path.GetFileNameWithoutExtension(path);
                        if (wanted.Contains(file) && !bundleOf.ContainsKey(file)) bundleOf[file] = Leaf(bundle.name);
                    }
                }
                catch (Exception ex)
                {
                    Faults.Skip("which mod added what", "a bundle", ex);
                }
                CatalogTiming.Add("asset bundles", started);
                yield return "which mod added what: asset bundles";
                started = CatalogTiming.Start();
            }
            if (bundleOf.Count == 0) yield break;

            var modOfBundle = new Dictionary<string, string>();
            var needed = new HashSet<string>(bundleOf.Values);
            foreach (var info in Chainloader.PluginInfos.Values.ToList())
            {
                var name = info?.Metadata?.Name;
                if (string.IsNullOrEmpty(name)) continue;
                started = CatalogTiming.Start();

                try
                {
                    var assembly = info.Instance != null ? info.Instance.GetType().Assembly : null;
                    if (assembly != null)
                    {
                        foreach (var resource in assembly.GetManifestResourceNames())
                        {
                            var lower = resource.ToLowerInvariant();
                            foreach (var bundle in needed)
                            {
                                if (lower == bundle || lower.EndsWith("." + bundle)) modOfBundle[bundle] = name;
                            }
                        }
                    }

                    var folder = Path.GetDirectoryName(info.Location);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder) && !IsPluginsRoot(folder))
                    {
                        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                        {
                            var leaf = Path.GetFileName(file).ToLowerInvariant();
                            var bare = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                            if (needed.Contains(leaf)) modOfBundle[leaf] = name;
                            else if (needed.Contains(bare)) modOfBundle[bare] = name;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Faults.Skip("which mod added what", name, ex);
                }
                CatalogTiming.Add("asset bundles", started);
                yield return "which mod added what: mods' files";
            }

            foreach (var prefab in prefabs)
            {
                if (prefab == null || ModOf.ContainsKey(prefab.name)) continue;
                if (bundleOf.TryGetValue(prefab.name.ToLowerInvariant(), out var bundle) && modOfBundle.TryGetValue(bundle, out var mod))
                {
                    ModOf[prefab.name] = mod;
                }
            }
        }

        private static string Leaf(string bundleName)
        {
            var name = (bundleName ?? "").Replace('\\', '/');
            var slash = name.LastIndexOf('/');
            return (slash >= 0 ? name.Substring(slash + 1) : name).ToLowerInvariant();
        }

        /// <summary>A mod dropped straight into the plugins folder shares it with everything else.</summary>
        private static bool IsPluginsRoot(string folder)
        {
            return string.Equals(Path.GetFullPath(folder).TrimEnd('\\', '/'), Path.GetFullPath(BepInEx.Paths.PluginPath).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
