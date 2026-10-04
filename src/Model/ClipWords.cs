using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What each of a creature's clips is, in a few words on its chip: the attack it plays, what the creature does in it, or idling.</summary>
    internal static class ClipWords
    {
        /// <summary>What the game's own actions are called, by the action's name.</summary>
        private static readonly Dictionary<string, string> Actions = new Dictionary<string, string>
        {
            ["jump"] = "jumps", ["consume"] = "eats", ["sleep"] = "sleeps", ["wake"] = "wakes", ["alert"] = "alerted",
            ["spawn"] = "spawns", ["stagger"] = "staggers", ["water"] = "in water", ["swim"] = "swims", ["fly"] = "flies", ["dead"] = "dies",
        };

        /// <summary>What a clip the creature idles in is called.</summary>
        public const string Idles = "idles";

        /// <summary>What one of the game's own actions is called, where it has a name.</summary>
        public static bool Action(string action, out string word) => Actions.TryGetValue(action, out word);

        /// <summary>The clip of an attack, by the weapon it is made with, and whether it is the weapon's second.</summary>
        public static string Attack(string weapon, bool second) => "attack " + weapon + (second ? ", second" : "");

        /// <summary>A clip found by its name to go with jumping, swimming or being in water, which it says.</summary>
        public static string ByName(string clip)
        {
            var lower = clip.ToLowerInvariant();
            var action = lower.Contains("jump") ? "jump" : lower.Contains("swim") ? "swim" : "water";
            return Actions[action] + ", by name";
        }
    }
}
