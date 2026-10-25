using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class EventsBetweenGamesTests
    {
        // Each Scry says hello as it joins, with the version of its messages, so every Scry knows
        // who plays with Scry. The game that runs an event sends it to those who did it, wherever
        // they are, and to those near enough to have seen it, if they have Scry; each applies it by
        // the same rule as alone. A game without Scry, or with other messages, is told by name.

        private const long Me = 101, Ana = 202, Bjorn = 303, Cara = 404;

        private static ScryPeers Roster(params (long Session, int Version)[] hellos)
        {
            var peers = new ScryPeers();
            foreach (var (session, version) in hellos) peers.Hello(session, version);
            return peers;
        }

        [Fact]
        public void APlayerWhoSaidHelloWithTheSameMessagesHasScry()
        {
            var peers = Roster((Ana, ScryPeers.Messages));
            Assert.True(peers.Has(Ana));
            Assert.False(peers.Has(Bjorn));
        }

        [Fact]
        public void OtherMessagesCountAsNoScryAndAreToldApart()
        {
            var peers = Roster((Ana, ScryPeers.Messages + 1));
            Assert.False(peers.Has(Ana));
            Assert.True(peers.Outdated(Ana));
            Assert.False(peers.Outdated(Bjorn));
        }

        private static readonly (long Session, string Name)[] Nobody = new (long, string)[0];

        [Fact]
        public void APlayerWhoLeftIsForgottenHelloAndAll()
        {
            var peers = Roster((Ana, ScryPeers.Messages), (Bjorn, ScryPeers.Messages));
            peers.InWorld(new[] { (Ana, "Ana"), (Bjorn, "Bjorn") }, 0, 0.0);
            peers.InWorld(new[] { (Bjorn, "Bjorn") }, 0, 1.0);
            Assert.False(peers.Has(Ana));
            Assert.True(peers.Has(Bjorn));
            // Gone, they are not played with however long after.
            Assert.Equal(new[] { ("Bjorn", PeerScry.Scry) }, peers.PlayingWith(Me, 1.0 + ScryPeers.HelloWait));
        }

        [Fact]
        public void APlayerRisingAgainAfterDyingIsStillPlayedWith()
        {
            // While they rise again the game lists them without a character, so by no session:
            // whoever is missing may be them, and is kept until every player is accounted for.
            var peers = Roster((Ana, ScryPeers.Messages));
            peers.InWorld(new[] { (Me, "Me"), (Ana, "Ana") }, 0, 0.0);
            peers.InWorld(new[] { (Me, "Me") }, 1, 5.0);
            Assert.True(peers.Has(Ana));
            Assert.Equal(new[] { ("Ana", PeerScry.Scry) }, peers.PlayingWith(Me, 5.0));
            peers.InWorld(new[] { (Me, "Me"), (Ana, "Ana") }, 0, 30.0);
            Assert.Equal(new[] { ("Ana", PeerScry.Scry) }, peers.PlayingWith(Me, 30.0));
        }

        [Fact]
        public void AHelloHeardBeforeThePlayerShowsInTheWorldIsKept()
        {
            // The hello and the game's list of players travel apart: either may come first.
            var peers = Roster((Ana, ScryPeers.Messages));
            peers.InWorld(Nobody, 0, 0.0);
            Assert.Empty(peers.PlayingWith(Me, 0.0));
            peers.InWorld(new[] { (Ana, "Ana") }, 0, 1.0);
            Assert.True(peers.Has(Ana));
            Assert.Equal(new[] { ("Ana", PeerScry.Scry) }, peers.PlayingWith(Me, 1.0));
        }

        [Fact]
        public void ThePlayersPlayedWithAreListedByNameWithWhetherTheirGamesHaveScryNeverTheCharacterItself()
        {
            var peers = Roster((Ana, ScryPeers.Messages), (Cara, ScryPeers.Messages + 1));
            peers.InWorld(new[] { (Me, "Me"), (Cara, "Cara"), (Bjorn, "bjorn"), (Ana, "Ana") }, 0, 0.0);
            Assert.Equal(new[] { ("Ana", PeerScry.Scry), ("bjorn", PeerScry.None), ("Cara", PeerScry.Outdated) },
                peers.PlayingWith(Me, ScryPeers.HelloWait));
        }

        [Fact]
        public void APlayerJustComeIntoTheWorldIsNotYetToldToLackScry()
        {
            // Their hello may still be on its way: the wait goes from when they came, however often
            // the world is looked at meanwhile.
            var peers = new ScryPeers();
            peers.InWorld(new[] { (Ana, "Ana") }, 0, 100.0);
            peers.InWorld(new[] { (Ana, "Ana") }, 0, 105.0);
            Assert.Empty(peers.PlayingWith(Me, 100.0 + ScryPeers.HelloWait - 1.0));
            Assert.Equal(new[] { ("Ana", PeerScry.None) }, peers.PlayingWith(Me, 100.0 + ScryPeers.HelloWait));
            // A hello tells at once.
            peers.Hello(Bjorn, ScryPeers.Messages);
            peers.InWorld(new[] { (Ana, "Ana"), (Bjorn, "Bjorn") }, 0, 1000.0);
            Assert.Equal(new[] { ("Ana", PeerScry.None), ("Bjorn", PeerScry.Scry) }, peers.PlayingWith(Me, 1000.0));
        }

        private static WorldEvent Death(float x, params long[] doers) =>
            new WorldEvent(Me, 7, Happened.Died, "Troll", x, 0f, 0f, new[] { ("TrollHide", 2) }, 2, doers);

        [Fact]
        public void AnEventReadsBackWholeFromItsMessage()
        {
            var sent = new WorldEvent(Ana, 42, Happened.Ate, "Boar", 1.5f, -2f, 3.25f, new[] { ("Raspberry", 1), ("Blueberries", 3) }, 3, new[] { Ana, Bjorn }, "Raspberry", "7:12");
            var read = EventWire.Read(EventWire.Write(sent));
            Assert.NotNull(read);
            Assert.Equal((sent.Game, sent.Number, sent.What, sent.Thing), (read.Game, read.Number, read.What, read.Thing));
            Assert.Equal((sent.X, sent.Y, sent.Z, sent.Level, sent.With, sent.Mark), (read.X, read.Y, read.Z, read.Level, read.With, read.Mark));
            Assert.Equal(sent.Came, read.Came);
            Assert.Equal(sent.Doers, read.Doers);
        }

        [Fact]
        public void AnEventWithNothingCameAndNoOneWhoDidItReadsBack()
        {
            var sent = new WorldEvent(Ana, 1, Happened.Born, "Wolf", 0f, 0f, 0f);
            var read = EventWire.Read(EventWire.Write(sent));
            Assert.Empty(read.Came);
            Assert.Empty(read.Doers);
            Assert.Null(read.With);
            Assert.Null(read.Mark);
        }

        [Fact]
        public void TheMessagesAreTheSecondKindSinceAnEventCarriesItsMark()
        {
            // A Scry of the first kind reads nothing of these and is told apart as needing an update.
            Assert.Equal(2, ScryPeers.Messages);
        }

        [Fact]
        public void AMessageOfOtherMessagesOrNoneAtAllReadsAsNothing()
        {
            var bytes = EventWire.Write(Death(0f, Me));
            bytes[0] ^= 0x7F;
            Assert.Null(EventWire.Read(bytes));
            Assert.Null(EventWire.Read(new byte[] { 1, 2, 3 }));
            Assert.Null(EventWire.Read(null));
            var cut = EventWire.Write(Death(0f, Me));
            Assert.Null(EventWire.Read(cut.Take(cut.Length - 3).ToArray()));
            // The right mark, but other messages after it.
            var other = EventWire.Write(Death(0f, Me));
            other[4] ^= 0x01;
            Assert.Null(EventWire.Read(other));
        }

        private static readonly ScryPeers Everyone = Roster((Ana, ScryPeers.Messages), (Bjorn, ScryPeers.Messages), (Cara, ScryPeers.Messages));

        private static List<long> To(WorldEvent told, ScryPeers peers, params (long Session, float X)[] players) =>
            EventRecipients.For(told, Me, players.Select(p => (p.Session, p.X, 0f, 0f)).ToList(), peers.Has);

        [Fact]
        public void ThoseWhoDidItGetItWhereverTheyAre()
        {
            // Ana hit the troll from far off; her player is not near it, nor even loaded here.
            Assert.Equal(new[] { Ana }, To(Death(0f, Ana), Everyone, (Bjorn, 500f)));
        }

        [Fact]
        public void ThoseNearEnoughToHaveSeenItGetIt()
        {
            // Within sight and a margin for where their game has them by now; the rule is theirs to apply.
            var got = To(Death(0f), Everyone, (Ana, 20f), (Bjorn, 34f), (Cara, 60f));
            Assert.Equal(new[] { Ana, Bjorn }, got);
        }

        [Fact]
        public void NeverTheGameThatRanItNorOneWithoutScryAndEachOnce()
        {
            var peers = Roster((Me, ScryPeers.Messages), (Ana, ScryPeers.Messages));
            var got = To(Death(5f, Me, Ana, Bjorn), peers, (Me, 0f), (Ana, 5f), (Bjorn, 5f));
            Assert.Equal(new[] { Ana }, got);
        }
    }
}
