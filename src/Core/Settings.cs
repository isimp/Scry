using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Scry's settings, as BepInEx keeps them in its config file, bound once as the plugin wakes
    /// (<see cref="Bind"/>), and where Scry keeps its own files. Each reads its default until
    /// then.
    /// </summary>
    internal static class Settings
    {
        private static ConfigEntry<KeyCode> _openKey;
        private static ConfigEntry<bool> _autoSpin;
        private static ConfigEntry<bool> _playOnSelect;
        private static ConfigEntry<bool> _lightDimmedRooms;
        private static ConfigEntry<float> _uiScale;
        private static ConfigEntry<bool> _logPreviews;
        private static ConfigEntry<bool> _showMonitor;
        private static ConfigEntry<bool> _focusSearch;
        private static ConfigEntry<float> _catalogDelay;
        private static ConfigEntry<bool> _walkWhileOpen;
        private static ConfigEntry<bool> _lookWithRightMouse;
        private static ConfigEntry<float> _tooltipDelay;
        private static ConfigEntry<int> _recentCount;
        private static ConfigEntry<float> _spinSpeed;
        private static ConfigEntry<bool> _readLocations;

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
                    Log.Warn($"Scry cannot open on {key}, which the game or the panel already uses; F7 opens it instead.");
                }
                return KeyCode.F7;
            }
        }

        private static KeyCode _toldKey = KeyCode.None;

        public static bool AutoSpin => _autoSpin?.Value ?? true;
        public static bool PlayOnSelect => _playOnSelect?.Value ?? true;
        public static float UiScale => _uiScale?.Value ?? 1f;
        public static bool FocusSearchOnOpen => _focusSearch?.Value ?? true;
        public static float CatalogDelay => Mathf.Clamp(_catalogDelay?.Value ?? 10f, 0f, 120f);
        public static bool WalkWhileOpen => _walkWhileOpen?.Value ?? true;
        public static bool LookWithRightMouse => _lookWithRightMouse?.Value ?? true;
        public static float TooltipDelay => Mathf.Clamp(_tooltipDelay?.Value ?? 0.35f, 0f, 3f);
        public static int RecentCount => _recentCount?.Value ?? 30;
        public static float SpinSpeed => Mathf.Clamp(_spinSpeed?.Value ?? 14f, 1f, 90f);
        public static bool ReadLocationsAutomatically => _readLocations?.Value ?? false;

        /// <summary>Whether the log tells what previews play and what Scry saw of each prefab, for finding out why something looks or sounds wrong.</summary>
        public static bool LogPreviews => _logPreviews?.Value ?? false;

        /// <summary>Whether the rooms dimmed below a dungeon's opened floor keep their own lights; settable for the self-test to see both.</summary>
        public static bool LightDimmedRooms
        {
            get => _lightDimmedRooms?.Value ?? false;
            set
            {
                if (_lightDimmedRooms != null) _lightDimmedRooms.Value = value;
            }
        }

        /// <summary>Whether the resource monitor shows; <c>/scry monitor</c> switches it.</summary>
        public static bool ShowMonitor
        {
            get => _showMonitor?.Value ?? false;
            set
            {
                if (_showMonitor != null) _showMonitor.Value = value;
            }
        }

        /// <summary>
        /// Where Scry keeps its own files, favourites and the panel's place on screen: beside its
        /// settings, in a folder of BepInEx's config folder named for it, so they go with the
        /// profile as its settings do.
        /// </summary>
        public static string DataFolder => Path.Combine(Paths.ConfigPath, About.Guid);

        /// <summary>Binds every setting to BepInEx's config file, with its section, default, range and description.</summary>
        public static void Bind(ConfigFile config)
        {
            _openKey = config.Bind("1 - General", "OpenKey", KeyCode.F7,
                "Opens and closes the Scry panel. /scry in the chat does the same.");
            _uiScale = config.Bind("1 - General", "PanelScale", 1f,
                new ConfigDescription("Size of the panel and its text, on top of the automatic scaling by screen height.",
                    new AcceptableValueRange<float>(0.6f, 2f)));
            _focusSearch = config.Bind("1 - General", "FocusSearchOnOpen", true,
                "Puts the keyboard in the search box when the panel opens, so you can type straight away. The compact view never does, so that the keys walk there until the search is clicked.");
            _walkWhileOpen = config.Bind("1 - General", "WalkWhileOpen", true,
                "Lets you walk with your keys while the panel is open and you are not typing in it. Off, your character stands still while the panel is open.");
            _lookWithRightMouse = config.Bind("1 - General", "LookWithRightMouse", true,
                "Holding the right mouse button outside the panel turns the camera, to look around while it is open.");
            _tooltipDelay = config.Bind("1 - General", "TooltipDelay", 0.35f,
                new ConfigDescription("Seconds the mouse rests on something in the panel before its tip shows.",
                    new AcceptableValueRange<float>(0f, 3f)));
            _recentCount = config.Bind("1 - General", "RecentCount", 30,
                new ConfigDescription("How many entries the Recent list remembers.",
                    new AcceptableValueRange<int>(1, Explorer.MostRecent)));
            _catalogDelay = config.Bind("1 - General", "CatalogDelay", 10f,
                new ConfigDescription("Seconds after entering a world before Scry starts reading the game's prefabs in the background. Opening the panel sooner starts it at once.",
                    new AcceptableValueRange<float>(0f, 120f)));
            _readLocations = config.Bind("1 - General", "ReadLocationsAutomatically", false,
                "Reads where things are found in each world's locations and dungeons by itself, in the background once the prefabs are read, taking a few minutes. Off, they are read only when asked, with Read all locations or /scry locations.");
            _autoSpin = config.Bind("2 - Preview", "AutoSpin", true,
                "Turns the model in the preview slowly while you are not dragging it.");
            _spinSpeed = config.Bind("2 - Preview", "SpinSpeed", 14f,
                new ConfigDescription("How fast the model turns by itself, in degrees a second, while AutoSpin is on.",
                    new AcceptableValueRange<float>(1f, 90f)));
            _playOnSelect = config.Bind("2 - Preview", "PlayOnSelect", true,
                "Plays a sound as soon as it is selected, so the list can be auditioned with the arrow keys.");
            _lightDimmedRooms = config.Bind("2 - Preview", "LightDimmedRooms", false,
                "Lets the rooms shown dimmed below a dungeon's opened floor keep their own lights, as the game lights them. Off, only the stage's light reaches them, and a big dungeon is filmed in about half the time.");
            _logPreviews = config.Bind("3 - Diagnostics", "LogPreviews", false,
                "Writes to the log what each preview played and what Scry found out about each prefab (its animator, its gear, what it leaves behind), for finding out why something looks or sounds wrong. Off, the log only says when the game has changed in a way Scry notices, and how long reading the catalog took.");
            _showMonitor = config.Bind("3 - Diagnostics", "ResourceMonitor", false,
                "Shows a small box in the screen's corner, also with the panel closed, telling what Scry costs and holds: its own time each frame with a graph of the last ten seconds and its parts, what it allocates, the bundles, copies, textures and meshes it holds, beside the game's memory. /scry monitor switches it. For finding faults, not for play.");
        }
    }
}
