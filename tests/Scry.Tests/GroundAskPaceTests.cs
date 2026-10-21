using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class GroundAskPaceTests
    {
        // Each answer the world's server gives on a ground tells something of where things lie,
        // so a game asking with made-up ground, cell by cell, could learn where every kind of
        // place and every dungeon of the world lies. The server answers each player a few asks of
        // a kind at once, then one a minute (a ground's dungeons one in five), as a game asks of
        // its own; the rest is passed over.

        [Fact]
        public void AGameIsAnsweredAFewAsksAtOnceThenOneAMinute()
        {
            var pace = new GroundAskPace();
            for (var i = 0; i < GroundAskPace.Burst(GroundAsk.Explored); i++) Assert.True(pace.Answers("steam:1", GroundAsk.Explored, 0));
            Assert.False(pace.Answers("steam:1", GroundAsk.Explored, 0));
            Assert.False(pace.Answers("steam:1", GroundAsk.Explored, 30));
            Assert.True(pace.Answers("steam:1", GroundAsk.Explored, 61));
            Assert.False(pace.Answers("steam:1", GroundAsk.Explored, 62));
            Assert.Equal(3, pace.PassedOver);
        }

        [Fact]
        public void AGroundsDungeonsAreAnsweredAFewAtOnceThenOneInFiveMinutes()
        {
            var pace = new GroundAskPace();
            for (var i = 0; i < GroundAskPace.Burst(GroundAsk.Dungeons); i++) Assert.True(pace.Answers("steam:1", GroundAsk.Dungeons, 0));
            Assert.False(pace.Answers("steam:1", GroundAsk.Dungeons, 0));
            Assert.False(pace.Answers("steam:1", GroundAsk.Dungeons, 200));
            Assert.True(pace.Answers("steam:1", GroundAsk.Dungeons, 301));
        }

        [Fact]
        public void AGameAskingAtItsOwnPaceIsNeverPassedOver()
        {
            var pace = new GroundAskPace();
            // The explored map on joining, then the ground new since at most once a minute, a
            // second either way as the network carries it.
            Assert.True(pace.Answers("steam:1", GroundAsk.Explored, 0));
            for (var t = 60.0; t < 3600; t += 60) Assert.True(pace.Answers("steam:1", GroundAsk.Explored, t + (t % 120 == 0 ? -1 : 1)), $"explored at {t}");
            // Shared ground on joining and after two maps read at a table in the first minute, then once a minute.
            Assert.True(pace.Answers("steam:1", GroundAsk.Shared, 0));
            Assert.True(pace.Answers("steam:1", GroundAsk.Shared, 10));
            Assert.True(pace.Answers("steam:1", GroundAsk.Shared, 20));
            for (var t = 80.0; t < 3600; t += 60) Assert.True(pace.Answers("steam:1", GroundAsk.Shared, t), $"shared at {t}");
            // Both grounds as their pins go on, each turned on once more in the first minute, then
            // each every ten minutes, a few seconds apart.
            foreach (var t in new[] { 0.0, 0.5, 20.0, 40.0 }) Assert.True(pace.Answers("steam:1", GroundAsk.Dungeons, t), $"dungeons at {t}");
            for (var t = 640.0; t < 3600; t += 600)
            {
                Assert.True(pace.Answers("steam:1", GroundAsk.Dungeons, t), $"explored ground's dungeons at {t}");
                Assert.True(pace.Answers("steam:1", GroundAsk.Dungeons, t + 2), $"shared ground's dungeons at {t + 2}");
            }
            Assert.Equal(0, pace.PassedOver);
        }

        [Fact]
        public void AGameProbingCellByCellLearnsLittleInAnHour()
        {
            var pace = new GroundAskPace();
            var explored = Enumerable.Range(0, 3600).Count(t => pace.Answers("steam:1", GroundAsk.Explored, t));
            var dungeons = Enumerable.Range(0, 3600).Count(t => pace.Answers("steam:1", GroundAsk.Dungeons, t));
            Assert.InRange(explored, 1, 3 + 60);
            Assert.InRange(dungeons, 1, 4 + 12);
        }

        [Fact]
        public void PlayersAndKindsOfAskArePacedApart()
        {
            var pace = new GroundAskPace();
            for (var i = 0; i < 3; i++) pace.Answers("steam:1", GroundAsk.Explored, 0);
            Assert.False(pace.Answers("steam:1", GroundAsk.Explored, 0));
            Assert.True(pace.Answers("steam:2", GroundAsk.Explored, 0));
            Assert.True(pace.Answers("steam:1", GroundAsk.Shared, 0));
            Assert.True(pace.Answers("steam:1", GroundAsk.Dungeons, 0));
        }

        [Fact]
        public void LeavingAndJoiningAgainStartsNothingAfreshButLongQuietForgets()
        {
            // A player is told apart by their connection's platform id, not by the connection, so
            // joining again gives no new asks; one quiet long enough to be answered as a newcomer
            // is let go of.
            var pace = new GroundAskPace();
            for (var i = 0; i < 3; i++) pace.Answers("steam:1", GroundAsk.Explored, 0);
            pace.Forget(30);
            Assert.Equal(1, pace.Held);
            Assert.False(pace.Answers("steam:1", GroundAsk.Explored, 31));
            pace.Forget(31 + 3 * 60);
            Assert.Equal(0, pace.Held);
            Assert.True(pace.Answers("steam:1", GroundAsk.Explored, 31 + 3 * 60));
        }

        [Fact]
        public void APlayerWithoutAnIdIsPacedAsOne()
        {
            var pace = new GroundAskPace();
            for (var i = 0; i < 3; i++) Assert.True(pace.Answers(null, GroundAsk.Shared, 0));
            Assert.False(pace.Answers("", GroundAsk.Shared, 0));
        }
    }
}
