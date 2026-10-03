using Xunit;

namespace Scry.Tests
{
    public class CombatTests
    {
        // Health at a level is the base times the level (Character.SetupMaxHealth); a star is a
        // level above the first. A hit at a level is 1 + half a star each (Attack.GetLevelDamageFactor).

        [Fact]
        public void EachStarAddsTheBaseHealthOnceMore()
        {
            Assert.Equal("1 star 400, 2 stars 600", CombatWords.StarHealth(200f, 2));
        }

        [Fact]
        public void EachStarAddsHalfOfEveryHit()
        {
            Assert.Equal("1 star ×1.5, 2 stars ×2", CombatWords.StarDamage(2));
            Assert.Equal("1 star ×1.5, 2 stars ×2, 3 stars ×2.5", CombatWords.StarDamage(3));
        }

        [Fact]
        public void WithoutStarsThereIsNothingToSay()
        {
            Assert.Null(CombatWords.StarHealth(200f, 0));
            Assert.Null(CombatWords.StarDamage(0));
        }

        [Fact]
        public void DamageIsToldBiggestFirstLeavingOutWhatIsNone()
        {
            Assert.Equal("40 blunt, 20 fire", CombatWords.Damage(new[] { ("fire", 20f), ("slash", 0f), ("blunt", 40f) }));
            Assert.Equal("12.5 pierce", CombatWords.Damage(new[] { ("pierce", 12.5f) }));
            Assert.Null(CombatWords.Damage(new[] { ("blunt", 0f) }));
        }

        [Fact]
        public void AnAttackSaysWhatItDoesHowFarAndHowOften()
        {
            Assert.Equal("40 blunt · a swing, reaching 2.5 m · every 3 s", CombatWords.Attack("40 blunt", "Horizontal", 0f, 2.5f, 3f));
            Assert.Equal("a shot, from 5 to 30 m · every 10 s", CombatWords.Attack(null, "Projectile", 5f, 30f, 10f));
            Assert.Equal("20 fire · around it, reaching 4 m", CombatWords.Attack("20 fire", "Area", 0f, 4f, 0f));
        }

        [Fact]
        public void SightTellsTheWholeFieldOfViewAndThatAnAlertedCreatureSeesAllRound()
        {
            // BaseAI.CanSeeTarget turns away a target more than m_viewAngle off the creature's
            // forward, either side, and only while it is not alerted.
            Assert.Equal("30 m, 180° ahead, all round once alerted", CombatWords.Sight(30f, 90f));
            Assert.Equal("40 m, 120° ahead, all round once alerted", CombatWords.Sight(40f, 60f));
            Assert.Equal("25 m, all round", CombatWords.Sight(25f, 180f));
            Assert.Equal("25 m, all round", CombatWords.Sight(25f, 200f));
        }

        [Fact]
        public void AlertRangeSaysHowNearItMustSeeYouToTurnOnYou()
        {
            // MonsterAI turns alerted on seeing its target within m_alertRange times the target's
            // stealth factor, which is below one only while the target sneaks.
            Assert.Equal("on seeing you within 90 m, nearer while you sneak", CombatWords.Alerted(90f));
            Assert.Equal("on seeing you within 12.5 m, nearer while you sneak", CombatWords.Alerted(12.5f));
        }

        [Fact]
        public void TheGamesUnlimitedAlertRangeIsNotTold()
        {
            // 9,999 is the field's default: whatever it sees, it turns on, which sight already tells.
            Assert.Null(CombatWords.Alerted(9999f));
        }

        [Fact]
        public void NoAlertRangeMeansSightAloneNeverAlertsIt()
        {
            // The check is "nearer than the range", which nothing is at a range of zero.
            Assert.Equal("never on sight alone", CombatWords.Alerted(0f));
        }

        [Fact]
        public void ChaseDistanceSaysHowFarFromItsSpawnItFollows()
        {
            // MonsterAI gives up once farther than m_maxChaseDistance from where it spawned and a
            // second has passed without sensing its target; none is set means no limit.
            Assert.Equal("beyond 500 m from where it spawned, once it has lost you", CombatWords.Chase(500f));
            Assert.Null(CombatWords.Chase(0f));
        }

        [Fact]
        public void ACreatureThePassiveEnemiesSettingDoesNotCalmIsToldSo()
        {
            // With the world setting Passive enemies on, a creature senses no one until provoked
            // (BaseAI.CanSenseTarget), unless it is marked m_passiveAggresive: then it hunts as
            // ever, and an animal, which only flees, flees as ever.
            Assert.Equal("attacks unprovoked all the same", CombatWords.PassiveEnemies(flees: false));
            Assert.Equal("flees from you all the same", CombatWords.PassiveEnemies(flees: true));
        }

        [Fact]
        public void AWeakSpotIsNamedAfterItsPartInPlainWords()
        {
            Assert.Equal("head", CombatWords.PartName("WEAKSPOT_HEAD"));
            Assert.Equal("head", CombatWords.PartName("WeakSpot_head"));
            Assert.Equal("eggs", CombatWords.PartName("WEAKSPOT_EGGS"));
            Assert.Equal("eye", CombatWords.PartName("EYE (1)"));
            Assert.Equal("left horn", CombatWords.PartName("weakspot_left_horn"));
        }

        [Fact]
        public void AWeakSpotWithNoNameLeftIsCalledJustThat()
        {
            Assert.Equal("weak spot", CombatWords.PartName("WeakSpot"));
            Assert.Equal("weak spot", CombatWords.PartName(""));
        }

        [Fact]
        public void ResistancesReadAsOneLineFromMostHarmToLeast()
        {
            var degrees = new[]
            {
                ("Very weak to", new[] { "pierce" }),
                ("Immune to", new[] { "spirit" }),
                ("Unaffected by", new[] { "chop", "pickaxe" }),
            };
            Assert.Equal("very weak to pierce; immune to spirit; unaffected by chop, pickaxe", CombatWords.Resistances(degrees));
        }

        [Fact]
        public void APartResistingNothingReadsAsLikeAnyOtherHit()
        {
            Assert.Equal("takes every hit in full", CombatWords.Resistances(new (string, string[])[0]));
        }

        // An attack's cost is what Attack.HaveStamina and the rest take for it: stamina, eitr,
        // health and a share of health, each only when the attack takes it.
        [Fact]
        public void AnAttackCostsWhatItTakes()
        {
            Assert.Equal(new[] { "20 stamina", "5 eitr", "10 health", "25% health" }, CombatWords.Costs(20f, 5f, 10f, 25f));
            Assert.Equal(new[] { "12.5 stamina" }, CombatWords.Costs(12.5f, 0f, 0f, 0f));
            Assert.Empty(CombatWords.Costs(0f, 0f, 0f, 0f));
        }

        // A weapon's second attack (Attack, as m_secondaryAttack) hits harder or softer than its
        // first by its own multipliers, and costs what it takes.
        [Fact]
        public void ASecondAttackTellsHowItDiffersAndWhatItCosts()
        {
            Assert.Equal("\u00d73 damage, \u00d72 knockback, \u00d71.5 stagger; costs 20 stamina, 4 eitr", CombatWords.SecondaryAttack(3f, 2f, 1.5f, new[] { "20 stamina", "4 eitr" }));
            Assert.Equal("\u00d72 damage", CombatWords.SecondaryAttack(2f, 1f, 1f, new string[0]));
            Assert.Equal("as hard as the first; costs 15 stamina", CombatWords.SecondaryAttack(1f, 1f, 1f, new[] { "15 stamina" }));
            Assert.Equal("as hard as the first", CombatWords.SecondaryAttack(1f, 1f, 1f, null));
        }
    }
}
