using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What the panel says of the compendium: a tab's count of what was found, a page not yet open and what it names.</summary>
    internal static class CompendiumWords
    {
        /// <summary>The name a closed page gives what was never met: what kind of thing it is, nothing more.</summary>
        public static string Unknown(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return "Unknown creature";
                case Kind.Item: return "Unknown item";
                case Kind.Piece: return "Unknown piece";
                case Kind.Resource: return "Unknown resource";
                case Kind.Spawner: return "Unknown spawner";
                case Kind.StatusEffect: return "Unknown status effect";
                case Kind.Biome: return "Unknown biome";
                case Kind.Location: return "Unknown place";
                case Kind.Raid: return "Unknown raid";
                default: return "Unknown";
            }
        }

        /// <summary>A kind's tab with the compendium on: how many of it were found, of all there are.</summary>
        public static string Tab(string label, int found, int total, string colour) =>
            $"{label}  <color=#{colour}>{Numbers.Count(found)}/{Numbers.Count(total)}</color>";

        /// <summary>The title of a list of what there is (what a mod adds) with the compendium on: how many of it the character has seen, which are all it shows.</summary>
        public static string Found(string title, int found) => $"{title}, {Numbers.Count(found)} found";

        /// <summary>The chip that shows everything for now, though the compendium is on.</summary>
        public const string Peek = "Peek";

        /// <summary>The peek chip's tip, by whether it is peeking.</summary>
        public static string PeekTip(bool peeking) => peeking
            ? "Show only what this character has met again"
            : "Show everything for now, not only what this character has met; it stops when the panel closes";

        /// <summary>The warning strip while peeking.</summary>
        public const string Peeking = "Peeking: everything shows, not only what this character has met, until the panel closes";

        /// <summary>The strip's chip that stops peeking.</summary>
        public const string StopPeeking = "Stop";

        /// <summary>The notice of what the character learned since it was last told: the first three by name, the rest counted.</summary>
        public static string NewlyLearned(IReadOnlyList<string> names)
        {
            var shown = names.Take(3).ToList();
            var more = names.Count - shown.Count;
            var listed = more > 0 ? string.Join(", ", shown) + $" and {Numbers.Count(more)} more"
                : shown.Count == 1 ? shown[0]
                : string.Join(", ", shown.Take(shown.Count - 1)) + " and " + shown[shown.Count - 1];
            return "Newly learned: " + listed;
        }

        /// <summary>The tip of the notice's cross.</summary>
        public const string PutNewsAway = "Put this away until more is learned";

        /// <summary>The notice's chip, listing what was learned lately.</summary>
        public const string ShowLearned = "Show";

        /// <summary>The chip listing what the character learned lately.</summary>
        public const string LearnedLately = "Learned lately";

        /// <summary>That chip's tip, by whether the list shows it.</summary>
        public static string LearnedTip(bool on) => (on ? "Showing" : "Show") + " what this character learned lately, newest first";

        /// <summary>The first line of a page not yet open, by how much the character knows of it.</summary>
        public static string Closed(KnownLevel level)
        {
            switch (level)
            {
                case KnownLevel.HeardOf: return "Heard of, not met yet";
                case KnownLevel.Seen: return "Seen, not yet come to know";
                default: return "Not met yet";
            }
        }

        /// <summary>What opens a page of this kind under the loose rules.</summary>
        public static string HowToKnow(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return "Fight it to learn all about it.";
                case Kind.Item: return "Pick it up, make it or eat it to learn all about it.";
                case Kind.Piece: return "Build it, use it or unlock it to learn all about it.";
                case Kind.Resource: return "Hit it or pick it to learn all about it.";
                case Kind.Spawner: return "Hit it to learn all about it.";
                case Kind.StatusEffect: return "Have it on you to learn all about it.";
                case Kind.Biome: return "Set foot in it to learn all about it.";
                case Kind.Location: return "Step inside to learn all about it.";
                case Kind.Raid: return "Live through it to learn all about it.";
                default: return "Meet it in the world to learn all about it.";
            }
        }
    }
}
