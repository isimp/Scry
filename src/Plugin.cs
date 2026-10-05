using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What BepInEx loads: it gives the log and the settings to the layers below, patches the
    /// game, registers the command, and hands each frame, its drawing and its end to the session
    /// and the panel.
    /// </summary>
    [BepInPlugin(About.Guid, About.Name, About.Version)]
    [BepInProcess("valheim.exe")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "Unity ends a plugin with OnDestroy, which unpatches; a MonoBehaviour is never disposed.")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        private void Awake()
        {
            Log.Source = Logger;

            // The panel places everything itself and uses no automatic layout, so the layout pass
            // Unity would otherwise run before every event, drawing the whole panel once more, is
            // left out.
            useGUILayout = false;

            Settings.Bind(Config);
            Timing.FrameDone += Monitor.Frame;
            ScryPanel.CloseAsked += Session.Hide;

            Compatibility.PatchFeatures = PatchFeatures;
            _harmony = new Harmony(About.Guid);
            Patch();

            // Without the chat command F7 still opens the panel.
            Guard.Run(Feature.Command, "the /scry command", Commands.Register);
        }

        /// <summary>What each patch is for, by the class that makes it, so a patch the game no longer allows names the feature it turns off.</summary>
        private static readonly Dictionary<Type, Feature> PatchFeatures = new Dictionary<Type, Feature>
        {
            { typeof(TextInputBlock), Feature.KeysKeptFromGame },
            { typeof(InventoryKeyBlock), Feature.TabKeptFromInventory },
            { typeof(LookThrough), Feature.LookAndWalkWhileOpen },
            { typeof(NoCombatWhileOpen), Feature.NoCombatWhileOpen },
            { typeof(LookCapture), Feature.CursorHeldWhileLooking },
            { typeof(WheelBlock), Feature.WheelKeptFromCamera },
            { typeof(SceneOrigins), Feature.PrefabOrigins },
            { typeof(StatusEffectOrigins), Feature.StatusEffectOrigins },
            { typeof(RaidOrigins), Feature.RaidOrigins },
            { typeof(LocationOrigins), Feature.LocationOrigins },
            { typeof(RoomOrigins), Feature.RoomOrigins },
            { typeof(DeathLoot), Feature.DropsSeenInPlay },
            { typeof(RagdollLootSetUp), Feature.DropsSeenInPlay },
            { typeof(RagdollLoot), Feature.DropsSeenInPlay },
            { typeof(ItemMade), Feature.DropsSeenInPlay },
        };

        /// <summary>
        /// Applies each patch class on its own. A game update that renames a patched method then
        /// costs only that patch's feature, and the others still go on; the startup check tells
        /// which.
        /// </summary>
        private void Patch()
        {
            foreach (var type in About.OwnTypes())
            {
                Guard.Each(Compatibility.FeatureOfPatch(type), "patching the game", type.Name, () =>
                {
                    if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0) _harmony.CreateClassProcessor(type).Patch();
                });
            }
        }

        private void Update()
        {
            var started = Timing.Start();
            Guard.Run(Feature.World, "the frame", Session.Update);
            var drops = Timing.Start();
            DropWatch.Save();
            Timing.Add("update drops", drops);
            Timing.Add("update", started);
        }

        private void LateUpdate() => Guard.Run(Feature.Stage, "drawing the stage", Session.LateUpdate, "render");

        private void OnGUI()
        {
            var started = Timing.Start();
            var kind = Event.current.type;
            // The panel catches its own sections; this is for a panel that cannot run at all,
            // which is told once rather than an error every frame.
            Guard.Run(Feature.Panel, "the panel", ScryPanel.OnGUI, "panel");
            if (Settings.LogPreviews) Timing.Add(EventPart(kind), started);

            // The resource monitor over everything, its own drawing a part of its own.
            if (Monitor.On && kind == EventType.Repaint) Guard.Run(Feature.ResourceMonitor, "the resource monitor", DrawMonitor, "monitor");
        }

        private static void DrawMonitor()
        {
            Monitor.Refresh();
            ScryPanel.MonitorGUI(Monitor.Lines, Monitor.Graph, Monitor.GraphTop);
        }

        private static readonly string[] EventParts = new string[64];

        /// <summary>The timing part for a kind of GUI event ("panel repaint"), made once.</summary>
        [Diagnostic]
        private static string EventPart(EventType kind)
        {
            var index = (int)kind;
            if (index < 0 || index >= EventParts.Length) return "panel " + kind;
            return EventParts[index] ?? (EventParts[index] = "panel " + kind);
        }

        private void OnDestroy()
        {
            DropWatch.Save(now: true);
            Guard.Run(Feature.Leaving, "closing down", Session.Shutdown);
            _harmony?.UnpatchSelf();
        }
    }
}
