using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class AttackChipTests
    {
        private static AttackInfo A(string item, string trigger, string shown = null, bool second = false) =>
            new AttackInfo(item, shown, trigger, second, item + (second ? "/2" : "/1"));

        [Fact]
        public void EachAttackOfACreatureIsOneChipNamedReadably()
        {
            var chips = AttackChips.For("Troll", new[]
            {
                A("troll_punch", "punch"), A("troll_groundslam", "groundslam"), A("troll_throw", "throw"),
            });

            Assert.Equal(new[] { "Punch", "Groundslam", "Throw" }, chips.Select(c => c.Label));
        }

        [Fact]
        public void AttacksPlayingTheSameAnimationAreOneChip()
        {
            var chips = AttackChips.For("Troll", new[] { A("troll_punch", "punch"), A("troll_punch_r", "punch") });

            var chip = Assert.Single(chips);
            Assert.Equal("troll_punch/1", chip.Key);
        }

        [Fact]
        public void TheGamesOwnNameIsUsedWhereItHasOne()
        {
            var chips = AttackChips.For("Draugr", new[] { A("draugr_axe", "attack", "Dragur axe") });

            Assert.Equal("Dragur axe", Assert.Single(chips).Label);
        }

        [Fact]
        public void ASecondAttackIsNamedSo()
        {
            var chips = AttackChips.For("Draugr", new[] { A("draugr_axe", "attack", "Dragur axe"), A("draugr_axe", "attack_spin", "Dragur axe", true) });

            Assert.Equal(new[] { "Dragur axe", "Dragur axe, second attack" }, chips.Select(c => c.Label));
        }

        [Fact]
        public void AttacksShownAlikeAreToldApartByTheirItem()
        {
            var chips = AttackChips.For("Troll", new[]
            {
                A("troll_log_swing_v", "swing_logv", "LOG"), A("troll_log_swing_h", "swing_logh", "LOG"),
            });

            Assert.Equal(new[] { "LOG (log swing v)", "LOG (log swing h)" }, chips.Select(c => c.Label));
        }

        [Theory]
        [InlineData("TrollFrost", "trollsnow_punch", "Punch")]
        [InlineData("DvergerMage", "DvergerStaffFire_fireball", "Fireball")]
        [InlineData("Troll_Summoned", "troll_summoned_throw", "Throw")]
        [InlineData("Draugr", "draugr_axe", "Axe")]
        public void AReadableNameLeavesOutTheCreaturesOwn(string creature, string item, string label)
        {
            Assert.Equal(label, AttackChips.Readable(item, creature));
        }

        [Fact]
        public void AnAttackWithoutAnAnimationIsStillOneChip()
        {
            var chips = AttackChips.For("Troll", new[] { A("troll_aoe", ""), A("troll_other", "") });

            Assert.Equal(2, chips.Count);
        }
    }

    public class HoldableTests
    {
        [Fact]
        public void WeaponsThatDrawNothingAreNotChoicesToHold()
        {
            var held = AttackChips.Holdable(new[] { ("troll_punch", "slap", false), ("troll_throw", "fireballattack", false) });

            Assert.Empty(held);
        }

        [Fact]
        public void TwoItemsDrawingTheSameWeaponAreOneChoice()
        {
            var held = AttackChips.Holdable(new[] { ("troll_log_swing_v", "LOG", true), ("troll_log_swing_h", "LOG", true) });

            Assert.Equal(new[] { "troll_log_swing_v" }, held);
        }

        [Fact]
        public void DifferentWeaponsThatShowAreChoices()
        {
            var held = AttackChips.Holdable(new[] { ("FW_BowDraugrFang", "Draugr fang", true), ("FW_KnifeSilver", "Silver knife", true), ("fw_kick", "kick", false) });

            Assert.Equal(new[] { "FW_BowDraugrFang", "FW_KnifeSilver" }, held);
        }
    }
}
