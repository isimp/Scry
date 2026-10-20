using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ServerScryTests
    {
        // Whether the world's server runs Scry is known from its hello, a moment after joining:
        // heard, its Scry is asked; not heard within the wait, it runs none, and nothing is asked
        // or waited for. Where this game runs the world, it is its own server. The compendium's
        // box names the server first among those played with, and says what a server without
        // Scry leaves out.

        [Fact]
        public void TheServersHelloTellsWhetherItRunsScry()
        {
            Assert.Equal(ServerScry.Here, ServerScries.Of(runsWorld: true, heard: false, outdated: false, sinceJoined: 0));
            Assert.Equal(ServerScry.Waiting, ServerScries.Of(false, false, false, ServerScries.HelloWait - 0.5));
            Assert.Equal(ServerScry.None, ServerScries.Of(false, false, false, ServerScries.HelloWait));
            Assert.Equal(ServerScry.Scry, ServerScries.Of(false, true, false, 1));
            Assert.Equal(ServerScry.Outdated, ServerScries.Of(false, false, true, 1));
            Assert.Equal(10.0, ServerScries.HelloWait);
        }

        [Fact]
        public void OnlyAServerWithScryOrThisGameIsAsked()
        {
            Assert.True(ServerScries.Answers(ServerScry.Here));
            Assert.True(ServerScries.Answers(ServerScry.Scry));
            Assert.False(ServerScries.Answers(ServerScry.Waiting));
            Assert.False(ServerScries.Answers(ServerScry.None));
            Assert.False(ServerScries.Answers(ServerScry.Outdated));
            // Waiting is neither asked nor given up on.
            Assert.False(ServerScries.GivesUp(ServerScry.Waiting));
            Assert.True(ServerScries.GivesUp(ServerScry.None));
            Assert.True(ServerScries.GivesUp(ServerScry.Outdated));
        }

        [Fact]
        public void TheBoxNamesTheServerFirstAmongThosePlayedWith()
        {
            var players = new List<(string, PeerScry)> { ("Ana", PeerScry.None), ("Cara", PeerScry.Scry) };
            Assert.Equal("The server · Scry, Ana · no Scry, Cara · Scry", PlayingWithWords.Row(players, ServerScry.Scry));
            Assert.Equal("The server · no Scry", PlayingWithWords.Row(new List<(string, PeerScry)>(), ServerScry.None));
            Assert.Equal("The server · needs updating", PlayingWithWords.Row(new List<(string, PeerScry)>(), ServerScry.Outdated));
            // Where this game runs the world, or before the hello's wait is over, no server is named.
            Assert.Equal("Ana · no Scry, Cara · Scry", PlayingWithWords.Row(players, ServerScry.Here));
            Assert.Null(PlayingWithWords.Row(new List<(string, PeerScry)>(), ServerScry.Waiting));
        }

        [Fact]
        public void ItsHintSaysWhatAServerWithoutScryLeavesOut()
        {
            Assert.Equal(PlayingWithWords.RowHint, PlayingWithWords.Hint(ServerScry.Scry));
            Assert.Equal(PlayingWithWords.RowHint, PlayingWithWords.Hint(ServerScry.Here));
            Assert.Equal("The server runs no Scry: what your explored and shared maps show, the world's census and the server's dungeon records are left out, and places count as you meet them. " + PlayingWithWords.RowHint,
                PlayingWithWords.Hint(ServerScry.None));
            Assert.Equal("The server's Scry needs updating: what your explored and shared maps show, the world's census and the server's dungeon records are left out, and places count as you meet them. " + PlayingWithWords.RowHint,
                PlayingWithWords.Hint(ServerScry.Outdated));
        }
    }
}
