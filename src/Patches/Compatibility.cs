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
    /// patches, the game code whose workings its previews copy (by the shape of that code as it
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
            { "LookThrough", "looking around and walking while the panel is open" },
            { "NoCombatWhileOpen", "keeping attacks, blocks and dodges from going off while the panel is open" },
            { "LookCapture", "holding the cursor while looking around" },
            { "WheelBlock", "keeping the mouse wheel from zooming the game camera while over the panel" },
            { "SceneOrigins", "telling the game's prefabs from those mods add" },
            { "StatusEffectOrigins", "telling the game's status effects from those mods add" },
        };

        /// <summary>
        /// The game methods whose workings Scry's previews copy, with the shape of their code in
        /// Valheim 1.0.16, read from the game's own assembly.
        /// </summary>
        private static readonly (Type Type, string Method, int Params, uint Shape, string Feature)[] Copied =
        {
            (typeof(VisEquipment), "AttachItem", 6, 0xD7DAC228, "gear on creatures and the person"),
            (typeof(Humanoid), "GiveDefaultItems", 0, 0x308C20BE, "what creatures carry"),
            (typeof(Humanoid), "EquipItem", 2, 0xB6B9953E, "which gear takes the place of which"),
            (typeof(Humanoid), "SetupVisEquipment", 2, 0x17BDBBAE, "armour on ragdolls"),
            (typeof(LevelEffects), "SetupLevelVisualization", 1, 0xD5E59AD9, "star-level looks"),
            (typeof(WearNTear), "UpdateVisual", 1, 0x390E7B55, "worn and broken looks"),
            (typeof(Fireplace), "UpdateState", 0, 0xA932F988, "fire looks"),
            (typeof(TeleportWorld), "UpdatePortal", 0, 0xB1368747, "portal looks"),
            (typeof(EffectFade), "SetActive", 1, 0xDC4AA873, "effects fading in and out"),
            (typeof(ItemStyle), "Setup", 1, 0xB6778751, "item styles"),
            (typeof(Plant), "Grow", 0, 0x387B2C65, "plants grown"),
            (typeof(Pickable), "SetPicked", 1, 0xD43BD169, "picked looks"),
            (typeof(Character), "OnDeath", 0, 0x63009BD2, "ragdoll deaths"),
            (typeof(Ragdoll), "Setup", 7, 0xD56D1722, "ragdoll deaths"),
            (typeof(Destructible), "CreateFragments", 2, 0x2A65B522, "pieces breaking apart"),
            (typeof(WearNTear), "RPC_CreateFragments", 1, 0x2724FEDC, "pieces breaking apart"),
            (typeof(TreeBase), "SpawnLog", 1, 0xBF8E65F4, "trees falling"),
            (typeof(EffectList), "Create", 6, 0xA8985D0C, "where effects appear on a model"),
            (typeof(AnimationEffect), "Effect", 1, 0xDF43C7EF, "sounds and effects animations name"),
            (typeof(AnimationEffect), "Attach", 1, 0x1974D1DE, "props animations hold"),
            (typeof(FootStep), "UpdateFootstepCurveTrigger", 1, 0xBB6FCBEA, "footsteps"),
            (typeof(ZSFX), "Play", 0, 0xF60FF796, "picking a sound's variants"),
            (typeof(MusicLocation), "Awake", 0, 0x80F33570, "location music"),
            (typeof(Character), "AddFireDamage", 2, 0xA441CEBC, "the status effects damage causes"),
            (typeof(Character), "AddFrostDamage", 2, 0x77AE9523, "the status effects damage causes"),
            (typeof(Character), "AddLightningDamage", 2, 0x068B93DE, "the status effects damage causes"),
            (typeof(Character), "AddPoisonDamage", 2, 0x77AE9523, "the status effects damage causes"),
            (typeof(Character), "AddSpiritDamage", 2, 0xA441CEBC, "the status effects damage causes"),
            (typeof(Door), "SetState", 1, 0xFD8700BC, "door looks"),
            (typeof(Container), "UpdateUseVisual", 0, 0x53F25066, "chest looks"),
            (typeof(Smelter), "UpdateState", 0, 0x74799120, "smelter looks"),
            (typeof(Smelter), "SetAnimation", 1, 0x8728BDAE, "smelter looks"),
            (typeof(Fermenter), "SlowUpdate", 0, 0xBF99D857, "fermenter looks"),
            (typeof(SapCollector), "UpdateEffects", 0, 0xA276633B, "sap collector looks"),
            (typeof(CraftingStation), "CustomUpdate", 2, 0x0F1DE708, "crafting stations in use"),
            (typeof(CraftingStation), "CheckFire", 0, 0xAE55015F, "crafting stations' fire"),
            (typeof(Windmill), "Update", 0, 0x5B823CE2, "windmills turning"),
            (typeof(Ship), "UpdateSailSize", 1, 0xF94FCCE3, "ship sails"),
            (typeof(Tameable), "SetSaddle", 1, 0x7BC32B79, "saddles"),
            (typeof(Character), "ForceJump", 2, 0xBF80A81B, "jump animations"),
            (typeof(Character), "TakeOff", 0, 0xA4E55A1C, "flyers taking off"),
            (typeof(Character), "OnDeathAnim", 0, 0xECCFE468, "death animations"),
            (typeof(Humanoid), "SetupAnimationState", 0, 0xA69F123F, "weapon stances"),
            (typeof(Humanoid), "UpdateBlock", 1, 0xE93DD6F9, "block animations"),
            (typeof(Humanoid), "UseItem", 3, 0x0FE529C5, "eating animations"),
            (typeof(BaseAI), "SetAlerted", 1, 0xA6DC542C, "alert animations"),
            (typeof(MonsterAI), "Wakeup", 0, 0xE05A55C9, "waking animations"),
            (typeof(MonsterAI), "UpdateConsumeItem", 2, 0x7D31D2DF, "eating animations"),
            (typeof(Attack), "Start", 9, 0xF77DB3EE, "attack swings"),
            (typeof(Attack), "GetProjectileSpawnPoint", 2, 0xA167E181, "thrown and shot projectiles"),
            (typeof(Attack), "FireProjectileBurst", 0, 0x9C942866, "thrown and shot projectiles"),
        };

        /// <summary>Public methods of the animation event receivers that are not events.</summary>
        private static readonly HashSet<string> NotEvents = new HashSet<string>
        {
            "CustomFixedUpdate", "CustomLateUpdate", "FindJoints", "CanChain",
        };

        private static bool _checked;

        /// <summary>Checks once, in a world, and tells the log.</summary>
        public static void Check()
        {
            if (_checked) return;
            _checked = true;

            var list = new Checklist();
            try
            {
                Member(list, typeof(SpawnSystem), "m_instances", "where creatures spawn");
                Member(list, typeof(ZSFX), "m_fadeOutTimer", "sounds playing on after a seek or pause");
                Patches(list);
                foreach (var copied in Copied) Code(list, copied.Type, copied.Method, copied.Params, copied.Shape, copied.Feature);
                Events(list, typeof(CharacterAnimEvent));
                Events(list, typeof(AnimationEffect));
                foreach (var layer in new[] { "Default", "terrain", "static_solid", "piece" })
                {
                    list.Add($"layer {layer}", $"falling copies landing on {layer}", LayerMask.NameToLayer(layer) >= 0 ? Found.Present : Found.Missing);
                }
                foreach (var script in StripPolicy.KeptScriptNames)
                {
                    list.Add($"script {script}", $"effects keeping their {script}", AccessTools.TypeByName(script) != null ? Found.Present : Found.Missing);
                }
                foreach (var effect in new[] { "Burning", "Frost", "Lightning", "Poison", "Spirit" })
                {
                    var found = ObjectDB.instance != null && ObjectDB.instance.GetStatusEffect(effect.GetStableHashCode()) != null;
                    list.Add($"status effect {effect}", $"linking damage to {effect}", found ? Found.Present : Found.Missing);
                }
                var player = ZNetScene.instance != null && ZNetScene.instance.GetPrefab("Player") != null;
                list.Add("prefab Player", "the person beside the model and items tried on", player ? Found.Present : Found.Missing);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not finish checking the game: {ex.Message}");
            }

            foreach (var line in list.Report())
            {
                if (list.AnyTrouble) Plugin.Log.LogWarning(line);
                else Plugin.Log.LogInfo(line);
            }
        }

        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static void Member(Checklist list, Type type, string name, string feature)
        {
            list.Add($"{type.Name}.{name}", feature, type.GetField(name, All) != null ? Found.Present : Found.Missing);
        }

        /// <summary>Each patch, by whether its method is there and carries Scry's patch now.</summary>
        private static void Patches(Checklist list)
        {
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                var attribute = type.GetCustomAttributes(typeof(HarmonyPatch), false).FirstOrDefault() as HarmonyPatch;
                if (attribute?.info?.declaringType == null) continue;

                var target = attribute.info.declaringType;
                var name = attribute.info.methodName;
                var feature = PatchFeatures.TryGetValue(type.Name, out var known) ? known : type.Name;
                var method = target.GetMethods(All).FirstOrDefault(m => m.Name == name);
                var patched = method != null && Harmony.GetPatchInfo(method)?.Owners.Contains(Plugin.Guid) == true;
                list.Add($"{target.Name}.{name}", feature, patched ? Found.Present : Found.Missing);
            }
        }

        private static void Code(Checklist list, Type type, string name, int parameters, uint shape, string feature)
        {
            var method = type.GetMethods(All).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameters);
            if (method == null)
            {
                list.Add($"{type.Name}.{name}", feature, Found.Missing);
                return;
            }
            var now = IlShape.Of(method.GetMethodBody()?.GetILAsByteArray());
            list.Add($"{type.Name}.{name}", feature, now == shape ? Found.Present : Found.Changed);
        }

        /// <summary>
        /// An animation event the game answers and Scry does not: a creature whose clips send it
        /// has its animations left silent, so the check tells of it.
        /// </summary>
        private static void Events(Checklist list, Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || method.ReturnType != typeof(void) || method.GetParameters().Length > 1) continue;
                if (NotEvents.Contains(method.Name) || AnimationEars.Answers(method.Name)) continue;
                list.Add($"animation event {type.Name}.{method.Name}", "sounds of animations that send it", Found.Missing);
            }
        }
    }
}
