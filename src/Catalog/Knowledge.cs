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

    internal static class Knowledge
    {
        private static readonly Dictionary<string, List<Source>> Where = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, HashSet<string>> BiomesOf = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ModOf = new Dictionary<string, string>(StringComparer.Ordinal);
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
            return BiomesOf.TryGetValue(prefab, out var set) ? set.ToArray() : new string[0];
        }

        public static string ModName(string name)
        {
            return ModOf.TryGetValue(name, out var mod) ? mod : "";
        }

        // Read prefab by prefab, then put together in the order the lines were always told in.
        private static readonly Dictionary<string, List<Source>> SpawnPointLines = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<Source>> DropLines = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<Source>> MakerLines = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly Dictionary<GameObject, string> ShownNames = new Dictionary<GameObject, string>();
        private static readonly HashSet<string> Failures = new HashSet<string>();

        /// <summary>Starts reading it all again for the current world.</summary>
        public static void Begin()
        {
            Where.Clear();
            BiomesOf.Clear();
            ModOf.Clear();
            ComesFrom.Clear();
            GiverList.Clear();
            SpawnPointLines.Clear();
            DropLines.Clear();
            MakerLines.Clear();
            ShownNames.Clear();
            Failures.Clear();
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
        }

        private static void Failed(string what, GameObject prefab, Exception ex)
        {
            if (Failures.Add(what + "|" + ex.GetType().Name + "|" + ex.Message))
            {
                Plugin.Log.LogWarning($"Scry could not read the {what} of {prefab.name}, and leaves them out (said once for this kind of failure): {ex.Message}");
            }
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
            Try("nests and spawn points", () => Merge(SpawnPointLines, (prefab, line) => Add(prefab, line.Text, line.Prefab)));
            Try("vegetation", Vegetation);
            yield return "where things live";

            Try("drops", () => Merge(DropLines, From));
            Try("makers", () => Merge(MakerLines, From));
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
                Plugin.Log.LogWarning($"Scry could not read {what}: {ex.Message}");
            }
            CatalogTiming.Add(what, started);
        }

        /// <summary>A line of what something comes from, for one item: each line once, and at most 40.</summary>
        private static void From(string item, Source line)
        {
            if (!ComesFrom.TryGetValue(item, out var lines)) ComesFrom[item] = lines = new List<Source>();
            if (!lines.Exists(l => l.Text == line.Text) && lines.Count < 40) lines.Add(line);
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

        /// <summary>Biomes by the names the game shows, joined.</summary>
        public static string BiomeNames(Heightmap.Biome biome)
        {
            var all = Singles(biome).ToList();
            var every = Enum.GetValues(typeof(Heightmap.Biome)).Cast<Heightmap.Biome>().Count(b => { var i = (int)b; return i != 0 && (i & (i - 1)) == 0; });
            if (all.Count == 0) return "no biome";
            if (all.Count >= every) return "every biome";

            return string.Join(", ", all.Select(b =>
            {
                var shown = CatalogBuilder.Localize("$biome_" + b.ToString().ToLowerInvariant());
                return shown.Length > 0 ? shown : Naming.FieldLabel(b.ToString());
            }));
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
                foreach (var data in list.m_spawners)
                {
                    if (data?.m_prefab == null || !data.m_enabled) continue;
                    var name = data.m_prefab.name;
                    AddBiomes(name, data.m_biome);

                    var parts = new List<string> { "Spawns in " + BiomeNames(data.m_biome) };
                    if (data.m_spawnAtDay != data.m_spawnAtNight) parts.Add(data.m_spawnAtNight ? "at night" : "by day");
                    parts.Add(Levels(data.m_minLevel, data.m_maxLevel));
                    if (data.m_groupSizeMax > 1) parts.Add($"in groups of {data.m_groupSizeMin} to {data.m_groupSizeMax}");
                    if (!string.IsNullOrEmpty(data.m_requiredGlobalKey)) parts.Add("once " + data.m_requiredGlobalKey + " is set");
                    Add(name, string.Join(", ", parts));
                }
            }
        }

        private static string Levels(int min, int max)
        {
            if (max <= 1) return "no stars";
            return min == max ? $"level {min}" : $"level {min} to {max}";
        }

        private static void Raids()
        {
            var events = RandEventSystem.instance?.m_events;
            if (events == null) return;

            foreach (var raid in events)
            {
                if (raid?.m_spawn == null || !raid.m_enabled) continue;
                foreach (var data in raid.m_spawn)
                {
                    if (data?.m_prefab == null) continue;
                    Add(data.m_prefab.name, $"Comes in the raid \"{raid.m_name}\"");
                }
            }
        }

        private static void SpawnPoints(GameObject prefab, List<Component> components)
        {
            foreach (var component in components)
            {
                if (!(component is SpawnArea area) || area.m_prefabs == null) continue;
                foreach (var data in area.m_prefabs)
                {
                    if (data?.m_prefab == null) continue;
                    Keep(SpawnPointLines, data.m_prefab, $"Comes from {Shown(prefab)}, {Levels(data.m_minLevel, data.m_maxLevel)}", prefab.name);
                }
            }
            foreach (var component in components)
            {
                if (!(component is CreatureSpawner point) || point.m_creaturePrefab == null) continue;
                Keep(SpawnPointLines, point.m_creaturePrefab, $"In dungeons or locations, from the spawn point {prefab.name}", prefab.name);
            }
        }

        private static void Vegetation()
        {
            var vegetation = ZoneSystem.instance?.m_vegetation;
            if (vegetation == null) return;

            foreach (var veg in vegetation)
            {
                if (veg?.m_prefab == null || !veg.m_enable) continue;
                var name = veg.m_prefab.name;
                AddBiomes(name, veg.m_biome);

                var line = "Grows in " + BiomeNames(veg.m_biome);
                if (veg.m_minAltitude > -1000f || veg.m_maxAltitude < 1000f)
                {
                    line += veg.m_maxAltitude < 1000f
                        ? $", {Mathf.RoundToInt(veg.m_minAltitude)} to {Mathf.RoundToInt(veg.m_maxAltitude)} m up"
                        : $", from {Mathf.RoundToInt(veg.m_minAltitude)} m up";
                }
                if (veg.m_minOceanDepth > 0f || veg.m_maxOceanDepth > 0f) line += ", in the sea";
                Add(name, line);
            }
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
                        var amount = drop.m_amountMin == drop.m_amountMax ? $"{drop.m_amountMin}" : $"{drop.m_amountMin} to {drop.m_amountMax}";
                        var chance = drop.m_chance < 1f ? $", {Mathf.RoundToInt(drop.m_chance * 100f)}%" : "";
                        Keep(DropLines, drop.m_prefab, $"Dropped by {Shown(prefab)} ({amount}{chance})", prefab.name);
                    }
                    continue;
                }

                if (component is Pickable pickable)
                {
                    Keep(DropLines, pickable.m_itemPrefab, $"Picked from {Shown(prefab)}", prefab.name);
                }

                var tables = DropTables(component.GetType());
                if (tables.Length == 0) continue;
                string shown = null;
                foreach (var field in tables)
                {
                    if (!(field.GetValue(component) is DropTable table) || table.m_drops == null) continue;
                    shown = shown ?? $"Comes out of {Shown(prefab)}";
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
                        Keep(MakerLines, to.gameObject, $"Made from {ItemName(from.gameObject)} in {Shown(prefab)}", from.gameObject.name);
                    }
                }
            }
        }

        /// <summary>What traders sell. They stand in locations, so only those loaded are found.</summary>
        private static void Traders()
        {
            foreach (var trader in Resources.FindObjectsOfTypeAll<Trader>())
            {
                if (trader == null || trader.m_items == null) continue;
                var name = CatalogBuilder.Localize(trader.m_name);
                if (name.Length == 0) name = trader.gameObject.name;
                foreach (var trade in trader.m_items)
                {
                    if (trade?.m_prefab == null) continue;
                    var stack = trade.m_stack > 1 ? $"{trade.m_stack} for " : "";
                    From(trade.m_prefab.gameObject.name, new Source($"Sold by {name}, {stack}{trade.m_price} coins", null));
                }
            }
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
                Plugin.Log.LogDebug($"Scry could not read the fields of {type.Name}: {ex.Message}");
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
                Plugin.Log.LogDebug($"Scry could not read the fields of {type.Name}: {ex.Message}");
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
                Plugin.Log.LogDebug($"Scry could not read the fields of {type.Name}: {ex.Message}");
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

        private static string ShownOf(GameObject prefab)
        {
            string token = null;
            var character = prefab.GetComponent<Character>();
            if (character != null) token = character.m_name;
            var piece = prefab.GetComponent<Piece>();
            if (token == null && piece != null) token = piece.m_name;
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
                    Plugin.Log.LogDebug($"Scry could not look through a bundle: {ex.Message}");
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
                    Plugin.Log.LogDebug($"Scry could not look through {name}: {ex.Message}");
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
