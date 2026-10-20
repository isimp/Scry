using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Joining a world whose server sets any of the compendium's rules says so once, in one line,
    /// as the box's lock alone is easy to miss; told again only when the server's rules change.
    /// A character whose compendium is off is asked once whether to keep one.
    /// </summary>
    public class RulesNoticeAndWelcomeTests
    {
        private static WorldRules Rules(WorldRule rule = WorldRule.PlayersChoose, bool peeking = true, bool floor = true, SourceLevel shared = SourceLevel.WithInsides) =>
            WorldRules.From(rule, peeking, floor, source => source == WorldSource.SharedMaps ? shared : source == WorldSource.WholeWorld ? SourceLevel.Counted : SourceLevel.WithInsides);

        [Fact]
        public void TheNoticeSaysWhatTheServerSetsInOneLine()
        {
            Assert.Equal("This server plays Strict; peeking and Known up to are off.", WorldRulesWords.Notice(Rules(WorldRule.Strict, peeking: false, floor: false)));
            Assert.Equal("This server plays Loose.", WorldRulesWords.Notice(Rules(WorldRule.Loose)));
            Assert.Equal("This server keeps peeking off.", WorldRulesWords.Notice(Rules(peeking: false)));
            Assert.Equal("This server keeps Known up to off.", WorldRulesWords.Notice(Rules(floor: false)));
            Assert.Equal("This server limits what its world shows as known.", WorldRulesWords.Notice(Rules(shared: SourceLevel.Seen)));
            Assert.Equal("This server plays Strict; peeking is off, and it limits what its world shows as known.", WorldRulesWords.Notice(Rules(WorldRule.Strict, peeking: false, shared: SourceLevel.Off)));
        }

        [Fact]
        public void AServerSettingNothingHasNoNotice()
        {
            Assert.Null(WorldRulesWords.Notice(Rules()));
            Assert.Null(WorldRulesWords.Notice(WorldRules.Open));
        }

        [Fact]
        public void TheNoticeIsToldOnceAWorldUntilTheRulesChange()
        {
            var book = new CompendiumBook();
            Assert.False(book.ToldRules("world", "This server plays Strict."));
            book.TellRules("world", "This server plays Strict.");
            var read = CompendiumBook.Load(book.Save());
            Assert.True(read.ToldRules("world", "This server plays Strict."));
            Assert.False(read.ToldRules("world", "This server plays Loose."));
            Assert.False(read.ToldRules("another", "This server plays Strict."));

            // Put back as it was told, or forgotten where it never was.
            read.TellRules("world", "This server plays Loose.");
            read.RulesToldAgain("world", "This server plays Strict.");
            Assert.Equal("This server plays Strict.", read.RulesTold("world"));
            read.RulesToldAgain("world", null);
            Assert.Null(read.RulesTold("world"));
            Assert.False(CompendiumBook.Load(read.Save()).ToldRules("world", "This server plays Strict."));
        }

        [Fact]
        public void ACharacterIsAskedOnceWhetherToKeepACompendium()
        {
            var book = new CompendiumBook();
            Assert.False(book.Welcomed);
            book.Welcome();
            Assert.True(CompendiumBook.Load(book.Save()).Welcomed);

            // One whose rules are chosen needs no question.
            var chose = new CompendiumBook { Rule = CompendiumRule.Loose };
            Assert.True(CompendiumBook.Load(chose.Save()).Welcomed);
        }

        [Fact]
        public void ACharacterOnAServerSettingTheRulesIsNotAsked()
        {
            var book = new CompendiumBook();
            Assert.True(book.WelcomeDue);
            book.Under(Rules(WorldRule.Strict));
            Assert.False(book.WelcomeDue);
            book.Under(Rules());
            Assert.True(book.WelcomeDue);
            book.Welcome();
            Assert.False(book.WelcomeDue);
        }
    }
}
