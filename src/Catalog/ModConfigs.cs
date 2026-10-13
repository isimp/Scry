using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace Scry
{
    /// <summary>
    /// The mods' settings, as each plugin's own config holds them (<c>BaseUnityPlugin.Config</c>):
    /// their sections indexed by the names they go by (<see cref="ConfigSections"/>) the first time
    /// one is asked for, their values read when asked, so a setting changed in game or by a server
    /// shows as it is now. Scry's own are left out.
    /// </summary>
    internal static class ModConfigs
    {
        /// <summary>Each section by the names it goes by, with its mod and config; null until first asked.</summary>
        private static Dictionary<string, List<(string Mod, ConfigFile File, string Section)>> _byName;

        /// <summary>Counts up whenever one of the indexed settings changes, so details telling them are told again.</summary>
        public static int Version { get; private set; }

        /// <summary>
        /// The sections about an item of this shown name or prefab, each with its mod and its
        /// settings in the config's order, their values as the config file writes them.
        /// </summary>
        public static List<(string Mod, string Section, List<(string Key, string Value)> Settings)> About(string shownName, string prefab)
        {
            var found = new List<(string, string, List<(string, string)>)>();
            var index = Index();
            var told = new HashSet<(ConfigFile, string)>();
            foreach (var name in ConfigSections.ItemNames(shownName, prefab))
            {
                if (!index.TryGetValue(name, out var sections)) continue;
                foreach (var (mod, file, section) in sections)
                {
                    if (!told.Add((file, section))) continue;
                    var settings = new List<(string, string)>();
                    foreach (var definition in file.Keys)
                    {
                        if (definition.Section == section) settings.Add((definition.Key, file[definition].GetSerializedValue()));
                    }
                    found.Add((mod, section, settings));
                }
            }
            return found;
        }

        private static Dictionary<string, List<(string Mod, ConfigFile File, string Section)>> Index()
        {
            if (_byName != null) return _byName;
            _byName = new Dictionary<string, List<(string, ConfigFile, string)>>(StringComparer.Ordinal);
            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                var mod = info?.Metadata?.Name;
                if (string.IsNullOrEmpty(mod) || (info.Metadata.GUID ?? "").StartsWith(Scry.About.Guid, StringComparison.Ordinal)) continue;
                Guard.Each(Feature.ModSettings, "mods' settings", mod, () =>
                {
                    var file = info.Instance != null ? info.Instance.Config : null;
                    if (file == null) return;
                    file.SettingChanged += (_, __) => Version++;
                    foreach (var section in file.Keys.Select(d => d.Section).Distinct())
                    {
                        foreach (var name in ConfigSections.NamesOf(section))
                        {
                            if (!_byName.TryGetValue(name, out var sections)) _byName[name] = sections = new List<(string, ConfigFile, string)>();
                            sections.Add((mod, file, section));
                        }
                    }
                });
            }
            return _byName;
        }
    }
}
