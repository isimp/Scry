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

        /// <summary>Reads it all again for the current world.</summary>
        public static void Gather(IEnumerable<GameObject> registered)
        {
            Where.Clear();
            BiomesOf.Clear();
            ModOf.Clear();
            ComesFrom.Clear();

            var prefabs = registered.Where(p => p != null).ToList();
            Try("world spawners", WorldSpawners);
            Try("raids", Raids);
            Try("nests and spawn points", () => Spawners(prefabs));
            Try("vegetation", Vegetation);
            Try("drops", () => Drops(prefabs));
            Try("makers and traders", () => Makers(prefabs));
            Try("status effect givers", () => Givers(prefabs));
            Try("Jotunn's registry", JotunnMods);
            Try("asset bundles", () => BundleMods(prefabs));
        }

        private static void Try(string what, Action act)
        {
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

        private static void Spawners(List<GameObject> prefabs)
        {
            foreach (var prefab in prefabs)
            {
                foreach (var area in prefab.GetComponentsInChildren<SpawnArea>(true))
                {
                    foreach (var data in area.m_prefabs)
                    {
                        if (data?.m_prefab == null) continue;
                        Add(data.m_prefab.name, $"Comes from {Shown(prefab)}, {Levels(data.m_minLevel, data.m_maxLevel)}", prefab.name);
                    }
                }
                foreach (var point in prefab.GetComponentsInChildren<CreatureSpawner>(true))
                {
                    if (point.m_creaturePrefab == null) continue;
                    Add(point.m_creaturePrefab.name, $"In dungeons or locations, from the spawn point {prefab.name}", prefab.name);
                }
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
        private static void Drops(List<GameObject> prefabs)
        {
            void From(GameObject item, string line, string target)
            {
                if (item == null) return;
                if (!ComesFrom.TryGetValue(item.name, out var lines)) ComesFrom[item.name] = lines = new List<Source>();
                if (!lines.Exists(l => l.Text == line) && lines.Count < 40) lines.Add(new Source(line, target));
            }

            foreach (var prefab in prefabs)
            {
                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;

                    if (component is CharacterDrop drops)
                    {
                        foreach (var drop in drops.m_drops)
                        {
                            if (drop?.m_prefab == null) continue;
                            var amount = drop.m_amountMin == drop.m_amountMax ? $"{drop.m_amountMin}" : $"{drop.m_amountMin} to {drop.m_amountMax}";
                            var chance = drop.m_chance < 1f ? $", {Mathf.RoundToInt(drop.m_chance * 100f)}%" : "";
                            From(drop.m_prefab, $"Dropped by {Shown(prefab)} ({amount}{chance})", prefab.name);
                        }
                        continue;
                    }

                    if (component is Pickable pickable)
                    {
                        From(pickable.m_itemPrefab, $"Picked from {Shown(prefab)}", prefab.name);
                    }

                    foreach (var field in DropTables(component.GetType()))
                    {
                        if (!(field.GetValue(component) is DropTable table) || table.m_drops == null) continue;
                        foreach (var data in table.m_drops) From(data.m_item, $"Comes out of {Shown(prefab)}", prefab.name);
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
        private static void Makers(List<GameObject> prefabs)
        {
            void From(GameObject item, string line, string target)
            {
                if (item == null) return;
                if (!ComesFrom.TryGetValue(item.name, out var lines)) ComesFrom[item.name] = lines = new List<Source>();
                if (!lines.Exists(l => l.Text == line) && lines.Count < 40) lines.Add(new Source(line, target));
            }

            foreach (var prefab in prefabs)
            {
                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
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
                            From(to.gameObject, $"Made from {ItemName(from.gameObject)} in {Shown(prefab)}", from.gameObject.name);
                        }
                    }
                }
            }

            foreach (var trader in Resources.FindObjectsOfTypeAll<Trader>())
            {
                if (trader == null || trader.m_items == null) continue;
                var name = CatalogBuilder.Localize(trader.m_name);
                if (name.Length == 0) name = trader.gameObject.name;
                foreach (var trade in trader.m_items)
                {
                    if (trade?.m_prefab == null) continue;
                    var stack = trade.m_stack > 1 ? $"{trade.m_stack} for " : "";
                    From(trade.m_prefab.gameObject, $"Sold by {name}, {stack}{trade.m_price} coins", null);
                }
            }
        }

        private static readonly Dictionary<Type, FieldInfo[]> ConversionFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Fields holding a list or array of entries with an m_from and an m_to item.</summary>
        private static FieldInfo[] Conversions(Type type)
        {
            if (ConversionFields.TryGetValue(type, out var known)) return known;
            var found = new List<FieldInfo>();
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
        private static void Givers(List<GameObject> prefabs)
        {
            GiverList.Clear();

            void Note(object owner, GameObject prefab)
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

            foreach (var prefab in prefabs)
            {
                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    Note(component, prefab);
                    var shared = (component as ItemDrop)?.m_itemData?.m_shared;
                    if (shared == null) continue;
                    Note(shared, prefab);
                    if (shared.m_attack != null) Note(shared.m_attack, prefab);
                    if (shared.m_secondaryAttack != null) Note(shared.m_secondaryAttack, prefab);
                }
            }
        }

        /// <summary>Fields holding a status effect, or a status effect's name.</summary>
        private static FieldInfo[] EffectRefs(Type type)
        {
            if (EffectRefFields.TryGetValue(type, out var known)) return known;
            var found = new List<FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    if (typeof(StatusEffect).IsAssignableFrom(field.FieldType)) found.Add(field);
                    else if (field.FieldType == typeof(string) && field.Name.IndexOf("statuseffect", StringComparison.OrdinalIgnoreCase) >= 0) found.Add(field);
                }
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
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags)) if (field.FieldType == typeof(DropTable)) found.Add(field);
            }
            known = found.ToArray();
            DropTableFields[type] = known;
            return known;
        }

        private static string Shown(GameObject prefab)
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

        /// <summary>
        /// Jotunn keeps a registry of what each mod built on it added, with the mod. Read by name,
        /// so Scry needs no reference to Jotunn and does nothing when it is not installed.
        /// </summary>
        private static void JotunnMods()
        {
            var registry = AccessTools.TypeByName("Jotunn.Utils.ModRegistry");
            if (registry == null) return;

            void Read(string method, string property)
            {
                var get = AccessTools.Method(registry, method, new Type[0]);
                if (get == null) return;
                if (!(get.Invoke(null, null) is System.Collections.IEnumerable entities)) return;

                foreach (var entity in entities)
                {
                    if (entity == null) continue;
                    var mod = AccessTools.Property(entity.GetType(), "SourceMod")?.GetValue(entity) as BepInEx.BepInPlugin;
                    var thing = AccessTools.Property(entity.GetType(), property)?.GetValue(entity) as UnityEngine.Object;
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
        private static void BundleMods(List<GameObject> prefabs)
        {
            var wanted = new HashSet<string>(prefabs.Where(p => !ModOf.ContainsKey(p.name) && Origins.Prefabs.Of(p.name) == Origin.Mod)
                .Select(p => p.name.ToLowerInvariant()));
            if (wanted.Count == 0) return;

            var bundleOf = new Dictionary<string, string>();
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle == null || bundle.isStreamedSceneAssetBundle) continue;
                foreach (var path in bundle.GetAllAssetNames())
                {
                    var file = Path.GetFileNameWithoutExtension(path);
                    if (wanted.Contains(file) && !bundleOf.ContainsKey(file)) bundleOf[file] = Leaf(bundle.name);
                }
            }
            if (bundleOf.Count == 0) return;

            var modOfBundle = new Dictionary<string, string>();
            var needed = new HashSet<string>(bundleOf.Values);
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                var name = info?.Metadata?.Name;
                if (string.IsNullOrEmpty(name)) continue;

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
            }

            foreach (var prefab in prefabs)
            {
                if (ModOf.ContainsKey(prefab.name)) continue;
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
