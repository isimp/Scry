using System;
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

        /// <summary>What the compendium is called in the title row.</summary>
        public const string ModeCaption = "Compendium";

        /// <summary>The compendium's button in the title row: the rules chosen, and that a box of its settings opens under it.</summary>
        public static string Button(CompendiumRule rule) => ModeCaption + " · " + RuleName(rule) + "  ▾";

        /// <summary>The caption over the compendium's box.</summary>
        public const string BoxCaption = "What your character knows, shown in the catalog";

        /// <summary>The caption of the rules' segments in the compendium's box.</summary>
        public const string RulesCaption = "Rules";

        /// <summary>The mode's tip.</summary>
        public const string ModeTip = "Show only what this character has come to know, by the rules chosen for it here; each character keeps its own";

        /// <summary>The tip of the arrow stepping the mode to a rule: its name and what it shows.</summary>
        public static string StepTip(CompendiumRule rule) => RuleName(rule) + ": " + RuleTip(rule);

        /// <summary>The rules the mode goes through, in its order.</summary>
        public static readonly IReadOnlyList<CompendiumRule> Rules = new[] { CompendiumRule.Off, CompendiumRule.Loose, CompendiumRule.Strict };

        /// <summary>A rule's name in the mode.</summary>
        public static string RuleName(CompendiumRule rule) => rule switch
        {
            CompendiumRule.Loose => "Loose",
            CompendiumRule.Strict => "Strict",
            _ => "Off",
        };

        /// <summary>What a rule shows, told after its name when stepping to it.</summary>
        public static string RuleTip(CompendiumRule rule) => rule switch
        {
            CompendiumRule.Loose => "list only what this character has seen, a page open once it has fought, held, built, used or entered it, and count in each kind's tab what was found of all there is",
            CompendiumRule.Strict => "list the same as Loose, but a page shows of how its entry connects (drops, places, recipes, attacks) only what this character saw go together, keeps back the names of what it has not seen, and the numbers the game never shows until you find them out",
            _ => "show everything. What this character comes to know is kept all the same, so turning the compendium on later shows everything met meanwhile",
        };

        /// <summary>The tip of the eye beside the mode that peeks at each page whole, by whether it is peeking.</summary>
        public static string PeekTip(bool peeking) => peeking
            ? "Show only what this character has met again"
            : "Show each page whole for now, its links leading anywhere and a whole name in quotes finding what this character has not met; the list stays as it knows it, and it stops when the panel closes";

        /// <summary>The warning strip while peeking.</summary>
        public const string Peeking = "Peeking: each page shows whole, its links lead anywhere and a whole name in quotes finds what this character has not met; the list stays as it knows it, until the panel closes";

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

        /// <summary>
        /// The notice of what was learned since last told: the things learned named
        /// (<see cref="NewlyLearned"/>), what a reading of the explored map brought counted, as it
        /// may be hundreds of places at once.
        /// </summary>
        public static string News(IReadOnlyList<string> named, int places, int biomes)
        {
            if (places + biomes == 0) return NewlyLearned(named);
            var counted = places > 0 && biomes > 0 ? $"{Naming.Count(places, "place", "places")} and {Naming.Count(biomes, "biome", "biomes")}"
                : places > 0 ? Naming.Count(places, "place", "places") : Naming.Count(biomes, "biome", "biomes");
            var map = "explored map showed " + counted;
            return named.Count == 0 ? "Your " + map : NewlyLearned(named) + "; your " + map;
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

        /// <summary>The line above the Locations tab's list with the compendium on, beside the button reading the explored map again.</summary>
        public const string MapBar = "Places in the ground this character explored count as seen, read again each session";

        /// <summary>The button reading the explored map again, in the Locations tab's bar.</summary>
        public const string ReadMap = "Read the map again";

        /// <summary>That chip's tip.</summary>
        public const string ReadMapTip = "Counts the kinds of place in the ground this character explored in this world as seen, as on its first visit with Scry";

        /// <summary>The log's line on a dedicated server.</summary>
        public static string ServerPart => "on a dedicated server Scry only answers players' explored maps, for their compendium, and keeps the group's records of the dungeons they walk";

        /// <summary>The caption of the milestone floor in the title row.</summary>
        public const string FloorCaption = "Floor";

        /// <summary>The milestone floor as stepped: the last tier it shows, by its biome, or none.</summary>
        public static string FloorName(int floor) => floor > 0 ? Naming.FieldLabel(Tiers.BiomeOf(floor)) : "none";

        /// <summary>The milestone floor named in a sentence.</summary>
        public static string Floor(int floor) => FloorCaption + ": " + FloorName(floor);

        /// <summary>The tip of the mark on a row, and the line under a page's title, of what shows as known only by the floor.</summary>
        public static string ByFloor(int floor) => $"Shown by the {Floor(floor)}, not yet come to know by this character";

        /// <summary>The tip of a link or chip on a page going to what shows as known only by the floor: that, above its own tip.</summary>
        public static string FloorTip(int floor, string tip) => Naming.Lines(ByFloor(floor), tip);

        /// <summary>The tip of raising the floor to a tier, with the boss that fell for it where known.</summary>
        public static string RaiseFloor(int to, string boss) =>
            $"Show everything of the {Naming.FieldLabel(Tiers.BiomeOf(to))} as known" + (string.IsNullOrEmpty(boss) ? "" : $": {boss} fell for this character");

        /// <summary>The tip of lowering the floor to a tier, or off.</summary>
        public static string LowerFloor(int to) => to > 0
            ? $"Show the {Naming.FieldLabel(Tiers.BiomeOf(to))} and what came before as known, not the {Naming.FieldLabel(Tiers.BiomeOf(to + 1))}"
            : "Show only what this character has met again";

        /// <summary>The floor's tip while the next boss has not fallen.</summary>
        public static string FloorWaits(string boss) => $"The next tier opens once {boss} falls for this character";

        /// <summary>
        /// How the character came to know a thing, on its page: the game's day, the way, and the
        /// biome it stood in, each where known ("day 12, fighting it in the Black Forest").
        /// </summary>
        public static string CameToKnow(CameBy way, int day, string biome)
        {
            var said = CameWay(way);
            if (!string.IsNullOrEmpty(biome)) said += " in the " + biome;
            return day >= 0 ? $"day {Numbers.Count(day)}, {said}" : said;
        }

        /// <summary>The way a thing came to be known, as the character did it.</summary>
        private static string CameWay(CameBy way)
        {
            switch (way)
            {
                case CameBy.Fought: return "fighting it";
                case CameBy.Killed: return "killing it";
                case CameBy.Tamed: return "taming it";
                case CameBy.Held: return "holding it";
                case CameBy.Made: return "making it";
                case CameBy.Eaten: return "eating it";
                case CameBy.Recipe: return "unlocking its recipe";
                case CameBy.Card: return "reading its card";
                case CameBy.Used: return "using it";
                case CameBy.Built: return "building it";
                case CameBy.Hit: return "hitting it";
                case CameBy.Picked: return "picking it";
                case CameBy.Trophy: return "holding its trophy";
                case CameBy.StoodIn: return "standing in it";
                case CameBy.WentInside: return "going inside";
                case CameBy.LivedThrough: return "living through it";
                case CameBy.Traded: return "trading with it";
                case CameBy.OnYou: return "having it on you";
                case CameBy.Given: return "from what gives it";
                case CameBy.Gives: return "taking what it gives";
                case CameBy.Chosen: return "choosing it at its stone";
                default: return "meeting it";
            }
        }

        /// <summary>The title of the box a page opens with, of what the character itself did with the thing.</summary>
        public const string YouTitle = "You";

        /// <summary>The You box's row of the character's kills of a creature.</summary>
        public const string KilledLabel = "Killed";

        /// <summary>
        /// That box's lines: the game's counts for the character, each where there is one, how
        /// often for kills, foods, picks and pieces, how many for things made and picked up; then
        /// how it came to know it, where told.
        /// </summary>
        public static List<(string Label, string Value)> You(CharacterCounts counts, string came)
        {
            var lines = new List<(string, string)>();
            if (counts != null)
            {
                void Often(string label, int count)
                {
                    if (count > 0) lines.Add((label, count == 1 ? "once" : Naming.Count(count, "time", "times")));
                }
                void Many(string label, int count)
                {
                    if (count > 0) lines.Add((label, Numbers.Count(count)));
                }
                Often(KilledLabel, counts.Killed);
                Often("Built", counts.Built);
                Often("Eaten", counts.Eaten);
                Often("Picked", counts.Picked);
                Many("Made", counts.Made);
                Many("Picked up", counts.PickedUp);
            }
            if (!string.IsNullOrEmpty(came)) lines.Add(("Came to know it", came));
            return lines;
        }

        /// <summary>
        /// How a known thing came to be known, for that box: the way kept in its book, else what
        /// known brought it along, else the floor, else known before the book was kept; none for
        /// one not known.
        /// </summary>
        public static string CameLine(CameBy? way, int day, string biome, string owner, bool byFloor, bool known)
        {
            if (way is CameBy by) return CameToKnow(by, day, biome);
            if (!string.IsNullOrEmpty(owner)) return CameWith(owner);
            if (byFloor) return "shown by the floor";
            return known ? KnownBefore : null;
        }

        /// <summary>A thing known because something known brings it along, as a creature its attacks.</summary>
        public static string CameWith(string owner) => "with " + owner;

        /// <summary>A thing known before its character's book was kept, from what the game kept of it.</summary>
        public const string KnownBefore = "before this book was kept";

        /// <summary>How many damage types a creature's grid keeps back under the strict rules, told faintly under it; none where it keeps none back.</summary>
        public static string NotYetHitWith(int count) => count > 0 ? $"{Numbers.Count(count)} more not yet hit with" : null;

        /// <summary>The faint line under a creature's attacks some of whose damage is kept back, until each hit the character.</summary>
        public static string NotYetTaken(int count) => count > 0 ? $"{Numbers.Count(count)} more not yet taken" : null;

        /// <summary>The tip of an attack's damage kept back.</summary>
        public const string NotYetTakenTip = "Its damage shows once this attack has hit you";

        /// <summary>The faint line where the weak spots a hit has not yet found are left out.</summary>
        public static string WeakSpotsNotFound(int count) =>
            count <= 0 ? null : $"{Numbers.Count(count)} more {(count == 1 ? "weak spot" : "weak spots")} not yet found";

        /// <summary>
        /// What a known page under the strict rules still keeps back, for the You box at its top,
        /// a row for each thing to do and what it shows: the deeds whose numbers it keeps back
        /// (each once), the damage types whose landing it keeps back, the attacks not yet taken,
        /// the weak spots not yet found, and the counts of drops or yields not yet seen. Once the
        /// character played with others, what another player's game may run says that it shows
        /// only where yours runs it (<see cref="ElsewhereWords.Suffix"/>).
        /// </summary>
        public static List<(string Label, string Value)> StillToFindOut(IEnumerable<string> deeds, IReadOnlyList<string> types, int attacks, int spots, IEnumerable<Tally> uncounted, bool withOthers = false)
        {
            var rows = new List<(string, string)>();
            var pending = new HashSet<Deed>();
            foreach (var proof in deeds) if (EntryKeys.IsDeed(proof, out var deed)) pending.Add(deed);
            void Pending(Deed what, string label, string value)
            {
                if (pending.Contains(what)) rows.Add((label, withOthers && OtherGames.MayRun(what) ? value + ElsewhereWords.Suffix : value));
            }
            Pending(Deed.Killed, "Kill one", "shows its health");
            Pending(Deed.Tamed, "Tame one", "shows how long taming and a meal take");
            Pending(Deed.Born, "See one born", "shows how long it is pregnant");
            Pending(Deed.GrewUp, "See one grow up", "shows how long that takes");
            Pending(Deed.GrewBack, "See one grow back", "shows how long that takes");
            Pending(Deed.Broke, "Break one", "shows its health");
            Pending(Deed.StarredFell, "See a starred one fall", "shows what stars change");
            Pending(Deed.Spawned, "See it spawn", "shows its pool and pace");
            Pending(Deed.Collected, "Collect from one", "shows its pace");
            if (types.Count > 0) rows.Add(("Hit it with", Naming.Commas(types) + (types.Count == 1 ? ": how it lands" : ": how they land")));
            if (attacks > 0) rows.Add(("Let it hit you", Naming.Count(attacks, "attack's", "attacks'") + " damage"));
            if (spots > 0) rows.Add((spots == 1 ? "Hit its weak spot" : "Hit its weak spots", Numbers.Count(spots) + " not yet found"));
            var tallies = new HashSet<Tally>(uncounted);
            var elsewhere = withOthers ? ElsewhereWords.Suffix : "";
            if (tallies.Contains(Tally.Fell)) rows.Add(("See it drop", "how many of each" + elsewhere));
            if (tallies.Contains(Tally.Gave)) rows.Add(("Break one", "how many of each it gives" + elsewhere));
            if (tallies.Contains(Tally.Held)) rows.Add(("Open one", "how many of each it holds"));
            if (tallies.Contains(Tally.Weather)) rows.Add(("Spend time in it", "how often each weather comes"));
            return rows;
        }

        /// <summary>What was found out of an entry (<see cref="CompendiumBook.FoundOut"/>), in a few words beside its name in the learned list; none for nothing.</summary>
        public static string FoundOutNote(string proof)
        {
            if (string.IsNullOrEmpty(proof)) return null;
            if (proof == EntryKeys.OnMap) return "seen on your explored map";
            if (EntryKeys.IsDeed(proof, out var deed))
            {
                switch (deed)
                {
                    case Deed.Tamed: return "its taming found out";
                    case Deed.Born: return "its pregnancy found out";
                    case Deed.GrewUp: return "its growing up found out";
                    case Deed.GrewBack: return "its growing back found out";
                    case Deed.StarredFell: return "what stars change found out";
                    case Deed.Spawned: return "its spawning found out";
                    case Deed.Collected: return "its pace found out";
                    default: return "its health found out";
                }
            }
            if (EntryKeys.IsWeakSpot(proof)) return "a weak spot found";
            if (EntryKeys.IsDamage(proof, out var type)) return $"how {type} lands found out";
            return "an attack's damage found out";
        }

        /// <summary>The value of a fact the strict rules keep back (<see cref="EntryKeys.Did"/>): ??? and what shows it (<see cref="ShowsOnce(string, bool)"/>).</summary>
        public static string KeptBack(string proof, bool mayRunElsewhere = false) => Hidden + KeptBackHint(proof, mayRunElsewhere);

        /// <summary>What follows the ??? of a fact kept back, drawn faint: what shows it.</summary>
        public static string KeptBackHint(string proof, bool mayRunElsewhere = false) => " · " + ShowsOnce(proof, mayRunElsewhere);

        /// <summary>
        /// What shows a number kept back, the one form every such hint takes: "shows once you have
        /// ...", and where another player's game may run it, that only where yours does (<see cref="ElsewhereWords.Suffix"/>).
        /// </summary>
        public static string ShowsOnce(string proof, bool mayRunElsewhere = false) => "shows once you have " + Seen(proof) + (mayRunElsewhere ? ElsewhereWords.Suffix : "");

        /// <summary>A faint line saying what shows a line of a table kept back, as a creature's health by its stars.</summary>
        public static string ShowsOnce(string what, string proof, bool mayRunElsewhere = false) => $"{what} {ShowsOnce(proof, mayRunElsewhere)}";

        private static string Seen(string proof)
        {
            EntryKeys.IsDeed(proof, out var deed);
            switch (deed)
            {
                case Deed.Tamed: return "tamed one";
                case Deed.Born: return "seen one born";
                case Deed.GrewUp: return "seen one grow up";
                case Deed.GrewBack: return "seen one grow back";
                case Deed.Broke: return "broken one";
                case Deed.StarredFell: return "seen a starred one fall";
                case Deed.Spawned: return "seen it spawn";
                case Deed.Collected: return "collected from one";
                default: return "killed one";
            }
        }

        /// <summary>The tip of a damage type a creature's grid keeps back.</summary>
        public static string NotYetHitWithTip(string type) => $"{type}: how it lands shows once you hit it with {type.ToLowerInvariant()}";

        /// <summary>
        /// What the strict rules left out of a section of a known page, told in a faint line at
        /// its end by what the section is about (its topic in the page's plan): drops not seen
        /// fall, places a creature lives not met in, givers not seen give it,
        /// anything else not seen together. None where nothing was left out.
        /// </summary>
        public static string LeftOutOf(string topic, int count)
        {
            if (count <= 0) return null;
            var more = Numbers.Count(count) + " more";
            switch (topic)
            {
                case "loot": return more + " not yet seen fall";
                case "lives": return more + (count == 1 ? " place" : " places") + " it lives not yet met in";
                case "from": return more + " not yet seen give it";
                default: return more + " not yet seen together";
            }
        }

        /// <summary>
        /// What the loose rules left out of a section, said at its end: how many more things the
        /// character has not met, a creature's places by what they are; null for none.
        /// </summary>
        public static string NotYetMet(string topic, int count)
        {
            if (count <= 0) return null;
            if (topic == "lives") return LeftOutOf(topic, count);
            return Numbers.Count(count) + " more not yet met";
        }

        /// <summary>A title without the count it ends with, for a row some of whose items were left out: nothing tells how many.</summary>
        public static string Uncounted(string title)
        {
            if (string.IsNullOrEmpty(title) || !title.EndsWith(")", StringComparison.Ordinal)) return title;
            var open = title.LastIndexOf(" (", StringComparison.Ordinal);
            if (open < 0) return title;
            for (var i = open + 2; i < title.Length - 1; i++)
            {
                if (!char.IsDigit(title[i]) && title[i] != ',') return title;
            }
            return title.Substring(0, open);
        }

        /// <summary>A name the strict rules keep back where it stands in a page's text.</summary>
        public const string Hidden = "???";

        /// <summary>The tab after All listing what the character learned lately.</summary>
        public const string LearnedTab = "Learned";

        /// <summary>That tab's tip, by whether the list shows it.</summary>
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

        /// <summary>Said under that on a closed page under the strict rules, where a page known still keeps some back until found out.</summary>
        public const string ClosedStrict = "Some of its page shows only once you find it out.";

        /// <summary>What opens a page of this kind: every way that makes it known, by the one verb for a page.</summary>
        public static string HowToKnow(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return "Fight it, tame it or hold its trophy to come to know it.";
                case Kind.Item: return "Hold it, make it, eat it, unlock its recipe or read its card to come to know it.";
                case Kind.Piece: return "Build it, use it or unlock it to come to know it.";
                case Kind.Resource: return "Hit it, pick it or hold what it gives to come to know it.";
                case Kind.Spawner: return "Hit it to come to know it.";
                case Kind.StatusEffect: return "Have it on you or choose it at its stone to come to know it.";
                case Kind.Biome: return "Set foot in it to come to know it.";
                case Kind.Location: return "Step inside to come to know it.";
                case Kind.Raid: return "Live through it to come to know it.";
                default: return "It comes along once you come to know what it belongs to.";
            }
        }
    }
}
