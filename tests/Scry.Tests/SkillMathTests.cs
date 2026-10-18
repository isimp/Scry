using Xunit;

namespace Scry.Tests
{
    public class SkillMathTests
    {
        // The game counts a skill's level whole, with what effects add (Skills.GetSkillLevel), and
        // most of its rules take that as a share of 100 that stops at 100 (Skills.GetSkillFactor).

        [Fact]
        public void YourLevelIsWholeWithWhatEffectsAdd()
        {
            Assert.Equal(34f, SkillMath.Level(34.9f, 0f));
            Assert.Equal(54f, SkillMath.Level(34.2f, 20f));
            // A death leaves a part of a level, which the game does not count.
            Assert.Equal(38f, SkillMath.Level(38.95f, 0f));
            Assert.Equal(120f, SkillMath.Level(100f, 20f));
        }

        [Fact]
        public void ASkillIsLearnedAtLevelOneOfItsOwnNotBeforeAndNotByEffects()
        {
            Assert.False(SkillMath.Learned(0f));
            Assert.False(SkillMath.Learned(0.95f));
            Assert.True(SkillMath.Learned(1f));
            Assert.True(SkillMath.Learned(38.95f));
        }

        [Fact]
        public void ALevelPastTheTopCountsAsTheTopForMostRules()
        {
            Assert.Equal(0.34f, SkillMath.Share(34f), 3);
            Assert.Equal(1f, SkillMath.Share(120f));
            Assert.Equal(0f, SkillMath.Share(-5f));
        }

        [Fact]
        public void AHitDealsAShareOfItsDamageThatGrowsWithTheSkill()
        {
            // Skills.GetRandomSkillRange: from 0.4 at no skill to 1 at the top, 0.15 either way, kept within 0 and 1.
            var none = SkillMath.HitShare(0f);
            Assert.Equal(0.25f, none.Least, 3);
            Assert.Equal(0.55f, none.Most, 3);
            var some = SkillMath.HitShare(34f);
            Assert.Equal(0.454f, some.Least, 3);
            Assert.Equal(0.754f, some.Most, 3);
            var top = SkillMath.HitShare(100f);
            Assert.Equal(0.85f, top.Least, 3);
            Assert.Equal(1f, top.Most, 3);
            Assert.Equal(1f, SkillMath.HitShare(140f).Most, 3);
        }

        [Fact]
        public void AnAttackCostsAThirdLessAtTheTop()
        {
            // Attack.GetAttackStamina, GetAttackEitr, GetAttackHealth and the bow's draw drain: less by a third of the share.
            Assert.Equal(1f, SkillMath.CostShare(0f), 3);
            Assert.Equal(0.8878f, SkillMath.CostShare(34f), 3);
            Assert.Equal(0.67f, SkillMath.CostShare(100f), 3);
        }

        [Fact]
        public void ABowDrawsFiveTimesAsFastAtTheTop()
        {
            // Humanoid.GetAttackDrawPercentage: from the bow's own time down to a fifth of it.
            Assert.Equal(2.5f, SkillMath.DrawTime(2.5f, 0f), 3);
            Assert.Equal(1.82f, SkillMath.DrawTime(2.5f, 34f), 3);
            Assert.Equal(0.5f, SkillMath.DrawTime(2.5f, 100f), 3);
        }

        [Fact]
        public void ACrossbowReloadsTwiceAsFastAtTheTop()
        {
            // ItemDrop.ItemData.GetWeaponLoadingTime: from its own time down to half of it.
            Assert.Equal(3.5f, SkillMath.ReloadTime(3.5f, 0f), 3);
            Assert.Equal(2.905f, SkillMath.ReloadTime(3.5f, 34f), 3);
            Assert.Equal(1.75f, SkillMath.ReloadTime(3.5f, 100f), 3);
        }

        [Fact]
        public void BlockingBlocksHalfAsMuchAgainAtTheTop()
        {
            // ItemDrop.ItemData.GetBlockPower: its block and half of it times the share.
            Assert.Equal(10f, SkillMath.Block(10f, 0f), 3);
            Assert.Equal(11.7f, SkillMath.Block(10f, 34f), 3);
            Assert.Equal(15f, SkillMath.Block(10f, 100f), 3);
        }

        [Fact]
        public void RunningIsFasterAndCostsHalfTheStaminaAtTheTop()
        {
            // Player.GetRunSpeedFactor: a quarter faster; Player.CheckRun: drain down to half.
            Assert.Equal(1f, SkillMath.RunSpeedShare(0f), 3);
            Assert.Equal(1.1025f, SkillMath.RunSpeedShare(41f), 3);
            Assert.Equal(1.25f, SkillMath.RunSpeedShare(100f), 3);
            Assert.Equal(1f, SkillMath.RunDrainShare(0f), 3);
            Assert.Equal(0.795f, SkillMath.RunDrainShare(41f), 3);
            Assert.Equal(0.5f, SkillMath.RunDrainShare(100f), 3);
        }

        [Fact]
        public void AFigureBetweenTwoEndsMovesWithTheSkill()
        {
            // Player.OnSwimming, FishingFloat.FixedUpdate and the scythe's reach go from the figure at no skill to the one at the top.
            Assert.Equal(6f, SkillMath.Between(6f, 3f, 0f), 3);
            Assert.Equal(4.5f, SkillMath.Between(6f, 3f, 50f), 3);
            Assert.Equal(3f, SkillMath.Between(6f, 3f, 100f), 3);
            Assert.Equal(2.5f, SkillMath.Between(1.5f, 2.5f, 130f), 3);
        }

        [Fact]
        public void JumpingIsStrongerByTwoFifthsAtTheTop()
        {
            // Character.Jump: up and forward alike.
            Assert.Equal(1f, SkillMath.JumpShare(0f), 3);
            Assert.Equal(1.2f, SkillMath.JumpShare(50f), 3);
            Assert.Equal(1.4f, SkillMath.JumpShare(100f), 3);
        }

        [Fact]
        public void ADodgeCostsHalfTheStaminaAtTheTop()
        {
            // Player.GetDodgeStaminaUse.
            Assert.Equal(1f, SkillMath.DodgeShare(0f), 3);
            Assert.Equal(0.75f, SkillMath.DodgeShare(50f), 3);
            Assert.Equal(0.5f, SkillMath.DodgeShare(100f), 3);
        }

        [Fact]
        public void SneakingDrainsAQuarterAtTheTopAndMostOfTheSavingComesEarly()
        {
            // Player.OnSneaking: down to a quarter, by the square root of the share.
            Assert.Equal(1f, SkillMath.SneakDrainShare(0f), 3);
            Assert.Equal(0.625f, SkillMath.SneakDrainShare(25f), 3);
            Assert.Equal(0.25f, SkillMath.SneakDrainShare(100f), 3);
        }

        [Fact]
        public void SneakingCutsHowFarCreaturesSeeYouMoreInTheDark()
        {
            // Player.UpdateStealth: a creature's sight and alert range times this (BaseAI.CanSeeTarget, MonsterAI.UpdateAI).
            Assert.Equal(0.5f, SkillMath.Stealth(0f, 0f), 3);
            Assert.Equal(1f, SkillMath.Stealth(0f, 1f), 3);
            Assert.Equal(0.2f, SkillMath.Stealth(100f, 0f), 3);
            Assert.Equal(0.6f, SkillMath.Stealth(100f, 1f), 3);
            Assert.Equal(0.35f, SkillMath.Stealth(50f, 0f), 3);
        }

        [Fact]
        public void AMountRunsFasterAndTiresLessWithYourRiding()
        {
            // Character.GetRunSpeedFactor with a rider; Sadle.UpdateRiding.
            Assert.Equal(1.25f, SkillMath.MountSpeedShare(100f), 3);
            Assert.Equal(1.1f, SkillMath.MountSpeedShare(40f), 3);
            Assert.Equal(0.8f, SkillMath.MountDrainShare(40f), 3);
            Assert.Equal(0.5f, SkillMath.MountDrainShare(100f), 3);
        }

        [Fact]
        public void CraftingTakesLessTimeAndMayGiveMore()
        {
            // InventoryGui.UpdateRecipe: the time less the share of its most; InventoryGui.DoCrafting,
            // CookingStation.OnInteract, Pickable.Interact: a bonus at the share of its most likely.
            Assert.Equal(1f, SkillMath.CraftTimeShare(0f, 0.6f), 3);
            Assert.Equal(0.7f, SkillMath.CraftTimeShare(50f, 0.6f), 3);
            Assert.Equal(0.4f, SkillMath.CraftTimeShare(100f, 0.6f), 3);
            Assert.Equal(0f, SkillMath.BonusChance(0f, 0.25f), 3);
            Assert.Equal(0.075f, SkillMath.BonusChance(30f, 0.25f), 3);
            Assert.Equal(0.25f, SkillMath.BonusChance(100f, 0.25f), 3);
        }

        [Fact]
        public void BuildingToolsTireLessAndWearLess()
        {
            // Player.GetBuildStamina: down to half; Player.GetPlaceDurability: less by the tool's most saved.
            Assert.Equal(0.75f, SkillMath.BuildStaminaShare(50f), 3);
            Assert.Equal(0.5f, SkillMath.BuildStaminaShare(100f), 3);
            Assert.Equal(1f, SkillMath.WearShare(0f, 0.5f), 3);
            Assert.Equal(0.8f, SkillMath.WearShare(40f, 0.5f), 3);
            Assert.Equal(0.5f, SkillMath.WearShare(100f, 0.5f), 3);
        }

        [Fact]
        public void SummonsAndShieldsGrowWithTheWholeLevelEvenPastTheTop()
        {
            // SpawnAbility.Spawn: a summon's hits times one and the level times its share; SE_Shield.SetLevel: absorbing plus so much a level.
            Assert.Equal(1f, SkillMath.SummonStrength(0f, 0.02f), 3);
            Assert.Equal(3f, SkillMath.SummonStrength(100f, 0.02f), 3);
            Assert.Equal(3.4f, SkillMath.SummonStrength(120f, 0.02f), 3);
            Assert.Equal(350f, SkillMath.ShieldAbsorb(200f, 5f, 30f), 3);
            Assert.Equal(800f, SkillMath.ShieldAbsorb(200f, 5f, 120f), 3);
        }

        [Fact]
        public void HowManySummonsYouHoldStepsUpAtTheLevelsTheStaffNames()
        {
            // SpawnAbility.Spawn reads its steps from the last back and takes the first the level reaches.
            var steps = new[] { 0, 20, 40, 60, 80 };
            Assert.Equal(0, SkillMath.SummonStep(steps, 0f));
            Assert.Equal(0, SkillMath.SummonStep(steps, 19f));
            Assert.Equal(1, SkillMath.SummonStep(steps, 20f));
            Assert.Equal(4, SkillMath.SummonStep(steps, 100f));
            Assert.Equal(-1, SkillMath.SummonStep(new[] { 10, 40 }, 5f));
            Assert.Equal(-1, SkillMath.SummonStep(new int[0], 50f));
        }

        [Fact]
        public void EachLevelNeedsMoreThanTheOneBefore()
        {
            // Skills.Skill.GetNextLevelRequirement: the next whole level to the power 1.5, halved, and a half.
            Assert.Equal(1f, SkillMath.NextLevelNeeds(0f), 3);
            Assert.Equal(104.03f, SkillMath.NextLevelNeeds(34f), 2);
            Assert.Equal(104.03f, SkillMath.NextLevelNeeds(34.6f), 2);
            Assert.Equal(500.5f, SkillMath.NextLevelNeeds(99f), 2);
        }

        [Fact]
        public void HowManyRisesTakeYouToTheNextLevel()
        {
            // Skills.Skill.Raise: each rise adds its amount, a level is reached once the progress holds what it needs.
            Assert.Equal(70, SkillMath.RisesToNext(34f, 0f, 1.5f));
            Assert.Equal(27, SkillMath.RisesToNext(34f, 64f, 1.5f));
            Assert.Equal(1, SkillMath.RisesToNext(34f, 104f, 1.5f));
            // Progress past what the level needs, as a mod may leave it, still takes one more rise.
            Assert.Equal(1, SkillMath.RisesToNext(34f, 110f, 1.5f));
            Assert.Equal(0, SkillMath.RisesToNext(100f, 0f, 1.5f));
            Assert.Equal(0, SkillMath.RisesToNext(34f, 0f, 0f));
        }

        [Fact]
        public void ADeathTakesAShareOfTheLevel()
        {
            // Skills.LowerAllSkills: the level less its share.
            Assert.Equal(2.05f, SkillMath.DeathLoss(41f, 0.05f), 3);
            Assert.Equal(0f, SkillMath.DeathLoss(0f, 0.05f), 3);
        }
    }
}
