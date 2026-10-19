using Xunit;

namespace Scry.Tests
{
    public class TestEventsTests
    {
        // The self-test sees events cross between games as a friend's game sends them: the game
        // running the world runs made-up events as its own and chooses who gets them by the rules
        // for every event. It runs only things the self-test makes up, a few at a time, and sends
        // each only to the game that asked, and a made-up event reaches no book but the self-test's
        // own, should one come late or reach another player.

        [Fact]
        public void TheWorldsGameRunsOnlyThingsTheSelfTestMakesUp()
        {
            Assert.True(TestEvents.Runs(TestEvents.Prefix + "CreatureNear"));
            Assert.False(TestEvents.Runs("Troll"));
            Assert.False(TestEvents.Runs("scrymadeupcreature"));
            Assert.False(TestEvents.Runs(null));
        }

        [Fact]
        public void AFewAreRunAtATime()
        {
            Assert.Equal(3, TestEvents.HowMany(3));
            Assert.Equal(TestEvents.Most, TestEvents.HowMany(TestEvents.Most + 50));
            Assert.Equal(0, TestEvents.HowMany(-1));
        }

        [Fact]
        public void ATestEventGoesOnlyToTheGameThatAskedAndOnlyWhereTheRulesChoseIt()
        {
            const long Asker = 101, Friend = 202;
            Assert.True(TestEvents.ToAsker(new[] { Friend, Asker }, Asker));
            Assert.False(TestEvents.ToAsker(new[] { Friend }, Asker));
            Assert.False(TestEvents.ToAsker(new long[0], Asker));
        }

        [Fact]
        public void TheEventsTakenLatelyAreKeptTheOldestForgotten()
        {
            var taken = new TakenLately();
            taken.Took(7, 1);
            Assert.True(taken.Has(7, 1));
            Assert.False(taken.Has(7, 2));
            Assert.False(taken.Has(8, 1));
            for (var i = 2; i <= TakenLately.Kept + 1; i++) taken.Took(7, i);
            Assert.False(taken.Has(7, 1));
            Assert.True(taken.Has(7, 2));
            Assert.True(taken.Has(7, TakenLately.Kept + 1));
        }

        [Fact]
        public void AnEventTakenTwiceTakesOneRoom()
        {
            var taken = new TakenLately();
            taken.Took(7, 1);
            taken.Took(7, 1);
            for (var i = 2; i <= TakenLately.Kept; i++) taken.Took(7, i);
            Assert.True(taken.Has(7, 1));
        }

        [Fact]
        public void AMadeUpEventReachesNoBookButTheSelfTestsOwn()
        {
            Assert.False(TestEvents.Reaches(TestEvents.Prefix + "CreatureNear", recordingForTest: false));
            Assert.True(TestEvents.Reaches(TestEvents.Prefix + "CreatureNear", recordingForTest: true));
            Assert.True(TestEvents.Reaches("Troll", recordingForTest: false));
            Assert.True(TestEvents.Reaches("Troll", recordingForTest: true));
        }
    }
}
