using Xunit;

namespace Scry.Tests
{
    public class GearLooksTests
    {
        // A creature rolling one of several sets fights with what its set gives it: its attacks
        // table lists the attacks of the look chosen, each look after "No gear" one set; "No gear"
        // lists them all, as it stands for no set in particular.

        private static readonly string[][] MageSets =
        {
            new[] { "Dverger_melee", "DvergerStaffFire_fireball", "DvergerSuitFire" },
            new[] { "Dverger_melee", "DvergerStaffIce_icebolt", "DvergerSuitIce" },
            new[] { "DvergerStaffHeal_heal", "DvergerSuitSupport" },
        };

        [Fact]
        public void AnAttackComesWithTheLooksWhoseSetGivesIt()
        {
            Assert.Equal(new[] { 1, 2 }, GearLooks.Of("Dverger_melee", MageSets));
            Assert.Equal(new[] { 3 }, GearLooks.Of("DvergerStaffHeal_heal", MageSets));
            // In no set, it is given whatever is rolled: every look's.
            Assert.Null(GearLooks.Of("Dverger_kick", MageSets));
            // A creature with no sets has no looks to tell apart.
            Assert.Null(GearLooks.Of("Dverger_melee", System.Array.Empty<string[]>()));
            Assert.Null(GearLooks.Of("Dverger_melee", null));
        }

        [Fact]
        public void TheTableListsTheAttacksOfTheLookChosenAndEveryOneAtNoGear()
        {
            var fireball = GearLooks.Of("DvergerStaffFire_fireball", MageSets);
            Assert.True(GearLooks.Shown(fireball, 1));
            Assert.False(GearLooks.Shown(fireball, 2));
            Assert.False(GearLooks.Shown(fireball, 3));
            Assert.True(GearLooks.Shown(fireball, 0));
            // One in no set shows at every look.
            Assert.True(GearLooks.Shown(null, 2));
        }

        [Fact]
        public void TheLineUnderTheTableCountsTheAttacksOfOtherLooks()
        {
            Assert.Equal("1 more attack comes with its other gear  ·  No gear lists every one", CombatWords.OtherGearAttacks(1));
            Assert.Equal("3 more attacks come with its other gear  ·  No gear lists every one", CombatWords.OtherGearAttacks(3));
        }
    }
}
