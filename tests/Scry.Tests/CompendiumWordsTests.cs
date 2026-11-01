using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumWordsTests
    {
        [Fact]
        public void ADamageGridKeepingATypeBackSaysOnItsCellWhatShowsIt()
        {
            // No faint count under the grid: each –– says on hover what shows it, the You box lists them.
            Assert.Equal("Fire: how it lands shows once you hit it with fire", CompendiumWords.NotYetHitWithTip("Fire"));
        }

        // A page not yet open names something not met only by its kind, and says how much the
        // character knows and what would open it.

        private static readonly Kind[] Counted = { Kind.Creature, Kind.Item, Kind.Piece, Kind.Resource, Kind.Spawner, Kind.StatusEffect, Kind.Location, Kind.Raid, Kind.Biome };

        [Fact]
        public void SomethingNotMetIsNamedOnlyByItsKindEachKindInItsOwnWords()
        {
            Assert.Equal("Unknown creature", CompendiumWords.Unknown(Kind.Creature));
            Assert.Equal("Unknown location", CompendiumWords.Unknown(Kind.Location));
            Assert.Equal(Counted.Length, Counted.Select(CompendiumWords.Unknown).Distinct().Count());
        }

        [Fact]
        public void AKindsTabCountsWhatWasFoundOfAllThereIs()
        {
            Assert.Equal("Creatures  <color=#aabbcc>37/1,112</color>", CompendiumWords.Tab("Creatures", 37, 1112, "aabbcc"));
        }

        [Fact]
        public void AListOfWhatThereIsSaysHowManyOfItWereFound()
        {
            // A mod's page lists what it adds; only what the character has seen is shown.
            Assert.Equal("Adds items (1,200), 3 seen", CompendiumWords.Found("Adds items (1,200)", 3));
        }

        [Fact]
        public void TheModeNamesItsRulesAndTellsWhatEachShows()
        {
            Assert.Equal(new[] { "Off", "Loose", "Strict" }, CompendiumWords.Rules.Select(CompendiumWords.RuleName));
            // A button in the title row names the rules chosen; the box under it holds them.
            Assert.Equal("Compendium", CompendiumWords.ModeCaption);
            Assert.Equal("Compendium · Off  ▾", CompendiumWords.Button(CompendiumRule.Off));
            Assert.Equal("Compendium · Loose  ▾", CompendiumWords.Button(CompendiumRule.Loose));
            Assert.Equal("Compendium · Strict  ▾", CompendiumWords.Button(CompendiumRule.Strict));
            Assert.Equal("What your character knows, shown in the catalog", CompendiumWords.BoxCaption);
            Assert.Equal("Rules", CompendiumWords.RulesCaption);
            Assert.Equal("Loose: only what your character has met shows; a page opens whole once it has fought, held, built, used or entered it", CompendiumWords.StepTip(CompendiumRule.Loose));
            Assert.Equal("Show only what your character has come to know, by the rules chosen for it here; each character keeps its own", CompendiumWords.ModeTip);
            Assert.Equal("show everything. What your character comes to know is kept all the same, so turning the compendium on later shows everything met meanwhile", CompendiumWords.RuleTip(CompendiumRule.Off));
            Assert.Equal("only what your character has met shows; a page opens whole once it has fought, held, built, used or entered it", CompendiumWords.RuleTip(CompendiumRule.Loose));
            Assert.Equal("as Loose, but a page shows only what your character saw go together, and keeps back the numbers the game never shows until it finds them out", CompendiumWords.RuleTip(CompendiumRule.Strict));
        }

        [Fact]
        public void PeekingWarnsThatEverythingShowsAndUntilWhen()
        {
            // Peeking shows the page looked at whole, not the whole catalog: the list stays as the character knows it.
            Assert.Equal("Show each page whole until you close the panel. Its links open any page, and a full name in quotes in the search finds things your character has not met. The list itself stays as your character knows it", CompendiumWords.PeekTip(false, pageShown: false));
            Assert.Equal("Show only what your character has met again", CompendiumWords.PeekTip(true, pageShown: true));
            // With a page shown, the eye peeks at that page alone; its strip can widen it.
            Assert.Equal("Show this page whole until you go to another page; its links open any page. Every page, on the strip that then shows, does the same for all pages until you close the panel", CompendiumWords.PeekTip(false, pageShown: true));
            Assert.Equal("Peeking at Troll: this page shows whole until you go to another", CompendiumWords.PeekingPageLine("Troll", compact: false));
            Assert.Equal("Peeking at Troll", CompendiumWords.PeekingPageLine("Troll", compact: true));
            Assert.Equal("Until you go to another page, this one shows whole and its links open any page. The list itself stays as your character knows it", CompendiumWords.PeekingPageTip);
            Assert.Equal("Every page", CompendiumWords.EveryPage);
            Assert.Equal("Show every page whole until you close the panel. A full name in quotes in the search finds things your character has not met", CompendiumWords.EveryPageTip);
            // The strip says it short; the rest in the tip of its line.
            Assert.Equal("Peeking: pages show whole until the panel closes", CompendiumWords.Peeking);
            Assert.Equal("Each page shows whole and its links open any page. A full name in quotes in the search finds things your character has not met. The list itself stays as your character knows it", CompendiumWords.PeekingTip);
            Assert.Equal("Stop peeking", CompendiumWords.StopPeeking);
            // The compact view's strip says it short enough not to slide; how long it lasts moves to the tip.
            Assert.Equal(CompendiumWords.Peeking, CompendiumWords.PeekingLine(compact: false));
            Assert.Equal("Peeking: pages show whole", CompendiumWords.PeekingLine(compact: true));
            Assert.Equal(CompendiumWords.PeekingTip, CompendiumWords.PeekingLineTip(compact: false));
            Assert.Equal("Until you close the panel, each page shows whole and its links open any page. A full name in quotes in the search finds things your character has not met. The list itself stays as your character knows it", CompendiumWords.PeekingLineTip(compact: true));
        }

        [Fact]
        public void TheNoticeOffersWhatWasLearnedAndToPutItAway()
        {
            // What the notice says of the news is told by JournalWords.News.
            Assert.Equal("Show what was learned", CompendiumWords.ShowLearned);
            Assert.Equal("Put this away until more is learned", CompendiumWords.PutNewsAway);
        }

        [Fact]
        public void CatchingUpFromTheMapSaysWhatItDidAndWhyNot()
        {
            Assert.Equal("the map of ground you explored will be read again for your character in this world", CompendiumWords.MapAgain);
            Assert.Equal("the map of ground you explored can be read only in a world", CompendiumWords.MapNoWorld);
            Assert.Equal("caught up from the map of ground you explored: 1,200 kinds of place lie in it, 3 of them new to the book", CompendiumWords.MapRead(1200, 3, fromServer: false));
            Assert.Equal("caught up from the map of ground you explored with the server's answer: 12 kinds of place lie in it, 0 of them new to the book", CompendiumWords.MapRead(12, 0, fromServer: true));
            Assert.Equal("answered a player's map of the ground they explored: 12 kinds of place lie in it", CompendiumWords.MapAnswered(12));
            Assert.Equal("the server did not answer on the map of ground you explored (it has no Scry, or one too old): places count as the character meets them", CompendiumWords.MapNoServerScry);
            Assert.Equal("Read the map again", CompendiumWords.ReadMap);
            Assert.Equal("Places in the ground your character explored show as the Ground you explored setting says. Scry reads the map again each session", CompendiumWords.MapBar);
            Assert.Equal("Check again which kinds of place lie in the ground your character explored in this world. Scry also does this each session and as you explore", CompendiumWords.ReadMapTip);
            Assert.Equal("on a dedicated server Scry only answers players' maps of the ground they explored, the world's count of places and their Scry's hello, sends the world's settings for the compendium, keeps how far and on which days they walk its dungeons, reads what was left inside them, and runs the self-test's made-up events for the player asking", CompendiumWords.ServerPart);
        }

        [Fact]
        public void TheFloorSaysHowFarItReachesAndWhatRaisingOrLoweringItDoes()
        {
            // In the title row, a caption and the biome stepped between arrows.
            // Its lowest value is a label like the others, capitalised as every value in the box is.
            Assert.Equal(("Known up to", "No tier", "Black Forest"), (CompendiumWords.FloorCaption, CompendiumWords.FloorName(0), CompendiumWords.FloorName(2)));
            // What it shows that the character never came to know is marked, on its row and page, named as the control is.
            Assert.Equal("Shown as known because Known up to reaches the Black Forest tier; your character does not know it yet", CompendiumWords.ByFloor(2));
            // A link to such a thing says so above where it goes.
            Assert.Equal("Shown as known because Known up to reaches the Black Forest tier; your character does not know it yet\nGo to Troll", CompendiumWords.FloorTip(2, "Go to Troll"));
            Assert.Equal("Show everything of the Swamp as known: your character defeated Bonemass", CompendiumWords.RaiseFloor(3, "Bonemass"));
            Assert.Equal("Show everything of the Swamp as known", CompendiumWords.RaiseFloor(3, null));
            Assert.Equal("Show the Meadows and what came before as known, not the Black Forest", CompendiumWords.LowerFloor(1));
            Assert.Equal("Show only what your character has met again", CompendiumWords.LowerFloor(0));
            Assert.Equal("The next tier opens once your character defeats The Elder", CompendiumWords.FloorWaits("The Elder"));
        }

        [Fact]
        public void ATitleWhoseItemsWereLeftOutLosesItsCount()
        {
            // Under the strict rules nothing tells how much was left out.
            Assert.Equal("Played by", CompendiumWords.Uncounted("Played by (1,200)"));
            Assert.Equal("Drops", CompendiumWords.Uncounted("Drops"));
            Assert.Equal("Raids (in the Plains)", CompendiumWords.Uncounted("Raids (in the Plains)"));
            Assert.Equal("Drops (12a", CompendiumWords.Uncounted("Drops (12a"));
            Assert.Equal("––", CompendiumWords.Hidden);
        }

        [Fact]
        public void ASectionsLineOfWhatWasNeverMetSaysWhatToDoAndWhatItCounts()
        {
            // Each line starts with the deed that names them here, then says what the count is of, by the section.
            Assert.Equal("Meet 55 more sources of Copper", CompendiumWords.MeetLine("from", 55, name: "Copper"));
            Assert.Equal("Meet 1 more source of Copper", CompendiumWords.MeetLine("from", 1, name: "Copper"));
            Assert.Equal("Meet 6 more items it drops", CompendiumWords.MeetLine("loot", 6));
            Assert.Equal("Meet 1 more item it drops", CompendiumWords.MeetLine("loot", 1));
            Assert.Equal("Find 3 more places it lives", CompendiumWords.MeetLine("lives", 3));
            Assert.Equal("Find 1 more place it lives", CompendiumWords.MeetLine("lives", 1));
            Assert.Equal("Meet 4 more things this room holds", CompendiumWords.MeetLine("room", 4));
            Assert.Equal("Meet 3 more creatures this room holds", CompendiumWords.MeetLine("room", 3, holds: Kind.Creature));
            // Any other section, or one with no name of its own, counts the things it goes with.
            Assert.Equal("Meet 5 more things it goes with", CompendiumWords.MeetLine("making", 5));
            Assert.Equal("Meet 1,200 more things it goes with", CompendiumWords.MeetLine(null, 1200));
        }

        [Fact]
        public void ASectionsLineOfWhatWasMetButNotSeenTogetherSaysWhatToWatchFor()
        {
            Assert.Equal("See 14 sources you've met give Copper", CompendiumWords.SeeLine("from", 14, name: "Copper"));
            Assert.Equal("See 1 source you've met give Copper", CompendiumWords.SeeLine("from", 1, name: "Copper"));
            Assert.Equal("See it drop 6 items you've met", CompendiumWords.SeeLine("loot", 6));
            Assert.Equal("Meet it in 2 places you've found", CompendiumWords.SeeLine("lives", 2));
            Assert.Equal("Meet it in 1 place you've found", CompendiumWords.SeeLine("lives", 1));
            Assert.Equal("See 4 things you've met in this room", CompendiumWords.SeeLine("room", 4));
            Assert.Equal("See 1 creature you've met in this room", CompendiumWords.SeeLine("room", 1, holds: Kind.Creature));
            Assert.Equal("See 5 things you've met with this", CompendiumWords.SeeLine("fight", 5));
        }

        [Fact]
        public void ASectionsLineDropsMoreWhereItNamesNothingAndSaysItWhereThePagesNameIsMissingOrLong()
        {
            Assert.Equal("Meet 55 sources of Copper", CompendiumWords.MeetLine("from", 55, alone: true, name: "Copper"));
            Assert.Equal("Find 2 places it lives", CompendiumWords.MeetLine("lives", 2, alone: true));
            Assert.Equal("Meet 2 sources of it", CompendiumWords.MeetLine("from", 2, alone: true));
            Assert.Equal("See 2 sources you've met give it", CompendiumWords.SeeLine("from", 2, name: "Ancient Bark Spear of the Long Night"));
        }

        [Fact]
        public void ASectionTheRulesLeftNothingOutOfTellsNothing()
        {
            Assert.Null(CompendiumWords.MeetLine("loot", 0));
            Assert.Null(CompendiumWords.SeeLine("lives", -1));
            Assert.Null(CompendiumWords.NeverMet(0));
            Assert.Null(CompendiumWords.LeftOutOf("room", 0));
        }

        [Fact]
        public void ALineOfWhatWasLeftOutCountsItByKindInItsHoverWhereItIsOfMoreThanOne()
        {
            // The most first, as many as are left out of each.
            var kinds = Enumerable.Repeat(Kind.Resource, 5).Concat(new[] { Kind.StatusEffect }).Concat(Enumerable.Repeat(Kind.Creature, 9));
            Assert.Equal("9 creatures, 5 resources, 1 status effect", CompendiumWords.ByKind(kinds));
            // A place is a place, as the line counts it.
            Assert.Equal("2 places, 1 biome", CompendiumWords.ByKind(new[] { Kind.Location, Kind.Biome, Kind.Location }));
            // One kind only says nothing the line does not.
            Assert.Null(CompendiumWords.ByKind(new[] { Kind.Item, Kind.Item }));
            Assert.Null(CompendiumWords.ByKind(new Kind[0]));
        }

        [Fact]
        public void ACardsListOrARaidsSentenceCountsWhatWasLeftOutAfterItsNames()
        {
            // Read after names in a sentence ("Brings Troll, and 2 more creatures you have not met"), not as a line of its own.
            Assert.Equal("2 more creatures you have not met", CompendiumWords.NeverMet(2, holds: Kind.Creature));
            Assert.Equal("2 things you have not met", CompendiumWords.NeverMet(2, alone: true));
            Assert.Equal("1 more creature you have met but not seen in this room", CompendiumWords.LeftOutOf("room", 1, holds: Kind.Creature));
            Assert.Equal("3 creatures you have met but not seen with this raid", CompendiumWords.LeftOutOf("raid", 3, alone: true, holds: Kind.Creature));
        }

        [Fact]
        public void TheListOfWhatWasLearnedLatelySaysWhatItHolds()
        {
            Assert.Equal("Learned", CompendiumWords.LearnedTab);
            Assert.Equal("Show what your character learned lately, newest first", CompendiumWords.LearnedTip(false));
            Assert.Equal("Showing what your character learned lately, newest first", CompendiumWords.LearnedTip(true));
        }

        [Fact]
        public void AClosedPageSaysHowMuchIsKnownAndWhatWouldOpenIt()
        {
            Assert.Equal("Not met yet", CompendiumWords.Closed(KnownLevel.Unknown));
            Assert.Equal("Heard of, not met yet", CompendiumWords.Closed(KnownLevel.HeardOf));
            Assert.Equal("Seen, not yet known", CompendiumWords.Closed(KnownLevel.Seen));
            // Every way that makes it known, by one verb for a page: come to know.
            Assert.Equal("Fight it, see it tamed, pet it or hold its trophy to come to know it.", CompendiumWords.HowToKnow(Kind.Creature));
            Assert.Equal("Hold it, make it, eat it, unlock its recipe or read its card to come to know it.", CompendiumWords.HowToKnow(Kind.Item));
            Assert.Equal("Have it on you or choose it at its stone to come to know it.", CompendiumWords.HowToKnow(Kind.StatusEffect));
            Assert.Equal("It comes along once you come to know what it belongs to.", CompendiumWords.HowToKnow(Kind.Sound));
            Assert.Equal(Counted.Length, Counted.Select(CompendiumWords.HowToKnow).Distinct().Count());
            Assert.All(Counted, kind => Assert.EndsWith("to come to know it.", CompendiumWords.HowToKnow(kind)));
            // Under the strict rules a page known still keeps some back: said too.
            Assert.Equal("Under Strict, numbers and connections show as you see them.", CompendiumWords.ClosedStrict);
        }
    }

    /// <summary>The texts kept one at a time for the panel, which draws them each frame: checked with nothing else asking meanwhile.</summary>
    [Collection(nameof(TextsDrawnEveryFrameTests))]
    public class TextsDrawnEveryFrameTests
    {
        [Fact]
        public void TextsDrawnEveryFrameAreTheSameStringAgain()
        {
            // The panel draws these each frame: made once for their inputs and given again, they add no garbage a frame.
            Assert.Same(CompendiumWords.Button(CompendiumRule.Strict), CompendiumWords.Button(CompendiumRule.Strict));
            Assert.NotEqual(CompendiumWords.Button(CompendiumRule.Loose), CompendiumWords.Button(CompendiumRule.Strict));
            Assert.Same(CompendiumWords.SeenRowNote("log"), CompendiumWords.SeenRowNote("log"));
            Assert.Same(CompendiumWords.SeenRowNote(null), CompendiumWords.SeenRowNote(""));
            Assert.Same(DetailWords.Amounted("2", "Wood"), DetailWords.Amounted("2", "Wood"));
            Assert.Equal("Wood", DetailWords.Amounted(null, "Wood"));
            Assert.Same(UnsureWords.Marked("Sells"), UnsureWords.Marked("Sells"));
            Assert.Same(CensusWords.TabTip(2, 3, 9), CensusWords.TabTip(2, 3, 9));
            Assert.Same(DungeonWords.Progress(true, true, 12, 12, 4, 20), DungeonWords.Progress(true, true, 12, 12, 4, 20));
            Assert.Null(DungeonWords.Progress(true, true, 12, 12, 20, 20));
        }
    }

    /// <summary>Runs the texts kept one at a time with nothing else running.</summary>
    [CollectionDefinition(nameof(TextsDrawnEveryFrameTests), DisableParallelization = true)]
    public class TextsDrawnEveryFrameCollection
    {
    }
}
