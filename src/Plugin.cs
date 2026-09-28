using System.IO;
using System.Linq;
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
        private static ConfigEntry<bool> _logPreviews;

        /// <summary>
        /// The key that opens the panel. One the game or the panel already uses for something
        /// else (a click, Escape, the mouse's back and forward buttons) would open and close it
        /// by accident, so F7 stands in for it, and the log says so once.
        /// </summary>
        public static KeyCode OpenKey
        {
            get
            {
                var key = _openKey?.Value ?? KeyCode.F7;
                if (key != KeyCode.Mouse0 && key != KeyCode.Mouse1 && key != KeyCode.Escape && key != KeyCode.Mouse3 && key != KeyCode.Mouse4) return key;
                if (_toldKey != key)
                {
                    _toldKey = key;
                    Log.LogWarning($"Scry cannot open on {key}, which the game or the panel already uses; F7 opens it instead.");
                }
                return KeyCode.F7;
            }
        }

        private static KeyCode _toldKey = KeyCode.None;

        public static bool AutoSpin => _autoSpin?.Value ?? true;
        public static bool PlayOnSelect => _playOnSelect?.Value ?? true;
        public static float UiScale => _uiScale?.Value ?? 1f;

        /// <summary>Whether the log tells what previews play and what Scry saw of each prefab, for finding out why something looks or sounds wrong.</summary>
        public static bool LogPreviews => _logPreviews?.Value ?? false;

        /// <summary>A note for the log about what a preview did, written only when <see cref="LogPreviews"/> is on.</summary>
        public static void Note(string line)
        {
            if (LogPreviews) Log.LogInfo(line);
        }

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

            // The panel places everything itself and uses no automatic layout, so the layout pass
            // Unity would otherwise run before every event, drawing the whole panel once more, is
            // left out.
            useGUILayout = false;

            _openKey = Config.Bind("1 - General", "OpenKey", KeyCode.F7,
                "Opens and closes the Scry panel. /scry in the chat does the same.");
            _uiScale = Config.Bind("1 - General", "PanelScale", 1f,
                new ConfigDescription("Size of the panel and its text, on top of the automatic scaling by screen height.",
                    new AcceptableValueRange<float>(0.6f, 2f)));
            _autoSpin = Config.Bind("2 - Preview", "AutoSpin", true,
                "Turns the model in the preview slowly while you are not dragging it.");
            _playOnSelect = Config.Bind("2 - Preview", "PlayOnSelect", true,
                "Plays a sound as soon as it is selected, so the list can be auditioned with the arrow keys.");
            _logPreviews = Config.Bind("3 - Diagnostics", "LogPreviews", false,
                "Writes to the log what each preview played and what Scry found out about each prefab (its animator, its gear, what it leaves behind), for finding out why something looks or sounds wrong. Off, the log only says when the game has changed in a way Scry notices, and how long reading the catalog took.");

            _harmony = new Harmony(Guid);
            Patch();

            // Without the chat command F7 still opens the panel.
            try { Commands.Register(); }
            catch (System.Exception ex) { Faults.Tell("the /scry command", ex); }
        }

        /// <summary>
        /// Applies each patch class on its own. A game update that renames a patched method then
        /// costs only that patch's feature, and the others still go on; the startup check tells
        /// which.
        /// </summary>
        private void Patch()
        {
            foreach (var type in OwnTypes())
            {
                try
                {
                    if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                    _harmony.CreateClassProcessor(type).Patch();
                }
                catch (System.Exception ex)
                {
                    Log.LogWarning($"Scry could not patch {type.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Scry's own types, those that load. One naming a game type an update removed cannot be
        /// loaded; the rest still are, so that one costs only itself.
        /// </summary>
        internal static System.Type[] OwnTypes()
        {
            try
            {
                return typeof(Plugin).Assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                Log.LogWarning($"Scry: {ex.Types.Count(t => t == null)} of its own parts cannot be loaded on this version of the game, and are off; the rest work on. ({ex.LoaderExceptions.FirstOrDefault()?.Message})");
                return ex.Types.Where(t => t != null).ToArray();
            }
        }

        private void Update()
        {
            var started = Timing.Start();
            try { Session.Update(); } catch (System.Exception ex) { Faults.Tell("the frame", ex); }
            Timing.Add("update", started);
        }

        private void LateUpdate()
        {
            var started = Timing.Start();
            try { Session.LateUpdate(); } catch (System.Exception ex) { Faults.Tell("drawing the stage", ex); }
            Timing.Add("render", started);
        }

        private void OnGUI()
        {
            var started = Timing.Start();
            var kind = Event.current.type;
            // The panel catches its own sections; this is for a panel that cannot run at all,
            // which is told once rather than an error every frame.
            try { ScryPanel.OnGUI(); }
            catch (ExitGUIException) { throw; }
            catch (System.Exception ex) { Faults.Tell("the panel", ex); }
            Timing.Add("panel", started);
            if (Plugin.LogPreviews) Timing.Add("panel " + kind, started);
        }

        private void OnDestroy()
        {
            try { Session.Shutdown(); }
            catch (System.Exception ex) { Faults.Tell("closing down", ex); }
            _harmony?.UnpatchSelf();
        }
    }
}
