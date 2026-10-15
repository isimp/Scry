using Xunit;

namespace Scry.Tests
{
    public class ComboStepTests
    {
        // Each step of a weapon's combo plays on the stage from its line in the attacks table: the
        // game pulls a step by its animation's name and the step's number (Attack.Start), so the
        // later steps' triggers are watched too, and a line's link says which step it plays.

        [Fact]
        public void AChainsLaterStepsArePulledByItsAnimationsNameAndTheStepsNumber()
        {
            var steps = ClipAttacks.ComboSteps("swing_axe", 3, t => true);

            Assert.Equal(new[] { (1, "swing_axe1"), (2, "swing_axe2") }, steps);
        }

        [Fact]
        public void AnAttackWithoutAChainOrWithoutTheTriggerHasNoLaterSteps()
        {
            Assert.Empty(ClipAttacks.ComboSteps("swing_axe", 1, t => true));
            Assert.Empty(ClipAttacks.ComboSteps("", 3, t => true));
            Assert.Equal(new[] { (2, "swing_axe2") }, ClipAttacks.ComboSteps("swing_axe", 3, t => t != "swing_axe1"));
        }

        [Fact]
        public void ALinesLinkSaysWhichStepOfWhichAttackItPlays()
        {
            Assert.Equal(EntryKeys.PlayAttack("AxeIron"), EntryKeys.PlayAttack("AxeIron", false, 0));

            Assert.True(EntryKeys.PlaysAttack(EntryKeys.PlayAttack("AxeIron", false, 2), out var item, out var second, out var step));
            Assert.Equal(("AxeIron", false, 2), (item, second, step));
            Assert.True(EntryKeys.PlaysAttack(EntryKeys.PlayAttack("SledgeIron", true, 1), out item, out second, out step));
            Assert.Equal(("SledgeIron", true, 1), (item, second, step));
            Assert.True(EntryKeys.PlaysAttack(EntryKeys.PlayAttack("Club", true), out item, out second, out step));
            Assert.Equal(("Club", true, 0), (item, second, step));
            Assert.False(EntryKeys.PlaysAttack("Club", out _, out _, out _));
        }

        [Fact]
        public void ALaterStepsClipIsNamedForItsSwing()
        {
            Assert.Equal("attack iron axe, swing 2", ClipWords.Swing(ClipWords.Attack("iron axe", second: false), 1));
            Assert.True(ClipWords.IsAttack(ClipWords.Swing(ClipWords.Attack("iron axe", second: false), 2)));
        }
    }
}
