using Xunit;

namespace Scry.Tests
{
    /// <summary>What pressing an effect list's chip does, as the game plays that list.</summary>
    public class ChipPlanTests
    {
        private static ChipFacts Facts(ListRole role, params string[] animatorParameters) => new ChipFacts(role, animatorParameters);

        [Fact]
        public void ACreaturesDeathWithARagdollFallsAsItAndPlaysTheList()
        {
            var plan = ChipPlan.For(new ChipFacts(ListRole.Death, "dead") { HasRagdoll = true });
            Assert.Equal(ChipStep.Ragdoll, plan.Step);
            Assert.True(plan.PlaysList);
        }

        [Fact]
        public void WhatTheGamePlaysWhenItDestroysAPrefabDestroysTheCopy()
        {
            var plan = ChipPlan.For(new ChipFacts(ListRole.Other) { IsDestroyed = true });
            Assert.Equal(ChipStep.Destroy, plan.Step);
            Assert.True(plan.PlaysList);
        }

        [Fact]
        public void ATreeStruckShakesItsTrunk()
        {
            var plan = ChipPlan.For(Facts(ListRole.TreeHit));
            Assert.Equal(ChipStep.ShakeTrunk, plan.Step);
            Assert.True(plan.PlaysList);
        }

        [Fact]
        public void AJumpPullsTheJumpTriggerAndAFlyerTakesOffInstead()
        {
            var jump = ChipPlan.For(Facts(ListRole.Jump, "jump"));
            Assert.Equal(ChipStep.Trigger, jump.Step);
            Assert.Equal("jump", jump.Parameter);

            var walker = ChipPlan.For(Facts(ListRole.Jump, "fly_takeoff", "jump"));
            Assert.Equal("jump", walker.Parameter);

            var flyer = ChipPlan.For(new ChipFacts(ListRole.Jump, "fly_takeoff", "jump") { Flying = true });
            Assert.Equal("fly_takeoff", flyer.Parameter);

            var flyerWithoutTakeOff = ChipPlan.For(new ChipFacts(ListRole.Jump, "jump") { Flying = true });
            Assert.Equal("jump", flyerWithoutTakeOff.Parameter);
        }

        [Fact]
        public void ADeathWithoutARagdollAndBeingAlertedHoldTheirSwitchUntilStopped()
        {
            var death = ChipPlan.For(Facts(ListRole.Death, "dead"));
            Assert.Equal(ChipStep.Switch, death.Step);
            Assert.Equal("dead", death.Parameter);
            Assert.True(death.UndoOnStop);

            var alert = ChipPlan.For(Facts(ListRole.Alerted, "alert"));
            Assert.Equal("alert", alert.Parameter);
            Assert.True(alert.UndoOnStop);
        }

        [Fact]
        public void WakingAndBlockingSwitchOnForAMoment()
        {
            var wake = ChipPlan.For(Facts(ListRole.Wakeup, "sleeping"));
            Assert.Equal(ChipStep.Switch, wake.Step);
            Assert.Equal("sleeping", wake.Parameter);
            Assert.Equal(1.2f, wake.OffAfter);

            var block = ChipPlan.For(Facts(ListRole.Block, "blocking"));
            Assert.Equal("blocking", block.Parameter);
            Assert.Equal(1.2f, block.OffAfter);
        }

        [Fact]
        public void EatingPullsEatElseConsume()
        {
            Assert.Equal("eat", ChipPlan.For(Facts(ListRole.Consume, "eat", "consume")).Parameter);
            Assert.Equal("consume", ChipPlan.For(Facts(ListRole.Consume, "consume")).Parameter);
        }

        [Fact]
        public void AnAnimatorWithoutTheSwitchFallsBackToTheClipThatPlaysTheList()
        {
            var plan = ChipPlan.For(new ChipFacts(ListRole.Jump) { HasOwnClip = true });
            Assert.Equal(ChipStep.OwnClip, plan.Step);
            Assert.True(plan.PlaysList);

            Assert.Equal(ChipStep.List, ChipPlan.For(Facts(ListRole.Alerted)).Step);
        }

        [Fact]
        public void AnItemsAttackIsPlayedWholeByItsClipWhichPlaysTheListItself()
        {
            var plan = ChipPlan.For(new ChipFacts(ListRole.Other) { HasAttackClip = true, HasOwnClip = true });
            Assert.Equal(ChipStep.AttackClip, plan.Step);
            Assert.False(plan.PlaysList);
        }

        [Fact]
        public void AnyOtherListJustPlays()
        {
            var plan = ChipPlan.For(Facts(ListRole.Other));
            Assert.Equal(ChipStep.List, plan.Step);
            Assert.True(plan.PlaysList);
        }
    }
}
