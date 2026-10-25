using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What the character did with things in words, from what its book keeps: the moment of a
    /// first sighting, kills by how they were dealt, deaths by what dealt them, the Learned list
    /// by day with how each thing came, and the notice's news of what happened.
    /// </summary>
    internal static class JournalWords
    {
        /// <summary>The time of day and the weather, as a moment tells them: "at dusk in the rain"; by day only the weather; none for neither.</summary>
        public static string When(DayPart time, string weather)
        {
            var during = WeatherWords.During(weather);
            string at;
            switch (time)
            {
                case DayPart.Dawn: at = "at dawn"; break;
                case DayPart.Dusk: at = "at dusk"; break;
                case DayPart.Night: at = "at night"; break;
                default: at = null; break;
            }
            if (at == null) return during;
            return during == null ? at : at + " " + during;
        }

        /// <summary>
        /// A first sighting: "day 9, at dusk in the rain, in the Black Forest; it had 1 star", each
        /// part where known, the stars where it had any.
        /// </summary>
        public static string FirstSeen(int day, DayPart time, string weather, string biome, int stars, string world = null)
        {
            var parts = new List<string>();
            if (day >= 0) parts.Add("day " + Numbers.Count(day) + InWorld(world));
            else if (world != null) parts.Add("in " + world);
            var when = When(time, weather);
            if (when != null) parts.Add(when);
            if (!string.IsNullOrEmpty(biome)) parts.Add("in the " + biome);
            var said = string.Join(", ", parts.ToArray());
            return stars > 0 ? $"{said}; it had {Naming.Count(stars, "star", "stars")}" : said;
        }

        /// <summary>
        /// The game's kills of a creature for the character, by how they were dealt: "14 times: 9 in
        /// melee, 3 with arrows or bolts, 2 with mixed attacks", the most first; "5 times, all in
        /// melee"; only the count where the game split none. The game counts a kill under one kind
        /// only where every hit was of it, the rest only in the total.
        /// </summary>
        public static string Kills(int killed, int melee, int ranged, int magic, int unarmed)
        {
            var often = CompendiumWords.Often(killed);
            var kinds = new List<(int Count, string Said)> { (melee, "in melee"), (ranged, "with arrows or bolts"), (magic, "by magic"), (unarmed, "bare-handed") }
                .Where(k => k.Count > 0).OrderByDescending(k => k.Count).ToList();
            if (kinds.Count == 0) return often;
            var mixed = killed - kinds.Sum(k => k.Count);
            if (kinds.Count == 1 && mixed <= 0) return killed == 1 ? $"{often}, {kinds[0].Said}" : $"{often}, all {kinds[0].Said}";
            var told = kinds.Select(k => Numbers.Count(k.Count) + " " + k.Said).ToList();
            if (mixed > 0) told.Add(Numbers.Count(mixed) + " with mixed attacks");
            return often + ": " + string.Join(", ", told.ToArray());
        }

        /// <summary>The deaths a creature dealt the character: "twice, last on day 21", "once, on day 21"; the count alone where no day was known.</summary>
        public static string KilledYou(int count, int lastDay)
        {
            var often = CompendiumWords.Often(count);
            if (lastDay < 0) return often;
            return count == 1 ? $"{often}, on day {Numbers.Count(lastDay)}" : $"{often}, last on day {Numbers.Count(lastDay)}";
        }

        /// <summary>A way of coming to know a thing as done, for its row in the Learned list.</summary>
        private static string Done(CameBy way)
        {
            switch (way)
            {
                case CameBy.Fought: return "fought";
                case CameBy.Killed: return "killed";
                case CameBy.Tamed: return "tamed";
                case CameBy.Held: return "held";
                case CameBy.Made: return "made";
                case CameBy.Eaten: return "eaten";
                case CameBy.Recipe: return "recipe unlocked";
                case CameBy.Card: return "card read";
                case CameBy.Used: return "used";
                case CameBy.Near: return "near";
                case CameBy.Built: return "built";
                case CameBy.Hit: return "hit";
                case CameBy.Picked: return "picked";
                case CameBy.Trophy: return "its trophy held";
                case CameBy.StoodIn: return "stepped into";
                case CameBy.WentInside: return "gone inside";
                case CameBy.LivedThrough: return "lived through";
                case CameBy.Traded: return "traded with";
                case CameBy.OnYou: return "had on you";
                case CameBy.Given: return "come by what gives it";
                case CameBy.Gives: return "its gift taken";
                case CameBy.Chosen: return "chosen at its stone";
                case CameBy.Born: return "seen born";
                case CameBy.GrewUp: return "seen grow up";
                default: return "known";
            }
        }

        /// <summary>
        /// A row's note in the Learned list: what was found out of it, the explored map it came
        /// off, the way it came to be known, else "known" or "seen".
        /// </summary>
        public static string LearnedNote(BookNews news)
        {
            switch (news.How)
            {
                case NewsHow.FoundOut:
                case NewsHow.Map:
                    return CompendiumWords.FoundOutNote(news.Proof);
                case NewsHow.Known:
                    return news.Way is CameBy way ? Done(way) : "known";
                default:
                    return "seen";
            }
        }

        /// <summary>A day's heading in the Learned list, "Earlier" for what has no day; named by its world where that is not the one played now.</summary>
        public static string DayHeading(int day, string world = null)
        {
            var said = day >= 0 ? "Day " + Numbers.Count(day) : "Earlier";
            return world != null ? world + "  \u00B7  " + said : said;
        }

        /// <summary>" in Elderwood" after a day, where the world is named; nothing otherwise.</summary>
        public static string InWorld(string world) => world != null ? " in " + world : "";

        /// <summary>Whether a group of the Learned list starts folded: all but the newest two groups in the order things were learned (a newer day of another world first), and what has no day.</summary>
        public static bool FoldedAtFirst(int groupRank, int day) => day < 0 || groupRank >= 2;

        /// <summary>What was found out of a thing, named in a sentence of the news: "how fire lands on Neck", "the health of Troll".</summary>
        public static string FoundOutOf(string proof, string name)
        {
            if (EntryKeys.IsStarsFell(proof, out var fell)) return $"what {CompendiumWords.StarsChange(fell)} on {name}";
            if (EntryKeys.IsDeed(proof, out var deed))
            {
                switch (deed)
                {
                    case Deed.Tamed: return $"how {name} tames";
                    case Deed.Born: return $"how {name} breeds";
                    case Deed.Hatched: return $"how {name} hatches";
                    case Deed.Ridden: return $"how {name} rides";
                    case Deed.GrewUp: return $"how {name} grows up";
                    case Deed.GrewBack: return $"how {name} grows back";
                    case Deed.StarredFell: return $"what stars change on {name}";
                    case Deed.Spawned: return $"how {name} spawns";
                    case Deed.Collected: return $"the pace of {name}";
                    case Deed.Grew: return $"how {name} grows";
                    case Deed.Finished: return $"how {name} works";
                    default: return $"the health of {name}";
                }
            }
            if (EntryKeys.IsWeakSpot(proof)) return $"a weak spot of {name}";
            if (EntryKeys.IsDamage(proof, out var type)) return $"how {type} lands on {name}";
            return $"what an attack of {name} deals";
        }

        /// <summary>Names up to three, or counts them and names the first two: "9 things, among them Troll and Forge".</summary>
        private static string Named(IReadOnlyList<string> names) =>
            names.Count <= 3 ? Naming.Joined(names) : $"{Naming.Count(names.Count, "thing", "things")}, among them {names[0]} and {names[1]}";

        /// <summary>
        /// The notice's news, from what was learned since it last told, newest first: the day it
        /// was learned ("Day 14", or "Since day 12" over several), then what the character came
        /// to know, saw and found out, named while there are few, what the game's records told
        /// counted, and the places and biomes the explored map showed.
        /// </summary>
        public static string News(IReadOnlyList<NewsLine> news, int places, int biomes)
        {
            var dated = news.Where(n => n.Day >= 0).ToList();
            var known = dated.Where(n => n.How == NewsHow.Known).Select(n => n.Name).ToList();
            var seen = dated.Where(n => n.How == NewsHow.Seen).Select(n => n.Name).ToList();
            var found = dated.Where(n => n.How == NewsHow.FoundOut).ToList();
            var before = news.Count(n => n.Day < 0 && n.How != NewsHow.Map);

            var parts = new List<string>();
            if (known.Count > 0) parts.Add("came to know " + Named(known));
            if (seen.Count > 0)
            {
                if (known.Count == 0 || known.Count + seen.Count <= 3) parts.Add("saw " + Named(seen));
                else parts.Add($"saw {Numbers.Count(seen.Count)} more");
            }
            if (found.Count > 0) parts.Add("found out " + FoundOutOf(found[0].Proof, found[0].Name) + (found.Count > 1 ? $" and {Numbers.Count(found.Count - 1)} more" : ""));
            if (before > 0) parts.Add($"read {Naming.Count(before, "thing", "things")} from the game's records of this character");
            if (places + biomes > 0)
            {
                var counted = places > 0 && biomes > 0 ? $"{Naming.Count(places, "place", "places")} and {Naming.Count(biomes, "biome", "biomes")}"
                    : places > 0 ? Naming.Count(places, "place", "places") : Naming.Count(biomes, "biome", "biomes");
                parts.Add("your explored map showed " + counted);
            }
            if (parts.Count == 0) return "";

            var said = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1).ToArray()) + ", and " + parts[parts.Count - 1];
            if (dated.Count == 0) return Naming.Capital(said);
            var first = dated.Min(n => n.Day);
            var last = dated.Max(n => n.Day);
            return (first == last ? "Day " : "Since day ") + Numbers.Count(first) + ": " + said;
        }
    }
}
