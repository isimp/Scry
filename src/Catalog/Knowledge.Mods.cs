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
    /// <summary>Which mod added a prefab.</summary>
    internal static partial class Knowledge
    {
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
