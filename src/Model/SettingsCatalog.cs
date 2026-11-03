using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a setting is set on the card: a key pressed, a switch, one of a few, several at once, a slider, or a choice kept in the character's book.</summary>
    internal enum SettingControl
    {
        Key,
        Switch,
        Choices,
        Ticks,
        Slider,
        Book,
    }

    /// <summary>What of the character's compendium a row sets, kept in its book rather than the config file, as the compendium's box sets it.</summary>
    internal enum BookPart
    {
        None,
        Rules,
        KnownUpTo,
        Source,

    }

    /// <summary>One of a setting's values offered on the card, as the config file writes it, and its name.</summary>
    internal readonly struct SettingChoice
    {
        public SettingChoice(string value, string name)
        {
            Value = value;
            Name = name;
        }

        public string Value { get; }
        public string Name { get; }
    }

    /// <summary>
    /// A row of the settings card: the config entries it sets, by key (several for ticks), or the
    /// part of the character's compendium; its label and its explanation, how it is set, whether
    /// the world's server decides it, and what the server allows of it, standing under it.
    /// </summary>
    internal sealed class SettingRow
    {
        public IReadOnlyList<string> Keys { get; set; } = Array.Empty<string>();
        public string Label { get; set; }
        public string Why { get; set; }
        public SettingControl Control { get; set; }

        /// <summary>Whether its explanation is its label's tip rather than a line under it, its group's intro saying what its rows share.</summary>
        public bool WhyAsTip { get; set; }

        /// <summary>A heading the row stands under within its section; none.</summary>
        public string Group { get; set; }

        public IReadOnlyList<SettingChoice> Choices { get; set; } = Array.Empty<SettingChoice>();

        /// <summary>Each tick's name, a key each.</summary>
        public IReadOnlyList<string> TickNames { get; set; } = Array.Empty<string>();

        /// <summary>A slider's range and step, and its value as the card says it, in its unit.</summary>
        public float Min { get; set; }
        public float Max { get; set; }
        public float Step { get; set; }
        public Func<float, string> Shown { get; set; } = v => Numbers.Amount(v);

        public bool ServerDecides { get; set; }

        /// <summary>What of the character's compendium the row sets, and the source of the world for a source's row.</summary>
        public BookPart Book { get; set; }
        public WorldSource Source { get; set; }

        /// <summary>What the world's server allows of the row, standing under it: set there while you play alone or host, shown on the row while a server decides it; none.</summary>
        public SettingRow Limit { get; set; }
    }

    /// <summary>A section of the settings card, its rows in order.</summary>
    internal sealed class SettingSection
    {
        public SettingSection(string name, params SettingRow[] rows)
        {
            Name = name;
            Rows = rows;
        }

        public string Name { get; }
        public IReadOnlyList<SettingRow> Rows { get; }
    }

    /// <summary>
    /// Every setting Scry keeps, as the settings card shows it: in sections from the panel to the
    /// world's server, each row with a label, a plain explanation always shown under it, and a
    /// control fit for it, each in one place under one name. The character's compendium stands in
    /// its own section, as its box sets it, what the server allows of each row under it; the rest
    /// the server decides in a section of its own. The card draws them as they stand here and
    /// sets them through the config file's own words for their values, the compendium's in its book.
    /// </summary>
    internal static class SettingsCatalog
    {
        public static readonly IReadOnlyList<SettingSection> Sections = new[]
        {
            new SettingSection("Panel",
                Key("OpenKey", "Open Scry with", "The key that opens and closes the panel, with Shift, Ctrl or Alt if you press it with them. /scry in the chat does the same."),
                Slider("PanelScale", "Panel size", "How big the panel and its text are, on top of the scaling to your screen's height. The new size applies when you let go.",
                    SettingsWords.SmallestSize, SettingsWords.LargestSize, 0.05f, v => Math.Abs(v - 1f) < 0.001f ? "normal size" : Numbers.Amount(v) + " times"),
                Switch("WalkWhileOpen", "Walk while open", "Your keys walk your character while the panel is open and you are not typing in it."),
                Switch("LookWithRightMouse", "Look with right mouse", "Holding the right mouse button outside the panel turns the camera, to look around while it is open."),
                Choices("FocusSearchOnOpen", "On opening", "Whether your keys go to the search box as the panel opens, or stay free until you click the search, to walk with while Walk while open is on. The compact view always leaves them free.",
                    ("false", "Keys free"), ("true", "Search ready")),
                Slider("TooltipDelay", "Tips show after", "How long the pointer rests on something in the panel before its tip shows.",
                    0f, 3f, 0.05f, v => v <= 0f ? "at once" : Numbers.Amount(v) + " s"),
                Slider("RecentCount", "Recent list", "How many entries the Recent list remembers.",
                    1f, Explorer.MostRecent, 1f, v => Numbers.Count((long)Math.Round(v)) + (Math.Round(v) == 1 ? " entry" : " entries"))),

            new SettingSection("Preview",
                Choices("StageQuality", "Stage", "Full draws the stage at the panel's size, with its grass. Fast draws it at half size, without grass or the lights below a dungeon's cut, for a big dungeon on a weaker machine.",
                    ("Full", "Full"), ("Fast", "Fast")),
                Switch("AutoSpin", "Turn by itself", "The model turns slowly while you are not dragging it. The View box's Spin does the same."),
                Slider("SpinSpeed", "Turning speed", "How fast the model turns by itself.",
                    1f, 90f, 1f, v => Numbers.Count((long)Math.Round(v)) + "° a second"),
                Switch("LightDimmedRooms", "Lights below the cut", "The rooms shown dimmed below a dungeon's opened floor keep their own lights, as the game lights them. Off, a big dungeon is drawn in about half the time."),
                Choices("PlayOnSelect", "Sounds", "Whether a sound plays as soon as it is selected, so you can listen through the list with the arrow keys, or only when you press Enter.",
                    ("true", "Play on select"), ("false", "Only on Enter")),
                Slider("PreviewLoudness", "Loudness", "How loud a sound plays when you select it, beside the game's own loudness. The volume over the stage starts here for each selection.",
                    0f, 1f, 0.05f, v => v <= 0f ? "silent" : Numbers.Percent(v)),
                Slider("LighterFilmFromMs", "Softer while moving", "While you turn or zoom the stage, one that took at least this long to draw is drawn a little softer, so a big dungeon moves smoothly. It is sharp again once the camera holds still.",
                    0f, StageFilm.LighterNeverMs, 1f, v => v <= 0f ? "always" : v >= StageFilm.LighterNeverMs ? "never" : Numbers.Count((long)Math.Round(v)) + " ms")),

            new SettingSection("Compendium", CompendiumRows().ToArray()),

            new SettingSection("Map",
                In("Dungeon marks", Switch("DungeonMarksOnMap", "Dungeon marks", SettingsWords.DungeonMarksWhy)),
                In("Dungeon marks", Choices("DungeonMarkNamesAlways", "Names", "Whether a mark's name shows under it always, as the game names its pins, or while you point at it.",
                    ("false", "When pointed at"), ("true", "Always"))),
                In("Dungeon marks", Choices("DungeonMarkSize", "Size", "How big a dungeon mark stands beside the game's pins; its name grows with it.",
                    MapLook.MarkScales.Select((s, i) => (Stored.Exact(s), SizeNames[i])).ToArray())),
                In("Dungeon marks", Switch("DungeonMarksOnMinimap", "On the minimap", "The dungeons you walked are marked on the minimap too, at its pins' size, without names.")),
                In("Show on map", Switch("RecordSeenSpots", "Remember what you see", "Keeps where your character saw ore, berries, flint and places, so a page's Show on map can mark them. /scry forget spots clears what is kept.")),
                In("Show on map", Choices("ShowOnMapOtherPins", "Your other pins", "While a page's kind is shown, so its marks stand out. Back to full when you hide it or close the map.",
                    ("Full", "Full"), ("Third", "A third"), ("Hidden", "Hidden"))),
                In("Show on map", Choices("ShowOnMapDarken", "Darken the map", "The map's picture under the marks while a kind is shown, so they stand out on any ground.",
                    ("Off", "Off"), ("Half", "Half"), ("Strong", "Strong"))),
                In("Show on map", Choices("ShowOnMapDotSize", "Dot size", "How big the dots stand beside the game's pins.",
                    ("Small", SizeNames[0]), ("Normal", SizeNames[1]), ("Large", SizeNames[2]))),
                In("Show on map", Switch("ShowOnMapZoomOut", "Zoom out to the marks", "The map opens just far enough out to hold the nearest marks, and your own zoom comes back when it closes.")),
                In("Show on map", Switch("ShowOnMapSquares", "Squares on your map", "Where a place you never came near stands somewhere inside, as the world's server tells it. Off, only what you saw is shown."))),

            new SettingSection("Background",
                Switch("ReadLocationsAutomatically", "Read every world's places", "Reads where things are found in each world's places and dungeons by itself once the catalog is read: a few minutes the first time in a world, kept for the next. Off, only when you press Find where things are."),
                Slider("CatalogDelay", "Start reading after", "How long after entering a world Scry starts reading the game's things in the background. Opening the panel sooner starts it at once.",
                    0f, 120f, 1f, v => v <= 0f ? "at once" : Numbers.Count((long)Math.Round(v)) + " s")),

            new SettingSection("This server", ServerRows().ToArray()),

            new SettingSection("For checking Scry",
                Switch("LogPreviews", "Log previews", "Writes to the log what each preview played and what Scry found out about each thing, to find out why something looks or sounds wrong."),
                Switch("ResourceMonitor", "Resource monitor", "Shows a small box in the screen's corner telling what Scry costs each frame and what it holds. /scry monitor switches it too.")),
        };

        /// <summary>
        /// What the first start's setup offers beside the compendium's rules: the key opening Scry,
        /// walking and looking while it is open, and what it puts on Valheim's map; each a row of the
        /// card's own, set the same way.
        /// </summary>
        public static readonly IReadOnlyList<SettingRow> Setup = new[]
        {
            Key("OpenKey", "Open Scry with", "The key that opens and closes the panel; /scry in the chat does the same."),
            Ticks(new[] { "WalkWhileOpen", "LookWithRightMouse" }, "While the panel is open", "Each on its own; both stay under the gear.", "Walk with your keys", "Look with the right mouse"),
            Ticks(new[] { "DungeonMarksOnMap", "RecordSeenSpots" }, "On Valheim's map", "Marks for the dungeons you walked or saw, and where you saw ore, berries and places, for a page's Show on map; the dungeons you only see stay unmarked without remembering what you see.", "Mark dungeons", "Remember what you see"),
        };

        /// <summary>Every row, with what the server allows of a row standing under it; for the tests.</summary>
        [ForTest]
        public static IEnumerable<SettingRow> AllRows =>
            Sections.SelectMany(s => s.Rows).SelectMany(r => r.Limit == null ? new[] { r } : new[] { r, r.Limit });

        /// <summary>
        /// The character's compendium, as its box sets it: the rules, Known up to and what each
        /// source of the world shows as known, each with what the world's server allows of it under
        /// it; then the news of what it learns.
        /// </summary>
        private static IEnumerable<SettingRow> CompendiumRows()
        {
            yield return Book(BookPart.Rules, CompendiumWords.RulesCaption, "Off shows every page whole. Loose shows the pages you met. Strict also keeps back what the game never shows, until you find it out.",
                Server(Choices("Rules", SettingsWords.WorldPlays, "The compendium's rules everyone joining your world plays by, or each player's own choice. A server you join sets its own.",
                    ("PlayersChoose", "Each player's"), ("Off", "Off"), ("Loose", "Loose"), ("Strict", "Strict"))));
            yield return Book(BookPart.KnownUpTo, CompendiumWords.FloorCaption, "Shows everything of the tiers whose bosses you beat as known, marked with stairs. You can lower it again.",
                Server(Switch("KnownUpTo", SettingsWords.WorldAllowsIt, "Whether players joining your world may show everything of the tiers whose bosses they beat as known. A server you join sets its own.")));
            foreach (var source in WorldSources.All)
            {
                var row = Book(BookPart.Source, CompendiumWords.SourceName(source), CompendiumWords.SourceTip(source),
                    Server(Choices(source + "UpTo", SettingsWords.WorldAllowsUpTo, "How much this source may show as known to players joining your world, at most. A server you join sets its own.",
                        WorldSources.Offered(source).Select(l => (l.ToString(), CompendiumWords.LevelName(l))).ToArray())));
                row.Source = source;
                row.WhyAsTip = true;
                yield return In(CompendiumWords.SourcesCaption, row);
            }
            yield return In(SettingsWords.NewsGroup, Choices("News", "News of what you learn", "What a notice under the header tells as your character learns it: all of it, new pages only, leaving out what was found out about one met before, or nothing, the Learned tab alone telling it.",
                ("All", "All of it"), ("NewPages", "New pages only"), ("Off", "Learned tab only")));
        }

        /// <summary>The words of a size chosen one of three, for the marks and the dots alike.</summary>
        private static string[] SizeNames => new[] { "Small", "Normal", "Large" };

        /// <summary>The line under a group's heading saying what its rows share, their own words on their labels' tips; none.</summary>
        public static string GroupIntro(string group) =>
            group == CompendiumWords.SourcesCaption ? CompendiumWords.SourcesHint + " " + SettingsWords.PointAtAName : null;

        /// <summary>What the world's server decides beside the compendium's rows, each saying so: what players may do and see, and what the server itself keeps.</summary>
        private static IEnumerable<SettingRow> ServerRows()
        {
            yield return Server(Switch("Peeking", "Peeking", "Whether players on this server may peek: show the page they are on whole, or every page until the panel closes."));
            yield return Server(Slider("SeenReach", "Seen within", "How near a player on this server must be to a creature to see its stars, gear and attacks, and to a thing to see it. At none, things are seen by their size alone.",
                0f, 150f, 1f, Metres));
            yield return Server(Slider("SeenOnMapReach", "Remembered within", "How far away a player's sight on this server remembers where ore, pickables and places stand, for Show on map. At none, nothing is remembered.",
                0f, 150f, 1f, Metres));
            yield return Server(Switch("SquaresOnMap", "Squares on the map", "Whether this server tells players the squares that hold a place in ground they explored but never came near."));
            yield return In(RunningTheWorld, Server(Switch("ReadTracesOnStart", "Read dungeon traces", "On the game that runs the world, reads what was left inside its dungeons once its records are read, so rooms emptied before anyone's walk was kept count as walked on this server.")));
            yield return In(RunningTheWorld, Server(Switch("TakeWalks", "Keep players' walks", "The server keeps the dungeon walks players' games send and tells them to everyone as walked on this server.")));
        }

        /// <summary>The heading of what only the game running the world reads, a dedicated server or a host.</summary>
        public const string RunningTheWorld = "When you run the world";

        /// <summary>A slider's value at its step, within its range, as a drag lands on it.</summary>
        public static float Stepped(SettingRow row, float value)
        {
            var steps = (float)Math.Round((value - row.Min) / row.Step);
            return Math.Max(row.Min, Math.Min(row.Max, row.Min + steps * row.Step));
        }

        /// <summary>Whether a choice's value is the one set: alike as words, or as numbers however each is written.</summary>
        public static bool SameValue(string choice, string set) =>
            set != null && (string.Equals(choice, set, StringComparison.OrdinalIgnoreCase)
                || Stored.TryExact(choice, out var a) && Stored.TryExact(set, out var b) && Math.Abs(a - b) < 0.001f);

        /// <summary>A switch's or a tick's value the other way, as the config file writes it.</summary>
        public static string Flipped(string value) => value == True ? False : True;

        /// <summary>How the config file writes a switch on and off.</summary>
        public const string True = "true", False = "false";

        private static string Metres(float v) => v <= 0f ? "none" : Numbers.Count((long)Math.Round(v)) + " m";

        private static SettingRow Key(string key, string label, string why) =>
            new SettingRow { Keys = new[] { key }, Label = label, Why = why, Control = SettingControl.Key };

        private static SettingRow Switch(string key, string label, string why) =>
            new SettingRow { Keys = new[] { key }, Label = label, Why = why, Control = SettingControl.Switch };

        private static SettingRow Choices(string key, string label, string why, params (string Value, string Name)[] choices) =>
            new SettingRow { Keys = new[] { key }, Label = label, Why = why, Control = SettingControl.Choices, Choices = choices.Select(c => new SettingChoice(c.Value, c.Name)).ToArray() };

        private static SettingRow Ticks(string[] keys, string label, string why, params string[] names) =>
            new SettingRow { Keys = keys, Label = label, Why = why, Control = SettingControl.Ticks, TickNames = names };

        private static SettingRow Slider(string key, string label, string why, float min, float max, float step, Func<float, string> shown) =>
            new SettingRow { Keys = new[] { key }, Label = label, Why = why, Control = SettingControl.Slider, Min = min, Max = max, Step = step, Shown = shown };

        private static SettingRow Book(BookPart part, string label, string why, SettingRow limit) =>
            new SettingRow { Label = label, Why = why, Control = SettingControl.Book, Book = part, Limit = limit };

        private static SettingRow In(string group, SettingRow row)
        {
            row.Group = group;
            return row;
        }

        private static SettingRow Server(SettingRow row)
        {
            row.ServerDecides = true;
            return row;
        }
    }
}
