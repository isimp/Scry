using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The words of the settings card, opened by the gear in the panel's title row: each row's
    /// caption, its choices and tips, and the steps a few of them take; and of the compendium's
    /// guide and the card asking a character whether to keep a compendium.
    /// </summary>
    internal static class SettingsWords
    {
        public const string Title = "Settings";
        public const string GearTip = "Settings: the panel's size and key, walking and looking while it is open, sounds, tips, the stage and the news";
        public const string Kept = "Kept in BepInEx/config/isimp.Scry.cfg, with the rest of Scry's settings, each described there.";

        public const string Size = "Panel size";
        public const string Key = "Open key";
        public const string PressAKey = "Press a key";
        public const string PressTip = "Press the key that is to open Scry; Escape keeps the one there is";
        public const string Waiting = "Press the key...";
        public const string WhileOpen = "While open";
        public const string Walk = "Walk";
        public const string Look = "Look with right mouse";
        public const string WhileOpenTip = "Walk with your keys while the panel is open and you are not typing in it; hold the right mouse button outside the panel to look around";
        public const string Opening = "On opening";
        public const string SearchReady = "Search ready";
        public const string KeysWalk = "Keys walk";
        public const string OpeningTip = "Whether the keys go to the search as the panel opens, or walk until the search is clicked";
        public const string Sounds = "Sounds";
        public const string PlayOnSelect = "Play on select";
        public const string OnlyOnEnter = "Only on Enter";
        public const string Tips = "Tips after";
        public const string Stage = "Stage";
        public const string Full = "Full";
        public const string Fast = "Fast";
        public const string StageTip = "Fast films the stage at half its size, without grass or the lights below a dungeon's cut, for a big dungeon on a weaker machine";
        public const string News = "News of what you learn";
        public const string Notice = "Notice";
        public const string LearnedOnly = "Learned tab only";
        public const string NewsTip = "Whether the notice under the header tells what the character learned, or the Learned tab alone";

        public const string SizeTip = "How big the panel is drawn, on top of its scaling to the screen's height; it takes the size as the slider is let go";

        /// <summary>The panel's sizes the slider reaches, on top of the scaling by screen height, as the config file allows them.</summary>
        public const float SmallestSize = 0.6f, LargestSize = 2f;

        /// <summary>The waits before a tip offered, in seconds.</summary>
        public static readonly float[] TipWaits = { 0f, 0.35f, 1f };

        /// <summary>A size dragged to, in steps of 0.05 within those the slider reaches.</summary>
        public static float SizeStep(float size) => Math.Max(SmallestSize, Math.Min(LargestSize, (float)Math.Round(size * 20f) / 20f));

        /// <summary>A size as the slider says it: "1.0", "1.25".</summary>
        public static string SizeName(float size)
        {
            var hundredths = (int)Math.Round(size * 100f);
            return Numbers.Fixed(size, hundredths % 10 == 0 ? 1 : 2);
        }

        /// <summary>A wait as its choice says it: "Now", "0.35 s".</summary>
        public static string TipWaitName(float seconds) => seconds <= 0f ? "Now" : Numbers.Fixed(seconds, seconds < 1f ? 2 : 0) + " s";

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
            ("Off, Loose and Strict", "Off shows everything the game holds. Loose shows a thing your character knows with its whole page, naming only what it has met; what it only saw stands on the stage with its page closed. Strict also keeps back how things connect until your character saw them go together, and the numbers the game never shows until it found them out, each saying what shows it."),
            ("Known up to", "Shows everything of the biomes whose bosses your character has beaten as known, tier by tier, marked with stairs where it shows something; it can be lowered again."),
            ("Peeking", "The eye beside the compendium's button shows the page you are on whole until you go to another; Every page in its notice shows every page whole until the panel closes."),
            ("Learned and You", "Learned lists what your character learned, day by day; under Strict also what it found out and saw go together, which Loose shows whole from the start. A page's You box tells when it first saw the thing and came to know it, and how its fights went."),
            ("What this world shows as known", "Each source can show more of the world as known: the ground you explored, maps shared at a cartography table, places a Vegvisir or rune stone marked, the places every map shows, and the whole world counted. What shows so is marked with a folded map; your book keeps only what you met, so turning a source down takes it all back."),
            ("Pins", "The pin at the end of a ground's row marks its dungeon entrances on the large map: solid where you walked it, hollow where someone on this server did, plain for an entrance alone. A click on a mark shows the dungeon in Scry."),
            ("Playing together", "Where your friends have Scry, what their game runs near you counts for you as it would alone. A server can set the rules for everyone; the box then shows them locked, your own choices kept beneath."),
        };

        // ----- Asking a character whether to keep a compendium -----

        /// <summary>The question, naming the character.</summary>
        public static string WelcomeTitle(string character) => string.IsNullOrEmpty(character) ? "Keep a compendium for this character?" : "Keep a compendium for " + character + "?";

        public const string WelcomeWords = "Scry can show only what this character has met, growing as you play. Change it any time in the Compendium box at the top of the panel.";

        /// <summary>The answers, each its name and what it means.</summary>
        public static readonly (string Name, string Means)[] WelcomeAnswers =
        {
            ("Loose", "Known pages whole, the unmet left out"),
            ("Strict", "Only what you saw go together"),
            ("Not now", "Everything, as a lookup"),
        };
    }
}
