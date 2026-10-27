using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Scry
{
    /// <summary>What each of a creature's clips is, in a few words on its chip: the attack it plays, what the creature does in it, or idling.</summary>
    internal static class ClipWords
    {
        /// <summary>The Animations section's button that forgets what each animator was seen to play (<see cref="KeptClips"/>).</summary>
        public const string WorkOutAgain = "Work out again";

        /// <summary>That button's hover.</summary>
        public const string WorkOutAgainTip = "Forget what Scry saw each animator play, kept from one session to the next, and work it out again";

        /// <summary>The reply to <c>/scry forget clips</c>: how many animators' clips were forgotten.</summary>
        public static string Forgotten(int probes) =>
            probes == 0 ? "no clips were kept; they are worked out again as pages open." : $"forgot the clips of {Numbers.Count(probes)} kept {(probes == 1 ? "look at an animator" : "looks at animators")}; they are worked out again as pages open.";

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

        /// <summary>The clip a creature dies in where its death hurts what stands near (<see cref="Scry.DeathAttack"/>), by what its death does: an attack too.</summary>
        public static string DeathAttack(string blast) => AttackStart + blast + ", as it dies";

        /// <summary>The clip of a later step of an attack's combo, by the attack's own name and the step from 0.</summary>
        public static string Swing(string attack, int step) => attack + ", swing " + Numbers.Count(step + 1);

        /// <summary>Whether a clip's tag is an attack's (<see cref="Attack"/>).</summary>
        public static bool IsAttack(string tag) => tag != null && tag.StartsWith(AttackStart, StringComparison.Ordinal);

        private const string AttackStart = "attack ";

        /// <summary>
        /// The tip of an attack's name in an attacks table, by whether its clip is known yet and is
        /// playing; a weapon shown on its own is put on the person first.
        /// </summary>
        public static string PlayAttackTip(bool known, bool playing, bool wearFirst = false) =>
            !known ? (wearFirst ? "Put it on the person and play this attack" : "Its animations are still being worked out; this plays once they are")
            : playing ? "Stop this attack" : "Play this attack";

        /// <summary>The tip of an attack's name in an attacks table kept back under the strict rules.</summary>
        public const string KeptAttackTip = "Plays once this attack hit you, or you saw it begun up close";

        /// <summary>One chip counting the attack clips kept back under the strict rules, and what frees them.</summary>
        public static string ClipsWait(int clips) => Naming.Count(clips, "clip waits", "clips wait") + "  \u00B7  see it attack, up close or taken";

        /// <summary>The tip of that chip.</summary>
        public const string KeptClipsTip = "They play once each attack hit you, or you saw it begun up close";

        /// <summary>What each group of clips is called (<see cref="ClipGroups"/>), in the order of <see cref="ClipGroup"/>.</summary>
        private static readonly string[] GroupNames =
        {
            "Attacks", "Moving", "Blocking", "Jumping, falling, swimming", "Hurt and dying", "Emotes", "Riding and resting", "Working", "Other",
        };

        /// <summary>A group's heading with how many clips it holds; moving says what with, for a selection that holds things.</summary>
        public static string GroupHeading(ClipGroup group, int clips, int stance)
        {
            var name = GroupNames[(int)group];
            if (group == ClipGroup.Moving && stance >= 0 && stance < HeldWith.Length) name += " with " + HeldWith[stance];
            return name + "  " + Numbers.Count(clips);
        }

        /// <summary>The heading of the clips played only with other gear in hand (<see cref="HeldClips"/>), listed only while the clips are filtered.</summary>
        public const string OtherGearHeading = "With other gear";

        /// <summary>What each stance is held with, by the game's numbers of them (<c>ItemDrop.ItemData.AnimationState</c>).</summary>
        private static readonly string[] HeldWith =
        {
            "bare hands", "one-handed weapons", "two-handed clubs", "bows", "shields", "torches", "a torch in the left hand", "atgeirs",
            "battleaxes", "fishing rods", "crossbows", "knives", "staffs", "greatswords", "magic items", "two axes", "feasts", "scythes",
        };

        /// <summary>The chip of such a clip, with what it is held with; "other gear" where the stance is not known.</summary>
        public static string OtherGear(string chip, int stance) =>
            chip + "  ·  with " + (stance >= 0 && stance < HeldWith.Length ? HeldWith[stance] : "other gear");

        /// <summary>The heading of the clips a creature never plays (<see cref="ClipLeftovers"/>), listed only while the clips are filtered.</summary>
        public const string UnplayedHeading = "Not played by this creature";

        /// <summary>The chip of such a clip.</summary>
        public static string Unplayed(string chip) => chip + "  ·  not played by this creature";

        /// <summary>An attack's name in its table where its animations are worked out and none is its own: plain, no link, and saying so.</summary>
        public static string NoAnimation(string name) => name + "  ·  no animation";

        /// <summary>
        /// That name's tip: the trigger the game starts it by (<c>Attack.Start</c>), and whether the
        /// creature's animator lacks it or has it leading to no animation; where the trigger is not
        /// known, only that none was found.
        /// </summary>
        public static string NoAnimationTip(string trigger, bool inAnimator) =>
            string.IsNullOrEmpty(trigger) ? "No animation of its own was found for this attack"
            : inAnimator ? $"The game starts it by \"{trigger}\", which leads this creature's animator to no animation"
            : $"The game starts it by \"{trigger}\", which this creature's animator does not have, so it plays no animation";

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
