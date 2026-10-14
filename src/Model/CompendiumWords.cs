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

        /// <summary>The reply to <c>/scry map</c> in a world.</summary>
        public const string MapAgain = "the explored map will be read again for this character in this world";

        /// <summary>The reply to <c>/scry map</c> outside a world.</summary>
        public const string MapNoWorld = "the explored map can be read only in a world";

        /// <summary>The log's line once the map was read.</summary>
        public static string MapRead(int kinds, int raised, bool fromServer) =>
            $"caught up from the explored map{(fromServer ? " with the server's answer" : "")}: {Numbers.Count(kinds)} kinds of place lie in explored ground, {Numbers.Count(raised)} of them new to the compendium";

        /// <summary>The server's log line on answering a player's map.</summary>
        public static string MapAnswered(int kinds) => $"answered a player's explored map: {Numbers.Count(kinds)} kinds of place lie in it";

        /// <summary>The log's line when the server never answered.</summary>
        public static string MapNoServerScry => "the server did not answer on the explored map (it has no Scry, or one too old): places count as the character meets them";

        /// <summary>The chip reading the explored map again, with the Locations tab picked.</summary>
        public const string ReadMap = "Read the map again";

        /// <summary>That chip's tip.</summary>
        public const string ReadMapTip = "Counts the kinds of place in the ground this character explored in this world as seen, as on its first visit with Scry";

        /// <summary>The log's line on a dedicated server.</summary>
        public static string ServerPart => "on a dedicated server Scry only answers players' explored maps, for their compendium";

        /// <summary>The milestone floor's label: the last tier it shows, by its biome, or none.</summary>
        public static string Floor(int floor) => "Floor: " + (floor > 0 ? Naming.FieldLabel(Tiers.BiomeOf(floor)) : "none");

        /// <summary>The tip of raising the floor to a tier, with the boss that fell for it where known.</summary>
        public static string RaiseFloor(int to, string boss) =>
            $"Show everything of the {Naming.FieldLabel(Tiers.BiomeOf(to))} as known" + (string.IsNullOrEmpty(boss) ? "" : $": {boss} fell for this character");

        /// <summary>The tip of lowering the floor to a tier, or off.</summary>
        public static string LowerFloor(int to) => to > 0
            ? $"Show the {Naming.FieldLabel(Tiers.BiomeOf(to))} and what came before as known, not the {Naming.FieldLabel(Tiers.BiomeOf(to + 1))}"
            : "Show only what this character has met again";

        /// <summary>The floor's tip while the next boss has not fallen.</summary>
        public static string FloorWaits(string boss) => $"The next tier opens once {boss} falls for this character";

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
