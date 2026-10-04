using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        public static string Attack(string weapon, bool second) => AttackStart + weapon + (second ? ", second" : "");

        /// <summary>Whether a clip's tag is an attack's (<see cref="Attack"/>).</summary>
        public static bool IsAttack(string tag) => tag != null && tag.StartsWith(AttackStart, StringComparison.Ordinal);

        private const string AttackStart = "attack ";

        /// <summary>A clip's chip: its name, and what it is where that is known and says more than the name does.</summary>
        public static string Row(string name, string what) => string.IsNullOrEmpty(what) || !SaysMore(name, what) ? name : name + "  \u00B7  " + what;

        /// <summary>
        /// Whether what a clip is says anything its name does not: a word of three letters or more
        /// that no word of the name is akin to ("swims" is said by "Swim Forward", "in water" is not
        /// said by "Idle Swim").
        /// </summary>
        private static bool SaysMore(string name, string what)
        {
            var named = Words(name);
            foreach (var word in Words(what))
            {
                if (word.Length >= 3 && !named.Any(n => Akin(n, word))) return true;
            }
            return false;
        }

        /// <summary>Two words alike in their first four letters, or the whole of a shorter one: "swim" and "swims", "sleeping" and "sleeps".</summary>
        private static bool Akin(string a, string b)
        {
            var n = Math.Min(Math.Min(a.Length, b.Length), 4);
            return n < 3 ? a == b : string.CompareOrdinal(a, 0, b, 0, n) == 0;
        }

        /// <summary>The words of a text, in lower case, letters only.</summary>
        private static List<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), "[a-z]+").Cast<Match>().Select(m => m.Value).ToList();

        /// <summary>The chip of the clip playing on its own, marked so.</summary>
        public static string Now(string chip) => "\u25B6 " + chip;

        /// <summary>A clip's tip: its name, its length, whether it loops, and whether it plays on its own now.</summary>
        public static string Tip(string name, float length, bool loops, bool now) =>
            $"{name}\n{Numbers.Fixed(length, 1)} s{(loops ? ", loops" : "")}{(now ? "\nPlaying on its own now" : "")}";

        /// <summary>How far a clip has played of its length.</summary>
        public static string Readout(float time, float length) => $"{Numbers.Fixed(time, 2)} / {Numbers.Fixed(length, 2)} s";

        /// <summary>The note over the chips of the clips an effect list goes with.</summary>
        public static string With(int clips) => clips > 1 ? "With its clips:" : "With its clip:";

        /// <summary>A clip found by its name to go with jumping, swimming or being in water, which it says.</summary>
        public static string ByName(string clip)
        {
            var lower = clip.ToLowerInvariant();
            var action = lower.Contains("jump") ? "jump" : lower.Contains("swim") ? "swim" : "water";
            return Actions[action] + ", by name";
        }
    }
}
