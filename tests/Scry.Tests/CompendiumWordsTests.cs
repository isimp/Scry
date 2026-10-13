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
