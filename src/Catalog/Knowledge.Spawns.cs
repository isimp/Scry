using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>Where things live and what gives them: spawn lists, raids, spawn points, what grows where, and what drops each item.</summary>
    internal static partial class Knowledge
    {
        // ----- Where things live -----

        private static void Add(string prefab, string line, string target = null, double chance = 1.0) => Add(prefab, new Source(line, target, chance: chance));

        /// <summary>Where a thing lives or comes from, each line once.</summary>
        private static void Add(string prefab, Source line)
        {
            if (string.IsNullOrEmpty(prefab)) return;
            if (!Where.TryGetValue(prefab, out var lines)) Where[prefab] = lines = new List<Source>();
            if (!lines.Exists(l => l.Text == line.Text)) lines.Add(line);
        }

        /// <summary>
        /// Where young come from and what they grow into: a tame creature's young
        /// (<c>Procreation</c>), what a young one grows up into (<c>Growup</c>) and what an egg
        /// hatches into (<c>EggGrow</c>), each told where the other comes from. An egg laid is an
        /// item, so it is told where items come from.
        /// </summary>
        private static void Breeding(GameObject prefab, List<Component> components)
        {
            void Born(GameObject young, SourceFacts from)
            {
                if (young == null) return;
                if (young.GetComponent<ItemDrop>() != null) Keep(DropLines, young, from);
                else Add(young.name, Told(from));
            }

            foreach (var component in components)
            {
                switch (component)
                {
                    case Procreation breed:
                        Born(breed.m_offspring, SourceFacts.Of(SourceWay.Born, prefab.name, Shown(prefab)));
                        Born(breed.m_noPartnerOffspring, SourceFacts.BornAlone(prefab.name, Shown(prefab)));
                        break;
                    case Growup grow:
                        foreach (var grown in GrownOf(grow)) Add(grown.name, Told(SourceFacts.Of(SourceWay.GrowsUp, prefab.name, Shown(prefab))));
                        break;
                    case EggGrow egg when egg.m_grownPrefab != null:
                        Add(egg.m_grownPrefab.name, Told(SourceFacts.Of(SourceWay.Hatches, prefab.name, Shown(prefab))));
                        break;
                }
            }
        }

        /// <summary>The fish each bait catches, with the chance it bites when it reaches the hook (<c>Fish.TestBate</c>).</summary>
        private static readonly Dictionary<string, List<(string Fish, float Chance)>> Baits = new Dictionary<string, List<(string, float)>>(StringComparer.Ordinal);

        /// <summary>The doors each key opens (<c>Door.m_keyItem</c>).</summary>
        private static readonly Dictionary<string, List<string>> Keys = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>The Forsaken power each trophy gives on its boss stone, by its name, and the stone (<c>ItemStand.m_guardianPower</c>).</summary>
        private static readonly Dictionary<string, (string Power, string Stone)> Powers = new Dictionary<string, (string, string)>(StringComparer.Ordinal);

        public static IReadOnlyList<(string Fish, float Chance)> Catches(string bait) =>
            bait != null && Baits.TryGetValue(bait, out var fish) ? fish : (IReadOnlyList<(string, float)>)Array.Empty<(string, float)>();

        public static IReadOnlyList<string> Opens(string key) =>
            key != null && Keys.TryGetValue(key, out var doors) ? doors : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>The power a trophy gives on its boss stone, and the stone, or nulls.</summary>
        public static (string Power, string Stone) PowerOf(string trophy) =>
            trophy != null && Powers.TryGetValue(trophy, out var power) ? power : (null, null);

        /// <summary>What a fish bites on, a door is opened with, and a boss stone's stand gives for a trophy, each told from the other side too.</summary>
        private static void BaitsKeysAndPowers(GameObject prefab, List<Component> components)
        {
            foreach (var component in components)
            {
                switch (component)
                {
                    case Fish fish when fish.m_baits != null:
                        foreach (var bait in fish.m_baits)
                        {
                            if (bait?.m_bait == null) continue;
                            if (!Baits.TryGetValue(bait.m_bait.gameObject.name, out var caught)) Baits[bait.m_bait.gameObject.name] = caught = new List<(string, float)>();
                            if (!caught.Exists(c => c.Fish == prefab.name)) caught.Add((prefab.name, bait.m_chance));
                        }
                        break;
                    case Door door when door.m_keyItem != null:
                        if (!Keys.TryGetValue(door.m_keyItem.gameObject.name, out var doors)) Keys[door.m_keyItem.gameObject.name] = doors = new List<string>();
                        if (!doors.Contains(prefab.name)) doors.Add(prefab.name);
                        break;
                    case ItemStand stand when stand.m_guardianPower != null && stand.m_supportedItems != null:
                        foreach (var trophy in stand.m_supportedItems)
                        {
                            if (trophy != null && !Powers.ContainsKey(trophy.gameObject.name)) Powers[trophy.gameObject.name] = (stand.m_guardianPower.name, prefab.name);
                        }
                        break;
                }
            }
        }

        /// <summary>What waits for each world key: raids, spawns, spawners and traders' wares (<see cref="UnlockBook"/>).</summary>
        public static readonly UnlockBook Unlocks = new UnlockBook();

        /// <summary>What a young one may grow into, each once.</summary>
        public static List<GameObject> GrownOf(Growup grow)
        {
            var grown = new List<GameObject>();
            if (grow.m_altGrownPrefabs != null) foreach (var alt in grow.m_altGrownPrefabs) if (alt?.m_prefab != null && !grown.Contains(alt.m_prefab)) grown.Add(alt.m_prefab);
            if (grown.Count == 0 && grow.m_grownPrefab != null) grown.Add(grow.m_grownPrefab);
            return grown;
        }

        /// <summary>The biomes something comes to only in weather a world event brings, by name (<see cref="SpawnBiomes"/>).</summary>
        private static readonly Dictionary<string, HashSet<string>> EventBiomesOf = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        public static string[] EventBiomes(string prefab) => EventBiomesOf.TryGetValue(prefab, out var set) ? set.ToArray() : Array.Empty<string>();

        private static void AddBiomeKey(Dictionary<string, HashSet<string>> into, string prefab, string key)
        {
            if (!into.TryGetValue(prefab, out var set)) into[prefab] = set = new HashSet<string>();
            set.Add(key);
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

        /// <summary>Every single biome by its enum name.</summary>
        public static string[] EveryBiomeKey => Enum.GetValues(typeof(Heightmap.Biome)).Cast<Heightmap.Biome>().Where(b => { var i = (int)b; return i != 0 && (i & (i - 1)) == 0; }).Select(b => b.ToString()).Distinct().ToArray();

        /// <summary>The single biomes in a set of flags by their enum names, as entries keep them for the search.</summary>
        public static string[] BiomeKeys(Heightmap.Biome biome) => Singles(biome).Select(b => b.ToString()).ToArray();

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

            // Each biome's own weathers, to tell a creature's home from where a world event's weather brings it.
            var weathers = new Dictionary<string, ICollection<string>>(StringComparer.Ordinal);
            if (EnvMan.instance?.m_biomes != null)
            {
                foreach (var setup in EnvMan.instance.m_biomes)
                {
                    if (setup?.m_environments == null) continue;
                    foreach (var key in BiomeKeys(setup.m_biome))
                    {
                        if (!weathers.TryGetValue(key, out var own)) weathers[key] = own = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var env in setup.m_environments) if (env != null && !string.IsNullOrEmpty(env.m_environment)) own.Add(env.m_environment);
                    }
                }
            }

            foreach (var list in lists)
            {
                Each(list.m_spawners, "world spawns", d => d.m_name.Length > 0 ? d.m_name : d.m_prefab != null ? d.m_prefab.name : "a spawn", data =>
                {
                    if (data.m_prefab == null || !data.m_enabled) return;
                    var name = data.m_prefab.name;
                    var (home, events) = SpawnBiomes.Split(BiomeKeys(data.m_biome), data.m_requiredEnvironments, b => weathers.TryGetValue(b, out var own) ? own : null);
                    foreach (var key in home) AddBiomeKey(BiomesOf, name, key);
                    foreach (var key in events) AddBiomeKey(EventBiomesOf, name, key);
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
                    Unlocks.Add(data.m_requiredGlobalKey, Unlock.Spawns, name);
                });
            }
        }

        private static void Raids()
        {
            var events = RandEventSystem.instance?.m_events;
            if (events == null) return;

            Each(events, "raids", r => r.m_name, raid =>
            {
                if (raid.m_spawn == null || !raid.m_enabled) return;
                var shown = CatalogBuilder.Localize(raid.m_startMessage);
                var start = $"Comes in the raid \"{(shown.Length > 0 ? shown : raid.m_name)}\"";
                var perPlayer = ByEachPlayer(raid);
                var facts = new SpawnFacts
                {
                    Biomes = raid.m_biome != 0 ? BiomeNames(raid.m_biome) : "",
                    Keys = perPlayer || raid.m_requiredGlobalKeys == null ? Array.Empty<string>() : raid.m_requiredGlobalKeys.ToArray(),
                    NotKeys = perPlayer || raid.m_notRequiredGlobalKeys == null ? Array.Empty<string>() : raid.m_notRequiredGlobalKeys.ToArray(),
                };
                var line = SpawnWords.Line(facts.Biomes.Length > 0 ? start + ", in" : start, facts, BossOf);
                foreach (var key in facts.Keys) Unlocks.Add(key, Unlock.RaidStarts, EntryKeys.For(Kind.Raid, raid.m_name));
                foreach (var key in facts.NotKeys) Unlocks.Add(key, Unlock.RaidEnds, EntryKeys.For(Kind.Raid, raid.m_name));
                if (perPlayer) line += ", for a player whose own progress calls for it";
                foreach (var data in raid.m_spawn)
                {
                    if (data?.m_prefab == null) continue;
                    Add(data.m_prefab.name, line, EntryKeys.For(Kind.Raid, raid.m_name));
                }
            });
        }

        /// <summary>
        /// Whether a raid comes by each player's own progress in this world, which then checks
        /// other keys for them (<see cref="RaidGrouping.ByEachPlayer"/>).
        /// </summary>
        public static bool ByEachPlayer(RandomEvent raid)
        {
            int Count<T>(List<T> list) => list?.Count ?? 0;
            var world = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.PlayerEvents);
            return RaidGrouping.ByEachPlayer(world, Count(raid.m_altRequiredPlayerKeysAny), Count(raid.m_altRequiredPlayerKeysAll), Count(raid.m_altRequiredKnownItems),
                Count(raid.m_altRequiredNotKnownItems), Count(raid.m_altNotRequiredPlayerKeys));
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
                var total = area.m_prefabs.Where(d => d?.m_prefab != null).Sum(d => d.m_weight);
                foreach (var data in area.m_prefabs)
                {
                    if (data?.m_prefab == null) continue;
                    Keep(SpawnPointLines, data.m_prefab, $"Comes from {Shown(prefab)}, {SpawnWords.PoolShare(data.m_weight, total, data.m_minLevel, data.m_maxLevel)}", prefab.name,
                        total > 0f ? data.m_weight / total : 0.0);
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
                Unlocks.Add(point.m_requiredGlobalKey, Unlock.Spawns, point.m_creaturePrefab.name);
                Unlocks.Add(point.m_blockingGlobalKey, Unlock.StopsSpawning, point.m_creaturePrefab.name);
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

                Add(name, SpawnWords.Grows(BiomeNames(veg.m_biome), veg.m_minAltitude, veg.m_maxAltitude,
                    veg.m_minOceanDepth > 0f || veg.m_maxOceanDepth > 0f, veg.m_inForest, veg.m_groupSizeMin, veg.m_groupSizeMax));
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
                        Keep(DropLines, drop.m_prefab, SourceFacts.Dropped(prefab.name, Shown(prefab), drop.m_amountMin, drop.m_amountMax, drop.m_onePerPlayer, drop.m_chance));
                    }
                    continue;
                }

                if (component is Pickable pickable)
                {
                    Keep(DropLines, pickable.m_itemPrefab, SourceFacts.Of(SourceWay.Picked, prefab.name, Shown(prefab)));
                }

                // A sapling or seedling tells what it grows into; what grows tells where from.
                if (component is Plant plant && plant.m_grownPrefabs != null)
                {
                    foreach (var grown in plant.m_grownPrefabs) Keep(DropLines, grown, SourceFacts.Of(SourceWay.GrowsFrom, prefab.name, Shown(prefab)));
                }

                var tables = DropTables(component.GetType());
                if (tables.Length == 0) continue;
                var of = TableOfPart(component);
                foreach (var field in tables)
                {
                    if (!(TypeFields.Value(field, component) is DropTable table) || table.m_drops == null) continue;
                    var info = InfoOf(table);
                    foreach (var data in table.m_drops)
                    {
                        if (data.m_item == null) continue;
                        Keep(DropLines, data.m_item, SourceFacts.FromTable(prefab.name, Shown(prefab), of, info, new DropInfo(data.m_item.name, data.m_stackMin, data.m_stackMax, data.m_weight)));
                    }
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
                    if (!(TypeFields.Value(field, component) is System.Collections.IEnumerable list)) continue;
                    foreach (var conversion in list)
                    {
                        if (conversion == null) continue;
                        var type = conversion.GetType();
                        var from = Conversion(type, "m_from", conversion) as ItemDrop;
                        var to = Conversion(type, "m_to", conversion) as ItemDrop;
                        if (from == null || to == null) continue;
                        // A fermenter's batch makes several (m_producedItems); a smelter's one.
                        var makes = Conversion(type, "m_producedItems", conversion) is int produced && produced > 1 ? produced : 1;
                        var making = new Making { Station = prefab.name, Output = to.gameObject.name, Makes = makes };
                        making.Inputs.Add((from.gameObject.name, 1));
                        Made.Add(making);
                        Uses.Add(from.gameObject.name, UseKind.TurnsInto, to.gameObject.name, 0, prefab.name);
                    }
                }

                // Producers make an item by themselves over time, up to what they hold.
                if (component is Beehive hive && hive.m_honeyItem != null)
                {
                    Keep(DropLines, hive.m_honeyItem.gameObject, SourceFacts.Made(prefab.name, Shown(prefab), hive.m_secPerUnit, hive.m_maxHoney, hive.m_biome != 0 ? BiomeNames(hive.m_biome) : ""));
                }
                if (component is SapCollector tap && tap.m_spawnItem != null)
                {
                    Keep(DropLines, tap.m_spawnItem.gameObject, SourceFacts.Made(prefab.name, Shown(prefab), tap.m_secPerUnit, tap.m_maxLevel, ""));
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
        /// What a drop table belongs to, which says how an item comes out of it in the words the
        /// other side's facts use (<see cref="SourceWords.Verb"/>): a tree felled, a log chopped, a
        /// rock mined, a chest found in, a bush picked, anything broken; a mod's own part, plainly.
        /// </summary>
        private static TableOf TableOfPart(Component component)
        {
            switch (component)
            {
                case TreeBase _: return TableOf.Tree;
                case TreeLog _: return TableOf.Log;
                case MineRock _:
                case MineRock5 _: return TableOf.Rock;
                case Container _: return TableOf.Container;
                case Pickable _: return TableOf.Pickable;
                case DropOnDestroyed _: return TableOf.Broken;
                default: return TableOf.Other;
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
                    From(trade.m_prefab.gameObject.name, Told(SourceFacts.Sold(self, name, trade.m_stack, trade.m_price, trade.m_requiredGlobalKey)));
                    Unlocks.Add(trade.m_requiredGlobalKey, Unlock.Sells, trade.m_prefab.gameObject.name);
                }
            });
        }

        private static readonly Dictionary<Type, FieldInfo[]> ConversionFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Fields holding a list or array of entries with an m_from and an m_to item.</summary>
        private static FieldInfo[] Conversions(Type type) => TypeFields.Matching(ConversionFields, type, field =>
        {
            var element = field.FieldType.IsArray ? field.FieldType.GetElementType()
                : field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>) ? field.FieldType.GetGenericArguments()[0] : null;
            return element != null && TypeFields.Named(element, "m_from")?.FieldType == typeof(ItemDrop) && TypeFields.Named(element, "m_to")?.FieldType == typeof(ItemDrop);
        });

        /// <summary>A field of a conversion entry by its name, whatever type it is, or null where it has none.</summary>
        private static object Conversion(Type type, string name, object conversion)
        {
            var field = TypeFields.Named(type, name);
            return field == null ? null : TypeFields.Value(field, conversion);
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
                    var value = TypeFields.Value(field, owner);
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
        private static FieldInfo[] EffectRefs(Type type) => TypeFields.Matching(EffectRefFields, type, field =>
            typeof(StatusEffect).IsAssignableFrom(field.FieldType)
            || field.FieldType == typeof(string) && field.Name.IndexOf("statuseffect", StringComparison.OrdinalIgnoreCase) >= 0);

        private static string ItemName(GameObject item)
        {
            var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            var name = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
            return name.Length > 0 ? name : item != null ? item.name : "";
        }

        /// <summary>
        /// The items a prefab gives: an item it is, the item a pickable yields, and what any of its
        /// drop tables holds (a chest's filling, what a rock or a pile of remains drops), each once.
        /// </summary>
        public static List<string> LootOf(GameObject prefab)
        {
            var items = new List<string>();
            if (prefab == null) return items;
            void Add(GameObject item)
            {
                if (item != null && !items.Contains(item.name)) items.Add(item.name);
            }
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                switch (component)
                {
                    case null:
                        continue;
                    case ItemDrop item when component.gameObject == prefab:
                        Add(item.gameObject);
                        break;
                    case Pickable pickable:
                        Add(pickable.m_itemPrefab);
                        break;
                }
                foreach (var field in DropTables(component.GetType()))
                {
                    if (!(TypeFields.Value(field, component) is DropTable table) || table.m_drops == null) continue;
                    foreach (var data in table.m_drops) Add(data.m_item);
                }
            }
            return items;
        }

        /// <summary>
        /// Whether a prefab gives loot of its own: a drop table holding anything on any of its
        /// components (a chest's filling, a rock's or tree's drops), or a pickable's item. A chest
        /// built by players has an empty table, and gives nothing.
        /// </summary>
        public static bool GivesLoot(GameObject prefab)
        {
            if (prefab == null) return false;
            foreach (var component in prefab.GetComponents<Component>())
            {
                if (component == null) continue;
                if (component is Pickable pickable && pickable.m_itemPrefab != null) return true;
                foreach (var field in DropTables(component.GetType()))
                {
                    if (TypeFields.Value(field, component) is DropTable table && table.m_drops != null && table.m_drops.Count > 0) return true;
                }
            }
            // A shell that breaks into what is mined gives what that gives, as its details tell.
            var breaks = prefab.GetComponent<Destructible>();
            var inside = breaks != null ? MinedInside(breaks.m_spawnWhenDestroyed) : null;
            return inside != null && inside != prefab && GivesLoot(inside);
        }

        /// <summary>A drop table as the model tells it: how it is rolled, and each item it holds, in its order.</summary>
        internal static DropTableInfo InfoOf(DropTable table)
        {
            var info = new DropTableInfo { Min = table.m_dropMin, Max = table.m_dropMax, Chance = table.m_dropChance, OneOfEach = table.m_oneOfEach };
            if (table.m_drops == null) return info;
            foreach (var data in table.m_drops) if (data.m_item != null) info.Drops.Add(new DropInfo(data.m_item.name, data.m_stackMin, data.m_stackMax, data.m_weight));
            return info;
        }

        /// <summary>The drop table fields of a type and its bases, found once per type; none for a mod's type whose fields cannot be read.</summary>
        internal static FieldInfo[] DropTables(Type type) => TypeFields.Matching(DropTableFields, type, field => field.FieldType == typeof(DropTable));

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
    }
}
