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
    /// Which mod added a prefab, a status effect, a location, a recipe or a station's conversion,
    /// from four clues in turn: Jotunn's registry, the mod whose assembly holds its scripts, a
    /// bundle a mod ships holding it by name, and the bundles holding the sounds and icons it
    /// uses (<see cref="ModAttribution"/>).
    /// </summary>
    internal static partial class Knowledge
    {
        // ----- Which mod -----

        /// <summary>Jotunn's plugin id, as it loads (Jotunn 2.30: "Loading [Jotunn 2.30.2] (com.jotunn.jotunn)").</summary>
        private const string JotunnGuid = "com.jotunn.jotunn";

        private static readonly Dictionary<(Type, string), PropertyInfo> Properties = new Dictionary<(Type, string), PropertyInfo>();

        /// <summary>The mod that added each recipe, by the recipe's name, as Jotunn's registry tells.</summary>
        private static readonly Dictionary<string, string> RecipeMods = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The mod that added each conversion, by its station, what goes in and what comes out, as Jotunn's registry tells.</summary>
        private static readonly Dictionary<(string Station, string From, string To), string> ConversionMods = new Dictionary<(string, string, string), string>();

        /// <summary>The mods whose scripts are on each prefab a mod added, until it is named; and the script types found on the game's own prefabs too, which other mods put there and so say nothing.</summary>
        private static readonly Dictionary<string, HashSet<Type>> ScriptsOf = new Dictionary<string, HashSet<Type>>(StringComparer.Ordinal);
        private static readonly HashSet<Type> ScriptsOnGamePrefabs = new HashSet<Type>();

        /// <summary>The plugin each assembly is, by name, Jotunn's and Scry's left out as they make nothing of their own.</summary>
        private static Dictionary<Assembly, string> _pluginOf;

        /// <summary>How many things each clue named, for the self-test to tell.</summary>
        public static readonly Dictionary<string, int> NamedBy = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>The clue each thing's mod was named by, by the thing's name (<see cref="UnsureWords.IsSureClue"/>).</summary>
        private static readonly Dictionary<string, string> ClueOf = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The clue a thing's mod was named by, or "" when none named it.</summary>
        public static string ModClue(string name) => name != null && ClueOf.TryGetValue(name, out var clue) ? clue : "";

        /// <summary>How many recipes and conversions Jotunn's registry names a mod for, for the self-test to tell.</summary>
        public static int RecipesNamed => RecipeMods.Count;
        public static int ConversionsNamed => ConversionMods.Count;

        /// <summary>The mod that added a recipe, or "" when the game's or not known.</summary>
        public static string RecipeMod(string recipe) => recipe != null && RecipeMods.TryGetValue(recipe, out var mod) ? mod : "";

        /// <summary>The mod that added a way a station makes something, when every way it takes in was added by that one mod; else "".</summary>
        public static string ConversionMod(Making making)
        {
            if (making == null || making.Inputs.Count == 0) return "";
            var mods = new List<string>();
            foreach (var (input, _) in making.Inputs)
            {
                if (!ConversionMods.TryGetValue((making.Station, input, making.Output), out var mod)) return "";
                mods.Add(mod);
            }
            return ModAttribution.Pick(null, mods) ?? "";
        }

        private static void ForgetMods()
        {
            RecipeMods.Clear();
            ConversionMods.Clear();
            ScriptsOf.Clear();
            ScriptsOnGamePrefabs.Clear();
            NamedBy.Clear();
            ClueOf.Clear();
            _pluginOf = null;
        }

        private static void Named(string name, string mod, string clue)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(mod) || ModOf.ContainsKey(name)) return;
            ModOf[name] = mod;
            ClueOf[name] = clue;
            NamedBy.TryGetValue(clue, out var count);
            NamedBy[clue] = count + 1;
        }

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

            object Value(object entity, string name)
            {
                var type = entity.GetType();
                if (!Properties.TryGetValue((type, name), out var property)) Properties[(type, name)] = property = AccessTools.Property(type, name);
                return property?.GetValue(entity);
            }

            void Each(string method, Action<string, object> take)
            {
                var get = AccessTools.Method(registry, method, new Type[0]);
                if (get == null) return;
                if (!(get.Invoke(null, null) is System.Collections.IEnumerable entities)) return;
                foreach (var entity in entities)
                {
                    if (entity == null) continue;
                    var mod = Value(entity, "SourceMod") as BepInEx.BepInPlugin;
                    if (mod != null) take(mod.Name, entity);
                }
            }

            // What it registers as a prefab or object, by that thing's name; a location may be
            // loaded only when placed, so it goes by the name it is registered under.
            void Read(string method, string property)
            {
                Each(method, (mod, entity) =>
                {
                    switch (Value(entity, property))
                    {
                        case UnityEngine.Object thing when thing != null: Named(thing.name, mod, UnsureWords.RegistryClue); break;
                        case string name: Named(name, mod, UnsureWords.RegistryClue); break;
                    }
                });
            }

            Read("GetPrefabs", "Prefab");
            Read("GetItems", "ItemPrefab");
            Read("GetPieces", "PiecePrefab");
            Read("GetStatusEffects", "StatusEffect");
            Read("GetCreatures", "Prefab");
            Read("GetVegetation", "Prefab");
            Read("GetClutter", "Prefab");
            Read("GetLocations", "Name");

            Each("GetRecipes", (mod, entity) =>
            {
                if (Value(entity, "Recipe") is Recipe recipe && recipe != null) RecipeMods[recipe.name] = mod;
            });
            Each("GetItemConversions", (mod, entity) =>
            {
                var config = Value(entity, "Config");
                if (config == null) return;
                var station = Value(config, "Station") as string;
                var from = Value(config, "FromItem") as string;
                var to = Value(config, "ToItem") as string;
                if (!string.IsNullOrEmpty(station) && !string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to)) ConversionMods[(station, from, to)] = mod;
            });
        }

        /// <summary>The plugin each assembly is, by its name, found once a catalog.</summary>
        private static Dictionary<Assembly, string> PluginOf()
        {
            if (_pluginOf != null) return _pluginOf;
            _pluginOf = new Dictionary<Assembly, string>();
            foreach (var info in Chainloader.PluginInfos.Values.ToList())
            {
                var assembly = info?.Instance != null ? info.Instance.GetType().Assembly : null;
                if (assembly == null || info.Metadata == null || info.Metadata.GUID == JotunnGuid || info.Metadata.GUID == Plugin.Guid) continue;
                if (!_pluginOf.ContainsKey(assembly)) _pluginOf[assembly] = info.Metadata.Name;
            }
            return _pluginOf;
        }

        /// <summary>
        /// The scripts on a prefab from a mod's assembly, noted as the catalog reads it: on a mod's
        /// prefab not yet named, a clue to its mod; on the game's own, a script some mod puts on
        /// prefabs at large, which says nothing of where a prefab came from.
        /// </summary>
        private static void Scripts(GameObject prefab, List<Component> components)
        {
            var origin = Origins.Prefabs.Of(prefab.name);
            if (origin == Origin.Unknown) return;
            var plugins = PluginOf();
            foreach (var component in components)
            {
                if (component == null) continue;
                var type = component.GetType();
                if (!plugins.ContainsKey(type.Assembly)) continue;
                if (origin == Origin.Vanilla) ScriptsOnGamePrefabs.Add(type);
                else
                {
                    if (!ScriptsOf.TryGetValue(prefab.name, out var types)) ScriptsOf[prefab.name] = types = new HashSet<Type>();
                    types.Add(type);
                }
            }
        }

        /// <summary>
        /// The mods' prefabs and status effects not yet named, by their scripts: those from one
        /// mod's assembly, and on none of the game's own prefabs, name that mod.
        /// </summary>
        private static void ScriptMods()
        {
            var plugins = PluginOf();
            foreach (var pair in ScriptsOf)
            {
                if (ModOf.ContainsKey(pair.Key)) continue;
                Named(pair.Key, ModAttribution.Pick(null, pair.Value.Where(t => !ScriptsOnGamePrefabs.Contains(t)).Select(t => plugins[t.Assembly])), "its scripts");
            }
            foreach (var effect in ModStatusEffects())
            {
                if (ModOf.ContainsKey(effect.name)) continue;
                if (plugins.TryGetValue(effect.GetType().Assembly, out var mod)) Named(effect.name, mod, "its scripts");
            }
        }

        private static IEnumerable<StatusEffect> ModStatusEffects()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_StatusEffects == null) yield break;
            foreach (var effect in db.m_StatusEffects) if (effect != null && Origins.StatusEffects.Of(effect.name) == Origin.Mod) yield return effect;
        }

        /// <summary>
        /// For the rest: a mod's prefabs usually come out of an asset bundle it ships, either as a
        /// file in its folder or embedded in its assembly. The bundles holding a prefab of the same
        /// name, or the sounds and icons it uses (a sound's clips, an item's or piece's icon, a
        /// status effect's), are found among those loaded, and the mod shipping a bundle of that
        /// name named (<see cref="ModAttribution.Pick"/>). A prefab made in code from the game's
        /// own parts, or a bundle named unlike anything the mod ships, stays unnamed.
        /// </summary>
        private static IEnumerable<string> BundleMods(List<GameObject> prefabs)
        {
            var started = CatalogTiming.Start();
            // What each thing not yet named is called and uses, its own name first.
            var clues = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var prefab in prefabs)
            {
                if (prefab == null || ModOf.ContainsKey(prefab.name) || Origins.Prefabs.Of(prefab.name) != Origin.Mod || clues.ContainsKey(prefab.name)) continue;
                Guard.Each("which mod added what", prefab.name, () => clues[prefab.name] = AssetsOf(prefab));
            }
            foreach (var effect in ModStatusEffects())
            {
                if (ModOf.ContainsKey(effect.name) || clues.ContainsKey(effect.name)) continue;
                var names = new List<string> { effect.name.ToLowerInvariant() };
                if (effect.m_icon != null && !string.IsNullOrEmpty(effect.m_icon.name)) names.Add(effect.m_icon.name.ToLowerInvariant());
                clues[effect.name] = names;
            }
            var wanted = new HashSet<string>(clues.Values.SelectMany(c => c));
            CatalogTiming.Add("asset bundles", started);
            if (wanted.Count == 0) yield break;

            // A bundle, or a mod's folder, at a time: looking through them all took some 150 ms.
            var bundlesOf = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (!Guard.Run("reading the loaded asset bundles", () => AssetBundle.GetAllLoadedAssetBundles().ToList(), out var bundles)) yield break;
            foreach (var bundle in bundles)
            {
                started = CatalogTiming.Start();
                Guard.Each("which mod added what", "a bundle", () =>
                {
                    if (bundle != null && !bundle.isStreamedSceneAssetBundle)
                    {
                        var leaf = Leaf(bundle.name);
                        foreach (var path in bundle.GetAllAssetNames())
                        {
                            var file = Path.GetFileNameWithoutExtension(path);
                            if (!wanted.Contains(file)) continue;
                            if (!bundlesOf.TryGetValue(file, out var holding)) bundlesOf[file] = holding = new HashSet<string>(StringComparer.Ordinal);
                            holding.Add(leaf);
                        }
                    }
                });
                CatalogTiming.Add("asset bundles", started);
                yield return "which mod added what: asset bundles";
            }
            if (bundlesOf.Count == 0) yield break;

            var modOfBundle = new Dictionary<string, string>();
            var needed = new HashSet<string>(bundlesOf.Values.SelectMany(b => b));
            foreach (var info in Chainloader.PluginInfos.Values.ToList())
            {
                var name = info?.Metadata?.Name;
                if (string.IsNullOrEmpty(name)) continue;
                started = CatalogTiming.Start();

                Guard.Each("which mod added what", name, () =>
                {
                    var assembly = info.Instance != null ? info.Instance.GetType().Assembly : null;
                    if (assembly != null)
                    {
                        foreach (var resource in assembly.GetManifestResourceNames())
                        {
                            var lower = resource.ToLowerInvariant();
                            foreach (var bundle in needed)
                            {
                                if (lower == bundle || lower.EndsWith("." + bundle, StringComparison.Ordinal)) modOfBundle[bundle] = name;
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
                });
                CatalogTiming.Add("asset bundles", started);
                yield return "which mod added what: mods' files";
            }

            IEnumerable<string> ModsHolding(string asset) =>
                bundlesOf.TryGetValue(asset, out var holding) ? holding.Select(b => modOfBundle.TryGetValue(b, out var mod) ? mod : null) : Enumerable.Empty<string>();

            foreach (var pair in clues)
            {
                if (ModOf.ContainsKey(pair.Key)) continue;
                var byName = ModAttribution.Pick(null, ModsHolding(pair.Value[0]));
                var mod = ModAttribution.Pick(byName, pair.Value.Skip(1).SelectMany(ModsHolding));
                Named(pair.Key, mod, byName != null ? "a bundle holding it" : "the bundles holding what it uses");
            }
        }

        /// <summary>
        /// A prefab's name and the names of what it uses that a mod would ship in its bundle with
        /// it: its sounds' clips and its icons, or, for what has neither (an effect), its
        /// materials' pictures. Materials' own names are left out, as mods ship copies of common
        /// ones; at most a few dozen names a prefab.
        /// </summary>
        private static List<string> AssetsOf(GameObject prefab)
        {
            const int most = 40;
            var names = new List<string> { prefab.name.ToLowerInvariant() };
            void Add(UnityEngine.Object asset)
            {
                if (asset == null || string.IsNullOrEmpty(asset.name) || names.Count >= most) return;
                var name = asset.name.ToLowerInvariant();
                if (!names.Contains(name)) names.Add(name);
            }

            foreach (var sfx in prefab.GetComponentsInChildren<ZSFX>(true)) if (sfx.m_audioClips != null) foreach (var clip in sfx.m_audioClips) Add(clip);
            foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true)) Add(source.clip);
            var icons = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
            if (icons != null) foreach (var icon in icons) Add(icon);
            var piece = prefab.GetComponent<Piece>();
            if (piece != null) Add(piece.m_icon);
            if (names.Count > 1) return names;

            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.HasProperty("_MainTex")) Add(material.mainTexture);
                }
            }
            return names;
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
