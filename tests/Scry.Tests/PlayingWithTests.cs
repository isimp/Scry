using Xunit;

namespace Scry.Tests
{
    public class PlayingWithTests
    {
        // What runs in a game without Scry, or with Scry of other messages, never reaches this
        // character, so nothing of it counts. A notice says so, by name, while it is true; closed,
        // it stays closed until another such player joins. The compendium's box lists everyone
        // played with and whether their game has Scry.

        [Fact]
        public void EveryoneOnScrySaysNothing()
        {
            Assert.Null(PlayingWithWords.Notice(new string[0], new string[0]));
        }

        [Fact]
        public void APlayerWithoutScryIsNamed()
        {
            Assert.Equal("Ana plays without Scry: a death in Ana's game you see by its remains, but breaks, births and station work there go unlearned.", PlayingWithWords.Notice(new[] { "Ana" }, new string[0]));
            Assert.Equal("Ana and Bjorn play without Scry: a death in their games you see by its remains, but breaks, births and station work there go unlearned.", PlayingWithWords.Notice(new[] { "Ana", "Bjorn" }, new string[0]));
        }

        [Fact]
        public void AScryOfOtherMessagesIsToldToNeedUpdating()
        {
            Assert.Equal("Bjorn's Scry needs updating: a death in Bjorn's game you see by its remains, but breaks, births and station work there go unlearned.", PlayingWithWords.Notice(new string[0], new[] { "Bjorn" }));
            Assert.Equal("Bjorn's and Cara's Scry need updating: a death in their games you see by its remains, but breaks, births and station work there go unlearned.", PlayingWithWords.Notice(new string[0], new[] { "Bjorn", "Cara" }));
        }

        [Fact]
        public void BothAreToldInOneNotice()
        {
            Assert.Equal("Ana plays without Scry and Bjorn's Scry needs updating: a death in their games you see by its remains, but breaks, births and station work there go unlearned.",
                PlayingWithWords.Notice(new[] { "Ana" }, new[] { "Bjorn" }));
        }

        [Fact]
        public void TheNoticeNamesThosePlayedWithWhoseGamesLackScry()
        {
            var players = new[] { ("Ana", PeerScry.None), ("Bjorn", PeerScry.Outdated), ("Cara", PeerScry.Scry) };
            Assert.Equal(PlayingWithWords.Notice(new[] { "Ana" }, new[] { "Bjorn" }), PlayingWithWords.Notice(players));
            Assert.Equal(new[] { "Ana", "Bjorn" }, PlayingWithWords.Lacking(players));
            Assert.Null(PlayingWithWords.Notice(new[] { ("Cara", PeerScry.Scry) }));
            Assert.Empty(PlayingWithWords.Lacking(new[] { ("Cara", PeerScry.Scry) }));
        }

        [Fact]
        public void TheBoxListsEveryoneAndWhetherTheirGameHasScry()
        {
            Assert.Equal("Ana · no Scry, Bjorn · needs updating, Cara · Scry",
                PlayingWithWords.Row(new[] { ("Ana", PeerScry.None), ("Bjorn", PeerScry.Outdated), ("Cara", PeerScry.Scry) }));
            Assert.Null(PlayingWithWords.Row(new (string, PeerScry)[0]));
        }

        [Fact]
        public void AClosedNoticeStaysClosedUntilAnotherSuchPlayerJoins()
        {
            var notice = new GapNotice();
            Assert.False(notice.Shows(new string[0]));
            Assert.True(notice.Shows(new[] { "Ana" }));
            notice.Close(new[] { "Ana" });
            Assert.False(notice.Shows(new[] { "Ana" }));
            Assert.True(notice.Shows(new[] { "Ana", "Bjorn" }));
            notice.Close(new[] { "Ana", "Bjorn" });
            Assert.False(notice.Shows(new[] { "Bjorn" }));
            // Leaving the world forgets what was closed.
            notice.Forget();
            Assert.True(notice.Shows(new[] { "Ana" }));
        }
    }
}
