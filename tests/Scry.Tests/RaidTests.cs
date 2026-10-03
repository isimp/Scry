using System.Collections.Generic;
using System.Linq;
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
        public void ARaidsCreaturesKeepComingWhileItLasts()
        {
            // There are no waves: for as long as the raid lasts, each creature is rolled again at
            // its pace (SpawnSystem.UpdateSpawnList), one with a most topped up to it near you,
            // one without one more each time.
            Assert.Equal("throughout the raid, each creature is rolled again at its pace and topped up to its most near you, so the fallen are replaced", RaidWords.KeepsComing(2, 2));
            Assert.Equal("throughout the raid, each creature is rolled again at its pace, one more each time", RaidWords.KeepsComing(0, 1));
            Assert.Equal("throughout the raid, each creature is rolled again at its pace, those with a most topped up to it near you, so the fallen are replaced", RaidWords.KeepsComing(1, 2));
            Assert.Null(RaidWords.KeepsComing(0, 0));
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

        [Fact]
        public void ABossFightIsNamedForItsBossAndOnWhileTheBossIsAlertedNearYou()
        {
            // RandEventSystem.GetForcedEvent: while EnemyHud shows a boss (EnemyHud.TestShow: alerted,
            // within m_maxShowDistanceBoss), the event its Character.m_bossEvent names is on.
            Assert.Equal("Fighting Eikthyr", RaidWords.Fighting("Eikthyr"));
            Assert.Equal("while Eikthyr is alerted within 100 m of you, its health bar showing", RaidWords.WhileFighting("Eikthyr", 100f));
        }

        [Fact]
        public void TheRaidsTabListsRaidsFirstThenBossFightsThenTheRest()
        {
            // A boss's event is its fight's music and weather, not a raid, even if it could be rolled.
            Assert.Equal(RaidRole.Raid, RaidGrouping.Role(random: true, standaloneInterval: 0f, namedByBoss: false));
            Assert.Equal(RaidRole.Raid, RaidGrouping.Role(random: false, standaloneInterval: 3000f, namedByBoss: false));
            Assert.Equal(RaidRole.BossFight, RaidGrouping.Role(random: false, standaloneInterval: 0f, namedByBoss: true));
            Assert.Equal(RaidRole.BossFight, RaidGrouping.Role(random: true, standaloneInterval: 0f, namedByBoss: true));
            Assert.Equal(RaidRole.Other, RaidGrouping.Role(random: false, standaloneInterval: 0f, namedByBoss: false));

            var groups = new[] { RaidRole.Raid, RaidRole.BossFight, RaidRole.Other }.Select(Groups.Raid).ToList();
            Assert.Equal(new[] { "Raids", "Boss fights", "Started by something else" }, groups.Select(g => g.Name));
            Assert.True(groups[0].Order < groups[1].Order && groups[1].Order < groups[2].Order);
        }

        [Fact]
        public void ARaidIsAsFarAlongAsTheStrongestBossItWaitsFor()
        {
            float Health(string key) => key == "defeated_eikthyr" ? 500f : key == "defeated_dragon" ? 7500f : 0f;
            Assert.Equal(7500f, RaidGrouping.Strength(new[] { "defeated_eikthyr", "defeated_dragon", "KilledTroll" }, Health));
            Assert.Equal(500f, RaidGrouping.Strength(new[] { "defeated_eikthyr" }, Health));
            Assert.Equal(0f, RaidGrouping.Strength(new string[0], Health));
            Assert.Equal(0f, RaidGrouping.Strength(null, Health));
        }

        [Fact]
        public void WithinAGroupTheyComeInTheOrderTheBossesAreFought()
        {
            // By the boss's health, which grows boss by boss (500 for Eikthyr to 20,000 for the
            // Fader), so a mod's boss falls in place too; those waiting for none come first, and
            // those alike stay in name order.
            var ranks = RaidGrouping.Ranks(new Dictionary<string, float>
            {
                ["army_moder"] = 7500f,
                ["wolves"] = 7500f,
                ["army_eikthyr"] = 500f,
                ["boars"] = 0f,
                ["army_goblin"] = 10000f,
            });
            Assert.Equal(0, ranks["boars"]);
            Assert.Equal(1, ranks["army_eikthyr"]);
            Assert.Equal(2, ranks["army_moder"]);
            Assert.Equal(2, ranks["wolves"]);
            Assert.Equal(3, ranks["army_goblin"]);
        }

        [Fact]
        public void RanksStopAtTheHighestTheListCanSortBy()
        {
            var many = Enumerable.Range(0, 100).ToDictionary(i => "raid" + i, i => (float)i);
            var ranks = RaidGrouping.Ranks(many);
            Assert.Equal(63, ranks["raid63"]);
            Assert.Equal(63, ranks["raid99"]);
            Assert.Equal(62, ranks["raid62"]);
        }

        // A raid's biomes are where the game rolls it (RandEventSystem.InValidBiome): those set,
        // or every biome when none is set. A boss's fight and what something else starts are never
        // rolled, so no biome is theirs, and no biome page lists them.
        [Fact]
        public void OnlyARolledRaidHasBiomesEveryOneWhenNoneIsSet()
        {
            var every = new[] { "Meadows", "BlackForest", "Swamp" };

            Assert.Equal(new[] { "Swamp" }, RaidGrouping.Biomes(RaidRole.Raid, new[] { "Swamp" }, every));
            Assert.Equal(every, RaidGrouping.Biomes(RaidRole.Raid, new string[0], every));
            Assert.Empty(RaidGrouping.Biomes(RaidRole.BossFight, every, every));
            Assert.Empty(RaidGrouping.Biomes(RaidRole.Other, new[] { "Swamp" }, every));
        }
    
        // A world set to pick raids by each player's own progress (the PlayerEvents world key)
        // checks a raid's conditions for a player where it sets any (RandEventSystem): keys any
        // or all of which the player has, items known or not, keys the player has not.

        [Fact]
        public void ARaidComesByEachPlayersProgressOnlyInAWorldSetSoAndWhereItSetsConditionsForAPlayer()
        {
            Assert.False(RaidGrouping.ByEachPlayer(false, 1, 1, 1, 1, 1));
            Assert.False(RaidGrouping.ByEachPlayer(true, 0, 0, 0, 0, 0));
            Assert.True(RaidGrouping.ByEachPlayer(true, 1, 0, 0, 0, 0));
            Assert.True(RaidGrouping.ByEachPlayer(true, 0, 2, 0, 0, 0));
            Assert.True(RaidGrouping.ByEachPlayer(true, 0, 0, 1, 0, 0));
            Assert.True(RaidGrouping.ByEachPlayer(true, 0, 0, 0, 1, 0));
            Assert.True(RaidGrouping.ByEachPlayer(true, 0, 0, 0, 0, 3));
        }

        // What each kind of damage puts on what it hits, one table for the item's facts and its links.

        [Fact]
        public void EachElementalDamageNamesTheStatusItGives()
        {
            Assert.Equal(new[] { "fire", "frost", "lightning", "poison", "spirit" }, CombatWords.DamageEffects.Select(d => d.Damage));
            Assert.Equal(new[] { "Burning", "Frost", "Lightning", "Poison", "Spirit" }, CombatWords.DamageEffects.Select(d => d.Effect));
        }
}
}
