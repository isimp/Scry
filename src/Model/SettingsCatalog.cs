using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a setting is set on the card: a key pressed, a switch, one of a few, several at once, or a slider.</summary>
    internal enum SettingControl
    {
        Key,
        Switch,
        Choices,
        Ticks,
        Slider,
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
    /// A row of the settings card: the config entries it sets, by key (several for ticks), its
    /// label and its explanation, how it is set, and whether the world's server decides it.
    /// </summary>
    internal sealed class SettingRow
    {
        public IReadOnlyList<string> Keys { get; set; } = Array.Empty<string>();
        public string Label { get; set; }
        public string Why { get; set; }
        public SettingControl Control { get; set; }

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
    /// control fit for it; what the server decides in a section of its own. The card draws them
    /// as they stand here and sets them through the config file's own words for their values.
    /// </summary>
    internal static class SettingsCatalog
    {
        public static readonly IReadOnlyList<SettingSection> Sections = new[]
        {
            new SettingSection("Panel",
                Key("OpenKey", "Open Scry with", "The key that opens and closes the panel. /scry in the chat does the same."),
                Slider("PanelScale", "Panel size", "How big the panel and its text are, on top of the scaling to your screen's height. The new size applies when you let go.",
                    SettingsWords.SmallestSize, SettingsWords.LargestSize, 0.05f, v => Math.Abs(v - 1f) < 0.001f ? "as scaled" : Numbers.Amount(v) + " times"),
                Switch("WalkWhileOpen", "Walk while open", "Your keys walk your character while the panel is open and you are not typing in it."),
                Switch("LookWithRightMouse", "Look with right mouse", "Holding the right mouse button outside the panel turns the camera, to look around while it is open."),
                Choices("FocusSearchOnOpen", "On opening", "Whether your keys go to the search box as the panel opens, or walk until you click the search. The compact view always lets them walk.",
                    ("false", "Keys walk"), ("true", "Search ready")),
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
                Slider("LighterFilmFromMs", "Softer while moving", "While you turn or zoom the stage, one that took at least this long to draw is drawn a little softer, so a big dungeon moves smoothly. It is sharp again once the camera holds still.",
                    0f, StageFilm.LighterNeverMs, 1f, v => v <= 0f ? "always" : v >= StageFilm.LighterNeverMs ? "never" : Numbers.Count((long)Math.Round(v)) + " ms")),

            new SettingSection("Compendium",
                Choices("NewsNotice", "News of what you learn", "Whether a notice under the header tells what your character learned as it learns it, or the Learned tab alone.",
                    ("true", "Notice"), ("false", "Learned tab only"))),

            new SettingSection("Map",
                In("Dungeon marks", Switch("DungeonMarksOnMap", "Dungeon marks", "Marks dungeons on Valheim's large map, telling how much of each was walked; a click on a mark opens its map in Scry. Off, no dungeon marks show.")),
                In("Dungeon marks", Ticks(new[] { "DungeonMarksWalkedByYou", "DungeonMarksWalkedOnServer", "DungeonMarkEntrances" }, "Marks shown",
                    "Each on its own. Others' walks show where a source shows them with insides; an entrance nobody walked stays faint until you point at it.",
                    "Walked by you", "Walked by others on this server", "Entrances nobody walked")),
                In("Dungeon marks", Choices("DungeonMarkNamesAlways", "Names", "Whether a mark's name shows under it always, as the game names its pins, or while you point at it.",
                    ("false", "When pointed at"), ("true", "Always"))),
                In("Dungeon marks", Slider("DungeonMarkSize", "Size", "How big a dungeon mark is beside the game's pins; its name grows with it.",
                    SettingsWords.SmallestMarkSize, SettingsWords.LargestMarkSize, 0.1f, MarkSize)),
                In("Dungeon marks", Switch("DungeonMarksOnMinimap", "On the minimap", "The dungeons you walked are marked on the minimap too, at its pins' size, without names.")),
                In("Show on map", Switch("RecordSeenSpots", "Remember what you see", "Keeps where your character saw ore, berries, flint and places, so a page's Show on map can mark them. /scry forget spots clears what is kept."))),

            new SettingSection("Background",
                Switch("ReadLocationsAutomatically", "Read every world's places", "Reads where things are found in each world's places and dungeons by itself once the catalog is read: a few minutes the first time in a world, kept for the next. Off, only when you press Find where things are."),
                Slider("CatalogDelay", "Start reading after", "How long after entering a world Scry starts reading the game's things in the background. Opening the panel sooner starts it at once.",
                    0f, 120f, 1f, v => v <= 0f ? "at once" : Numbers.Count((long)Math.Round(v)) + " s")),

            new SettingSection("This server", ServerRows().ToArray()),

            new SettingSection("For checking Scry",
                Switch("LogPreviews", "Log previews", "Writes to the log what each preview played and what Scry found out about each thing, to find out why something looks or sounds wrong."),
                Switch("ResourceMonitor", "Resource monitor", "Shows a small box in the screen's corner telling what Scry costs each frame and what it holds. /scry monitor switches it too.")),
        };

        /// <summary>What the world's server decides, each saying so: the compendium's rules, what players may do and see, and what the server itself keeps.</summary>
        private static IEnumerable<SettingRow> ServerRows()
        {
            yield return Server(Choices("Rules", "Compendium rules", "The compendium's rules everyone on this server plays by, or each player's own choice.",
                ("PlayersChoose", "Each player's"), ("Off", "Off"), ("Loose", "Loose"), ("Strict", "Strict")));
            yield return Server(Switch("Peeking", "Peeking", "Whether players on this server may peek: show the page they are on whole, or every page until the panel closes."));
            yield return Server(Switch("KnownUpTo", "Known up to", "Whether players on this server may show everything of the tiers whose bosses they beat as known."));
            yield return Server(Slider("SeenReach", "Seen within", "How near a player on this server must be to a creature to see its stars, gear and attacks, and to a thing to see it. At none, things are seen by their size alone.",
                0f, 150f, 1f, Metres));
            yield return Server(Slider("SeenOnMapReach", "Remembered within", "How far away a player's sight on this server remembers where ore, pickables and places stand, for Show on map. At none, nothing is remembered.",
                0f, 150f, 1f, Metres));
            yield return Server(Switch("SquaresOnMap", "Squares on the map", "Whether this server tells players the squares that hold a place in ground they explored but never came near."));
            foreach (var source in WorldSources.All)
            {
                yield return Server(Choices(source + "UpTo", CompendiumWords.SourceName(source), $"The most {CompendiumWords.SourceName(source)} may show as known on this server.",
                    WorldSources.Offered(source).Select(l => (l.ToString(), CompendiumWords.LevelName(l))).ToArray()));
            }
            yield return Server(Switch("ReadTracesOnStart", "Read dungeon traces", "On the game that runs the world, reads what was left inside its dungeons once its records are read, so rooms emptied before anyone's walk was kept count as walked on this server."));
            yield return Server(Switch("TakeWalks", "Keep players' walks", "The server keeps the dungeon walks players' games send and tells them to everyone as walked on this server."));
        }

        /// <summary>A mark's size as the slider says it, beside the game's pins.</summary>
        private static string MarkSize(float v) =>
            Math.Abs(v - 1f) < 0.001f ? "a pin's size" : v < 1f ? Numbers.Amount(v) + " of a pin" : Numbers.Amount(v) + " pins' size";

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
