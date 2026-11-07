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
        private static ConfigEntry<KeyboardShortcut> _openKey;
        private static ConfigEntry<bool> _autoSpin;
        private static ConfigEntry<bool> _playOnSelect;
        private static ConfigEntry<float> _previewLoudness;
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
        private static ConfigEntry<float> _spinSpeed, _lighterFilmFrom;
        private static ConfigEntry<bool> _readLocations;
        private static ConfigEntry<StageQuality> _stageQuality;
        private static ConfigEntry<NewsShown> _news;
        private static ConfigEntry<bool> _markNames;
        private static ConfigEntry<float> _markSize;
        private static ConfigEntry<bool> _marksOnMinimap;
        private static ConfigEntry<bool> _recordSeenSpots;
        private static ConfigEntry<OtherPinsShown> _mapOtherPins;
        private static ConfigEntry<MapDarkening> _mapDarken;
        private static ConfigEntry<DotSizeChoice> _mapDotSize;
        private static ConfigEntry<DotSizeChoice> _miniDotSize;
        private static ConfigEntry<bool> _mapZoomOut, _mapSquares;

        /// <summary>
        /// The key that opens the panel, without its modifiers (<see cref="OpenMods"/>). One the
        /// game or the panel already uses for something else (a click, Escape, the mouse's back and
        /// forward buttons) would open and close it by accident, so F7 stands in for it, alone, and
        /// the log says so once. The settings card sets it (<see cref="SetOpenKey"/>).
        /// </summary>
        public static KeyCode OpenKey
        {
            get
            {
                var key = _openKey?.Value.MainKey ?? KeyCode.F7;
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

        /// <summary>The modifiers held with the key opening the panel; none where F7 stands in for a key it cannot be.</summary>
        public static KeyMods OpenMods => _openKey != null && OpensPanel(_openKey.Value.MainKey) ? ModsOf(_openKey.Value.Modifiers) : KeyMods.None;

        /// <summary>The key opening the panel as a player says it: "Shift+F7".</summary>
        public static string OpenKeyName => OpenKeys.Name(OpenKey.ToString(), OpenMods);

        /// <summary>The key opening the panel set, with its modifiers; one the game or the panel already uses is refused.</summary>
        public static void SetOpenKey(KeyCode key, KeyMods mods)
        {
            if (_openKey == null || !OpensPanel(key)) return;
            var held = new List<KeyCode>();
            if ((mods & KeyMods.Control) != 0) held.Add(KeyCode.LeftControl);
            if ((mods & KeyMods.Alt) != 0) held.Add(KeyCode.LeftAlt);
            if ((mods & KeyMods.Shift) != 0) held.Add(KeyCode.LeftShift);
            _openKey.Value = new KeyboardShortcut(key, held.ToArray());
        }

        /// <summary>The modifiers among keys, either side of the keyboard alike.</summary>
        public static KeyMods ModsOf(IEnumerable<KeyCode> keys)
        {
            var mods = KeyMods.None;
            bool leftControl = false, rightControl = false, altGr = false;
            foreach (var key in keys)
            {
                if (key == KeyCode.LeftShift || key == KeyCode.RightShift) mods |= KeyMods.Shift;
                else if (key == KeyCode.LeftControl || key == KeyCode.RightControl) mods |= KeyMods.Control;
                else if (key == KeyCode.LeftAlt || key == KeyCode.RightAlt || key == KeyCode.AltGr) mods |= KeyMods.Alt;
                leftControl |= key == KeyCode.LeftControl;
                rightControl |= key == KeyCode.RightControl;
                altGr |= key == KeyCode.RightAlt || key == KeyCode.AltGr;
            }
            // Windows sends a left Ctrl with the right Alt on layouts with AltGr: that Alt is Alt alone.
            if (altGr && leftControl && !rightControl) mods &= ~KeyMods.Control;
            return mods;
        }

        /// <summary>The modifiers held now.</summary>
        public static KeyMods HeldMods() => ModsOf(HeldModifierKeys());

        private static IEnumerable<KeyCode> HeldModifierKeys()
        {
            foreach (var key in ModifierKeys) if (Input.GetKey(key)) yield return key;
        }

        private static readonly KeyCode[] ModifierKeys = { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.AltGr };

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
        public static float PreviewLoudness { get => Mathf.Clamp01(_previewLoudness?.Value ?? 1f); set => Set(_previewLoudness, Mathf.Clamp01(value)); }
        public static float UiScale { get => _uiScale?.Value ?? 1f; set => Set(_uiScale, Mathf.Clamp(value, 0.6f, 2f)); }
        public static bool FocusSearchOnOpen { get => _focusSearch?.Value ?? false; set => Set(_focusSearch, value); }
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

        /// <summary>An older config's dungeon marks switch, left off, until a character's book takes it (<see cref="KeptMigration"/>); none once taken or where it was on.</summary>
        private static ConfigEntry<bool> _oldDungeonMarks;

        public static bool DungeonMarksWereOff => _oldDungeonMarks != null && !_oldDungeonMarks.Value;

        /// <summary>The old switch taken out of the config file, a character's book having kept the dungeons off.</summary>
        public static void DungeonMarksTaken()
        {
            if (_oldDungeonMarks == null || _config == null) return;
            _config.Remove(_oldDungeonMarks.Definition);
            _config.Save();
            _oldDungeonMarks = null;
        }

        /// <summary>How the stage is filmed: whole, or faster for a weaker machine (<see cref="Scry.StageQuality"/>).</summary>
        public static StageQuality StageQuality { get => _stageQuality?.Value ?? StageQuality.Full; set => Set(_stageQuality, value); }

        /// <summary>From how many milliseconds a still film of the stage takes it is filmed lighter while its camera moves (<see cref="StageFilm.Lighter"/>).</summary>
        public static float LighterFilmFromMs { get => Mathf.Clamp(_lighterFilmFrom?.Value ?? StageFilm.LighterFromMs, 0f, StageFilm.LighterNeverMs); set => Set(_lighterFilmFrom, Mathf.Clamp(value, 0f, StageFilm.LighterNeverMs)); }

        /// <summary>What the notice under the header tells of what the character learned: all of it, new pages only, or nothing, the Learned tab alone telling it.</summary>
        public static NewsShown News { get => _news?.Value ?? NewsShown.All; set => Set(_news, value); }

        /// <summary>Whether each dungeon mark on the large map shows its name under it always, as the game's pins do, rather than while the pointer rests on it.</summary>
        public static bool MarkNamesAlways { get => _markNames?.Value ?? false; set => Set(_markNames, value); }

        /// <summary>How big each dungeon mark on the map is, as a share of a map pin.</summary>
        public static float DungeonMarkSize { get => Mathf.Clamp(_markSize?.Value ?? 1f, SettingsWords.SmallestMarkSize, SettingsWords.LargestMarkSize); set => Set(_markSize, Mathf.Clamp(value, SettingsWords.SmallestMarkSize, SettingsWords.LargestMarkSize)); }

        /// <summary>Whether the minimap shows what is kept on the map too: the kinds' dots and the dungeons' marks.</summary>
        public static bool DungeonMarksOnMinimap { get => _marksOnMinimap?.Value ?? false; set => Set(_marksOnMinimap, value); }

        /// <summary>Whether the character's sight remembers where it saw things worth finding again, for the map.</summary>
        public static bool RecordSeenSpots { get => _recordSeenSpots?.Value ?? true; set => Set(_recordSeenSpots, value); }

        /// <summary>The config file every setting is bound to, for the settings card to read and set each by its key.</summary>
        private static ConfigFile _config;
        private static readonly Dictionary<string, ConfigEntryBase> ByKey = new Dictionary<string, ConfigEntryBase>();

        /// <summary>How Show on map sets the rest of the map back and draws its marks (<see cref="MapLook"/>), whether it zooms out to them, and whether it shows the server's squares.</summary>
        public static OtherPinsShown MapOtherPins { get => _mapOtherPins?.Value ?? OtherPinsShown.Third; set => Set(_mapOtherPins, value); }
        public static MapDarkening MapDarken { get => _mapDarken?.Value ?? MapDarkening.Half; set => Set(_mapDarken, value); }
        public static DotSizeChoice MapDotSize { get => _mapDotSize?.Value ?? DotSizeChoice.Normal; set => Set(_mapDotSize, value); }

        /// <summary>How big the marks kept on the map stand on the minimap, apart from the map's own dots.</summary>
        public static DotSizeChoice MinimapDotSize { get => _miniDotSize?.Value ?? MapDotSize; set => Set(_miniDotSize, value); }
        public static bool MapZoomOut { get => _mapZoomOut?.Value ?? true; set => Set(_mapZoomOut, value); }
        public static bool MapSquares { get => _mapSquares?.Value ?? true; set => Set(_mapSquares, value); }

        /// <summary>A setting by its key in the config file, as the settings card sets it (<see cref="SettingsCatalog"/>); none before the settings are bound.</summary>
        public static ConfigEntryBase Entry(string key)
        {
            if (_config == null || key == null) return null;
            if (ByKey.TryGetValue(key, out var entry)) return entry;
            foreach (var definition in _config.Keys)
            {
                if (definition.Key != key) continue;
                entry = _config[definition];
                ByKey[key] = entry;
                return entry;
            }
            return null;
        }

        /// <summary>
        /// The news's single switch it was before, read once as the file has it and let go of:
        /// turned off, the news stays off (<see cref="NewsChoices.From"/>). Read by its definition,
        /// as no setting of the card's.
        /// </summary>
        private static void KeepNewsOff(ConfigFile config)
        {
            var before = config.Bind(new ConfigDefinition("1 - General", "NewsNotice"), true);
            if (!before.Value) _news.Value = NewsChoices.From(false);
            config.Remove(before.Definition);
            config.Save();
        }

        /// <summary>Settings no longer kept, taken out of the config file.</summary>
        private static void Retire(ConfigFile config, params string[] keys)
        {
            foreach (var key in keys) config.Remove(config.Bind(new ConfigDefinition("1 - General", key), true).Definition);
            config.Save();
        }

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
        private static ConfigEntry<PlacesTold> _worldPlaces;
        private static ConfigEntry<float> _worldSeenReach, _worldSeenOnMapReach;

        /// <summary>How far, in metres, a player's sight records where a thing worth finding again stands, as the world sets it; none at 0.</summary>
        public static float SeenOnMapReach => _worldSeenOnMapReach?.Value ?? SeenOnMap.DefaultReach;

        /// <summary>How the world's server tells players the places in their explored ground: not at all, as squares (<see cref="PlaceSquares"/>) or as dots (<see cref="PlacesToldBy"/>); read by the server as it answers.</summary>
        public static PlacesTold PlacesOnMap => _worldPlaces?.Value ?? PlacesTold.Squares;
        private static readonly Dictionary<WorldSource, ConfigEntry<SourceLevel>> WorldUpTo = new Dictionary<WorldSource, ConfigEntry<SourceLevel>>();
        private static ServerSync.ConfigSync _sync;

        /// <summary>Whether the world's settings are the server's, synced to this game, rather than its own; for the self-test.</summary>
        public static bool WorldFromServer => _sync != null && !_sync.IsSourceOfTruth;

        /// <summary>The world's rules (<see cref="WorldRules"/>): as the server's Scry sets them while connected to it, else as this game's own settings, which a game running the world sets for those joining it.</summary>
        public static WorldRules World() =>
            _worldRules == null ? WorldRules.Open
            : WorldRules.From(_worldRules.Value, _worldPeeking.Value, _worldFloor.Value, source => WorldUpTo.TryGetValue(source, out var entry) ? entry.Value : SourceLevel.WithInsides, _worldSeenReach.Value);

        /// <summary>Binds every setting to BepInEx's config file, with its section, default, range and description.</summary>
        public static void Bind(ConfigFile config)
        {
            _config = config;
            ByKey.Clear();
            BindWorld(config);
            _openKey = config.Bind("1 - General", "OpenKey", new KeyboardShortcut(KeyCode.F7),
                "Opens and closes the Scry panel, with Shift, Ctrl or Alt held where it names them, as F7 + LeftShift; other keys held besides never stop it. /scry in the chat does the same.");
            _uiScale = config.Bind("1 - General", "PanelScale", 1f,
                new ConfigDescription("Size of the panel and its text, on top of the automatic scaling by screen height.",
                    new AcceptableValueRange<float>(0.6f, 2f)));
            _focusSearch = config.Bind("1 - General", "FocusSearchOnOpen", false,
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
            // The dungeon marks' switch became the dungeons' line in the list of what is kept on the map: one left off stays in the file until the first character read takes it.
            var oldDungeonMarks = config.Bind(new ConfigDefinition("1 - General", "DungeonMarksOnMap"), true);
            if (oldDungeonMarks.Value) Retire(config, "DungeonMarksOnMap");
            else _oldDungeonMarks = oldDungeonMarks;
            _news = config.Bind("1 - General", "News", NewsShown.All,
                "What the notice under the header tells of what the character learned, as it learns it: All of it; NewPages, pages newly met only, leaving out what was found out about one met before; Off, the Learned tab alone tells it.");
            KeepNewsOff(config);
            _markNames = config.Bind("1 - General", "DungeonMarkNamesAlways", false,
                "Each dungeon mark on Valheim's large map shows its name under it, as the game names its pins. Off, a mark shows its name while the pointer rests on it. Resting on one tells how far it was walked either way.");
            _markSize = config.Bind("1 - General", "DungeonMarkSize", 1f,
                new ConfigDescription("How big each dungeon mark on the map is, as a share of a map pin: 0.5 x-small, 0.7 small, 1 a pin's size, 1.4 large. A mark's name grows with it.",
                    new AcceptableValueRange<float>(SettingsWords.SmallestMarkSize, SettingsWords.LargestMarkSize)));
            // A size kept from before the sizes offered goes to the nearest of them.
            _markSize.Value = MapLook.NearestMarkScale(_markSize.Value);
            // Which marks show, by how they were walked or which grounds: now every dungeon walked or seen, as Show on map shows places.
            Retire(config, "DungeonMarksWalkedOnServer", "DungeonMarkEntrances", "DungeonMarksWalkedByYou");
            _marksOnMinimap = config.Bind("1 - General", "DungeonMarksOnMinimap", false,
                "Shows what is kept on the map on the minimap too, at its pins' size: the kinds' dots and the dungeons' marks, without names or squares.");
            _recordSeenSpots = config.Bind("1 - General", "RecordSeenSpots", true,
                "Remembers where your character saw ore deposits, berries, flint and the like, and places, for showing them on Valheim's map. Off, nothing new is remembered; what is remembered stays until /scry forget spots.");
            _mapOtherPins = config.Bind("1 - General", "ShowOnMapOtherPins", OtherPinsShown.Third,
                "While a page's kind is shown on Valheim's map, your other pins and their names stand at full, at a third of their brightness, or hidden, so its marks stand out. Back to full when it is hidden or the map closes.");
            _mapDarken = config.Bind("1 - General", "ShowOnMapDarken", MapDarkening.Half,
                "While a kind is shown, the map's picture under its marks is not darkened, darkened by half, or strongly.");
            _mapDotSize = config.Bind("1 - General", "ShowOnMapDotSize", DotSizeChoice.Normal,
                "How big the dots of a kind shown or kept on the map are drawn beside the game's pins: XSmall, Small, Normal or Large.");
            // Set apart from the map's dots, it starts as they stand, so the minimap looks as it did until it is changed.
            _miniDotSize = config.Bind("1 - General", "MinimapDotSize", _mapDotSize.Value,
                "How big the marks kept on the map stand on the minimap beside its pins: XSmall, Small, Normal or Large. It starts as the map's dot size.");
            _mapZoomOut = config.Bind("1 - General", "ShowOnMapZoomOut", true,
                "Opens the map zoomed out just far enough to hold the nearest marks of the kind shown; your own zoom comes back as it closes. Off, the map keeps your zoom.");
            _mapSquares = config.Bind("1 - General", "ShowOnMapSquares", true,
                "Shows what the world's server tells of places you never came near: the squares each stands somewhere in, or hollow dots where each stands. Off, only what you saw is shown. Whether and how the server tells them is its own setting, PlacesOnMap.");
            _autoSpin = config.Bind("2 - Preview", "AutoSpin", true,
                "Turns the model in the preview slowly while you are not dragging it. The View box's Spin switches it.");
            _spinSpeed = config.Bind("2 - Preview", "SpinSpeed", 14f,
                new ConfigDescription("How fast the model turns by itself, in degrees a second, while AutoSpin is on.",
                    new AcceptableValueRange<float>(1f, 90f)));
            _playOnSelect = config.Bind("2 - Preview", "PlayOnSelect", true,
                "Plays a sound as soon as it is selected, so the list can be auditioned with the arrow keys.");
            _previewLoudness = config.Bind("2 - Preview", "PreviewLoudness", 1f,
                new ConfigDescription("How loud a preview plays as it is selected, as a share of how loud the game plays the same sound; the volume over the stage starts there for each selection.",
                    new AcceptableValueRange<float>(0f, 1f)));
            _stageQuality = config.Bind("2 - Preview", "StageQuality", StageQuality.Full,
                "How the stage is drawn. Full draws it at the panel's own size, with its grass, and with the lights below a dungeon's cut as LightDimmedRooms says. Fast draws it at half size, without grass or those lights, for a big dungeon on a weaker machine.");
            _lighterFilmFrom = config.Bind("2 - Preview", "LighterFilmFromMs", StageFilm.LighterFromMs,
                new ConfigDescription("While the preview's camera moves (turned, zoomed, dragged, spinning or settling on what it shows), draws a stage whose last still picture took at least this many milliseconds at three quarters of its size with softer edges, so a big dungeon moves smoothly; it is drawn whole again as soon as the camera holds still. A creature or an item takes a millisecond or two and stays sharp. 0 draws every moving stage lighter, 1,000 none. StageQuality Fast draws it at half size always.",
                    new AcceptableValueRange<float>(0f, StageFilm.LighterNeverMs)));
            _lightDimmedRooms = config.Bind("2 - Preview", "LightDimmedRooms", false,
                "Lets the rooms shown dimmed below a dungeon's opened floor keep their own lights, as the game lights them. Off, only the stage's light reaches them, and a big dungeon is drawn in about half the time.");
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
                SettingsWords.Synced("The compendium's rules everyone in this world plays by: Off, Loose or Strict; PlayersChoose leaves them to each player."));
            _worldPeeking = config.Bind("4 - World", "Peeking", true,
                SettingsWords.Synced("Whether players may peek, the page shown showing whole until they go to another, or every page until the panel closes."));
            _worldFloor = config.Bind("4 - World", "KnownUpTo", true,
                SettingsWords.Synced("Whether players may raise Known up to, showing everything of the tiers below as known."));
            _worldSeenReach = config.Bind("4 - World", "SeenReach", WorldRules.SeenReachSetting.Default,
                new ConfigDescription(SettingsWords.Synced("How near, in metres, you must be to a creature to see its stars, what it carries and its attacks begin, and to an item, piece or resource to see it; 10 is as far as the game names a creature and marks its stars. An attack is also seen begun as far as its creature uses it from. At 0 no stars, gear or attacks are kept, and things are seen by their size alone."),
                    new AcceptableValueRange<float>(0f, 150f)));
            Sync(_worldRules);
            Sync(_worldPeeking);
            Sync(_worldFloor);
            _worldSeenOnMapReach = config.Bind("4 - World", "SeenOnMapReach", SeenOnMap.DefaultReach,
                new ConfigDescription(SettingsWords.Synced("How far away, in metres, a player's own sight remembers where an ore deposit, a pickable, an ancient root or a place stands, for showing it on the map; things smaller than a person are seen nearer. At 0 nothing is remembered."),
                    new AcceptableValueRange<float>(0f, 150f)));
            _worldPlaces = config.Bind("4 - World", "PlacesOnMap", PlacesTold.Squares,
                "How Show on map tells players the places in ground they explored or saw on a shared map but never came near: Off, not at all, players seeing only the places they saw themselves; Squares, the 128 m square each stands in, so they know one stands somewhere inside, never where; Dots, where each stands, as a hollow dot. A player's older Scry is told squares. The ground is the player's game's own word, so on Dots a changed game can learn where every place of the world stands. Only the server's own setting counts.");
            // The squares' on-and-off switch became this setting: one left off reads as Off, once, and goes from the file.
            var oldSquares = config.Bind(new ConfigDefinition("4 - World", "SquaresOnMap"), true);
            _worldPlaces.Value = PlacesToldBy.FromOld(oldSquares.Value, _worldPlaces.Value);
            config.Remove(oldSquares.Definition);
            config.Save();
            Sync(_worldSeenReach);
            Sync(_worldSeenOnMapReach);
            Sync(_worldPlaces);
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
