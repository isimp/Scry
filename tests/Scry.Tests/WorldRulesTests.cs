using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class WorldRulesTests
    {
        // The world's server may set what the compendium allows, in its config synced to every
        // player by ServerSync: the rules everyone plays by or each player's choice, whether
        // peeking and Known up to are allowed, and the highest level each source may show. The
        // book keeps the player's own choices; what shows is them within what the world allows,
        // and they come back as the world allows more.

        [Fact]
        public void AWorldThatSetsNothingLeavesEveryChoiceToThePlayer()
        {
            var book = new CompendiumBook { Rule = CompendiumRule.Loose, Floor = 2 };
            book.Show(WorldSource.SharedMaps, SourceLevel.WithInsides);
            book.Under(WorldRules.Open);
            Assert.Equal(CompendiumRule.Loose, book.Rule);
            Assert.Equal(2, book.Floor);
            Assert.Equal(SourceLevel.WithInsides, book.Shows(WorldSource.SharedMaps));
            Assert.False(WorldRules.Open.RulesSet);
            Assert.True(WorldRules.Open.Peeking);
            Assert.True(WorldRules.Open.FloorAllowed);
            Assert.All(WorldSources.All, s => Assert.True(WorldRules.Open.Allows(s, WorldSources.Offered(s).Last())));
        }

        [Fact]
        public void TheRulesTheWorldSetsAreEveryonesThePlayersChoiceKeptBeneath()
        {
            var book = new CompendiumBook { Rule = CompendiumRule.Loose };
            var version = book.SourcesVersion;
            book.Under(new WorldRules { Rule = CompendiumRule.Strict });
            Assert.Equal(CompendiumRule.Strict, book.Rule);
            Assert.Equal(CompendiumRule.Loose, book.ChosenRule);
            Assert.True(book.SourcesVersion > version);
            // Chosen while set, the player's own choice changes beneath it.
            book.Rule = CompendiumRule.Off;
            Assert.Equal(CompendiumRule.Strict, book.Rule);
            book.Under(WorldRules.Open);
            Assert.Equal(CompendiumRule.Off, book.Rule);
            // Kept with the character as chosen, never as the world set it.
            book.Under(new WorldRules { Rule = CompendiumRule.Strict });
            Assert.DoesNotContain("rule\tStrict", book.Save());
        }

        [Fact]
        public void KnownUpToNotAllowedShowsNoFloorTheChoiceKept()
        {
            var book = new CompendiumBook { Rule = CompendiumRule.Loose, Floor = 3 };
            book.Under(new WorldRules { FloorAllowed = false });
            Assert.Equal(0, book.Floor);
            Assert.Equal(3, book.ChosenFloor);
            book.Under(WorldRules.Open);
            Assert.Equal(3, book.Floor);
        }

        [Fact]
        public void ASourceShowsNoMoreThanTheWorldAllowsItsChoiceComingBackAsItAllowsMore()
        {
            var book = new CompendiumBook();
            book.Show(WorldSource.SharedMaps, SourceLevel.WithInsides);
            book.Show(WorldSource.WholeWorld, SourceLevel.Counted);
            var rules = new WorldRules();
            rules.UpTo(WorldSource.SharedMaps, SourceLevel.Seen);
            rules.UpTo(WorldSource.WholeWorld, SourceLevel.Off);
            rules.UpTo(WorldSource.Explored, SourceLevel.Off);
            book.Under(rules);
            Assert.Equal(SourceLevel.Seen, book.Shows(WorldSource.SharedMaps));
            Assert.Equal(SourceLevel.WithInsides, book.Chosen(WorldSource.SharedMaps));
            Assert.Equal(SourceLevel.Off, book.Shows(WorldSource.WholeWorld));
            // Your own ground is written as seen as it comes, so no world takes it below that.
            Assert.Equal(SourceLevel.Seen, book.Shows(WorldSource.Explored));
            Assert.True(rules.Allows(WorldSource.SharedMaps, SourceLevel.Seen));
            Assert.False(rules.Allows(WorldSource.SharedMaps, SourceLevel.Known));
            Assert.True(rules.Allows(WorldSource.Explored, SourceLevel.Seen));
            Assert.False(rules.Allows(WorldSource.Explored, SourceLevel.Known));
            Assert.Equal(SourceLevel.Seen, rules.Highest(WorldSource.SharedMaps));
            Assert.Equal(SourceLevel.Seen, rules.Highest(WorldSource.Explored));
            // A cap between two offered levels shows the highest offered beneath it.
            var counted = new WorldRules();
            counted.UpTo(WorldSource.WholeWorld, SourceLevel.Known);
            book.Under(counted);
            Assert.Equal(SourceLevel.Counted, book.Shows(WorldSource.WholeWorld));
            book.Under(WorldRules.Open);
            Assert.Equal(SourceLevel.WithInsides, book.Shows(WorldSource.SharedMaps));
            Assert.DoesNotContain("shows\tSharedMaps\tSeen", book.Save());
        }

        [Fact]
        public void RulesAlikeChangeNothingOthersCountTheViewAgain()
        {
            var a = new WorldRules { Rule = CompendiumRule.Strict, Peeking = false };
            a.UpTo(WorldSource.SharedMaps, SourceLevel.Seen);
            var b = new WorldRules { Rule = CompendiumRule.Strict, Peeking = false };
            b.UpTo(WorldSource.SharedMaps, SourceLevel.Seen);
            Assert.True(a.SameAs(b));
            Assert.False(a.SameAs(WorldRules.Open));
            var book = new CompendiumBook();
            book.Under(a);
            var version = book.SourcesVersion;
            book.Under(b);
            Assert.Equal(version, book.SourcesVersion);
            // What the book plays under is its own copy: rules changed after are not its own until handed again.
            a.UpTo(WorldSource.SharedMaps, SourceLevel.Off);
            Assert.Equal(SourceLevel.Seen, book.World.Highest(WorldSource.SharedMaps));
            b.UpTo(WorldSource.SharedMaps, SourceLevel.Known);
            Assert.False(a.SameAs(b));
            Assert.False(new WorldRules { FloorAllowed = false }.SameAs(WorldRules.Open));
            Assert.False(new WorldRules { Peeking = false }.SameAs(WorldRules.Open));
        }

        [Fact]
        public void TheServersSettingsMakeTheWorldsRules()
        {
            var rules = WorldRules.From(WorldRule.Strict, peeking: false, floor: true, s => s == WorldSource.SharedMaps ? SourceLevel.Seen : SourceLevel.WithInsides);
            Assert.Equal(CompendiumRule.Strict, rules.Rule);
            Assert.False(rules.Peeking);
            Assert.True(rules.FloorAllowed);
            Assert.Equal(SourceLevel.Seen, rules.Highest(WorldSource.SharedMaps));
            Assert.Equal(SourceLevel.Counted, rules.Highest(WorldSource.WholeWorld));
            Assert.Null(WorldRules.From(WorldRule.PlayersChoose, true, true, _ => SourceLevel.WithInsides).Rule);
            Assert.Equal(CompendiumRule.Off, WorldRules.From(WorldRule.Off, true, true, _ => SourceLevel.WithInsides).Rule);
            Assert.Equal(CompendiumRule.Loose, WorldRules.From(WorldRule.Loose, true, false, _ => SourceLevel.WithInsides).Rule);
            Assert.False(WorldRules.From(WorldRule.Loose, true, false, _ => SourceLevel.WithInsides).FloorAllowed);
            Assert.True(WorldRules.From(WorldRule.PlayersChoose, true, true, _ => SourceLevel.WithInsides).SameAs(WorldRules.Open));
        }

        [Fact]
        public void TheBoxSaysWhatTheWorldSetsAndWhy()
        {
            Assert.Equal("This server sets the rules: Strict. An admin changes it in the server's Scry config.", WorldRulesWords.RulesSet(CompendiumRule.Strict));
            Assert.Equal("This server allows no peeking.", WorldRulesWords.NoPeeking);
            Assert.Equal("This server keeps Known up to off.", WorldRulesWords.NoFloor);
            Assert.Equal("This server allows Shared maps up to Seen.", WorldRulesWords.UpTo(WorldSource.SharedMaps, SourceLevel.Seen));
            Assert.Equal("This server allows The whole world up to Off.", WorldRulesWords.UpTo(WorldSource.WholeWorld, SourceLevel.Off));
            // Each source's setting says what it caps and what it takes.
            Assert.Equal("The highest level Shared maps may show in the compendium's box; one chosen above it shows this one. Off, Seen, Known or WithInsides.", WorldRulesWords.UpToSetting(WorldSource.SharedMaps));
            Assert.Equal("The highest level The whole world may show in the compendium's box; one chosen above it shows this one. Off or Counted.", WorldRulesWords.UpToSetting(WorldSource.WholeWorld));
            Assert.Equal("The highest level Ground you explored may show in the compendium's box; one chosen above it shows this one. Seen, Known or WithInsides.", WorldRulesWords.UpToSetting(WorldSource.Explored));
        }
    }
}
