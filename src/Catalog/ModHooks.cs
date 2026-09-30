using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// Which mods hook into the game's own code for what creatures drop, what drop tables give
    /// and where creatures spawn, from Harmony's record of every patch (<c>Harmony.GetPatchInfo</c>),
    /// each patch's mod being the plugin whose assembly holds it. Such a mod can change these
    /// beyond anything read from the prefabs, so the details say so (<see cref="ModHookWords"/>).
    /// A creature's death is hooked for many reasons (a death screen, a skill), so a hook there
    /// counts only when its own code names a drop list; a hook into the drop methods themselves
    /// always counts. Read with the catalog, once the mods have made their patches.
    /// </summary>
    internal static class ModHooks
    {
        private static readonly Dictionary<HookedRule, List<string>> ByRule = new Dictionary<HookedRule, List<string>>();
        private static readonly Dictionary<string, List<HookedRule>> ByMod = new Dictionary<string, List<HookedRule>>(StringComparer.Ordinal);

        /// <summary>The game's methods each rule is decided in, and whether a hook there counts only when it names a drop list.</summary>
        private static readonly (HookedRule Rule, Type Type, string Method, bool OnlyNamingDrops)[] Methods =
        {
            (HookedRule.Drops, typeof(Character), "OnDeath", true),
            (HookedRule.Drops, typeof(Humanoid), "OnDeath", true),
            (HookedRule.Drops, typeof(CharacterDrop), "OnDeath", false),
            (HookedRule.Drops, typeof(CharacterDrop), "GenerateDropList", false),
            (HookedRule.Drops, typeof(CharacterDrop), "DropItems", false),
            (HookedRule.Drops, typeof(Ragdoll), "SpawnLoot", false),
            (HookedRule.Loot, typeof(DropTable), "GetDropList", false),
            (HookedRule.Loot, typeof(DropTable), "GetDropListItems", false),
            (HookedRule.Loot, typeof(Container), "AddDefaultItems", false),
            (HookedRule.Loot, typeof(DropOnDestroyed), "OnDestroyed", false),
            (HookedRule.Loot, typeof(Pickable), "RPC_Pick", false),
            (HookedRule.Loot, typeof(Pickable), "Drop", false),
            (HookedRule.Spawns, typeof(SpawnSystem), "IsSpawnPointGood", false),
            (HookedRule.Spawns, typeof(SpawnSystem), "UpdateSpawnList", false),
            (HookedRule.Spawns, typeof(SpawnSystem), "Spawn", false),
            (HookedRule.Spawns, typeof(SpawnArea), "SpawnOne", false),
            (HookedRule.Spawns, typeof(CreatureSpawner), "Spawn", false),
        };

        /// <summary>The types whose members, named in a hook's code, make it a hook into drops.</summary>
        private static readonly HashSet<string> DropTypes = new HashSet<string>(StringComparer.Ordinal) { nameof(CharacterDrop), nameof(DropTable) };

        /// <summary>The mods hooking into a rule, by name, in the order found.</summary>
        public static IReadOnlyList<string> Mods(HookedRule rule) => ByRule.TryGetValue(rule, out var mods) ? mods : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>The rules a mod hooks into.</summary>
        public static IReadOnlyList<HookedRule> Rules(string mod) => mod != null && ByMod.TryGetValue(mod, out var rules) ? rules : (IReadOnlyList<HookedRule>)Array.Empty<HookedRule>();

        /// <summary>Every mod hooking into any rule.</summary>
        public static IEnumerable<string> AllMods => ByMod.Keys;

        /// <summary>Every hook looked at, with its mod, the method it hooks and whether it counted, for the self-test to tell.</summary>
        public static readonly List<(string Mod, string Method, bool Counted)> Seen = new List<(string, string, bool)>();

        public static void Read()
        {
            ByRule.Clear();
            ByMod.Clear();
            Seen.Clear();
            var plugins = PluginsByAssembly();
            foreach (var (rule, type, name, onlyNamingDrops) in Methods)
            {
                // A method a game update renamed would leave its mods unnamed without a word, so it is told.
                var methods = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == name).ToList();
                if (methods.Count == 0) Faults.Skip("mods' hooks", type.Name + "." + name, new MissingMethodException(type.Name, name));
                foreach (var method in methods)
                {
                    var info = Harmony.GetPatchInfo(method);
                    if (info == null) continue;
                    foreach (var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
                    {
                        if (patch?.PatchMethod == null || patch.owner == Plugin.Guid) continue;
                        var mod = ModOf(patch, plugins);
                        var counted = !onlyNamingDrops || NamesDrops(patch.PatchMethod);
                        Seen.Add((mod, type.Name + "." + name, counted));
                        if (counted) Note(rule, mod);
                    }
                }
            }
        }

        private static void Note(HookedRule rule, string mod)
        {
            if (string.IsNullOrEmpty(mod)) return;
            if (!ByRule.TryGetValue(rule, out var mods)) ByRule[rule] = mods = new List<string>();
            if (!mods.Contains(mod)) mods.Add(mod);
            if (!ByMod.TryGetValue(mod, out var rules)) ByMod[mod] = rules = new List<HookedRule>();
            if (!rules.Contains(rule)) rules.Add(rule);
        }

        /// <summary>Whether a hook's own code names a member of a drop list's type, or the type itself (<see cref="IlShape.Names"/>).</summary>
        private static bool NamesDrops(MethodInfo hook)
        {
            try
            {
                var body = hook.GetMethodBody();
                if (body == null) return false;
                var typeArgs = hook.DeclaringType != null && hook.DeclaringType.IsGenericType ? hook.DeclaringType.GetGenericArguments() : null;
                var methodArgs = hook.IsGenericMethod ? hook.GetGenericArguments() : null;
                foreach (var token in IlShape.Names(body.GetILAsByteArray()))
                {
                    MemberInfo named;
                    try { named = hook.Module.ResolveMember(token, typeArgs, methodArgs); }
                    catch { continue; }
                    if (Names(named)) return true;
                }
            }
            catch (Exception ex)
            {
                Faults.Skip("mods' hooks", hook.DeclaringType?.FullName ?? hook.Name, ex);
            }
            return false;
        }

        /// <summary>Whether a member named in code is, or belongs to, or is made for, a drop list's type (GetComponent&lt;CharacterDrop&gt; too).</summary>
        private static bool Names(MemberInfo named)
        {
            switch (named)
            {
                case null:
                    return false;
                case Type type:
                    return DropTypes.Contains(type.Name) || (type.DeclaringType != null && DropTypes.Contains(type.DeclaringType.Name));
                case MethodInfo method when method.IsGenericMethod && method.GetGenericArguments().Any(a => DropTypes.Contains(a.Name)):
                    return true;
                default:
                    var owner = named.DeclaringType;
                    return owner != null && (DropTypes.Contains(owner.Name) || (owner.DeclaringType != null && DropTypes.Contains(owner.DeclaringType.Name)));
            }
        }

        /// <summary>The name of the mod a patch belongs to: the plugin whose assembly holds its method, else the plugin its Harmony id names, else the id.</summary>
        private static string ModOf(Patch patch, Dictionary<Assembly, string> plugins)
        {
            var assembly = patch.PatchMethod.DeclaringType?.Assembly;
            if (assembly != null && plugins.TryGetValue(assembly, out var name)) return name;
            if (patch.owner != null && BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(patch.owner, out var info) && info?.Metadata != null) return info.Metadata.Name;
            return patch.owner;
        }

        private static Dictionary<Assembly, string> PluginsByAssembly()
        {
            var plugins = new Dictionary<Assembly, string>();
            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                var assembly = info?.Instance != null ? info.Instance.GetType().Assembly : null;
                if (assembly != null && info.Metadata != null && !plugins.ContainsKey(assembly)) plugins[assembly] = info.Metadata.Name;
            }
            return plugins;
        }
    }
}
