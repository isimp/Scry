using System.IO;
using Xunit;

namespace Scry.Tests
{
    public class FactWordsTests
    {
        // What a page says around its facts: the parts that could not be read, values shown as
        // they are, and the links a value carries that play music or open a website rather than
        // going to an entry.

        [Fact]
        public void APartThatCouldNotBeReadIsNamedAtTheEnd()
        {
            Assert.Equal("the drops details", FactWords.Part("drops"));
            Assert.Equal("facts drops", FactWords.PartTiming("drops"));
            Assert.Equal("the drops, recipe details could not be read (the log says why)", FactWords.NotShown(new[] { "drops", "recipe" }));
        }

        [Fact]
        public void AValueIsShownAsItIs()
        {
            Assert.Equal("1,234.5", FactWords.Value(1234.5f));
            Assert.Equal("1,500", FactWords.Value(1500));
            Assert.Equal("yes", FactWords.Value(true));
            Assert.Equal("no", FactWords.Value(false));
            Assert.Equal("status effect", FactWords.Value(Kind.StatusEffect));
            Assert.Equal("Moder", FactWords.Value("Moder"));
        }

        [Fact]
        public void AChoiceIsShownByItsNameAndNotAtAllByANumber()
        {
            Assert.Equal("Status effect", FactWords.Choice(Kind.StatusEffect));
            Assert.Equal("Read only, Hidden", FactWords.Choice(FileAttributes.ReadOnly | FileAttributes.Hidden));
            Assert.Null(FactWords.Choice((Kind)999));
            Assert.Null(FactWords.Value((Kind)999));
        }

        [Fact]
        public void MusicIsPlayedByTheEntrysOwnLinkOrByName()
        {
            Assert.True(EntryKeys.PlaysMusic(EntryKeys.PlayMusic, out var own));
            Assert.Null(own);
            Assert.True(EntryKeys.PlaysMusic(EntryKeys.PlayMusicNamed("Meadows_day"), out var named));
            Assert.Equal("Meadows_day", named);
            Assert.False(EntryKeys.PlaysMusic("se:Rested", out _));
            Assert.False(EntryKeys.PlaysMusic(null, out _));
        }

        [Fact]
        public void AWebsiteLinkOpensItsAddress()
        {
            Assert.Equal("https://example.org/mod", EntryKeys.WebsiteOf(EntryKeys.Website("https://example.org/mod")));
            Assert.Null(EntryKeys.WebsiteOf("se:Rested"));
            Assert.Null(EntryKeys.WebsiteOf(null));
        }

        [Fact]
        public void ASpawnerLabelsEachCreatureItSpawns() => Assert.Equal("Spawns Greydwarf", SpawnWords.Spawns("Greydwarf"));

        [Fact]
        public void ADoorSaysItsKeyAndWhetherOpeningUsesItUp()
        {
            Assert.Equal("Crypt key, used up", BuildWords.OpenedWith("Crypt key", usedUp: true));
            Assert.Equal("Crypt key", BuildWords.OpenedWith("Crypt key", usedUp: false));
        }

        [Fact]
        public void ATrophySaysThePowerItGivesOnItsBossStone() => Assert.Equal("gives Eikthyr", ItemWords.Gives("Eikthyr"));

        [Fact]
        public void AnyTextCanBeginWithACapital()
        {
            Assert.Equal("Health ×2", Naming.Capital("health ×2"));
            Assert.Equal("", Naming.Capital(""));
            Assert.Null(Naming.Capital(null));
        }
    }
}
