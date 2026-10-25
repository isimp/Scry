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
                case Kind.Trader: return "Unknown trader";
                case Kind.Item: return "Unknown item";
                case Kind.Piece: return "Unknown piece";
                case Kind.Resource: return "Unknown resource";
                case Kind.Spawner: return "Unknown spawner";
                case Kind.StatusEffect: return "Unknown status effect";
                case Kind.Biome: return "Unknown biome";
                case Kind.Location: return "Unknown location";
                case Kind.Raid: return "Unknown raid";
                default: return "Unknown";
            }
        }

        /// <summary>A kind's tab with the compendium on: how many of it were found, of all there are.</summary>
        public static string Tab(string label, int found, int total, string colour) =>
            $"{label}  <color=#{colour}>{Numbers.Count(found)}/{Numbers.Count(total)}</color>";

        /// <summary>The title of a list of what there is (what a mod adds) with the compendium on: how many of it the character has seen, which are all it shows.</summary>
        public static string Found(string title, int found) => $"{title}, {Numbers.Count(found)} seen";

        /// <summary>What the compendium is called in the title row.</summary>
        public const string ModeCaption = "Compendium";

        /// <summary>The compendium's button in the title row: the rules chosen, and that a box of its settings opens under it.</summary>
        public static string Button(CompendiumRule rule) => ModeCaption + " · " + RuleName(rule) + "  ▾";

        /// <summary>The caption over the compendium's box.</summary>
        public const string BoxCaption = "What your character knows, shown in the catalog";

        /// <summary>The caption of the rules' segments in the compendium's box.</summary>
        public const string RulesCaption = "Rules";

        /// <summary>The mode's tip.</summary>
        public const string ModeTip = "Show only what your character has come to know, by the rules chosen for it here; each character keeps its own";

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
            CompendiumRule.Loose => "only what your character has met shows; a page opens whole once it has fought, held, built, used or entered it",
            CompendiumRule.Strict => "as Loose, but a page shows only what your character saw go together, and keeps back the numbers the game never shows until it finds them out",
            _ => "show everything. What your character comes to know is kept all the same, so turning the compendium on later shows everything met meanwhile",
        };

        /// <summary>The tip of the eye beside the mode, by whether it is peeking and whether a page is shown for it to peek at alone, else at each page.</summary>
        public static string PeekTip(bool peeking, bool pageShown) => peeking
            ? "Show only what your character has met again"
            : pageShown
                ? "Show this page whole for now, its links leading anywhere, until you go to another page; its notice can show every page whole until the panel closes"
                : "Show each page whole for now, its links leading anywhere and a whole name in quotes finding what your character has not met; the list stays as it knows it, and it stops when the panel closes";

        /// <summary>The warning strip while peeking at the page shown alone: in the compact view its name alone, the rest in its tip.</summary>
        public static string PeekingPageLine(string name, bool compact) => compact ? "Peeking at " + name : "Peeking at " + name + ": this page shows whole until you go to another";

        /// <summary>The tip of the strip's line while peeking at one page.</summary>
        public const string PeekingPageTip = "Until you go to another page, this one shows whole and its links lead anywhere; the list stays as your character knows it";

        /// <summary>The strip's chip widening a peek at one page to every page.</summary>
        public const string EveryPage = "Every page";

        /// <summary>That chip's tip.</summary>
        public const string EveryPageTip = "Show every page whole until the panel closes, a whole name in quotes finding what your character has not met";

        /// <summary>The warning strip while peeking, short; the rest in its tip.</summary>
        public const string Peeking = "Peeking: pages show whole until the panel closes";
        public const string PeekingTip = "Each page shows whole, its links lead anywhere and a whole name in quotes finds what your character has not met; the list stays as it knows it";

        /// <summary>The peeking strip's line: in the compact view short enough for its strip not to slide, how long it lasts moved to the tip (<see cref="PeekingLineTip"/>).</summary>
        public static string PeekingLine(bool compact) => compact ? "Peeking: pages show whole" : Peeking;

        /// <summary>The peeking strip's tip: in the compact view saying too how long it lasts, as its line does not.</summary>
        public static string PeekingLineTip(bool compact) => compact
            ? "Until the panel closes, each page shows whole, its links lead anywhere and a whole name in quotes finds what your character has not met; the list stays as it knows it"
            : PeekingTip;

        /// <summary>The strip's chip that stops peeking.</summary>
        public const string StopPeeking = "Stop peeking";

        /// <summary>The tip of the notice's cross.</summary>
        public const string PutNewsAway = "Put this away until more is learned";

        /// <summary>The notice's chip, listing what was learned lately.</summary>
        public const string ShowLearned = "Show what was learned";

        /// <summary>The reply to <c>/scry map</c> in a world.</summary>
        public const string MapAgain = "the explored map will be read again for your character in this world";

        /// <summary>The reply to <c>/scry map</c> outside a world.</summary>
        public const string MapNoWorld = "the explored map can be read only in a world";

        /// <summary>The log's line once the map was read.</summary>
        public static string MapRead(int kinds, int found, bool fromServer) =>
            $"caught up from the explored map{(fromServer ? " with the server's answer" : "")}: {Numbers.Count(kinds)} kinds of place lie in explored ground, {Numbers.Count(found)} of them new to it";

        /// <summary>The server's log line on answering a player's map.</summary>
        public static string MapAnswered(int kinds) => $"answered a player's explored map: {Numbers.Count(kinds)} kinds of place lie in it";

        /// <summary>The server's log line, once a player, for one whose asks about ground came sooner than it answers (<see cref="GroundAskPace"/>).</summary>
        public static string GroundAsksPassedOver(string player) => $"{(string.IsNullOrEmpty(player) ? "a player" : player)}'s game asked about its ground more often than the server answers; what came sooner was passed over";

        /// <summary>The log's line when the server never answered.</summary>
        public static string MapNoServerScry => "the server did not answer on the explored map (it has no Scry, or one too old): places count as the character meets them";

        /// <summary>The line above the Locations tab's list with the compendium on, beside the button reading the explored map again.</summary>
        public const string MapBar = "Places in the ground your character explored show as Ground you explored says, read again each session";

        /// <summary>The button reading the explored map again, in the Locations tab's bar.</summary>
        public const string ReadMap = "Look at the map again";

        /// <summary>That chip's tip.</summary>
        public const string ReadMapTip = "Read again which kinds of place lie in the ground your character explored in this world, as on its first visit with Scry";

        /// <summary>The log's line on a dedicated server.</summary>
        public static string ServerPart => "on a dedicated server Scry only answers players' explored maps, the world's census and their Scry's hello, sends the world's settings for the compendium, keeps how far and on which days they walk its dungeons, reads what was left inside them, and runs the self-test's made-up events for the player asking";

        /// <summary>The caption of the milestone floor in the title row.</summary>
        public const string FloorCaption = "Known up to";

        /// <summary>The milestone floor as stepped: the last tier it shows, by its biome, or none.</summary>
        public static string FloorName(int floor) => floor > 0 ? BiomeWords.Shown(Tiers.BiomeOf(floor)) : "No tier";

        /// <summary>The tip of the mark on a row, and the line under a page's title, of what shows as known only by the floor, named by its control: "Shown as known up to Black forest".</summary>
        public static string ByFloor(int floor) => $"Shown as {FloorCaption.ToLowerInvariant()} the {FloorName(floor)} tier, not yet come to know by your character";

        /// <summary>The tip of a link or chip on a page going to what shows as known only by the floor: that, above its own tip.</summary>
        public static string FloorTip(int floor, string tip) => Naming.Lines(ByFloor(floor), tip);

        /// <summary>The tip of raising the floor to a tier, with the boss that fell for it where known.</summary>
        public static string RaiseFloor(int to, string boss) =>
            $"Show everything of the {BiomeWords.Shown(Tiers.BiomeOf(to))} as known" + (string.IsNullOrEmpty(boss) ? "" : $": {boss} fell for your character");

        /// <summary>The tip of lowering the floor to a tier, or off.</summary>
        public static string LowerFloor(int to) => to > 0
            ? $"Show the {BiomeWords.Shown(Tiers.BiomeOf(to))} and what came before as known, not the {BiomeWords.Shown(Tiers.BiomeOf(to + 1))}"
            : "Show only what your character has met again";

        /// <summary>The floor's tip while the next boss has not fallen.</summary>
        public static string FloorWaits(string boss) => $"The next tier opens once {boss} falls for your character";

        /// <summary>
        /// How the character came to know a thing, on its page: the game's day, the time of day
        /// and the weather, the way, what was in hand where a hand did it (a fight, a kill, a hit),
        /// and the biome it stood in, each where known ("day 12, at night, fighting it with your
        /// Flint spear in the Black Forest").
        /// </summary>
        public static string CameToKnow(CameBy way, int day, string biome, DayPart time = DayPart.None, string weather = null, string inHand = null, string world = null)
        {
            // When, where, then how, as the first sighting reads; the world named where it is not the one played now, as a character's book spans worlds.
            var parts = new List<string>();
            if (day >= 0) parts.Add("day " + Numbers.Count(day) + JournalWords.InWorld(world));
            else if (world != null) parts.Add(JournalWords.InWorld(world).Trim());
            var when = JournalWords.When(time, weather);
            if (when != null) parts.Add(when);
            if (!string.IsNullOrEmpty(biome)) parts.Add("in the " + biome);
            var said = CameWay(way);
            var handed = way == CameBy.Fought || way == CameBy.Killed || way == CameBy.Hit;
            if (handed && !string.IsNullOrEmpty(inHand)) said += " with your " + inHand;
            parts.Add(said);
            return string.Join(", ", parts.ToArray());
        }

        /// <summary>How often, as a count reads: "once", "twice", "14 times".</summary>
        public static string Often(int count) => count == 1 ? "once" : count == 2 ? "twice" : Naming.Count(count, "time", "times");

        /// <summary>The way a thing came to be known, as the character did it.</summary>
        private static string CameWay(CameBy way)
        {
            switch (way)
            {
                case CameBy.Fought: return "fighting it";
                case CameBy.Killed: return "killing it";
                case CameBy.Tamed: return "taming it";
                case CameBy.SeenTamed: return "seeing it tamed";
                case CameBy.Petted: return "petting it";
                case CameBy.Held: return "holding it";
                case CameBy.Made: return "making it";
                case CameBy.Eaten: return "eating it";
                case CameBy.Recipe: return "unlocking its recipe";
                case CameBy.Card: return "reading its card";
                case CameBy.Used: return "using it";
                case CameBy.Near: return "being near it";
                case CameBy.Built: return "building it";
                case CameBy.Hit: return "hitting it";
                case CameBy.Picked: return "picking it";
                case CameBy.Trophy: return "holding its trophy";
                case CameBy.StoodIn: return "stepping into it";
                case CameBy.WentInside: return "stepping inside";
                case CameBy.LivedThrough: return "living through it";
                case CameBy.Traded: return "trading with it";
                case CameBy.OnYou: return "having it on you";
                case CameBy.Given: return "from what gives it";
                case CameBy.Gives: return "taking what it gives";
                case CameBy.Chosen: return "choosing it at its stone";
                case CameBy.Born: return "seeing it born";
                case CameBy.GrewUp: return "seeing it grow up";
                default: return "meeting it";
            }
        }

        /// <summary>The title of the box a page opens with, of what the character itself did with the thing.</summary>
        public const string YouTitle = "You";

        /// <summary>The You box's row of the character's kills of a creature.</summary>
        public const string KilledLabel = "Killed";

        /// <summary>
        /// That box's lines: the game's counts for the character, each where there is one, kills
        /// by how they were dealt (<see cref="JournalWords.Kills"/>), how often a creature killed
        /// the character, how often for foods, picks and pieces, how many for things made and
        /// picked up; then when it was first seen and how it came to know it, where told.
        /// </summary>
        public static List<(string Label, string Value)> You(CharacterCounts counts, string came, string firstSeen = null, string killedYou = null)
        {
            var lines = new List<(string, string)>();
            if (counts != null && counts.Killed > 0) lines.Add((KilledLabel, JournalWords.Kills(counts.Killed, counts.Melee, counts.Ranged, counts.Magic, counts.Unarmed)));
            if (!string.IsNullOrEmpty(killedYou)) lines.Add(("Killed by it", killedYou));
            if (counts != null)
            {
                void Times(string label, int count)
                {
                    if (count > 0) lines.Add((label, Often(count)));
                }
                void Many(string label, int count)
                {
                    if (count > 0) lines.Add((label, Numbers.Count(count)));
                }
                Times("Built", counts.Built);
                Times("Eaten", counts.Eaten);
                Times("Picked", counts.Picked);
                Many("Made", counts.Made);
                Many("Picked up", counts.PickedUp);
            }
            if (!string.IsNullOrEmpty(firstSeen)) lines.Add((FirstSeenLabel, firstSeen));
            if (!string.IsNullOrEmpty(came)) lines.Add((CameLabel, came));
            return lines;
        }

        /// <summary>The You box's line telling when the thing was first seen.</summary>
        public const string FirstSeenLabel = "First seen";

        /// <summary>The You box's line telling how the thing came to be known.</summary>
        public const string CameLabel = "Came to know it";

        /// <summary>
        /// How a known thing came to be known, for that box: the way kept in its book, else what
        /// known brought it along, else the floor's tier that shows it (none at 0), else known
        /// before the book was kept; none for one not known.
        /// </summary>
        public static string CameLine(CameBy? way, int day, string biome, string owner, int floor, bool known, DayPart time = DayPart.None, string weather = null, string inHand = null,
            WorldSource? byWorld = null, string world = null)
        {
            if (way is CameBy by) return CameToKnow(by, day, biome, time, weather, inHand, world);
            if (!string.IsNullOrEmpty(owner)) return CameWith(owner);
            if (floor > 0) return $"shown as {FloorCaption.ToLowerInvariant()} the {FloorName(floor)} tier";
            if (byWorld is WorldSource source) return "shown as known from " + SourcePhrase(source);
            return known ? KnownBefore : null;
        }

        /// <summary>The box's heading over the world's sources (<see cref="WorldSource"/>).</summary>
        public const string SourcesCaption = "What this world shows as known";

        /// <summary>The faint line under the sources, saying what turning one down does.</summary>
        public const string SourcesHint = "What these show is marked with this on its row and page; your book keeps only what you met, so turning one down takes it all back.";

        /// <summary>A source's name in the box.</summary>
        public static string SourceName(WorldSource source)
        {
            switch (source)
            {
                case WorldSource.Explored: return "Ground you explored";
                case WorldSource.SharedMaps: return "Shared maps";
                case WorldSource.Marked: return "Places marked for you";
                case WorldSource.OnEveryMap: return "Places on every map";
                default: return "The whole world";
            }
        }

        /// <summary>The title row's chip of a page under the strict rules: how much of what it keeps back was found out, short where the row is narrow.</summary>
        public static string FoundOutCount(int found, int of, bool narrow) => Numbers.Count(found) + " of " + Numbers.Count(of) + (narrow ? "" : " found out");

        /// <summary>The chip of a page under the strict rules whose every number kept back was found out.</summary>
        public const string AllFoundOut = "All found out";

        /// <summary>The chip's tip of a page found out whole.</summary>
        public const string AllFoundOutTip = "Everything this page keeps back under Strict was found out";

        /// <summary>That chip's tip: what was found out, or that the page keeps nothing back.</summary>
        public static string AllFoundOutTipFor(PageFinds finds) => finds.Any ? AllFoundOutTip : "Nothing of this page is kept back under Strict";

        /// <summary>The chip of a page the strict rules would keep back from, shown whole by the floor (none) or a source of the world.</summary>
        public static string WholeBy(WorldSource? source) => source is WorldSource by ? "Shown whole from " + SourcePhrase(by) : "Shown whole by " + FloorCaption;

        /// <summary>What shows a page whole, and how to see what is still to find out, on the chip's hover.</summary>
        public static string WholeTip(WorldSource? source) => source is WorldSource by
            ? SourceName(by) + " shows this page whole, as the compendium's box has it show; turn it down there to see what is still to find out"
            : FloorCaption + " shows this page whole, as it shows every page of the tiers whose bosses your character has beaten; lower it in the compendium's box to see what is still to find out";

        /// <summary>The chip's tip of a page with something still to find out: each thing to do and what it shows, as its You box lists them.</summary>
        public static string FindsTip(System.Collections.Generic.IEnumerable<(string Label, string Value)> still)
        {
            var lines = new System.Collections.Generic.List<string> { "Still to find out:" };
            var todo = false;
            foreach (var (label, value) in still)
            {
                lines.Add(label + " \u00b7 " + value);
                todo = true;
            }
            if (todo) lines.Add("A click goes to them in its You box");
            return string.Join("\n", lines);
        }

        /// <summary>A source as a sentence names it: "from your explored ground".</summary>
        private static string SourcePhrase(WorldSource source)
        {
            switch (source)
            {
                case WorldSource.Explored: return "your explored ground";
                case WorldSource.SharedMaps: return "shared maps";
                case WorldSource.Marked: return "the places marked for you";
                case WorldSource.OnEveryMap: return "the places every map shows";
                default: return "this world's census";
            }
        }

        /// <summary>What a source's places are, on its name's hover.</summary>
        public static string SourceTip(WorldSource source)
        {
            switch (source)
            {
                case WorldSource.Explored: return "The places lying in ground you explored yourself, and the biomes under it, by your map, which reaches 100 m around you whatever you saw. Seen as you explore; the places known, with their insides, or all off, as you choose.";
                case WorldSource.SharedMaps: return "The places lying in ground from maps you read at a cartography table.";
                case WorldSource.Marked: return "The places a Vegvisir or a rune stone marked on your map, though you never looked at them. Seen as they are marked; known, with their insides, or off, as you choose.";
                case WorldSource.OnEveryMap: return "The places the game shows on every map: traders, the start, and the places Hildir sends you to.";
                default: return "Every kind of place this world holds, by the server's census of it: how many of each there are, never where.";
            }
        }

        /// <summary>The pin at the end of a ground's row, as its tip is headed.</summary>
        public const string PinName = "Mark dungeons on the big map";

        /// <summary>What the pin does.</summary>
        private const string PinDoes = "Marks every dungeon entrance in this ground on Valheim's big map. With insides, each one walked on this server shows what was walked. A click opens it in Scry. Dungeons you walked are marked anyway.";

        /// <summary>The pin's tip: what it does, then whether it is on.</summary>
        public static string PinTip(bool on) => Naming.Lines(PinDoes, on ? "On." : "Off.");

        /// <summary>The caption of the box's row turning every mark of Scry's on the big map on or off.</summary>
        public const string MarksCaption = "Marks on the map";

        /// <summary>The row's tip: what it does, then whether it is on.</summary>
        public static string MarksTip(bool on) => Naming.Lines(
            "Whether Valheim's big map shows Scry's marks at all: those the pins ask for and those of the dungeons you walked, for every character",
            on ? "On." : "Off.");

        /// <summary>The tip of With insides dimmed on a row none of whose places found in this world has insides: what would have.</summary>
        public static string NoInsides(WorldSource source)
        {
            switch (source)
            {
                case WorldSource.Marked: return "None of the places marked for you in this world has insides yet. The Queen's lair and Hildir's crypt, cave and tower would.";
                case WorldSource.OnEveryMap: return "None of the places every map shows in this world has insides yet. Hildir's crypt, cave and tower show once she sends you to them.";
                default: return "None of the places in this ground has insides yet.";
            }
        }

        /// <summary>A level's name on its segment.</summary>
        public static string LevelName(SourceLevel level)
        {
            switch (level)
            {
                case SourceLevel.Off: return "Off";
                case SourceLevel.Counted: return "Counted";
                case SourceLevel.Seen: return "Seen";
                case SourceLevel.Known: return "Known";
                default: return "With insides";
            }
        }

        /// <summary>What a level shows of a source's places, on its segment's hover.</summary>
        public static string LevelTip(SourceLevel level)
        {
            switch (level)
            {
                case SourceLevel.Off: return "Shows nothing of them";
                case SourceLevel.Counted: return "Every kind of place this world holds is heard of, with how many there are; none is seen until you find it.";
                case SourceLevel.Seen: return "Listed, their pages closed";
                case SourceLevel.Known: return "Their pages open, with what a known place brings along";
                default: return "Known, with what anyone on this server walked inside: those rooms and what they hold. A dungeon nobody walked shows only its entrance.";
            }
        }

        /// <summary>The tip on what a source shows: from where, and as what (<see cref="CompendiumView.ByWorld"/>).</summary>
        public static string ByWorld(WorldSource source, KnownLevel shown) =>
            shown < KnownLevel.Seen ? $"Heard of from {SourcePhrase(source)}, not yet met by your character"
            : $"Shown as {(shown == KnownLevel.Known ? "known" : "seen")} from {SourcePhrase(source)}, not yet met by your character";

        /// <summary>A tip on a chip or link to what a source shows, its own tip under it.</summary>
        public static string WorldTip(WorldSource source, KnownLevel shown, string tip) => Naming.Lines(ByWorld(source, shown), tip);

        /// <summary>A thing known because something known brings it along, as a creature its attacks.</summary>
        public static string CameWith(string owner) => "along with " + owner;

        /// <summary>A thing known before its character's book was kept, from what the game kept of it.</summary>
        public const string KnownBefore = "known before Scry kept track of your character";

        /// <summary>The tip of an attack's damage kept back.</summary>
        public const string NotYetTakenTip = "Its damage shows once this attack hit you";

        /// <summary>
        /// What a known page under the strict rules still keeps back, for the You box at its top,
        /// a row for each thing to do and what it shows: the deeds whose numbers it keeps back
        /// (each once), the damage types whose landing it keeps back, the attacks not yet taken,
        /// the weak spots not yet found, the counts of drops or yields not yet seen, and, for the
        /// stage, the star levels and the gear not yet seen up close.
        /// </summary>
        public static List<(string Label, string Value)> StillToFindOut(IEnumerable<string> deeds, IReadOnlyList<string> types, int attacks, int spots, IEnumerable<Tally> uncounted,
            IEnumerable<int> starsUnseen = null, IEnumerable<string> gearUnseen = null, bool ofItem = false)
        {
            var rows = new List<(string, string)>();
            var pending = new HashSet<Deed>();
            var starsPending = new SortedSet<int>();
            foreach (var proof in deeds)
            {
                if (EntryKeys.IsDeed(proof, out var deed)) pending.Add(deed);
                else if (EntryKeys.IsStarsFell(proof, out var fell)) starsPending.Add(fell);
            }
            foreach (var fell in starsPending) rows.Add(($"See {Naming.Count(fell, "star", "stars")} fall", $"shows what {StarsChange(fell)}"));
            void Pending(Deed what, string label, string value)
            {
                if (pending.Contains(what)) rows.Add((label, value));
            }
            Pending(Deed.Killed, pending.Contains(Deed.Ridden) ? "Kill or ride one" : "Kill one", "shows its health");
            Pending(Deed.Tamed, "Tame one", "shows how long taming and a meal take");
            Pending(Deed.Born, "See one born", "shows how long it is pregnant");
            Pending(Deed.GrewUp, "See one grow up", "shows how long that takes");
            Pending(Deed.Hatched, "See one hatch", "shows how long that takes");
            Pending(Deed.Ridden, "Ride one", "shows its stamina and speed when ridden");
            Pending(Deed.GrewBack, "See one grow back", "shows how long that takes");
            Pending(Deed.Broke, "Break one", "shows its health");
            Pending(Deed.StarredFell, "See a starred one fall", "shows what stars change");
            Pending(Deed.Spawned, "See it spawn", "shows its pool and pace");
            Pending(Deed.Collected, "Collect from one", "shows its pace");
            Pending(Deed.Grew, "See one grow", "shows how long that takes");
            Pending(Deed.Finished, "See one finish", "shows how long each takes and what it burns");
            if (types.Count > 0) rows.Add(("Hit it with", Naming.Commas(types) + (types.Count == 1 ? ": how it lands" : ": how they land")));
            if (attacks > 0) rows.Add(("See it attack", Naming.Count(attacks, "attack's", "attacks'") + " damage, up close or taken"));
            if (spots > 0) rows.Add((spots == 1 ? "Hit its weak spot" : "Hit its weak spots", Numbers.Count(spots) + " not yet found"));
            var tallies = new HashSet<Tally>(uncounted);
            // On an item's page the counts are its givers': the rows name them, not the item.
            if (tallies.Contains(Tally.Fell)) rows.Add(ofItem ? ("See it fall", "from what drops it: how many") : ("See it drop", "how many of each"));
            if (tallies.Contains(Tally.Gave)) rows.Add(ofItem ? ("See it given", "by what gives it: how many") : ("See it give", "how many of each"));
            if (tallies.Contains(Tally.Held)) rows.Add(ofItem ? ("Find it in a chest", "how many it holds") : ("Open one", "how many of each it holds"));
            if (tallies.Contains(Tally.Weather)) rows.Add(("Spend time in it", "how often each weather comes"));
            foreach (var stars in starsUnseen ?? Array.Empty<int>()) rows.Add(($"See {Naming.Count(stars, "star", "stars")} up close", "shows it so on the stage"));
            foreach (var gear in gearUnseen ?? Array.Empty<string>()) rows.Add(("See it carry", $"{gear} up close: dresses it so on the stage"));
            return rows;
        }

        /// <summary>What was found out of an entry (<see cref="CompendiumBook.FoundOut"/>), in a few words beside its name in the learned list; none for nothing.</summary>
        public static string FoundOutNote(string proof)
        {
            if (string.IsNullOrEmpty(proof)) return null;
            if (proof == EntryKeys.OnMap) return "seen on your explored map";
            if (EntryKeys.IsStarsFell(proof, out var fell)) return $"what {StarsChange(fell)} found out";
            if (EntryKeys.IsDeed(proof, out var deed))
            {
                switch (deed)
                {
                    case Deed.Tamed: return "its taming found out";
                    case Deed.Born: return "its pregnancy found out";
                    case Deed.Hatched: return "its hatching found out";
                    case Deed.Ridden: return "its riding found out";
                    case Deed.GrewUp: return "its growing up found out";
                    case Deed.GrewBack: return "its growing back found out";
                    case Deed.StarredFell: return "what stars change found out";
                    case Deed.Spawned: return "its spawning found out";
                    case Deed.Collected: return "its pace found out";
                    case Deed.Grew: return "its growing found out";
                    case Deed.Finished: return "its working found out";
                    default: return "its health found out";
                }
            }
            if (EntryKeys.IsWeakSpot(proof)) return "a weak spot found out";
            if (EntryKeys.IsDamage(proof, out var type)) return $"how {type} lands found out";
            return "an attack's damage found out";
        }

        /// <summary>What so many stars change, as a sentence has it: "1 star changes", "2 stars change".</summary>
        public static string StarsChange(int stars) => Naming.Count(stars, "star", "stars") + (stars == 1 ? " changes" : " change");

        /// <summary>What shows a number kept back, the one form every such hint takes: "shows once you have ...".</summary>
        public static string ShowsOnce(string proof) => "shows once you have " + Seen(proof);

        /// <summary>A faint line saying what shows a line of a table kept back, as a creature's health by its stars.</summary>
        public static string ShowsOnce(string what, string proof) => $"{what} {ShowsOnce(proof)}";

        /// <summary>As <see cref="ShowsOnce(string, string)"/>, a creature that can be ridden showing its health once killed or ridden, as the game prints the health of the one ridden.</summary>
        public static string ShowsOnce(string what, string proof, bool rideable) =>
            rideable && proof == EntryKeys.Did(Deed.Killed) ? $"{what} shows once you have killed or ridden one" : ShowsOnce(what, proof);

        /// <summary>The hover of a name kept back in a table's cell or a chain's chip under the strict rules.</summary>
        public const string NotSeenTogetherTip = "Named once you see it together with this page";

        /// <summary>The hover of a section's line counting what the rules left out: what names them here.</summary>
        public static string LeftOutTip(bool strict) =>
            strict ? "Named here once you see each together with this page; met elsewhere, it stays left out here" : "Named here once you meet each";

        /// <summary>The faint line under a section whose recipe rows wait for the game to list them for the character.</summary>
        public static string RecipeRowsWait(int rows) => rows == 1 ? "1 recipe row waits for the game to list it" : $"{Numbers.Count(rows)} recipe rows wait for the game to list them";

        private static string Seen(string proof)
        {
            if (EntryKeys.IsStarsFell(proof, out var fell)) return $"seen one with {Naming.Count(fell, "star", "stars")} fall";
            EntryKeys.IsDeed(proof, out var deed);
            switch (deed)
            {
                case Deed.Tamed: return "tamed one";
                case Deed.Born: return "seen one born";
                case Deed.Hatched: return "seen one hatch";
                case Deed.Ridden: return "ridden one";
                case Deed.GrewUp: return "seen one grow up";
                case Deed.GrewBack: return "seen one grow back";
                case Deed.Broke: return "broken one";
                case Deed.StarredFell: return "seen a starred one fall";
                case Deed.Spawned: return "seen it spawn";
                case Deed.Collected: return "collected from one";
                case Deed.Grew: return "seen one grow";
                case Deed.Finished: return "seen one finish";
                default: return "killed one";
            }
        }

        /// <summary>The tip of a damage type a creature's grid keeps back.</summary>
        public static string NotYetHitWithTip(string type) => $"{type}: how it lands shows once you hit it with {type.ToLowerInvariant()}";

        /// <summary>
        /// What the strict rules left out of a section of a known page, told in a faint line at
        /// its end by what the section is about (its topic in the page's plan): drops not seen
        /// fall, places a creature lives not met in, givers not seen give it,
        /// anything else not seen together; without "more" where nothing else is listed
        /// (<paramref name="alone"/>). None where nothing was left out.
        /// </summary>
        public static string LeftOutOf(string topic, int count, bool alone = false)
        {
            if (count <= 0) return null;
            var more = Numbers.Count(count) + (alone ? "" : " more");
            switch (topic)
            {
                case "loot": return more + " not yet seen fall";
                case "lives": return more + (count == 1 ? " place" : " places") + " it lives not yet met in";
                case "from": return more + " not yet seen give it";
                case "room": return more + " not yet seen here";
                default: return more + " not yet seen together";
            }
        }

        /// <summary>The line at the end of a place's runestone texts with the compendium on, counting those the character has not read: "2 more not yet read"; null for none.</summary>
        public static string NotYetRead(int count) => count > 0 ? $"{Numbers.Count(count)} more not yet read" : null;

        /// <summary>A headline tile's count with the compendium on: how many of the row's the page names, "3 met".</summary>
        public static string MetCount(int count) => $"{Numbers.Count(count)} met";

        /// <summary>The faint line under a row's title whose wares wait for a boss not yet fallen, under the loose rules: "2 more to unlock"; null for none.</summary>
        public static string ToUnlock(int count) => count > 0 ? $"{Numbers.Count(count)} more to unlock" : null;

        /// <summary>
        /// What the loose rules left out of a section, said at its end: how many more things the
        /// character has not met, a creature's places by what they are; without "more" where
        /// nothing else is listed (<paramref name="alone"/>); null for none.
        /// </summary>
        public static string NotYetMet(string topic, int count, bool alone = false)
        {
            if (count <= 0) return null;
            if (topic == "lives") return LeftOutOf(topic, count, alone);
            return Numbers.Count(count) + (alone ? "" : " more") + " not yet met";
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
        public const string Hidden = "\u2013\u2013";

        /// <summary>Why a kept file no longer stands in place: it could not be read, so it was set aside under its new name and a new one started.</summary>
        public static string SetAside(string file) => $"it could not be read, so it was set aside as {file} and a new one started, which saves";

        /// <summary>The notice of a lasting file that could not be read: what it held and what it was set aside as.</summary>
        public static string SetAsideNotice(SetAsideKind kind, string file)
        {
            switch (kind)
            {
                case SetAsideKind.Compendium: return $"This character's compendium could not be read. It was set aside as {file} and a new one started.";
                case SetAsideKind.Walks: return $"The maps of the dungeons your character walked could not be read. They were set aside as {file} and new ones started.";
                default: return $"This world's dungeon records could not be read. They were set aside as {file} and new ones started.";
            }
        }

        /// <summary>The notice's chip copying where the file set aside lies, and its tip.</summary>
        public const string CopyPath = "Copy path";
        public const string CopyPathTip = "Copy where the file set aside lies, to look at it or put it back with the game closed";

        /// <summary>The tip of the notice's cross.</summary>
        public const string PutSetAsideAway = "Put this away; the file set aside stays where it is";

        /// <summary>The tab after All listing what the character learned lately.</summary>
        public const string LearnedTab = "Learned";

        /// <summary>That tab's tip, by whether the list shows it.</summary>
        public static string LearnedTip(bool on) => (on ? "Showing" : "Show") + " what your character learned lately, newest first";

        /// <summary>A list row's note of an entry only seen, its page closed.</summary>
        public const string SeenRowTag = "seen";

        /// <summary>A list row's note of an entry only seen, before what else the row tells, where it tells anything: "seen · 2 of 3".</summary>
        public static string SeenRowNote(string note) => string.IsNullOrEmpty(note) ? SeenRowTag : SeenRowTag + " · " + note;

        /// <summary>The lines of a page only seen telling what sight gave (<see cref="Explorer.SeenLines"/>).</summary>
        public const string SeenWithLabel = "Seen with";
        public const string SeenCarryingLabel = "Seen carrying";
        public const string SeenInLabel = "Seen in";

        /// <summary>The tip of a star level the stage does not offer under the strict rules: what shows it.</summary>
        public static string StarsNotSeenTip(int stars) => $"Shows once you have seen one with {Naming.Count(stars, "star", "stars")} up close";

        /// <summary>The tip of a gear look the stage does not offer under the strict rules: what shows it.</summary>
        public const string LookNotSeenTip = "Shows once you have seen one carrying each of its items up close";

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
        public const string ClosedStrict = "Under Strict, numbers and connections show as you see them.";

        /// <summary>What opens a page of this kind: every way that makes it known, by the one verb for a page.</summary>
        public static string HowToKnow(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return "Fight it, see it tamed, pet it or hold its trophy to come to know it.";
                case Kind.Trader: return "Open its store to come to know it.";
                case Kind.Item: return "Hold it, make it, eat it, unlock its recipe or read its card to come to know it.";
                case Kind.Piece: return "Build it, use it, stand near it or unlock it to come to know it.";
                case Kind.Resource: return "Hit it, pick it or hold what it gives to come to know it.";
                case Kind.Spawner: return "Hit it to come to know it.";
                case Kind.StatusEffect: return "Have it on you or choose it at its stone to come to know it.";
                case Kind.Biome: return "Step into it to come to know it.";
                case Kind.Location: return "Step inside to come to know it.";
                case Kind.Raid: return "Live through it to come to know it.";
                default: return "It comes along once you come to know what it belongs to.";
            }
        }
    }
}
