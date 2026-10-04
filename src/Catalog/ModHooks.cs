using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// Which mods hook into the game's own code for what Scry tells: what creatures drop, what
    /// drop tables give, where creatures spawn, comfort, how stations, producers and fires
    /// work, wear and support, costs, item stats, eating, growing, the weather, raids, taming,
    /// trading and container sizes. Found from Harmony's record of every patch (<c>Harmony.GetPatchInfo</c>),
    /// each patch's mod being the plugin whose assembly holds it. Such a mod can change these
    /// beyond anything read from the prefabs, so the details say so (<see cref="ModHookWords"/>).
    /// A method hooked for many reasons (a creature's death, a station's waking, checking a
    /// cost) counts only when the hook's own code names what decides the rule there (a drop
    /// list, the station's own settings, an amount), or when it rewrites the method (a
    /// transpiler, whose additions are not in its own code); a hook into a method deciding the
    /// rule itself always counts. Read with the catalog, once the mods have made their patches.
    /// </summary>
    internal static class ModHooks
    {
        private static readonly Dictionary<HookedRule, List<string>> ByRule = new Dictionary<HookedRule, List<string>>();
        private static readonly Dictionary<string, List<HookedRule>> ByMod = new Dictionary<string, List<HookedRule>>(StringComparer.Ordinal);

        /// <summary>A hook there counts whatever its code names: no member asked for.</summary>
        private static readonly Func<MemberInfo, bool> Always;

        /// <summary>The game's methods each rule is decided in, and what a hook there must name to count, if anything.</summary>
        private static readonly (HookedRule Rule, Type Type, string Method, Func<MemberInfo, bool> MustName)[] Methods =
        {
            (HookedRule.Drops, typeof(Character), "OnDeath", NamesDrops),
            (HookedRule.Drops, typeof(CharacterDrop), "OnDeath", Always),
            (HookedRule.Drops, typeof(CharacterDrop), "GenerateDropList", Always),
            (HookedRule.Drops, typeof(CharacterDrop), "DropItems", Always),
            (HookedRule.Drops, typeof(Ragdoll), "SpawnLoot", Always),
            (HookedRule.Loot, typeof(DropTable), "GetDropList", Always),
            (HookedRule.Loot, typeof(DropTable), "GetDropListItems", Always),
            (HookedRule.Loot, typeof(Container), "AddDefaultItems", Always),
            (HookedRule.Loot, typeof(DropOnDestroyed), "OnDestroyed", Always),
            (HookedRule.Loot, typeof(Pickable), "RPC_Pick", Always),
            (HookedRule.Loot, typeof(Pickable), "Drop", Always),
            (HookedRule.Spawns, typeof(SpawnSystem), "IsSpawnPointGood", Always),
            (HookedRule.Spawns, typeof(SpawnSystem), "UpdateSpawnList", Always),
            (HookedRule.Spawns, typeof(SpawnSystem), "Spawn", Always),
            (HookedRule.Spawns, typeof(SpawnArea), "SpawnOne", Always),
            (HookedRule.Spawns, typeof(CreatureSpawner), "Spawn", Always),

            (HookedRule.Comfort, typeof(SE_Rested), "CalculateComfortLevel", Always),
            (HookedRule.Comfort, typeof(SE_Rested), "GetNearbyComfortPieces", Always),

            (HookedRule.Smelting, typeof(Smelter), "UpdateSmelter", Always),
            (HookedRule.Smelting, typeof(Smelter), "OnAddOre", Always),
            (HookedRule.Smelting, typeof(Smelter), "OnAddFuel", Always),
            (HookedRule.Smelting, typeof(Smelter), "GetItemConversion", Always),
            (HookedRule.Smelting, typeof(Smelter), "IsItemAllowed", Always),
            (HookedRule.Smelting, typeof(Smelter), "Spawn", Always),
            (HookedRule.Smelting, typeof(Smelter), "Awake", Fields(typeof(Smelter), "m_maxOre", "m_maxFuel", "m_fuelPerProduct", "m_secPerProduct", "m_conversion", "m_fuelItem", "m_requiresRoof")),
            (HookedRule.Cooking, typeof(CookingStation), "UpdateCooking", Always),
            (HookedRule.Cooking, typeof(CookingStation), "CookItem", Always),
            (HookedRule.Cooking, typeof(CookingStation), "UpdateFuel", Always),
            (HookedRule.Cooking, typeof(CookingStation), "GetItemConversion", Always),
            (HookedRule.Cooking, typeof(CookingStation), "IsItemAllowed", Always),
            (HookedRule.Cooking, typeof(CookingStation), "Awake", Fields(typeof(CookingStation), "m_conversion", "m_slots", "m_maxFuel", "m_secPerFuel", "m_useFuel", "m_requireFire", "m_fuelItem")),
            (HookedRule.Fermenting, typeof(Fermenter), "GetFermentationTime", Always),
            (HookedRule.Fermenting, typeof(Fermenter), "GetItemConversion", Always),
            (HookedRule.Fermenting, typeof(Fermenter), "IsItemAllowed", Always),
            (HookedRule.Fermenting, typeof(Fermenter), "Awake", Fields(typeof(Fermenter), "m_fermentationDuration", "m_conversion")),
            (HookedRule.Producing, typeof(Beehive), "UpdateBees", Always),
            (HookedRule.Producing, typeof(Beehive), "IncreseLevel", Always),
            (HookedRule.Producing, typeof(Beehive), "CheckBiome", Always),
            (HookedRule.Producing, typeof(Beehive), "Awake", Fields(typeof(Beehive), "m_secPerUnit", "m_maxHoney", "m_biome", "m_maxCover")),
            (HookedRule.Producing, typeof(SapCollector), "UpdateTick", Always),
            (HookedRule.Producing, typeof(SapCollector), "IncreseLevel", Always),
            (HookedRule.Producing, typeof(SapCollector), "Awake", Fields(typeof(SapCollector), "m_secPerUnit", "m_maxLevel")),
            (HookedRule.Burning, typeof(Fireplace), "UpdateFireplace", Always),
            (HookedRule.Burning, typeof(Fireplace), "Awake", Fields(typeof(Fireplace), "m_maxFuel", "m_secPerFuel", "m_infiniteFuel", "m_startFuel")),

            (HookedRule.Wear, typeof(WearNTear), "UpdateWear", Always),
            (HookedRule.Wear, typeof(WearNTear), "UpdateSupport", Always),
            (HookedRule.Wear, typeof(WearNTear), "HaveRoof", Always),
            (HookedRule.Wear, typeof(WearNTear), "GetMaterialProperties", Always),
            (HookedRule.Wear, typeof(WearNTear), "GetMaxSupport", Always),
            (HookedRule.Wear, typeof(WearNTear), "GetMinSupport", Always),
            (HookedRule.Wear, typeof(WearNTear), "Awake", Fields(typeof(WearNTear), "m_health", "m_noRoofWear", "m_noSupportWear", "m_supports", "m_materialType")),

            (HookedRule.Crafting, typeof(Recipe), "GetAmount", Always),
            (HookedRule.Crafting, typeof(Recipe), "GetRequiredStation", Always),
            (HookedRule.Crafting, typeof(Recipe), "GetRequiredStationLevel", Always),
            (HookedRule.Crafting, typeof(Player), "HaveRequirements", Fields(typeof(Piece.Requirement), "m_amount", "m_amountPerLevel")),
            (HookedRule.Crafting, typeof(Player), "ConsumeResources", Fields(typeof(Piece.Requirement), "m_amount", "m_amountPerLevel")),

            (HookedRule.ItemStats, typeof(ItemDrop.ItemData), "GetDamage", Always),
            (HookedRule.ItemStats, typeof(ItemDrop.ItemData), "GetArmor", Always),
            (HookedRule.ItemStats, typeof(ItemDrop.ItemData), "GetWeight", Always),
            (HookedRule.ItemStats, typeof(ItemDrop.ItemData), "GetMaxDurability", Always),
            (HookedRule.ItemStats, typeof(ItemDrop.ItemData), "GetBaseBlockPower", Always),
            (HookedRule.ItemStats, typeof(ItemDrop.ItemData), "GetBlockPower", Always),
            (HookedRule.ItemStats, typeof(ItemDrop.ItemData), "GetDeflectionForce", Always),

            (HookedRule.Food, typeof(Player), "EatFood", Always),
            (HookedRule.Food, typeof(Player), "CanEat", Always),
            (HookedRule.Food, typeof(Player), "UpdateFood", Always),
            (HookedRule.Food, typeof(Player), "GetTotalFoodValue", Always),

            (HookedRule.Growth, typeof(Plant), "GetGrowTime", Always),
            (HookedRule.Growth, typeof(Plant), "Grow", Always),
            (HookedRule.Growth, typeof(Plant), "UpdateHealth", Always),
            (HookedRule.Growth, typeof(Plant), "HaveGrowSpace", Always),
            (HookedRule.Growth, typeof(Plant), "HaveRoof", Always),
            (HookedRule.Growth, typeof(Plant), "Awake", Fields(typeof(Plant), "m_growTime", "m_growTimeMax", "m_biome", "m_needCultivatedGround")),
            (HookedRule.Growth, typeof(Pickable), "UpdateRespawn", Always),
            (HookedRule.Growth, typeof(Pickable), "ShouldRespawn", Always),
            (HookedRule.Growth, typeof(Pickable), "Awake", Fields(typeof(Pickable), "m_respawnTimeMinutes", "m_amount")),

            (HookedRule.Weather, typeof(EnvMan), "UpdateEnvironment", Always),
            (HookedRule.Weather, typeof(EnvMan), "GetAvailableEnvironments", Always),
            (HookedRule.Weather, typeof(EnvMan), "SelectWeightedEnvironment", Always),
            (HookedRule.Weather, typeof(EnvMan), "GetEnvironmentOverride", Always),
            (HookedRule.Weather, typeof(EnvMan), "CalculateWet", Always),
            (HookedRule.Weather, typeof(EnvMan), "CalculateCold", Always),
            (HookedRule.Weather, typeof(EnvMan), "CalculateFreezing", Always),

            (HookedRule.Raids, typeof(RandEventSystem), "UpdateRandomEvent", Always),
            (HookedRule.Raids, typeof(RandEventSystem), "GetPossibleRandomEvents", Always),
            (HookedRule.Raids, typeof(RandEventSystem), "HaveGlobalKeys", Always),
            (HookedRule.Raids, typeof(RandEventSystem), "CheckBase", Always),
            (HookedRule.Raids, typeof(RandEventSystem), "InValidBiome", Always),
            (HookedRule.Raids, typeof(RandEventSystem), "PlayerIsReadyForEvent", Always),
            (HookedRule.Raids, typeof(RandEventSystem), "StartRandomEvent", Always),

            (HookedRule.Taming, typeof(Tameable), "Tame", Always),
            (HookedRule.Taming, typeof(Tameable), "TamingUpdate", Always),
            (HookedRule.Taming, typeof(Tameable), "OnConsumedItem", Always),
            (HookedRule.Taming, typeof(Tameable), "DecreaseRemainingTime", Always),
            (HookedRule.Taming, typeof(Tameable), "GetRemainingTime", Always),
            (HookedRule.Taming, typeof(Tameable), "IsHungry", Always),
            (HookedRule.Taming, typeof(Procreation), "Procreate", Always),
            (HookedRule.Taming, typeof(Procreation), "ReadyForProcreation", Always),
            (HookedRule.Taming, typeof(Procreation), "MakePregnant", Always),
            (HookedRule.Taming, typeof(Procreation), "IsDue", Always),

            (HookedRule.Trading, typeof(Trader), "GetAvailableItems", Always),
            (HookedRule.Trading, typeof(Trader), "Start", Fields(typeof(Trader), "m_items")),

            (HookedRule.Storage, typeof(Container), "Awake", Fields(typeof(Container), "m_width", "m_height")),
        };

        /// <summary>The types whose members, named in a hook's code, make it a hook into drops.</summary>
        private static readonly HashSet<string> DropTypes = new HashSet<string>(StringComparer.Ordinal) { nameof(CharacterDrop), nameof(DropTable) };

        /// <summary>A hook naming one of a type's fields, by their names.</summary>
        private static Func<MemberInfo, bool> Fields(Type type, params string[] names)
        {
            var wanted = new HashSet<string>(names, StringComparer.Ordinal);
            return member => member is FieldInfo field && field.DeclaringType == type && wanted.Contains(field.Name);
        }

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
            foreach (var (rule, type, name, mustName) in Methods)
            {
                // A method a game update renamed would leave its mods unnamed without a word, so it is told.
                var methods = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == name).ToList();
                if (methods.Count == 0) Faults.Skip("mods' hooks", type.Name + "." + name, new MissingMethodException(type.Name, name));
                foreach (var method in methods)
                {
                    var info = Harmony.GetPatchInfo(method);
                    if (info == null) continue;
                    var transpilers = new HashSet<Patch>(info.Transpilers);
                    foreach (var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
                    {
                        if (patch?.PatchMethod == null || patch.owner == About.Guid) continue;
                        var mod = ModOf(patch, plugins);
                        // A transpiler rewrites the method itself, so what it puts there need not be in its own code.
                        var counted = mustName == null || transpilers.Contains(patch) || Names(patch.PatchMethod, mustName);
                        Seen.Add((mod, Naming.MemberPath(type.Name, name), counted));
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

        /// <summary>Whether a hook's own code names a member the rule is decided by (<see cref="IlShape.Names"/>).</summary>
        private static bool Names(MethodInfo hook, Func<MemberInfo, bool> wanted) =>
            Guard.Each("mods' hooks", hook.DeclaringType?.FullName ?? hook.Name, () => NamesIn(hook, wanted), out var names) && names;

        private static bool NamesIn(MethodInfo hook, Func<MemberInfo, bool> wanted)
        {
            var body = hook.GetMethodBody();
            if (body == null) return false;
            var typeArgs = hook.DeclaringType != null && hook.DeclaringType.IsGenericType ? hook.DeclaringType.GetGenericArguments() : null;
            var methodArgs = hook.IsGenericMethod ? hook.GetGenericArguments() : null;
            foreach (var token in IlShape.Names(body.GetILAsByteArray()))
            {
                // A token the module cannot resolve, of a type it does not load, names nothing looked for.
                MemberInfo named = null;
                if (Steps.Run(() => named = hook.Module.ResolveMember(token, typeArgs, methodArgs), null) != null) continue;
                if (named != null && wanted(named)) return true;
            }
            return false;
        }

        /// <summary>Whether a member named in code is, or belongs to, or is made for, a drop list's type (GetComponent&lt;CharacterDrop&gt; too).</summary>
        private static bool NamesDrops(MemberInfo named)
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
