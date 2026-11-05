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
        public static string Button(CompendiumRule rule)
        {
            if (Buttons.TryGetValue(rule, out var button)) return button;
            return Buttons[rule] = ModeCaption + " · " + RuleName(rule) + "  ▾";
        }

        /// <summary>The compendium's button by rule, made once: the header draws it every frame.</summary>
        private static readonly Dictionary<CompendiumRule, string> Buttons = new Dictionary<CompendiumRule, string>();

        /// <summary>The caption over the compendium's box.</summary>
        public const string BoxCaption = "What your character knows, shown in the catalog";

        /// <summary>The caption of the rules' segments in the compendium's box.</summary>
        public const string RulesCaption = "Rules";

        /// <summary>The mode's tip.</summary>
        public const string ModeTip = "Show only what your character has come to know, by the rules chosen for it here; each character keeps its own. Click to choose";

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
                ? "Show this page whole until you go to another page; its links open any page. Every page, on the strip that then shows, does the same for all pages until you close the panel"
                : "Show each page whole until you close the panel. Its links open any page, and a full name in quotes in the search finds things your character has not met. The list itself stays as your character knows it";

        /// <summary>The warning strip while peeking at the page shown alone: in the compact view its name alone, the rest in its tip.</summary>
        public static string PeekingPageLine(string name, bool compact) => compact ? "Peeking at " + name : "Peeking at " + name + ": this page shows whole until you go to another";

        /// <summary>The tip of the strip's line while peeking at one page.</summary>
        public const string PeekingPageTip = "Until you go to another page, this one shows whole and its links open any page. The list itself stays as your character knows it";

        /// <summary>The strip's chip widening a peek at one page to every page.</summary>
        public const string EveryPage = "Every page";

        /// <summary>That chip's tip.</summary>
        public const string EveryPageTip = "Show every page whole until you close the panel. A full name in quotes in the search finds things your character has not met";

        /// <summary>The warning strip while peeking, short; the rest in its tip.</summary>
        public const string Peeking = "Peeking: pages show whole until the panel closes";
        public const string PeekingTip = "Each page shows whole and its links open any page. A full name in quotes in the search finds things your character has not met. The list itself stays as your character knows it";

        /// <summary>The peeking strip's line: in the compact view short enough for its strip not to slide, how long it lasts moved to the tip (<see cref="PeekingLineTip"/>).</summary>
        public static string PeekingLine(bool compact) => compact ? "Peeking: pages show whole" : Peeking;

        /// <summary>The peeking strip's tip: in the compact view saying too how long it lasts, as its line does not.</summary>
        public static string PeekingLineTip(bool compact) => compact
            ? "Until you close the panel, each page shows whole and its links open any page. A full name in quotes in the search finds things your character has not met. The list itself stays as your character knows it"
            : PeekingTip;

        /// <summary>The strip's chip that stops peeking.</summary>
        public const string StopPeeking = "Stop peeking";

        /// <summary>The tip of the notice's cross.</summary>
        public const string PutNewsAway = "Put this away until more is learned";

        /// <summary>The notice's chip, listing what was learned lately.</summary>
        public const string ShowLearned = "Show what was learned";

        /// <summary>The reply to <c>/scry map</c> in a world.</summary>
        public const string MapAgain = "the map of ground you explored will be read again for your character in this world";

        /// <summary>The reply to <c>/scry map</c> outside a world.</summary>
        public const string MapNoWorld = "the map of ground you explored can be read only in a world";

        /// <summary>The log's line once the map was read.</summary>
        public static string MapRead(int kinds, int found, bool fromServer) =>
            $"caught up from the map of ground you explored{(fromServer ? " with the server's answer" : "")}: {Naming.Count(kinds, "kind of place lies", "kinds of place lie")} in it, {Numbers.Count(found)} of them new to the book";

        /// <summary>The server's log line on answering a player's map.</summary>
        public static string MapAnswered(int kinds) => $"answered a player's map of the ground they explored: {Naming.Count(kinds, "kind of place lies", "kinds of place lie")} in it";

        /// <summary>The server's log line, once a player, for one whose asks about ground came sooner than it answers (<see cref="GroundAskPace"/>).</summary>
        public static string GroundAsksPassedOver(string player) => $"{(string.IsNullOrEmpty(player) ? "a player" : player)}'s game asked about its ground more often than the server answers; what came sooner was passed over";

        /// <summary>The log's line when the server never answered.</summary>
        public static string MapNoServerScry => "the server did not answer on the map of ground you explored (it has no Scry, or one too old): places count as the character meets them";

        /// <summary>The line above the Locations tab's list with the compendium on, beside the button reading the explored map again.</summary>
        public const string MapBar = "Places in the ground your character explored show as the Ground you explored setting says. Scry reads the map again each session";

        /// <summary>The button reading the explored map again, in the Locations tab's bar.</summary>
        public const string ReadMap = "Read the map again";

        /// <summary>That chip's tip.</summary>
        public const string ReadMapTip = "Check again which kinds of place lie in the ground your character explored in this world. Scry also does this each session and as you explore";

        /// <summary>The log's line on a dedicated server.</summary>
        public static string ServerPart => "on a dedicated server Scry only answers players' maps of the ground they explored, the world's count of places and their Scry's hello, sends the world's settings for the compendium, keeps how far and on which days they walk its dungeons, reads what was left inside them, and runs the self-test's made-up events for the player asking";

        /// <summary>The caption of the milestone floor in the title row.</summary>
        public const string FloorCaption = "Known up to";

        /// <summary>The milestone floor as stepped: the last tier it shows, by its biome, or none.</summary>
        public static string FloorName(int floor) => floor > 0 ? BiomeWords.Shown(Tiers.BiomeOf(floor)) : "No tier";

        /// <summary>The tip of the mark on a row, and the line under a page's title, of what shows as known only by the floor, named by its control: "Shown as known because Known up to reaches the Black forest tier".</summary>
        public static string ByFloor(int floor) => $"Shown as known because {FloorCaption} reaches the {FloorName(floor)} tier; your character does not know it yet";

        /// <summary>The tip of a link or chip on a page going to what shows as known only by the floor: that, above its own tip.</summary>
        public static string FloorTip(int floor, string tip) => Naming.Lines(ByFloor(floor), tip);

        /// <summary>The tip of raising the floor to a tier, with the boss that fell for it where known.</summary>
        public static string RaiseFloor(int to, string boss) =>
            $"Show everything of the {BiomeWords.Shown(Tiers.BiomeOf(to))} as known" + (string.IsNullOrEmpty(boss) ? "" : $": your character defeated {boss}");

        /// <summary>The tip of lowering the floor to a tier, or off.</summary>
        public static string LowerFloor(int to) => to > 0
            ? $"Show the {BiomeWords.Shown(Tiers.BiomeOf(to))} and what came before as known, not the {BiomeWords.Shown(Tiers.BiomeOf(to + 1))}"
            : "Show only what your character has met again";

        /// <summary>The floor's tip while the next boss has not fallen.</summary>
        public static string FloorWaits(string boss) => $"The next tier opens once your character defeats {boss}";

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
            var handed = way == CameBy.Fought || way == CameBy.Killed || way == CameBy.Hit || way == CameBy.MinedPieces;
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
                case CameBy.Given: return "from something that gives it";
                case CameBy.Gives: return "taking the things it gives";
                case CameBy.Chosen: return "choosing it at its stone";
                case CameBy.Born: return "seeing it born";
                case CameBy.GrewUp: return "seeing it grow up";
                case CameBy.MinedPieces: return "mining its pieces";
                case CameBy.Tapped: return "tapping it";
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
        public const string SourcesHint = "Things these show are marked like this on their row and page. Your book keeps only what your character met, so turning a source down takes back all it showed.";

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
        public const string AllFoundOutTip = "Your character found out everything this page keeps back under Strict";

        /// <summary>That chip's tip: what was found out, or that the page keeps nothing back.</summary>
        public static string AllFoundOutTipFor(PageFinds finds) => finds.Any ? AllFoundOutTip : "Nothing of this page is kept back under Strict";

        /// <summary>The chip of a page the strict rules would keep back from, shown whole by the floor (none) or a source of the world.</summary>
        public static string WholeBy(WorldSource? source) => source is WorldSource by ? "Shown whole from " + SourcePhrase(by) : "Shown whole by " + FloorCaption;

        /// <summary>What shows a page whole, and how to see what is still to find out, on the chip's hover.</summary>
        public static string WholeTip(WorldSource? source) => source is WorldSource by
            ? SourceName(by) + " shows this page whole, as the compendium's box has it show; turn it down there to see what is still to find out"
            : FloorCaption + " shows this page whole, as it shows every page of the tiers whose bosses your character has beaten; lower it in the compendium's box to see what is still to find out";

        /// <summary>The title row's chip of a page under the loose rules: how much of what it names was met, short where the row is narrow.</summary>
        public static string MetOf(int met, int of, bool narrow) => Numbers.Count(met) + " of " + Numbers.Count(of) + (narrow ? "" : " met");

        /// <summary>The chip of a page under the loose rules everything it names was met of.</summary>
        public const string AllMet = "All met";

        /// <summary>That chip's tip.</summary>
        public const string AllMetTip = "Your character has met everything this page names";

        /// <summary>
        /// The chip's tip of a page with something still to find out, or under the loose rules
        /// (<paramref name="met"/>) still to meet: each thing to do and what it shows, as its You box lists them.
        /// </summary>
        public static string FindsTip(System.Collections.Generic.IEnumerable<(string Label, string Value)> still, bool met = false)
        {
            var lines = new System.Collections.Generic.List<string> { met ? "Still to meet:" : "Still to find out:" };
            var todo = false;
            foreach (var (label, value) in still)
            {
                lines.Add(label + " \u00b7 " + value);
                todo = true;
            }
            if (todo) lines.Add("Click to see them in this page's You box");
            return string.Join("\n", lines);
        }

        /// <summary>A source as a sentence names it: "from your explored ground".</summary>
        private static string SourcePhrase(WorldSource source)
        {
            switch (source)
            {
                case WorldSource.Explored: return "ground you explored";
                case WorldSource.SharedMaps: return "shared maps";
                case WorldSource.Marked: return "the places marked for you";
                case WorldSource.OnEveryMap: return "the places every map shows";
                default: return "the world's count of places";
            }
        }

        /// <summary>What a source's places are, on its name's hover.</summary>
        public static string SourceTip(WorldSource source)
        {
            switch (source)
            {
                case WorldSource.Explored: return "The places in ground you explored yourself, and the biomes there, as your map shows it: it reveals 100 m around you whether or not you looked. Seen as you explore; set this row to Known, With insides or Off as you like.";
                case WorldSource.SharedMaps: return "The places lying in ground from maps you read at a cartography table.";
                case WorldSource.Marked: return "The places a Vegvisir or a rune stone marked on your map, though you never looked at them. Seen as they are marked; set this row to Known, With insides or Off as you like.";
                case WorldSource.OnEveryMap: return "The places the game shows on every map: traders, the start, and the places Hildir sends you to.";
                default: return "Every kind of place this world holds, as the server counts them: how many of each there are, never where.";
            }
        }

        /// <summary>The tip of With insides dimmed on a row none of whose places found in this world has insides: what would have.</summary>
        public static string NoInsides(WorldSource source)
        {
            switch (source)
            {
                case WorldSource.Marked: return "None of the places marked for you in this world has rooms to show yet. The Queen's lair and Hildir's crypt, cave and tower would, once marked.";
                case WorldSource.OnEveryMap: return "None of the places every map shows in this world has rooms to show yet. Hildir's crypt, cave and tower will, once she sends you to them.";
                default: return "None of the places in this ground has rooms to show yet.";
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
                case SourceLevel.Counted: return "Counts how many places of each kind this world holds; none shows as seen until you find it.";
                case SourceLevel.Seen: return "Lists them, their pages closed";
                case SourceLevel.Known: return "Opens their pages, with what a known place brings along";
                default: return "Like Known, and also shows the rooms anyone on this server has walked, and what is in them. A dungeon nobody has walked shows only its entrance.";
            }
        }

        /// <summary>The tip on what a source shows: from where, and as what (<see cref="CompendiumView.ByWorld"/>).</summary>
        public static string ByWorld(WorldSource source, KnownLevel shown) =>
            shown < KnownLevel.Seen ? $"Heard of from {SourcePhrase(source)}; your character has not met it yet"
            : $"Shown as {(shown == KnownLevel.Known ? "known" : "seen")} from {SourcePhrase(source)}; your character has not met it yet";

        /// <summary>A tip on a chip or link to what a source shows, its own tip under it.</summary>
        public static string WorldTip(WorldSource source, KnownLevel shown, string tip) => Naming.Lines(ByWorld(source, shown), tip);

        /// <summary>A thing known because something known brings it along, as a creature its attacks.</summary>
        public static string CameWith(string owner) => "along with " + owner;

        /// <summary>A thing known before its character's book was kept, from what the game kept of it.</summary>
        public const string KnownBefore = "known before Scry kept track of your character";

        /// <summary>The tip of an attack's damage kept back.</summary>
        public const string NotYetTakenTip = "Its damage shows once this attack has hit you";

        /// <summary>
        /// What a known page under the strict rules still keeps back, for the You box at its top,
        /// a row for each thing to do and what it shows: the deeds whose numbers it keeps back
        /// (each once), the damage types whose landing it keeps back, the attacks not yet taken,
        /// the weak spots not yet found, the counts of drops or yields not yet seen, for the
        /// stage the star levels and the gear not yet seen up close, and last the names it leaves
        /// out, those never met (to meet first) apart from those met but not seen together.
        /// </summary>
        public static List<(string Label, string Value)> StillToFindOut(IEnumerable<string> deeds, IReadOnlyList<string> types, int attacks, int spots, IEnumerable<Tally> uncounted,
            IEnumerable<int> starsUnseen = null, IEnumerable<string> gearUnseen = null, bool ofItem = false, int namesApart = 0, int namesUnmet = 0, int unread = 0)
        {
            var rows = new List<(string, string)>();
            var pending = new HashSet<Deed>();
            var starsPending = new SortedSet<int>();
            var raiders = 0;
            var landedPending = new SortedSet<int>();
            foreach (var proof in deeds)
            {
                if (EntryKeys.IsDeed(proof, out var deed)) pending.Add(deed);
                else if (EntryKeys.IsStarsFell(proof, out var fell)) starsPending.Add(fell);
                else if (EntryKeys.IsRaider(proof, out _)) raiders++;
                else if (EntryKeys.IsLanded(proof, out var landed)) landedPending.Add(landed);
            }
            foreach (var fell in starsPending) rows.Add(($"See one with {Naming.Count(fell, "star", "stars")} fall", $"shows what {StarsChange(fell)}"));
            void Pending(Deed what, string label, string value)
            {
                if (pending.Contains(what)) rows.Add((label, value));
            }
            Pending(Deed.Killed, pending.Contains(Deed.Ridden) ? "Kill or ride one" : "Kill one", "shows its health");
            Pending(Deed.Fought, "Fight one", "shows how it senses, chases and flees");
            Pending(Deed.Felt, "Have it on you", "shows how it works on you");
            Pending(Deed.Sprang, "See one spring", "shows how it hurts and rearms");
            Pending(Deed.Raided, "See its raiders up close", "shows how likely it is to start");
            Pending(Deed.Guarded, "See its creatures up close", "shows the stars its spawn points give");
            foreach (var landed in landedPending) rows.Add(($"Catch one with {FishWords.LandedStars(landed)}", $"shows how one with {FishWords.LandedStars(landed)} fights"));
            if (raiders > 0) rows.Add(("See them up close", Naming.Count(raiders, "kind of raider", "kinds of raider") + ": how many come, how often, how strong"));
            Pending(Deed.Tamed, "Tame one", "shows how long taming and a meal take");
            Pending(Deed.Fed, "See one eat", "shows how long a meal lasts");
            Pending(Deed.Born, "See one born", "shows how long it is pregnant");
            Pending(Deed.GrewUp, "See one grow up", "shows how long that takes");
            Pending(Deed.Hatched, "See one hatch", "shows how long that takes");
            Pending(Deed.Ridden, "Ride one", "shows its stamina and speed when ridden");
            Pending(Deed.Caught, "Catch one", "shows how it bites and fights and what a catch brings");
            Pending(Deed.GrewBack, "See one grow back", "shows how long that takes");
            Pending(Deed.Broke, "Break one", "shows its health");
            Pending(Deed.StarredFell, "See a starred one fall", "shows what stars change");
            Pending(Deed.Spawned, "See it spawn", "shows its pool and pace");
            Pending(Deed.Collected, "Collect from one", "shows its pace");
            Pending(Deed.Grew, "Grow one or see one grow", "shows how long that takes");
            Pending(Deed.Finished, "See one finish", "shows how long each takes and what it burns");
            if (types.Count > 0) rows.Add(("Hit it with", Naming.Commas(types) + (types.Count == 1 ? ": how it lands" : ": how they land")));
            if (attacks > 0) rows.Add(("See it attack", Naming.Count(attacks, "attack's", "attacks'") + " damage, up close or taken"));
            if (spots > 0) rows.Add((spots == 1 ? "Hit its weak spot" : "Hit its weak spots", Numbers.Count(spots) + " not yet found"));
            var tallies = new HashSet<Tally>(uncounted);
            // On an item's page the counts are its givers': the rows name them, not the item.
            if (tallies.Contains(Tally.Fell)) rows.Add(ofItem ? ("See something drop it", "shows how many it drops") : ("See it drop", "how many of each"));
            if (tallies.Contains(Tally.Gave)) rows.Add(ofItem ? ("See something give it", "shows how many it gives") : ("See it give", "how many of each"));
            if (tallies.Contains(Tally.Held)) rows.Add(ofItem ? ("Find it in a chest", "how many it holds") : ("Open one", "how many of each it holds"));
            if (tallies.Contains(Tally.Weather)) rows.Add(("Spend time in it", "how often each weather comes"));
            foreach (var stars in starsUnseen ?? Array.Empty<int>()) rows.Add(($"See {Naming.Count(stars, "star", "stars")} up close", "shows it so on the stage"));
            foreach (var gear in gearUnseen ?? Array.Empty<string>()) rows.Add(("See it carry", $"{gear} up close: shows it carrying that on the stage"));
            if (namesUnmet > 0) rows.Add(("Meet first", Naming.Count(namesUnmet, "thing this page names", "things this page names")));
            if (namesApart > 0) rows.Add(("See it together", Naming.Count(namesApart, "thing", "things") + " you've met, with this page"));
            if (unread > 0) rows.Add(("Read its runestones", Naming.Count(unread, "text", "texts") + " you have not read"));
            return rows;
        }

        /// <summary>So many stars in words: "no stars", "1 star", "2 stars".</summary>
        public static string StarsWords(int stars) => stars <= 0 ? "no stars" : Naming.Count(stars, "star", "stars");

        /// <summary>What was found out of an entry (<see cref="CompendiumBook.FoundOut"/>), in a few words beside its name in the learned list; none for nothing.</summary>
        public static string FoundOutNote(string proof)
        {
            if (string.IsNullOrEmpty(proof)) return null;
            if (proof == EntryKeys.OnMap) return "seen in ground you explored";
            if (EntryKeys.IsStarsFell(proof, out var fell)) return $"what {StarsChange(fell)} found out";
            if (EntryKeys.IsRaider(proof, out _)) return "a raider's numbers found out";
            if (EntryKeys.IsLanded(proof, out var landed)) return $"how it fights with {FishWords.LandedStars(landed)} found out";
            if (EntryKeys.IsDeed(proof, out var deed))
            {
                switch (deed)
                {
                    case Deed.Tamed: return "its taming found out";
                    case Deed.Fed: return "its meals found out";
                    case Deed.Fought: return "its ways found out";
                    case Deed.Felt: return "how it works found out";
                    case Deed.Sprang: return "its springing found out";
                    case Deed.Raided: return "its chance to start found out";
                    case Deed.Guarded: return "its creatures' stars found out";
                    case Deed.Born: return "its pregnancy found out";
                    case Deed.Hatched: return "its hatching found out";
                    case Deed.Ridden: return "its riding found out";
                    case Deed.Caught: return "its fight found out";
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

        /// <summary>The hover of a name kept back in a table's cell under the strict rules.</summary>
        public const string NotSeenTogetherTip = "Named once you see it together with this page";

        /// <summary>The hover of a chain's step kept back under the strict rules, named by the chain's own links (<see cref="Chain.Linked"/>).</summary>
        public const string ChainStepKeptTip = "Named once you see it go to the step before or after it";

        /// <summary>The hover of a section's line counting what the rules left out as never met: meeting it, the first step to its being named here.</summary>
        public static string MeetFirstTip(bool strict) => strict ? "Met nowhere yet: meet each first" : "Named here once you meet each";

        /// <summary>
        /// The hover of a section's line counting what the strict rules left out though met: the
        /// way it comes to be seen together with the page, by the section's topic.
        /// </summary>
        public static string TogetherTip(string topic)
        {
            switch (topic)
            {
                case "loot": return "You've met these but not yet seen them drop from this. Each is named here once you see it drop as this dies";
                case "from": return "You've met these but not yet seen them give this. Each is named here once you see this come of it: picked, broken, mined, made or dropped";
                case "lives": return "You've found these places but not yet met this in them. Each is named here once you meet this there";
                case "room": return "You've met these but not yet seen them in a dungeon with this room. Each is named here once you see it inside one";
                default:
                    return "You've met these but not yet seen them go together with this page. Each is named here once you see the two go together in play, "
                        + "as a drop falls, a ware stands in a store, an item goes into or comes out of a station, a tool breaks it or a creature eats it";
            }
        }

        /// <summary>
        /// What the rules left out of a card's list or a raid's sentence as never met, read after
        /// the names it lists, naming the one kind they are of (<paramref name="holds"/>), else
        /// "things": "2 more creatures you have not met"; without "more" where it lists none
        /// (<paramref name="alone"/>); null for none.
        /// </summary>
        public static string NeverMet(int count, bool alone = false, Kind? holds = null) =>
            count > 0 ? Numbers.Count(count) + (alone ? " " : " more ") + Held(count, holds) + " you have not met" : null;

        /// <summary>
        /// A section's line of what the rules left out as never met, said as what to do and what it
        /// counts by what the section is about: "Meet 55 more sources of Copper", "Meet 6 more items
        /// it drops", "Find 3 more places it lives", "Meet 4 more creatures this room holds", "Meet
        /// 5 more things it goes with". The page's <paramref name="name"/> is said where the section
        /// is of where it comes from, "it" for none or one too long to read in a line; a room's
        /// things by the one kind they are of (<paramref name="holds"/>). Without "more" where the
        /// section names nothing else (<paramref name="alone"/>); null for none.
        /// </summary>
        public static string MeetLine(string topic, int count, bool alone = false, string name = null, Kind? holds = null)
        {
            if (count <= 0) return null;
            var counted = Numbers.Count(count) + (alone ? " " : " more ");
            switch (topic)
            {
                case "from": return "Meet " + counted + Naming.Noun(count, "source", "sources") + " of " + Named(name);
                case "loot": return "Meet " + counted + Naming.Noun(count, "item", "items") + " it drops";
                case "lives": return "Find " + counted + Naming.Noun(count, "place", "places") + " it lives";
                case "room": return "Meet " + counted + Held(count, holds) + " this room holds";
                default: return "Meet " + counted + Naming.Noun(count, "thing", "things") + " it goes with";
            }
        }

        /// <summary>
        /// A section's line of what the strict rules left out though met, said as what to watch for:
        /// "See 14 sources you've met give Copper", "See it drop 6 items you've met", "Meet it in 2
        /// places you've found", "See 4 things you've met in this room", "See 5 things you've met
        /// with this"; the page's name and a room's kind as in <see cref="MeetLine"/>; null for none.
        /// </summary>
        public static string SeeLine(string topic, int count, string name = null, Kind? holds = null)
        {
            if (count <= 0) return null;
            var counted = Numbers.Count(count) + " ";
            switch (topic)
            {
                case "from": return "See " + counted + Naming.Noun(count, "source", "sources") + " you've met give " + Named(name);
                case "loot": return "See it drop " + counted + Naming.Noun(count, "item", "items") + " you've met";
                case "lives": return "Meet it in " + counted + Naming.Noun(count, "place", "places") + " you've found";
                case "room": return "See " + counted + Held(count, holds) + " you've met in this room";
                default: return "See " + counted + Naming.Noun(count, "thing", "things") + " you've met with this";
            }
        }

        /// <summary>The longest page name a section's line says, past which it says "it".</summary>
        private const int LineNameLength = 24;

        private static string Named(string name) => string.IsNullOrEmpty(name) || name.Length > LineNameLength ? "it" : name;

        private static string Held(int count, Kind? holds) => holds is Kind kind ? Kinds.Noun(kind, count) : Naming.Noun(count, "thing", "things");

        /// <summary>
        /// A line's hover count of what it left out by kind, the most first: "9 creatures, 5
        /// resources"; null where all are of one kind, which the line says already.
        /// </summary>
        public static string ByKind(IEnumerable<Kind> kinds)
        {
            var counts = new Dictionary<Kind, int>();
            foreach (var kind in kinds) counts[kind] = counts.TryGetValue(kind, out var had) ? had + 1 : 1;
            if (counts.Count < 2) return null;
            var order = counts.Keys.ToList();
            order.Sort((a, b) => counts[a] != counts[b] ? counts[b].CompareTo(counts[a]) : a.CompareTo(b));
            return Naming.Commas(order.Select(k => Numbers.Count(counts[k]) + " " + Kinds.Noun(k, counts[k])));
        }

        /// <summary>The faint line under a section whose recipe rows wait for the game to list them for the character.</summary>
        public static string RecipeRowsWait(int rows) => rows == 1 ? "1 recipe shows here once your character unlocks it" : $"{Numbers.Count(rows)} recipes show here once your character unlocks them";

        private static string Seen(string proof)
        {
            if (EntryKeys.IsStarsFell(proof, out var fell)) return $"seen one with {Naming.Count(fell, "star", "stars")} fall";
            if (EntryKeys.IsRaider(proof, out _)) return "seen one of these raiders up close";
            if (EntryKeys.IsLanded(proof, out var landed)) return $"landed one with {FishWords.LandedStars(landed)}";
            EntryKeys.IsDeed(proof, out var deed);
            switch (deed)
            {
                case Deed.Tamed: return "tamed one";
                case Deed.Fed: return "seen one eat";
                case Deed.Fought: return "fought one";
                case Deed.Felt: return "had it on you";
                case Deed.Sprang: return "seen one spring";
                case Deed.Raided: return "seen one of its raiders up close";
                case Deed.Guarded: return "seen one of its creatures up close there";
                case Deed.Born: return "seen one born";
                case Deed.Hatched: return "seen one hatch";
                case Deed.Ridden: return "ridden one";
                case Deed.Caught: return "caught one";
                case Deed.GrewUp: return "seen one grow up";
                case Deed.GrewBack: return "seen one grow back";
                case Deed.Broke: return "broken one";
                case Deed.StarredFell: return "seen a starred one fall";
                case Deed.Spawned: return "seen it spawn";
                case Deed.Collected: return "collected from one";
                case Deed.Grew: return "grown or seen one grow";
                case Deed.Finished: return "seen one finish";
                default: return "killed one";
            }
        }

        /// <summary>The tip of a damage type a creature's grid keeps back.</summary>
        public static string NotYetHitWithTip(string type) => $"{type}: how it lands shows once you hit it with {type.ToLowerInvariant()}";

        /// <summary>
        /// What the strict rules left out of a card's list or a raid's sentence though met, read
        /// after the names it lists (<see cref="NeverMet"/>), naming their one kind as it does: a
        /// room's "1 more creature you have met but not seen in this room", a raid's "1 more
        /// creature you have met but not seen with this raid"; null for none.
        /// </summary>
        public static string LeftOutOf(string topic, int count, bool alone = false, Kind? holds = null)
        {
            if (count <= 0) return null;
            var with = topic == "room" ? "in this room" : topic == "raid" ? "with this raid" : "with this";
            return Numbers.Count(count) + (alone ? " " : " more ") + Held(count, holds) + " you have met but not seen " + with;
        }

        /// <summary>The line at the end of a place's runestone texts with the compendium on, asking to read those the character has not: "Read 2 more texts", without "more" where none is shown (<paramref name="alone"/>); null for none.</summary>
        public static string NotYetRead(int count, bool alone = false) => count > 0 ? $"Read {Numbers.Count(count)}{(alone ? "" : " more")} {Naming.Noun(count, "text", "texts")}" : null;

        /// <summary>A headline tile's count with the compendium on: how many of the row's the page names, "3 met".</summary>
        public static string MetCount(int count) => $"{Numbers.Count(count)} met";

        /// <summary>The faint line under a row's title whose wares wait for a boss not yet fallen, under the loose rules: "2 wares still to unlock"; null for none.</summary>
        public static string ToUnlock(int count) => count > 0 ? $"{Numbers.Count(count)} {Naming.Noun(count, "ware", "wares")} still to unlock" : null;

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
                case SetAsideKind.Compendium: return $"Scry could not read this character's compendium, so it set the old file aside as {file} and started a new one.";
                case SetAsideKind.Walks: return $"Scry could not read the maps of the dungeons your character walked, so it set the old file aside as {file} and started new ones.";
                default: return $"Scry could not read this world's dungeon records, so it set the old file aside as {file} and started new ones.";
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
        public static string SeenRowNote(string note)
        {
            if (string.IsNullOrEmpty(note)) return SeenRowTag;
            if (SeenRowNotes.TryGet(note, out var said)) return said;
            said = SeenRowTag + " · " + note;
            SeenRowNotes.Set(note, said);
            return said;
        }

        /// <summary>A family's row note: how many of its kinds the character met, and of those how many it knows where not all: "3 kinds, 1 known".</summary>
        public static string FamilyNote(int kinds, int known) =>
            Numbers.Count(kinds) + " kinds" + (known > 0 && known < kinds ? ", " + Numbers.Count(known) + " known" : "");

        /// <summary>What a family's page switch is captioned by.</summary>
        public const string KindsCaption = "Kinds";

        /// <summary>A kind's words on the switch, "seen" after one only seen: "2 · seen".</summary>
        public static string KindSwitchLabel(string label, bool seen)
        {
            if (!seen) return label;
            if (KindSwitchLabels.TryGet(label, out var said)) return said;
            said = label + " · " + SeenRowTag;
            KindSwitchLabels.Set(label, said);
            return said;
        }

        /// <summary>The switch's seen labels by their label, kept: the page draws them every frame.</summary>
        private static readonly RecentCache<string, string> KindSwitchLabels = new RecentCache<string, string>(256, StringComparer.Ordinal);

        /// <summary>The section setting a family's kinds side by side.</summary>
        public const string KindsHeading = "KINDS";

        /// <summary>The title of the table of where a family's kinds differ.</summary>
        public const string KindsDifferTitle = "Where its kinds differ";

        /// <summary>The seen rows' notes by their note, kept: the list draws them every frame.</summary>
        private static readonly RecentCache<string, string> SeenRowNotes = new RecentCache<string, string>(512, StringComparer.Ordinal);

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
                case KnownLevel.Seen: return "Seen, not yet known";
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
