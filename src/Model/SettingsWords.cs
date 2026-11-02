using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The words of the settings card, opened by the gear in the panel's title row, beside each
    /// setting's own in its catalogue (<see cref="SettingsCatalog"/>): the card's title, where its
    /// changes are kept, its reset and whose the server's settings are; and of the compendium's
    /// guide and the card asking a character whether to keep a compendium.
    /// </summary>
    internal static class SettingsWords
    {
        public const string Title = "Settings";
        public const string GearTip = "Settings: the panel, the preview, the compendium's news, the map, reading in the background, and what this server decides";
        public const string KeptAtOnce = "Every change is kept at once, in isimp.Scry.cfg.";
        public const string Reset = "Reset to defaults";
        public const string Close = "Close";
        public const string ServerDecides = "This server decides these, in its own settings; you see what it chose.";
        public const string ServerYours = "While you play alone or host, these are yours, and players joining your world play by them. A server you join decides them for its world.";

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

        // ----- Asking a character whether to keep a compendium -----

        /// <summary>The question, naming the character.</summary>
        public static string WelcomeTitle(string character) => string.IsNullOrEmpty(character) ? "Keep a compendium for your character?" : "Keep a compendium for " + character + "?";

        public const string WelcomeWords = "Scry can show only what your character has met, growing as you play. Change it any time in the Compendium box at the top of the panel.";

        /// <summary>The answers, each its name and what it means.</summary>
        public static readonly (string Name, string Means)[] WelcomeAnswers =
        {
            ("Loose", "Known pages whole, the unmet left out"),
            ("Strict", "Numbers and connections only as you find them out"),
            ("Not now", "Everything, as a lookup"),
        };
    }
}
