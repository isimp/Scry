using Xunit;

namespace Scry.Tests
{
    /// <summary>A page under the strict rules tells in its title row how much of it was found out, or why it shows whole.</summary>
    public class PageFindsTests
    {
        [Fact]
        public void EachThingThePageKeepsBackCountsFoundOrStillKept()
        {
            var finds = new PageFinds();
            Assert.False(finds.Any);
            finds.Count(kept: true);
            finds.Count(kept: false);
            finds.Count(kept: true);
            Assert.Equal((1, 3), (finds.Found, finds.Of));
            Assert.True(finds.Any);
            Assert.False(finds.AllFound);
            var all = new PageFinds();
            all.Count(kept: false);
            all.Count(kept: false);
            Assert.True(all.AllFound);
            // A page that keeps nothing back has all of it found out too, so every known page under the strict rules carries the chip.
            Assert.True(new PageFinds().AllFound);
            Assert.Equal("Nothing of this page is kept back under Strict", CompendiumWords.AllFoundOutTipFor(new PageFinds()));
            Assert.Equal("Everything this page keeps back under Strict was found out", CompendiumWords.AllFoundOutTipFor(all));
        }

        [Fact]
        public void TheChipSaysHowMuchWasFoundOutOrWhyThePageShowsWhole()
        {
            Assert.Equal("7 of 12 found out", CompendiumWords.FoundOutCount(7, 12, narrow: false));
            Assert.Equal("7 of 12", CompendiumWords.FoundOutCount(7, 12, narrow: true));
            Assert.Equal("All found out", CompendiumWords.AllFoundOut);
            Assert.Equal("Whole by Known up to", CompendiumWords.WholeBy(null));
            Assert.Equal("Whole from shared maps", CompendiumWords.WholeBy(WorldSource.SharedMaps));
            Assert.Equal("Still to find out:\nKill one · shows its health\nSee it attack · 2 attacks' damage, up close or taken\nA click goes to them in its You box",
                CompendiumWords.FindsTip(new[] { ("Kill one", "shows its health"), ("See it attack", "2 attacks' damage, up close or taken") }));
            // Nothing still to do: nothing in the You box to go to.
            Assert.Equal("Still to find out:", CompendiumWords.FindsTip(new (string, string)[0]));
            Assert.Equal("Known up to shows this page whole, as it shows every page of the tiers whose bosses this character has beaten; lower it in the compendium's box to see what is still to find out",
                CompendiumWords.WholeTip(null));
            Assert.Equal("Shared maps shows this page whole, as the compendium's box has it show; turn it down there to see what is still to find out",
                CompendiumWords.WholeTip(WorldSource.SharedMaps));
        }
    }
}
