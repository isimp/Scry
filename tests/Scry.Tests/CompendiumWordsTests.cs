using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumWordsTests
    {
        // A page not yet open names something not met only by its kind, and says how much the
        // character knows and what would open it.

        private static readonly Kind[] Counted = { Kind.Creature, Kind.Item, Kind.Piece, Kind.Resource, Kind.Spawner, Kind.StatusEffect, Kind.Location, Kind.Raid, Kind.Biome };

        [Fact]
        public void SomethingNotMetIsNamedOnlyByItsKindEachKindInItsOwnWords()
        {
            Assert.Equal("Unknown creature", CompendiumWords.Unknown(Kind.Creature));
            Assert.Equal("Unknown place", CompendiumWords.Unknown(Kind.Location));
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
            Assert.Equal("Adds items (1,200), 3 found", CompendiumWords.Found("Adds items (1,200)", 3));
        }

        [Fact]
        public void PeekingWarnsThatEverythingShowsAndUntilWhen()
        {
            Assert.Equal("Peek", CompendiumWords.Peek);
            Assert.Equal("Show everything for now, not only what this character has met; it stops when the panel closes", CompendiumWords.PeekTip(false));
            Assert.Equal("Show only what this character has met again", CompendiumWords.PeekTip(true));
            Assert.Equal("Peeking: everything shows, not only what this character has met, until the panel closes", CompendiumWords.Peeking);
            Assert.Equal("Stop", CompendiumWords.StopPeeking);
        }

        [Fact]
        public void TheNoticeNamesTheFirstThingsLearnedAndCountsTheRest()
        {
            Assert.Equal("Newly learned: Troll", CompendiumWords.NewlyLearned(new[] { "Troll" }));
            Assert.Equal("Newly learned: Troll, Neck and Boar", CompendiumWords.NewlyLearned(new[] { "Troll", "Neck", "Boar" }));
            Assert.Equal("Newly learned: Troll, Neck, Boar and 1,200 more", CompendiumWords.NewlyLearned(new[] { "Troll", "Neck", "Boar" }.Concat(Enumerable.Repeat("x", 1200)).ToArray()));
            Assert.Equal("Show", CompendiumWords.ShowLearned);
            Assert.Equal("Put this away until more is learned", CompendiumWords.PutNewsAway);
        }

        [Fact]
        public void CatchingUpFromTheMapSaysWhatItDidAndWhyNot()
        {
            Assert.Equal("the explored map will be read again for this character in this world", CompendiumWords.MapAgain);
            Assert.Equal("the explored map can be read only in a world", CompendiumWords.MapNoWorld);
            Assert.Equal("caught up from the explored map: 1,200 kinds of place lie in explored ground, 3 of them new to the compendium", CompendiumWords.MapRead(1200, 3, fromServer: false));
            Assert.Equal("caught up from the explored map with the server's answer: 12 kinds of place lie in explored ground, 0 of them new to the compendium", CompendiumWords.MapRead(12, 0, fromServer: true));
            Assert.Equal("answered a player's explored map: 12 kinds of place lie in it", CompendiumWords.MapAnswered(12));
            Assert.Equal("the server did not answer on the explored map (it has no Scry, or one too old): places count as the character meets them", CompendiumWords.MapNoServerScry);
            Assert.Equal("Read the map again", CompendiumWords.ReadMap);
            Assert.Equal("Counts the kinds of place in the ground this character explored in this world as seen, as on its first visit with Scry", CompendiumWords.ReadMapTip);
            Assert.Equal("on a dedicated server Scry only answers players' explored maps, for their compendium", CompendiumWords.ServerPart);
        }

        [Fact]
        public void TheFloorSaysHowFarItReachesAndWhatRaisingOrLoweringItDoes()
        {
            Assert.Equal("Floor: none", CompendiumWords.Floor(0));
            Assert.Equal("Floor: Black forest", CompendiumWords.Floor(2));
            Assert.Equal("Show everything of the Swamp as known: Bonemass fell for this character", CompendiumWords.RaiseFloor(3, "Bonemass"));
            Assert.Equal("Show everything of the Swamp as known", CompendiumWords.RaiseFloor(3, null));
            Assert.Equal("Show the Meadows and what came before as known, not the Black forest", CompendiumWords.LowerFloor(1));
            Assert.Equal("Show only what this character has met again", CompendiumWords.LowerFloor(0));
            Assert.Equal("The next tier opens once The Elder falls for this character", CompendiumWords.FloorWaits("The Elder"));
        }

        [Fact]
        public void ATitleWhoseItemsWereLeftOutLosesItsCount()
        {
            // Under the strict rules nothing tells how much was left out.
            Assert.Equal("Played by", CompendiumWords.Uncounted("Played by (1,200)"));
            Assert.Equal("Drops", CompendiumWords.Uncounted("Drops"));
            Assert.Equal("Raids (in the Plains)", CompendiumWords.Uncounted("Raids (in the Plains)"));
            Assert.Equal("???", CompendiumWords.Hidden);
        }

        [Fact]
        public void TheListOfWhatWasLearnedLatelySaysWhatItHolds()
        {
            Assert.Equal("Learned lately", CompendiumWords.LearnedLately);
            Assert.Equal("Show what this character learned lately, newest first", CompendiumWords.LearnedTip(false));
            Assert.Equal("Showing what this character learned lately, newest first", CompendiumWords.LearnedTip(true));
        }

        [Fact]
        public void AClosedPageSaysHowMuchIsKnownAndWhatWouldOpenIt()
        {
            Assert.Equal("Not met yet", CompendiumWords.Closed(KnownLevel.Unknown));
            Assert.Equal("Heard of, not met yet", CompendiumWords.Closed(KnownLevel.HeardOf));
            Assert.Equal("Seen, not yet come to know", CompendiumWords.Closed(KnownLevel.Seen));
            Assert.Equal("Fight it to learn all about it.", CompendiumWords.HowToKnow(Kind.Creature));
            Assert.Equal(Counted.Length, Counted.Select(CompendiumWords.HowToKnow).Distinct().Count());
        }
    }
}
