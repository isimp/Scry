using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What a creature that rolled a set surely wears, as <c>Humanoid.GiveDefaultItems</c> hands
    /// out the set and equips each of its items but weapons: whatever of it was seen worn that no
    /// other set or list gives tells the set, and with it the rest of what the set puts on.
    /// </summary>
    public class WornTogetherTests
    {
        private static (string, Slot) I(string item, Slot slot) => (item, slot);

        /// <summary>A Dvergr mage's three sets, as the game gives them: attacks in hand, a staff for utility, a robe.</summary>
        private static List<WornSet> MageSets() => WornTogether.Of(new[]
        {
            new[] { I("Dverger_melee", Slot.RightHand), I("DvergerStaffFire_fireball", Slot.RightHand), I("DvergerStaffFire", Slot.Utility), I("DvergerSuitFire", Slot.Chest), I("DvergerStaffFire_clusterbomb", Slot.RightHand) },
            new[] { I("Dverger_melee", Slot.RightHand), I("DvergerStaffIce_icebolt", Slot.RightHand), I("DvergerStaffIce", Slot.Utility), I("DvergerStaffNova", Slot.RightHand), I("DvergerSuitIce", Slot.Chest) },
            new[] { I("DvergerStaffSupport_buff", Slot.RightHand), I("DvergerStaffHeal_heal", Slot.RightHand), I("DvergerStaffHeal", Slot.Utility), I("DvergerSuitSupport", Slot.Chest), I("DvergerMistile", Slot.RightHand) },
        }, new[] { "DvergerHairMale", "DvergerHairFemale" }, new Slot[0]);

        [Fact]
        public void AMageSeenInItsFireRobeWasSeenHoldingItsFireStaff()
        {
            var seen = new HashSet<string> { "DvergerSuitFire", "DvergerHairMale" };

            Assert.True(WornTogether.Seen("DvergerStaffFire", MageSets(), seen.Contains));
            Assert.False(WornTogether.Seen("DvergerStaffIce", MageSets(), seen.Contains));
            Assert.False(WornTogether.Seen("DvergerStaffHeal", MageSets(), seen.Contains));
        }

        [Fact]
        public void WhatASetHoldsInHandIsNeverTakenAsSeenFromIt()
        {
            // The game equips no weapon as it hands out a set; the AI picks one in a fight.
            var seen = new HashSet<string> { "DvergerSuitFire" };

            Assert.False(WornTogether.Seen("DvergerStaffFire_fireball", MageSets(), seen.Contains));
            Assert.Equal(new[] { "DvergerStaffFire", "DvergerSuitFire" }, MageSets()[0].Worn);
        }

        [Fact]
        public void WhatSeveralSetsOrTheCreaturesOtherListsGiveTellsNoSet()
        {
            // A Fuling berserker's arm guard is in both its sets; its backbones and its cap in one each.
            var sets = WornTogether.Of(new[]
            {
                new[] { I("GoblinBrute_ArmGuard", Slot.Chest), I("GoblinBrute_Backbones", Slot.Head), I("GoblinBrute_LegBones", Slot.Utility) },
                new[] { I("GoblinBrute_ExecutionerCap", Slot.Head), I("GoblinBrute_ArmGuard", Slot.Chest), I("GoblinBrute_LegBones", Slot.Utility) },
            }, new[] { "GoblinBrute_ExecutionerCap" }, new Slot[0]);

            Assert.False(WornTogether.Seen("GoblinBrute_LegBones", sets, new HashSet<string> { "GoblinBrute_ArmGuard" }.Contains));
            // The cap comes from another list too, so it tells no set either.
            Assert.False(WornTogether.Seen("GoblinBrute_LegBones", sets, new HashSet<string> { "GoblinBrute_ExecutionerCap" }.Contains));
            Assert.True(WornTogether.Seen("GoblinBrute_LegBones", sets, new HashSet<string> { "GoblinBrute_Backbones" }.Contains));
            Assert.Equal(new[] { "GoblinBrute_Backbones" }, sets[0].Tells);
        }

        [Fact]
        public void OfASetsItemsForOneSlotOnlyTheLastIsWorn()
        {
            // Each item equipped takes the place of the one before it in its slot.
            var sets = WornTogether.Of(new[] { new[] { I("HoodA", Slot.Head), I("Robe", Slot.Chest), I("HoodB", Slot.Head) } }, new string[0], new Slot[0]);

            Assert.Equal(new[] { "Robe", "HoodB" }, sets[0].Worn);
            Assert.False(WornTogether.Seen("HoodA", sets, new HashSet<string> { "Robe" }.Contains));
            Assert.True(WornTogether.Seen("HoodB", sets, new HashSet<string> { "Robe" }.Contains));
        }

        [Fact]
        public void ASlotTheCreaturesChanceItemsMayFillLaterIsNotSure()
        {
            // Random items are handed out after the set and may take a slot of it.
            var sets = WornTogether.Of(new[] { new[] { I("Robe", Slot.Chest), I("Staff", Slot.Utility) } }, new[] { "Belt" }, new[] { Slot.Utility });

            Assert.Equal(new[] { "Robe" }, sets[0].Worn);
            Assert.False(WornTogether.Seen("Staff", sets, new HashSet<string> { "Robe" }.Contains));
        }

        [Fact]
        public void OnlyTheBodysSlotsAreSureHandsNot()
        {
            var sure = System.Enum.GetValues(typeof(Slot)).Cast<Slot>().Where(WornTogether.IsSure).ToArray();

            Assert.Equal(new[] { Slot.Head, Slot.Chest, Slot.Legs, Slot.Shoulders, Slot.Utility }, sure);
        }

        [Fact]
        public void NothingIsTakenAsSeenWithNothingSeenOrNoSets()
        {
            Assert.False(WornTogether.Seen("DvergerStaffFire", MageSets(), _ => false));
            Assert.False(WornTogether.Seen("DvergerStaffFire", new List<WornSet>(), _ => true));
            Assert.False(WornTogether.Seen("DvergerStaffFire", null, _ => true));
            Assert.Empty(WornTogether.Of(null, null, null));
        }
    }
}
