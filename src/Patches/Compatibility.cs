using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Checks the parts of the game Scry relies on, once, when it first reads the catalog in a
    /// world, and says in the log what it found: the members it reaches by name, the methods it
    /// patches, the game code whose rules its previews follow (by the shape of that code as it
    /// was when this version was made, see <see cref="IlShape"/>), the animation events the game
    /// answers, the layers falling copies land on, the effect scripts copies keep, and the status
    /// effects and prefab it looks up by name. A game update that moves any of these is told as
    /// the feature it affects, rather than found out as a preview gone quietly wrong.
    /// </summary>
    internal static class Compatibility
    {
        /// <summary>What each patch is for, by the class that makes it.</summary>
        private static readonly Dictionary<string, string> PatchFeatures = new Dictionary<string, string>
        {
            { "TextInputBlock", "keeping the game's keys away while the panel is open" },
            { "InventoryKeyBlock", "keeping Tab from opening the inventory while typing in the panel" },
            { "LookThrough", "looking around and walking while the panel is open" },
            { "NoCombatWhileOpen", "keeping attacks, blocks and dodges from going off while the panel is open" },
            { "LookCapture", "holding the cursor while looking around" },
            { "WheelBlock", "keeping the mouse wheel from zooming the game camera while over the panel" },
            { "SceneOrigins", "telling the game's prefabs from those mods add" },
            { "StatusEffectOrigins", "telling the game's status effects from those mods add" },
        };

        /// <summary>
        /// The game methods whose rules Scry follows, with the shape of their code in
        /// Valheim 1.0.16, read from the game's own assembly.
        /// </summary>
        private static readonly (string Type, string Method, int Params, uint Shape, string Feature)[] Watched =
        {
            ("VisEquipment", "AttachItem", 6, 0xD7DAC228, "gear on creatures and the person"),
            ("Humanoid", "GiveDefaultItems", 0, 0x308C20BE, "what creatures carry"),
            ("Humanoid", "EquipItem", 2, 0xB6B9953E, "which gear takes the place of which"),
            ("Humanoid", "SetupVisEquipment", 2, 0x17BDBBAE, "armour on ragdolls"),
            ("LevelEffects", "SetupLevelVisualization", 1, 0xD5E59AD9, "star-level looks"),
            ("WearNTear", "UpdateVisual", 1, 0x390E7B55, "worn and broken looks"),
            ("Fireplace", "UpdateState", 0, 0xA932F988, "fire looks"),
            ("TeleportWorld", "UpdatePortal", 0, 0xB1368747, "portal looks"),
            ("EffectFade", "SetActive", 1, 0xDC4AA873, "effects fading in and out"),
            ("ItemStyle", "Setup", 1, 0xB6778751, "item styles"),
            ("Plant", "Grow", 0, 0x387B2C65, "plants grown"),
            ("Pickable", "SetPicked", 1, 0xD43BD169, "picked looks"),
            ("Character", "OnDeath", 0, 0x63009BD2, "ragdoll deaths"),
            ("Ragdoll", "Setup", 7, 0xD56D1722, "ragdoll deaths"),
            ("Destructible", "CreateFragments", 2, 0x2A65B522, "pieces breaking apart"),
            ("WearNTear", "RPC_CreateFragments", 1, 0x2724FEDC, "pieces breaking apart"),
            ("TreeBase", "SpawnLog", 1, 0xBF8E65F4, "trees falling"),
            ("TreeLog", "Destroy", 2, 0xD6731DC4, "logs splitting into halves"),
            ("Destructible", "Destroy", 1, 0xD9E62863, "what things leave when broken"),
            ("TreeBase", "Shake", 0, 0x60417B85, "trees shaking when struck"),
            ("TreeBase", "ShakeAnimation", 0, 0xBA9F74EE, "trees shaking when struck"),
            ("EffectList", "Create", 6, 0xA8985D0C, "where effects appear on a model"),
            ("AnimationEffect", "Effect", 1, 0xDF43C7EF, "sounds and effects animations name"),
            ("AnimationEffect", "Attach", 1, 0x1974D1DE, "props animations hold"),
            ("FootStep", "UpdateFootstepCurveTrigger", 1, 0xBB6FCBEA, "footsteps"),
            ("ZSFX", "Play", 0, 0xF60FF796, "picking a sound's variants"),
            ("MusicLocation", "Awake", 0, 0x80F33570, "location music"),
            ("Character", "AddFireDamage", 2, 0xA441CEBC, "the status effects damage causes"),
            ("Character", "AddFrostDamage", 2, 0x77AE9523, "the status effects damage causes"),
            ("Character", "AddLightningDamage", 2, 0x068B93DE, "the status effects damage causes"),
            ("Character", "AddPoisonDamage", 2, 0x77AE9523, "the status effects damage causes"),
            ("Character", "AddSpiritDamage", 2, 0xA441CEBC, "the status effects damage causes"),
            ("Door", "SetState", 1, 0xFD8700BC, "door looks"),
            ("Container", "UpdateUseVisual", 0, 0x53F25066, "chest looks"),
            ("Smelter", "UpdateState", 0, 0x74799120, "smelter looks"),
            ("Smelter", "SetAnimation", 1, 0x8728BDAE, "smelter looks"),
            ("Fermenter", "SlowUpdate", 0, 0xBF99D857, "fermenter looks"),
            ("SapCollector", "UpdateEffects", 0, 0xA276633B, "sap collector looks"),
            ("CraftingStation", "CustomUpdate", 2, 0x0F1DE708, "crafting stations in use"),
            ("CraftingStation", "CheckFire", 0, 0xAE55015F, "crafting stations' fire"),
            ("Windmill", "Update", 0, 0x5B823CE2, "windmills turning"),
            ("Tameable", "SetSaddle", 1, 0x7BC32B79, "saddles"),
            ("AnimationObjectToggle", "SetGameObject", 2, 0xFB641210, "parts animations show and hide"),
            ("Character", "ForceJump", 2, 0xBF80A81B, "jump animations"),
            ("Character", "TakeOff", 0, 0xA4E55A1C, "flyers taking off"),
            ("Character", "OnDeathAnim", 0, 0xECCFE468, "death animations"),
            ("Humanoid", "SetupAnimationState", 0, 0xA69F123F, "weapon stances"),
            ("Humanoid", "UpdateBlock", 1, 0xE93DD6F9, "block animations"),
            ("Humanoid", "UseItem", 3, 0x0FE529C5, "eating animations"),
            ("BaseAI", "SetAlerted", 1, 0xA6DC542C, "alert animations"),
            ("MonsterAI", "Wakeup", 0, 0xE05A55C9, "waking animations"),
            ("MonsterAI", "UpdateConsumeItem", 2, 0x7D31D2DF, "eating animations"),
            ("Attack", "Start", 9, 0xF77DB3EE, "attack swings"),
            ("Attack", "GetProjectileSpawnPoint", 2, 0xA167E181, "thrown and shot projectiles"),
            ("Attack", "FireProjectileBurst", 0, 0x9C942866, "thrown and shot projectiles"),
            ("Attack", "OnAttackTrigger", 0, 0x8575D354, "what an attack plays as it strikes"),
            ("Attack", "DoMeleeAttack", 0, 0x02E4B054, "where swings hit"),
            ("Attack", "GetMeleeAttackDir", 2, 0x2F48BDB7, "where swings hit"),
            ("Attack", "DoAreaAttack", 0, 0x8E76EA3A, "where area attacks hit"),
            ("Attack", "ProjectileAttackTriggered", 0, 0x6F38B2FF, "what throws play as they let go"),
            ("Attack", "GetAttackOrigin", 0, 0x1F38E4AA, "where attacks come from"),
            ("Projectile", "Setup", 6, 0xAC11A4B1, "throws hitting with their own effects"),
            ("Character", "Awake", 0, 0x26223A54, "the body the game animates"),
            ("Humanoid", "InAttack", 0, 0x9FA3519B, "which clips are an attack's"),
            ("Character", "RPC_Stagger", 2, 0xF2A24690, "stagger animations"),
            ("Character", "AddStaggerDamage", 3, 0xCEA5F8B3, "hits heard with staggers"),
            ("Character", "UpdateContinousEffects", 0, 0xABFA49E2, "water and flying effects kept going"),
            ("Character", "UpdateSwimming", 1, 0x0FCF0647, "swimming animations"),
            ("FootStep", "FindBestStepEffect", 2, 0x2A458304, "which step a creature makes"),
            ("MonsterAI", "Sleep", 0, 0x76D7B742, "sleeping animations"),
            ("MonsterAI", "UpdateSleep", 1, 0x0849E9C0, "what is heard after waking"),
            ("BaseAI", "DoIdleSound", 0, 0xEAD56C82, "idle sounds heard around idle clips"),
            ("RandomIdle", "OnStateEnter", 3, 0x387CD94B, "idle clips picked at random"),
            ("RandomIdle", "OnStateUpdate", 3, 0x1E89789A, "idle clips picked at random"),
            ("RandomIdle", "GetRandomIdle", 1, 0x7A49F403, "idle clips picked at random"),
            ("CreatureSpawner", "Spawn", 0, 0xF64EBBF9, "spawn roars"),
            ("SpawnAbility", "Spawn", 0, 0xD3A37693, "spawn roars"),
            ("Player", "SetupAwake", 0, 0x38272BB3, "the person standing from the start"),

            // What the details tell the game's rules from: if one changes, its words may be off.
            ("CharacterDrop", "GenerateDropList", 0, 0x1D58F486, "creature drop amounts, chances and stars in the details"),
            ("DropTable", "AddItemToList", 2, 0xAECEE75C, "drop table amounts in the details"),
            ("DropTable", "GetDropList", 1, 0xBF6963CC, "drop table rolls in the details"),
            ("BaseAI", "CanSeeTarget", 7, 0xDE5EBD88, "what creatures see, in the details"),
            ("Character", "SetupMaxHealth", 0, 0x6F57CA0E, "health with stars in the details"),
            ("Character", "GetDamageModifiers", 1, 0x14A24DBA, "weak spots in the details"),
            ("MonsterAI", "UpdateAI", 1, 0xF625E201, "when creatures turn on you, in the details"),
            ("MonsterAI", "UpdateTarget", 4, 0x71A722F8, "how far creatures chase, in the details"),
            ("WearNTear", "GetMaterialProperties", 4, 0x12182085, "support in the details"),
            ("WearNTear", "UpdateSupport", 0, 0x291BD05E, "support lost in the details"),
            ("WearNTear", "UpdateWear", 1, 0xE472FFDA, "rain, ash and snow wear in the details"),
            ("SpawnArea", "UpdateSpawn", 0, 0x97E19B30, "spawners' pace in the details"),
            ("SpawnArea", "SpawnOne", 0, 0x0897D075, "spawners' caps and stars in the details"),
            ("SpawnArea", "SelectWeightedPrefab", 0, 0xDA338F1E, "spawners' shares in the details"),
            ("SpawnArea", "GetInstances", 2, 0xE34A2F7A, "spawners' caps in the details"),
            ("SpawnArea", "FindSpawnPoint", 2, 0xCEBBCAD3, "where spawners put what they spawn, in the details"),
            ("Attack", "GetLevelDamageFactor", 0, 0x56C23F2E, "damage with stars in the details"),
            ("Player", "HaveRequirementItems", 4, 0xB9AF033A, "upgrade kits told apart from ingredients"),
            ("InventoryGui", "SetupRequirementList", 4, 0xB8443C09, "upgrade kits told apart from ingredients"),
            ("InventoryGui", "UpdateRecipeList", 1, 0xC89F3E86, "what an upgrade station does, in the details"),
            ("InventoryGui", "CanRepair", 1, 0xD57710C7, "where items are repaired, in the details"),
            ("Recipe", "GetRequiredStationLevel", 1, 0x7AD112DA, "station levels for upgrades in the details"),
            ("ItemDrop+ItemData", "GetTooltip", 6, 0xE717E1A4, "item stats in the details"),
            ("ItemDrop+ItemData", "AddBlockTooltip", 3, 0xDF722134, "block and parry in the details"),
            ("Player", "GetBodyArmor", 0, 0x03C09085, "which armour counts, in the details"),
            ("HitData", "ApplyResistance", 2, 0x8BB9A7D5, "true damage in the details"),
            ("SE_Rested", "CalculateComfortLevel", 2, 0x71FEE0E8, "comfort groups in the details"),
            ("SE_Stats", "GetTooltipString", 0, 0x8126FDA1, "status effect stats in the details"),
            ("Player", "CanConsumeItem", 2, 0x27D77A45, "effects that cannot be taken together, in the details"),
            ("Incinerator+IncineratorConversion", "AttemptCraft", 2, 0x7D4E45C5, "what the obliterator makes, in the details"),
            ("RandEventSystem", "HaveGlobalKeys", 2, 0xBAF80048, "what raids wait for, in the details"),
            ("Trader", "GetAvailableItems", 0, 0x19BA382C, "what traders sell and when, in the details"),
            ("Fermenter", "UpdateCover", 2, 0x861BA786, "what a fermenter needs, in the details"),
            ("Beehive", "UpdateBees", 0, 0x14D30E4F, "what a beehive makes and needs, in the details"),
            ("Beehive", "HaveFreeSpace", 0, 0x67355E85, "what a beehive makes and needs, in the details"),
            ("Beehive", "CheckBiome", 0, 0x0719B463, "what a beehive makes and needs, in the details"),
            ("SapCollector", "UpdateTick", 0, 0x82F639F5, "what a sap collector makes and needs, in the details"),
            ("Container", "AddDefaultItems", 0, 0xD14D7482, "what chests hold, in the details"),
            ("MineRock5", "DamageArea", 2, 0x80C15677, "a rock's health per piece, in the details"),
            ("Plant", "GetGrowTime", 0, 0x424F2EF8, "how plants grow, in the details"),
            ("Plant", "UpdateHealth", 1, 0xC5AAD837, "how plants grow, in the details"),
            ("Pickable", "UpdateRespawn", 0, 0x31AD3B33, "when what is picked grows back, in the details"),
            ("Tameable", "TamingUpdate", 0, 0xB81F3F62, "taming and feeding in the details"),
            ("Tameable", "IsHungry", 0, 0x38421197, "taming and feeding in the details"),
            ("Character", "GetMaxHealthBase", 0, 0x1BFF8AE9, "the world's level in the details"),
            ("SEMan", "HaveStatusEffectCategory", 1, 0xE7A9814B, "effects that cannot be taken together, in the details"),
        };

        /// <summary>Public methods of the animation event receivers that are not events.</summary>
        private static readonly HashSet<string> NotEvents = new HashSet<string>
        {
            "CustomFixedUpdate", "CustomLateUpdate", "FindJoints", "CanChain",
        };

        private static bool _checked;

        /// <summary>The features the check found turned off by a missing part.</summary>
        private static List<string> _off = new List<string>();

        /// <summary>How many features may be off, cheap enough to ask every frame.</summary>
        public static int OffCount => _off.Count + Faults.ChangedFeatures.Count;

        /// <summary>
        /// Every feature this version cannot offer on this version of the game: those the check
        /// found missing a part, and those that failed since because the game changed.
        /// </summary>
        public static List<string> FeaturesOff()
        {
            var all = new List<string>(_off);
            foreach (var feature in Faults.ChangedFeatures) if (!all.Contains(feature)) all.Add(feature);
            return all;
        }

        /// <summary>Checks once, in a world, and tells the log.</summary>
        public static void Check()
        {
            if (_checked) return;
            _checked = true;

            // Every game type is found by name and every part checked on its own, so a type or
            // member an update removed is told as missing rather than stopping the check.
            var list = new Checklist();
            void Each(string part, Action check)
            {
                try { check(); }
                // A check that cannot run says so; it turns no feature off, so the panel does not show it.
                catch (Exception ex) { Plugin.Log.LogWarning($"Scry could not check {part} at start, and goes on without knowing: {ex.Message}"); }
            }
            Each("SpawnSystem.m_instances", () => Member(list, "SpawnSystem", "m_instances", "where creatures spawn"));
            Each("ZSFX.m_fadeOutTimer", () => Member(list, "ZSFX", "m_fadeOutTimer", "sounds playing on after a seek or pause"));
            Each("the patches", () => Patches(list));
            foreach (var watched in Watched)
            {
                Each($"{watched.Type}.{watched.Method}", () => Code(list, watched.Type, watched.Method, watched.Params, watched.Shape, watched.Feature));
            }
            Each("CharacterAnimEvent", () => Events(list, "CharacterAnimEvent"));
            Each("AnimationEffect", () => Events(list, "AnimationEffect"));
            foreach (var layer in new[] { "Default", "terrain", "static_solid", "piece" })
            {
                Each($"layer {layer}", () => list.Add($"layer {layer}", $"falling copies landing on {layer}", LayerMask.NameToLayer(layer) >= 0 ? Found.Present : Found.Missing));
            }
            foreach (var script in StripPolicy.KeptScriptNames)
            {
                Each($"script {script}", () => list.Add($"script {script}", $"effects keeping their {script}", GameType(script) != null ? Found.Present : Found.Missing));
            }
            foreach (var effect in new[] { "Burning", "Frost", "Lightning", "Poison", "Spirit" })
            {
                Each($"status effect {effect}", () =>
                {
                    var found = ObjectDB.instance != null && ObjectDB.instance.GetStatusEffect(effect.GetStableHashCode()) != null;
                    list.Add($"status effect {effect}", $"linking damage to {effect}", found ? Found.Present : Found.Missing);
                });
            }
            Each("prefab Player", () =>
            {
                var player = ZNetScene.instance != null && ZNetScene.instance.GetPrefab("Player") != null;
                list.Add("prefab Player", "the person beside the model and items tried on", player ? Found.Present : Found.Missing);
            });

            _off = list.FeaturesOff;
            foreach (var line in list.Report())
            {
                if (list.AnyTrouble) Plugin.Log.LogWarning(line);
                else Plugin.Log.LogInfo(line);
            }
        }

        /// <summary>
        /// One of the game's own types by name, from the game's assembly, where every script the
        /// copies keep is. Looking through all assemblies instead loads every mod's types, and
        /// logs the errors of those that cannot be.
        /// </summary>
        private static Type GameType(string name) => typeof(ZNetScene).Assembly.GetType(name, false);

        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static void Member(Checklist list, string typeName, string name, string feature)
        {
            var type = GameType(typeName);
            list.Add($"{typeName}.{name}", feature, type?.GetField(name, All) != null ? Found.Present : Found.Missing);
        }

        /// <summary>Each patch, by whether its method is there and carries Scry's patch now.</summary>
        private static void Patches(Checklist list)
        {
            foreach (var type in Plugin.OwnTypes())
            {
                HarmonyPatch attribute;
                try { attribute = type.GetCustomAttributes(typeof(HarmonyPatch), false).FirstOrDefault() as HarmonyPatch; }
                catch (Exception) { list.Add(type.Name, PatchFeatures.TryGetValue(type.Name, out var off) ? off : type.Name, Found.Missing); continue; }
                if (attribute?.info?.declaringType == null) continue;

                var target = attribute.info.declaringType;
                var name = attribute.info.methodName;
                var feature = PatchFeatures.TryGetValue(type.Name, out var known) ? known : type.Name;
                var method = target.GetMethods(All).FirstOrDefault(m => m.Name == name);
                var patched = method != null && Harmony.GetPatchInfo(method)?.Owners.Contains(Plugin.Guid) == true;
                list.Add($"{target.Name}.{name}", feature, patched ? Found.Present : Found.Missing);
            }
        }

        private static void Code(Checklist list, string typeName, string name, int parameters, uint shape, string feature)
        {
            var type = GameType(typeName);
            var method = type?.GetMethods(All).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameters);
            if (method == null)
            {
                list.Add($"{typeName}.{name}", feature, Found.Missing);
                return;
            }
            // A coroutine's steps are in the state machine made for it, not in its own method.
            var steps = type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(t => IlShape.IsStateMachineOf(t.Name, name))
                ?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ?? method;
            var now = IlShape.Of(steps.GetMethodBody()?.GetILAsByteArray());
            list.Add($"{typeName}.{name}", feature, now == shape ? Found.Present : Found.Changed);
        }

        /// <summary>
        /// An animation event the game answers and Scry does not: a creature whose clips send it
        /// has its animations left silent, so the check tells of it.
        /// </summary>
        private static void Events(Checklist list, string typeName)
        {
            var type = GameType(typeName);
            if (type == null)
            {
                list.Add(typeName, "sounds of animations", Found.Missing);
                return;
            }
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || method.ReturnType != typeof(void) || method.GetParameters().Length > 1) continue;
                if (NotEvents.Contains(method.Name) || AnimationEars.Answers(method.Name)) continue;
                list.Add($"animation event {type.Name}.{method.Name}", "sounds of animations that send it", Found.Missing);
            }
        }
    }
}
