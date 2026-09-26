using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    [BepInPlugin(Guid, "Scry", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.Scry";

        public static ManualLogSource Log;

        private static ConfigEntry<KeyCode> _openKey;
        private static ConfigEntry<bool> _autoSpin;
        private static ConfigEntry<bool> _playOnSelect;
        private static ConfigEntry<float> _uiScale;

        public static KeyCode OpenKey => _openKey?.Value ?? KeyCode.F7;
        public static bool AutoSpin => _autoSpin?.Value ?? true;
        public static bool PlayOnSelect => _playOnSelect?.Value ?? true;
        public static float UiScale => _uiScale?.Value ?? 1f;

        /// <summary>
        /// Where Scry keeps its own files: favourites and the panel's place on screen. Outside the
        /// BepInEx folder, so a mod manager replacing a profile's configs leaves them alone, and
        /// shared by every profile on this computer.
        /// </summary>
        public static string DataFolder => Path.Combine(Application.persistentDataPath, "Scry");

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            _openKey = Config.Bind("1 - General", "OpenKey", KeyCode.F7,
                "Opens and closes the Scry panel. /scry in the chat does the same.");
            _uiScale = Config.Bind("1 - General", "PanelScale", 1f,
                new ConfigDescription("Size of the panel and its text, on top of the automatic scaling by screen height.",
                    new AcceptableValueRange<float>(0.6f, 2f)));
            _autoSpin = Config.Bind("2 - Preview", "AutoSpin", true,
                "Turns the model in the preview slowly while you are not dragging it.");
            _playOnSelect = Config.Bind("2 - Preview", "PlayOnSelect", true,
                "Plays a sound as soon as it is selected, so the list can be auditioned with the arrow keys.");

            _harmony = new Harmony(Guid);
            Patch();

            Commands.Register();
        }

        /// <summary>
        /// Applies each patch class on its own. A game update that renames a patched method then
        /// costs only that patch's feature, and the others still go on; the startup check tells
        /// which.
        /// </summary>
        private void Patch()
        {
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
                catch (System.Exception ex)
                {
                    Log.LogWarning($"Scry could not patch {type.Name}: {ex.Message}");
                }
            }
        }

        private void Update()
        {
            Session.Update();
        }

        private void LateUpdate()
        {
            Session.LateUpdate();
        }

        private void OnGUI()
        {
            ScryPanel.OnGUI();
        }

        private void OnDestroy()
        {
            Session.Shutdown();
            _harmony?.UnpatchSelf();
        }
    }
}
