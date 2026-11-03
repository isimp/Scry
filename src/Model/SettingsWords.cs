using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The words of the settings card, opened by the gear in the panel's title row, beside each
    /// setting's own in its catalogue (<see cref="SettingsCatalog"/>): the card's title, where its
    /// changes are kept, its reset and whose the server's settings are; and of the compendium's
    /// guide and the first start's setup.
    /// </summary>
    internal static class SettingsWords
    {
        public const string Title = "Settings";
        public const string GearTip = "Settings: the panel, the preview, your character's compendium, the map, reading in the background, and what this server decides";
        public const string KeptAtOnce = "Every change is kept at once, in isimp.Scry.cfg.";
        public const string Reset = "Reset to defaults";
        public const string Close = "Close";
        public const string ServerDecides = "This server decides these, in its own settings; you see what it chose. What it allows your compendium stands under each row of the Compendium section.";
        public const string ServerYours = "While you play alone or host, these are yours, and players joining your world play by them. A server you join decides them for its world. What your world allows players' compendiums stands under each row of the Compendium section.";

        // ----- The compendium's section -----

        /// <summary>The line heading the compendium's section: whose it is, and where the box sets the same.</summary>
        public static string CompendiumFor(string character) =>
            (string.IsNullOrEmpty(character) ? "For your character" : "For " + character) + " in this world. The compendium's box at the top sets the same.";

        /// <summary>What every setting the world's server sends ends with in the config file.</summary>
        public const string WorldSynced = " Set on the world's server, its value holds for every player connected to it.";

        /// <summary>A setting's description in the config file, saying the world's server decides it.</summary>
        public static string Synced(string description) => description + WorldSynced;

        /// <summary>What the dungeon marks' switch does, on the card and in the config file alike.</summary>
        public const string DungeonMarksWhy = "Marks dungeons on Valheim's large map and, where you ask for it, the minimap, telling how much of each was walked; a click on a mark opens its map in Scry. Off, no dungeon mark shows on either map, yours or others', entrances neither; a page's Show on map still marks what it shows.";

        /// <summary>A ground's tick in the Map section's row of which dungeons are marked.</summary>
        public static string PinnedGroundTick(WorldSource source) => source == WorldSource.SharedMaps ? "On shared maps" : "In ground you explored";

        public const string PointAtAName = "Point at a name for what it covers.";
        public const string HowItWorks = "How the compendium works";

        public const string NoBook = "Your character's compendium is read once you stand in a world.";
        public const string RulesOff = "With the rules Off, Known up to and what this world shows wait for Loose or Strict.";
        public const string NewsGroup = "News";

        /// <summary>The line under the section's intro while a server decides what it allows, or while it is yours to allow.</summary>
        public const string LimitsSome = "This server limits some of these; what it keeps from you is dimmed, saying so under its row.";
        public const string LimitsNone = "This server leaves every one of these to you.";

        /// <summary>The line while a server decides what it allows: that it limits some rows, or none.</summary>
        public static string LimitsServer(WorldRules rules)
        {
            var limits = WorldRulesWords.OnRow(BookPart.Rules, default, rules) != null || WorldRulesWords.OnRow(BookPart.KnownUpTo, default, rules) != null;
            foreach (var source in WorldSources.All) limits |= WorldRulesWords.OnRow(BookPart.Source, source, rules) != null;
            return limits ? LimitsSome : LimitsNone;
        }
        public const string LimitsYours = "While you play alone or host, the lines with the lock are what players joining your world may choose. A server you join sets its own.";

        /// <summary>What the world allows of a row, as the line under it says.</summary>
        public const string WorldPlays = "Your world plays";
        public const string WorldAllowsIt = "Your world allows it";
        public const string WorldAllowsUpTo = "Your world allows up to";

        /// <summary>The tip of the box's gear, opening the settings' Compendium section.</summary>
        public const string BoxSettingsTip = "All of these, and the news of what you learn, in the settings' Compendium section";

        public const string PressAKey = "Press a key";
        public const string PressTip = "Press the key you want to open Scry with, holding Shift, Ctrl or Alt with it if you like; Escape keeps the current one";
        public const string Waiting = "Press the key...";
        public const string Off = "Off";
        public const string On = "On";

        /// <summary>The dungeon marks' sizes the slider reaches, as a share of a map pin.</summary>
        public const float SmallestMarkSize = 0.5f, LargestMarkSize = 2f;

        /// <summary>The panel's sizes the slider reaches, on top of the scaling by screen height, as the config file allows them.</summary>
        public const float SmallestSize = 0.6f, LargestSize = 2f;

        /// <summary>Which of the values offered is nearest the one set, as a value set in the config file between two lights the nearest.</summary>
        public static int Nearest(IReadOnlyList<float> offered, float value)
        {
            var best = 0;
            for (var i = 1; i < offered.Count; i++) if (Math.Abs(offered[i] - value) < Math.Abs(offered[best] - value)) best = i;
            return best;
        }

        // ----- The compendium's guide -----

        public const string GuideTitle = "How the compendium works";
        public const string GuideTip = "How the compendium works: its rules, Known up to, peeking, and what a world shows as known";

        /// <summary>The guide's paragraphs, each its caption and its words.</summary>
        public static readonly (string Caption, string Words)[] Guide =
        {
            ("Off, Loose and Strict", "Off shows everything the game holds. Loose shows each thing your character knows with its whole page, but names only what your character has met. What the game's own menus show comes with the thing: a recipe's materials, a piece's cost and tool, a store's wares. Each section says how many things it left out, and a page's title row counts how much of what it names your character has met. A thing your character only saw stands on the stage with its page closed, telling what sight gave. Strict also keeps back what the game never shows of how things connect, until your character saw them go together. It keeps back the numbers the game never shows, too, until your character found them out, and each says what shows it. Under Strict, each section also counts what it kept back, and a page's title row counts how much of all that was found out. The stage shows a creature only with the stars and gear your character saw on it up close."),
            ("Known up to", "Known up to shows everything of the biomes whose bosses your character has beaten as known, tier by tier. What it shows is marked with stairs. You can lower it again."),
            ("Peeking", "The eye beside the compendium's button shows the page you are on whole until you go to another page. Every page, on the strip that then shows, does the same for all pages until you close the panel."),
            ("Learned and You", "Learned lists what your character learned, day by day. Under Strict it also lists the numbers your character found out, which Loose shows from the start. A row there opens its page at what changed, lit for a moment. A page's You box tells when your character first saw the thing and came to know it, and how its fights went."),
            ("What this world shows as known", "Each source can show more of the world as known: Ground you explored, Shared maps (read at a cartography table), Places marked for you (by a Vegvisir or rune stone), Places on every map, and The whole world, counted. What a source shows is marked with a folded map. Your book keeps only what your character met, so turning a source down takes back all it showed."),
            ("Pins", "The pin at the end of a ground's row marks its dungeon entrances on the large map: solid where you walked it, ticked where every room was walked, by you or with others on this server, hollow where only others did, faint for an entrance alone until pointed at. Each mark carries a glyph for the dungeon's kind. A click on a mark shows the dungeon in Scry."),
            ("Playing together", "Where your friends have Scry, what their game runs near you counts for you as it would alone. A server can set the rules for everyone; the box then shows them locked, your own choices kept beneath."),
        };

        // ----- The first start's setup -----

        public const string SetupTitle = "Set up Scry";
        public const string SetupIntro = "A few things worth choosing now. Each is set already: change what you like, or press Done.";
        public const string Done = "Done";
        public const string Later = "Later";
        public const string SetupKept = "All of this stays under the gear; the compendium's box at the top sets your rules too.";
        public const string RunSetupAgain = "Run the setup again";

        /// <summary>The compendium's step, naming the character where it has a name.</summary>
        public static string SetupCompendium(string character) => string.IsNullOrEmpty(character) ? "Compendium for your character" : "Compendium for " + character;

        /// <summary>The compendium's step where the server sets its rules, naming them.</summary>
        public static string SetupRulesSet(string rule) => "This server sets the compendium's rules: " + rule + ". Its own settings decide them for everyone.";

        /// <summary>The compendium's rules to choose from, each its name and what it means.</summary>
        public static readonly (CompendiumRule Rule, string Name, string Means)[] SetupRules =
        {
            (CompendiumRule.Off, "Off", "Every page whole, as a lookup"),
            (CompendiumRule.Loose, "Loose", "Pages you met whole, the rest left out"),
            (CompendiumRule.Strict, "Strict", "Only what you found out, as you play"),
        };
    }
}
