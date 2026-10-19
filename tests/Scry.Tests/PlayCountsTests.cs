using Xunit;

namespace Scry.Tests
{
    public class PlayCountsTests
    {
        // Each hook of the compendium counts how often it fired this session, so ordinary play
        // shows which really ran on this machine: a zero where it surely happened is a fault.

        [Fact]
        public void EachKindOfThingPlayRecordsIsCountedOnItsOwn()
        {
            var play = new PlayCounts();
            play.Note(PlayEvent.HitTold);
            play.Note(PlayEvent.HitTold);
            play.Note(PlayEvent.BreakCounted);

            Assert.Equal(2, play.Of(PlayEvent.HitTold));
            Assert.Equal(1, play.Of(PlayEvent.BreakCounted));
            Assert.Equal(0, play.Of(PlayEvent.Born));
        }

        [Fact]
        public void TheCountsAreToldWithEveryKindTheNoneAmongThem()
        {
            // A zero is as telling as any count, so every kind is said.
            var play = new PlayCounts();
            play.Note(PlayEvent.HitTold);
            play.Note(PlayEvent.HitTold);
            play.Note(PlayEvent.WeakSpotHit);
            play.Note(PlayEvent.LootCounted);

            Assert.Equal("this session: 2 hits told, 1 weak spot hit, 0 hits taken, 1 kill's loot counted, 0 breaks counted, "
                + "0 births seen, 0 seen grow up, 0 seen grow back, 0 tamed, 0 map reads, "
                + "0 picks counted, 0 chests counted, 0 starred falls seen, 0 spawns seen, 0 collected from, 0 weathers seen", play.Told());
        }
    }
}
