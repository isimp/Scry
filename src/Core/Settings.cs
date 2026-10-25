using System.Collections.Generic;
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
        private static ConfigEntry<bool> _dungeonMarks;
        private static ConfigEntry<StageQuality> _stageQuality;
        private static ConfigEntry<bool> _newsNotice;
        private static ConfigEntry<bool> _markNames;

        /// <summary>
        /// The key that opens the panel. One the game or the panel already uses for something
        /// else (a click, Escape, the mouse's back and forward buttons) would open and close it
        /// by accident, so F7 stands in for it, and the log says so once. The settings box sets it.
        /// </summary>
        public static KeyCode OpenKey
        {
            set
            {
                if (_openKey != null && OpensPanel(value)) _openKey.Value = value;
            }
            get
            {
                var key = _openKey?.Value ?? KeyCode.F7;
                if (OpensPanel(key)) return key;
                if (_toldKey != key)
                {
                    _toldKey = key;
                    Log.Warn($"Scry cannot open on {key}, which the game or the panel already uses; F7 opens it instead.");
                }
                return KeyCode.F7;
            }
        }

        private static KeyCode _toldKey = KeyCode.None;

        /// <summary>Whether a key may open the panel: none the game or the panel already uses for something else.</summary>
        public static bool OpensPanel(KeyCode key) =>
            key != KeyCode.None && key != KeyCode.Mouse0 && key != KeyCode.Mouse1 && key != KeyCode.Escape && key != KeyCode.Mouse3 && key != KeyCode.Mouse4;

        /// <summary>Whether the model turns by itself; the View box's Spin switches it, kept in the config file.</summary>
        public static bool AutoSpin
        {
            get => _autoSpin?.Value ?? true;
            set
            {
                if (_autoSpin != null) _autoSpin.Value = value;
            }
        }
        // What the settings box and the boxes beside what they change set; each kept in the config file.
        public static bool PlayOnSelect { get => _playOnSelect?.Value ?? true; set => Set(_playOnSelect, value); }
        public static float UiScale { get => _uiScale?.Value ?? 1f; set => Set(_uiScale, Mathf.Clamp(value, 0.6f, 2f)); }
        public static bool FocusSearchOnOpen { get => _focusSearch?.Value ?? true; set => Set(_focusSearch, value); }
        public static float CatalogDelay => Mathf.Clamp(_catalogDelay?.Value ?? 10f, 0f, 120f);
        public static bool WalkWhileOpen { get => _walkWhileOpen?.Value ?? true; set => Set(_walkWhileOpen, value); }
        public static bool LookWithRightMouse { get => _lookWithRightMouse?.Value ?? true; set => Set(_lookWithRightMouse, value); }
        public static float TooltipDelay { get => Mathf.Clamp(_tooltipDelay?.Value ?? 0.35f, 0f, 3f); set => Set(_tooltipDelay, Mathf.Clamp(value, 0f, 3f)); }
        public static int RecentCount => _recentCount?.Value ?? 30;
        public static float SpinSpeed { get => Mathf.Clamp(_spinSpeed?.Value ?? 14f, 1f, 90f); set => Set(_spinSpeed, Mathf.Clamp(value, 1f, 90f)); }
        public static bool ReadLocationsAutomatically { get => _readLocations?.Value ?? true; set => Set(_readLocations, value); }

        /// <summary>Whether a dedicated server reads what was left inside the world's dungeons by itself once the world's records are read; off, only as an admin asks.</summary>
        public static bool ReadTracesOnStart => _readTracesOnStart?.Value ?? true;

        /// <summary>Whether the world's server keeps the dungeon walks players' games send, and tells them to everyone.</summary>
        public static bool TakeWalks => _takeWalks?.Value ?? true;

        private static ConfigEntry<bool> _readTracesOnStart, _takeWalks;

        /// <summary>Whether each dungeon been in is marked on Valheim's large map.</summary>
        public static bool DungeonMarksOnMap { get => _dungeonMarks?.Value ?? true; set => Set(_dungeonMarks, value); }

        /// <summary>How the stage is filmed: whole, or faster for a weaker machine (<see cref="Scry.StageQuality"/>).</summary>
        public static StageQuality StageQuality { get => _stageQuality?.Value ?? StageQuality.Full; set => Set(_stageQuality, value); }

        /// <summary>Whether the notice under the header tells what the character learned, else the Learned tab alone does.</summary>
        public static bool NewsNotice { get => _newsNotice?.Value ?? true; set => Set(_newsNotice, value); }

        /// <summary>Whether each dungeon mark on the large map shows its name under it always, as the game's pins do, rather than while the pointer rests on it.</summary>
        public static bool MarkNamesAlways { get => _markNames?.Value ?? false; set => Set(_markNames, value); }

        /// <summary>A setting's new value, kept in the config file; nothing before the settings are bound.</summary>
        private static void Set<T>(ConfigEntry<T> entry, T value)
        {
            if (entry != null) entry.Value = value;
        }

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

        /// <summary>
        /// Where Scry keeps what it has read and could read again, what the places hold: in a folder
        /// of BepInEx's cache folder named for it, which a profile's sync and export leave out, as
        /// they take the config folders and files of the kinds settings are written in.
        /// </summary>
        public static string CacheFolder => Path.Combine(Paths.CachePath, About.Guid);

        /// <summary>What the world's server allows the compendium, synced to every player by ServerSync (see src/ServerSync/README.md).</summary>
        private static ConfigEntry<WorldRule> _worldRules;
        private static ConfigEntry<bool> _worldPeeking, _worldFloor;
        private static ConfigEntry<float> _worldSeenReach;
        private static readonly Dictionary<WorldSource, ConfigEntry<SourceLevel>> WorldUpTo = new Dictionary<WorldSource, ConfigEntry<SourceLevel>>();
        private static ServerSync.ConfigSync _sync;

        /// <summary>Whether the world's settings are the server's, synced to this game, rather than its own; for the self-test.</summary>
        [Diagnostic, ForTest]
        public static bool WorldFromServer => _sync != null && !_sync.IsSourceOfTruth;

        /// <summary>The world's rules (<see cref="WorldRules"/>): as the server's Scry sets them while connected to it, else as this game's own settings, which a game running the world sets for those joining it.</summary>
        public static WorldRules World() =>
            _worldRules == null ? WorldRules.Open
            : WorldRules.From(_worldRules.Value, _worldPeeking.Value, _worldFloor.Value, source => WorldUpTo.TryGetValue(source, out var entry) ? entry.Value : SourceLevel.WithInsides, _worldSeenReach.Value);

        /// <summary>Binds every setting to BepInEx's config file, with its section, default, range and description.</summary>
        public static void Bind(ConfigFile config)
        {
            BindWorld(config);
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
            _readLocations = config.Bind("1 - General", "ReadLocationsAutomatically", true,
                "Reads where things are found in each world's locations and dungeons by itself, in the background once the prefabs are read, taking a few minutes the first time in a world and kept for the next. Off, they are read only when asked, with Find where things are or /scry locations.");
            _dungeonMarks = config.Bind("1 - General", "DungeonMarksOnMap", true,
                "Marks each dungeon you have been in on Valheim's large map, just above its entrance, telling how much of it you walked; a click on a mark opens its map in Scry. Off, the map shows nothing of Scry's.");
            _newsNotice = config.Bind("1 - General", "NewsNotice", true,
                "The notice under the header tells what the character learned, as it learns it. Off, the Learned tab alone tells it.");
            _markNames = config.Bind("1 - General", "DungeonMarkNamesAlways", false,
                "Each dungeon mark on Valheim's large map shows its name under it, as the game names its pins. Off, a mark shows its name while the pointer rests on it. Resting on one tells how far it was walked either way.");
            _autoSpin = config.Bind("2 - Preview", "AutoSpin", true,
                "Turns the model in the preview slowly while you are not dragging it. The View box's Spin switches it.");
            _spinSpeed = config.Bind("2 - Preview", "SpinSpeed", 14f,
                new ConfigDescription("How fast the model turns by itself, in degrees a second, while AutoSpin is on.",
                    new AcceptableValueRange<float>(1f, 90f)));
            _playOnSelect = config.Bind("2 - Preview", "PlayOnSelect", true,
                "Plays a sound as soon as it is selected, so the list can be auditioned with the arrow keys.");
            _stageQuality = config.Bind("2 - Preview", "StageQuality", StageQuality.Full,
                "How the stage is filmed. Full films it at the panel's own size with its grass and the lights below a dungeon's cut as LightDimmedRooms says. Fast films it at half the size, without grass or those lights, for a big dungeon on a weaker machine.");
            _lightDimmedRooms = config.Bind("2 - Preview", "LightDimmedRooms", false,
                "Lets the rooms shown dimmed below a dungeon's opened floor keep their own lights, as the game lights them. Off, only the stage's light reaches them, and a big dungeon is filmed in about half the time.");
            _logPreviews = config.Bind("3 - Diagnostics", "LogPreviews", false,
                "Writes to the log what each preview played and what Scry found out about each prefab (its animator, its gear, what it leaves behind), for finding out why something looks or sounds wrong. Off, the log only says when the game has changed in a way Scry notices, and how long reading the catalog took.");
            _readTracesOnStart = config.Bind("5 - Server", "ReadTracesOnStart", true,
                "On the game that runs the world, a dedicated server or a host, reads what was left inside the world's dungeons once the world's records are read, a few milliseconds a frame, so rooms emptied before anyone's walk was kept count as walked on this server. Off, they are read only when an admin types /scry read traces. Only the server's own setting counts.");
            _takeWalks = config.Bind("5 - Server", "TakeWalks", true,
                "Keeps the dungeon walks players' games send, every room anyone stood in and the first and last day, and tells them to everyone as walked on this server. Off, the server keeps no one's walks: each player keeps their own, and the others see only what the traces show. Only the server's own setting counts.");
            _showMonitor = config.Bind("3 - Diagnostics", "ResourceMonitor", false,
                "Shows a small box in the screen's corner, also with the panel closed, telling what Scry costs and holds: its own time each frame with a graph of the last ten seconds and its parts, what it allocates, the bundles, copies, textures and meshes it holds, beside the game's memory. /scry monitor switches it. For finding faults, not for play.");
        }

        /// <summary>
        /// The world's settings, in a section of their own, each synced to every player by
        /// ServerSync: set on the world's server, its values win while a player is connected to
        /// it, the player's own kept beneath; players without Scry join as before, and only an
        /// admin changes them in game. Left as they are, every choice is each player's.
        /// </summary>
        public static void BindWorld(ConfigFile config)
        {
            _sync = new ServerSync.ConfigSync(About.Guid) { DisplayName = About.Name, CurrentVersion = About.Version, ModRequired = false, IsLocked = true };
            _worldRules = config.Bind("4 - World", "Rules", WorldRule.PlayersChoose,
                "The compendium's rules everyone in this world plays by: Off, Loose or Strict; PlayersChoose leaves them to each player. Set on the server, it holds for every player connected to it.");
            _worldPeeking = config.Bind("4 - World", "Peeking", true,
                "Whether players may peek, the page shown showing whole until they go to another, or every page until the panel closes.");
            _worldFloor = config.Bind("4 - World", "KnownUpTo", true,
                "Whether players may raise Known up to, showing everything of the tiers below as known.");
            _worldSeenReach = config.Bind("4 - World", "SeenReach", WorldRules.SeenReachSetting.Default,
                new ConfigDescription("How near, in metres, you must be to a creature to see its stars, what it carries and its attacks begin, and to an item, piece or resource to see it; 10 is as far as the game names a creature and marks its stars. At 0 no stars, gear or attacks are kept, and things are seen by their size alone.",
                    new AcceptableValueRange<float>(0f, 150f)));
            Sync(_worldRules);
            Sync(_worldPeeking);
            Sync(_worldFloor);
            Sync(_worldSeenReach);
            foreach (var source in WorldSources.All)
            {
                var offered = WorldSources.Offered(source);
                var entry = config.Bind("4 - World", source + "UpTo", offered[offered.Length - 1], WorldRulesWords.UpToSetting(source));
                WorldUpTo[source] = entry;
                Sync(entry);
            }
        }

        private static void Sync<T>(ConfigEntry<T> entry) => _sync.AddConfigEntry(entry).SynchronizedConfig = true;
    }
}
