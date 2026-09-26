using Xunit;

namespace Scry.Tests
{
    public class WeaponNameTests
    {
        [Theory]
        [InlineData("TrollFrost", "trollsnow_punch", "Punch")]
        [InlineData("DvergerMage", "DvergerStaffFire_fireball", "Fireball")]
        [InlineData("Troll_Summoned", "troll_summoned_throw", "Throw")]
        [InlineData("Draugr", "draugr_axe", "Axe")]
        [InlineData("Troll", "troll_log_swing_v", "Log swing v")]
        public void AReadableNameLeavesOutTheCreaturesOwn(string creature, string item, string label)
        {
            Assert.Equal(label, WeaponChoices.Readable(item, creature));
        }
    }

    public class HoldableTests
    {
        [Fact]
        public void WeaponsThatDrawNothingAreNotChoicesToHold()
        {
            var held = WeaponChoices.Holdable(new[] { ("troll_punch", "slap", false), ("troll_throw", "fireballattack", false) });

            Assert.Empty(held);
        }

        [Fact]
        public void TwoItemsDrawingTheSameWeaponAreOneChoice()
        {
            var held = WeaponChoices.Holdable(new[] { ("troll_log_swing_v", "LOG", true), ("troll_log_swing_h", "LOG", true) });

            Assert.Equal(new[] { "troll_log_swing_v" }, held);
        }

        [Fact]
        public void DifferentWeaponsThatShowAreChoices()
        {
            var held = WeaponChoices.Holdable(new[] { ("FW_BowDraugrFang", "Draugr fang", true), ("FW_KnifeSilver", "Silver knife", true), ("fw_kick", "kick", false) });

            Assert.Equal(new[] { "FW_BowDraugrFang", "FW_KnifeSilver" }, held);
        }
    }
}
