using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class SpotModesTests
    {
        // A single spot on the map crossed off, as the game crosses its pins, or highlighted.

        [Fact]
        public void ARightClickGoesThroughTheModesAndBackToPlain()
        {
            Assert.Equal(SpotMode.Crossed, SpotModes.Next(SpotMode.Plain));
            Assert.Equal(SpotMode.Highlighted, SpotModes.Next(SpotMode.Crossed));
            Assert.Equal(SpotMode.Plain, SpotModes.Next(SpotMode.Highlighted));
        }

        [Fact]
        public void ACrossedMarkDimsAndNeverStandsBrighterThanItsKind()
        {
            Assert.Equal(SpotModes.CrossedShare, SpotModes.ShareOf(SpotMode.Crossed, 1f));
            // Its kind faded by a pick: as faded as the rest, not brought up to the crossed share.
            Assert.Equal(0.2f, SpotModes.ShareOf(SpotMode.Crossed, 0.2f));
            Assert.Equal(1f, SpotModes.ShareOf(SpotMode.Plain, 1f));
            Assert.Equal(1f, SpotModes.ShareOf(SpotMode.Highlighted, 1f));
            Assert.Equal(0.2f, SpotModes.ShareOf(SpotMode.Highlighted, 0.2f));
        }

        [Fact]
        public void FarOutAKindsSpotsMergeOnlyWithTheirOwnModeAndAHighlightedOneNever()
        {
            // Six spots of one kind lying together, two crossed, one highlighted.
            var modes = new[] { SpotMode.Plain, SpotMode.Plain, SpotMode.Plain, SpotMode.Crossed, SpotMode.Crossed, SpotMode.Highlighted };
            var merged = new MarkStacks();
            merged.Gather(modes.Length, i => (i * 5f, 0f), 100f, i => SpotModes.MergeGroup(0, modes[i], i));
            Assert.Equal(3, merged.CountAt(merged.AnchorOf(0)));
            Assert.Equal(2, merged.CountAt(merged.AnchorOf(3)));
            Assert.NotEqual(merged.AnchorOf(0), merged.AnchorOf(3));
            Assert.Equal(5, merged.AnchorOf(5));
            // Another kind's plain spots never join them.
            Assert.NotEqual(SpotModes.MergeGroup(0, SpotMode.Plain, 0), SpotModes.MergeGroup(1, SpotMode.Plain, 1));
            Assert.NotEqual(SpotModes.MergeGroup(0, SpotMode.Crossed, 0), SpotModes.MergeGroup(1, SpotMode.Plain, 1));
            // Two highlighted of one kind stay apart.
            Assert.NotEqual(SpotModes.MergeGroup(0, SpotMode.Highlighted, 4), SpotModes.MergeGroup(0, SpotMode.Highlighted, 5));
        }

        [Fact]
        public void ASpotsModeIsKeptWithTheCharacterForItsWorld()
        {
            var book = new CompendiumBook();
            Assert.Equal(SpotMode.Plain, book.SpotModeAt("one", 100, -200));
            Assert.True(book.SetSpotMode("one", 100, -200, SpotMode.Crossed));
            book.SetSpotMode("one", -5, 7, SpotMode.Highlighted);
            var back = CompendiumBook.Load(book.Save());
            Assert.Equal(SpotMode.Crossed, back.SpotModeAt("one", 100, -200));
            Assert.Equal(SpotMode.Highlighted, back.SpotModeAt("one", -5, 7));
            // Another world's spot at the same place is its own.
            Assert.Equal(SpotMode.Plain, back.SpotModeAt("two", 100, -200));
        }

        [Fact]
        public void BackToPlainLeavesNothingKept()
        {
            var book = new CompendiumBook();
            book.SetSpotMode("one", 100, 200, SpotMode.Crossed);
            book.SetSpotMode("one", 100, 200, SpotMode.Plain);
            Assert.Equal(SpotMode.Plain, book.SpotModeAt("one", 100, 200));
            Assert.DoesNotContain("spotmode", book.Save());
        }

        [Fact]
        public void TheSameThingToldOfAndSeenSharesItsModeButANeighbourDoesNot()
        {
            var book = new CompendiumBook();
            book.SetSpotMode("one", 100, 200, SpotMode.Crossed);
            Assert.Equal(SpotMode.Crossed, book.SpotModeAt("one", 101, 198));
            Assert.Equal(SpotMode.Plain, book.SpotModeAt("one", 103, 200));
            // Set from nearby it replaces the one kept, so one thing never holds two.
            book.SetSpotMode("one", 101, 199, SpotMode.Highlighted);
            Assert.Equal(SpotMode.Highlighted, book.SpotModeAt("one", 100, 200));
            book.SetSpotMode("one", 100, 200, SpotMode.Plain);
            Assert.Equal(SpotMode.Plain, book.SpotModeAt("one", 101, 199));
        }

        [Fact]
        public void AChangeIsToldOnlyAsItChanges()
        {
            var book = new CompendiumBook();
            var was = book.SpotModesVersion;
            Assert.False(book.SetSpotMode("one", 1, 1, SpotMode.Plain));
            Assert.Equal(was, book.SpotModesVersion);
            Assert.True(book.SetSpotMode("one", 1, 1, SpotMode.Crossed));
            Assert.True(book.SpotModesVersion > was);
            Assert.False(book.SetSpotMode("one", 1, 1, SpotMode.Crossed));
        }

        [Fact]
        public void ALineThatCannotBeReadIsPassedOver()
        {
            var book = CompendiumBook.Load("spotmode\tone\tx\t2\tCrossed\nspotmode\tone\t1\t2\tNever\nspotmode\tone\t1\t2\n");
            Assert.Equal(SpotMode.Plain, book.SpotModeAt("one", 1, 2));
        }

        [Fact]
        public void ThePointerSaysWhatTheNextClickDoes()
        {
            Assert.Equal("right-click: cross off", SpotWords.ModeHint(SpotMode.Plain, 1));
            Assert.Equal("crossed off · right-click: highlight", SpotWords.ModeHint(SpotMode.Crossed, 1));
            Assert.Equal("highlighted · right-click: back to plain", SpotWords.ModeHint(SpotMode.Highlighted, 1));
            Assert.Equal("right-click: cross off all 6", SpotWords.ModeHint(SpotMode.Plain, 6));
            Assert.Equal("crossed off · right-click: highlight all 2", SpotWords.ModeHint(SpotMode.Crossed, 2));
        }

        [Fact]
        public void ThatStandsOnALineUnderTheMarksOwnWords()
        {
            Assert.Equal("Rock copper\nright-click: cross off", SpotWords.WithModeHint("Rock copper", SpotMode.Plain, 1));
        }

        [Fact]
        public void AChipSaysHowManyAreCrossed()
        {
            Assert.Equal("Rock copper 6", SpotWords.KindChip("Rock copper", 6, 0));
            Assert.Equal("Rock copper 6 · 2 crossed", SpotWords.KindChip("Rock copper", 6, 0, 2));
            Assert.Equal("Grave 1 · 4 told · 3 crossed", SpotWords.KindChip("Grave", 1, 4, 3));
        }
    }
}
