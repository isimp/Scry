using Xunit;

namespace Scry.Tests
{
    public class RaidTests
    {
        // RandEventSystem.UpdateRandomEvent rolls every m_eventIntervalMin minutes at m_eventChance,
        // then picks at random among the raids that can start (StartRandomEvent).

        [Fact]
        public void TheRaidRollSaysHowOftenAndThatOneIsPickedAmongThoseThatCan()
        {
            Assert.Equal("every 46 min, 20% each time, one picked among the raids that can start", RaidWords.Roll(46f, 20f));
        }

        [Fact]
        public void ARaidWithATimerOfItsOwnSaysSo()
        {
            // A raid with m_standaloneInterval is rolled on its own besides.
            Assert.Equal("on its own every 50 min, 25% each time", RaidWords.OwnRoll(3000f, 25f));
            Assert.Equal("on its own every 50 min", RaidWords.OwnRoll(3000f, 100f));
            Assert.Null(RaidWords.OwnRoll(0f, 100f));
        }

        [Fact]
        public void ARaidComesForSomeoneInItsBiomesNearTheirBase()
        {
            // RandEventSystem.CheckBase: a base-only raid needs a base value of 3 around the player,
            // counted within 20 m (EffectArea.GetBaseValue).
            Assert.Equal("someone in Meadows, Black Forest with 3 or more base pieces within 20 m", RaidWords.ComesFor("Meadows, Black Forest", nearBaseOnly: true));
            Assert.Equal("someone in Mountain, Plains, base or not", RaidWords.ComesFor("Mountain, Plains", nearBaseOnly: false));
            Assert.Equal("anyone, anywhere", RaidWords.ComesFor("", nearBaseOnly: false));
            Assert.Equal("someone with 3 or more base pieces within 20 m", RaidWords.ComesFor("", nearBaseOnly: true));
        }

        [Fact]
        public void ARaidLastsItsTimePausedWhileNobodyIsInIt()
        {
            Assert.Equal("90 s, paused while nobody is within 96 m", RaidWords.Lasts(90f, pauses: true, range: 96f));
            Assert.Equal("2.5 min", RaidWords.Lasts(150f, pauses: false, range: 96f));
        }

        [Fact]
        public void EachCreatureOfARaidSaysHowManyHowOftenAndHowLikely()
        {
            // SpawnSystem.UpdateSpawnList: every m_spawnInterval, m_spawnChance, while fewer than
            // m_maxSpawned are near.
            Assert.Equal("up to 8 at once, every 10 s at 58%, up to 2 stars", RaidWords.Spawn(8, 10f, 58f, "up to 2 stars"));
            Assert.Equal("up to 2 at once, every 20 s", RaidWords.Spawn(2, 20f, 100f, null));
        }

        [Fact]
        public void ARaidIsOnTheTableFromItsKeyUntilItsEndingKey()
        {
            // RandEventSystem.HaveGlobalKeys: every required key set, none of the others.
            string Boss(string key) => key == "defeated_eikthyr" ? "Eikthyr" : key == "defeated_gdking" ? "The Elder" : null;
            Assert.Equal("from the start", RaidWords.Starts(new string[0], new string[0], Boss));
            Assert.Equal("from the start, until Eikthyr is defeated", RaidWords.Starts(new string[0], new[] { "defeated_eikthyr" }, Boss));
            Assert.Equal("once Eikthyr is defeated, until The Elder is defeated", RaidWords.Starts(new[] { "defeated_eikthyr" }, new[] { "defeated_gdking" }, Boss));
            Assert.Equal("once the world key \"KilledTroll\" is set", RaidWords.Starts(new[] { "KilledTroll", "" }, null, Boss));
        }

        [Fact]
        public void ACreatureWithNoCapComesOneEachTime()
        {
            // With m_maxSpawned at 0 the game spawns at most one each interval and counts none.
            Assert.Equal("one every 5 s", RaidWords.Spawn(0, 5f, 100f, null));
        }
    }
}
