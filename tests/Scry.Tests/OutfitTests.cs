using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class OutfitTests
    {
        [Fact]
        public void KeptItemsStayOnTogether()
        {
            var outfit = new Outfit();
            outfit.Keep("HelmetBronze", Slot.Head);
            outfit.Keep("ArmorBronzeChest", Slot.Chest);

            Assert.Equal(new[] { "HelmetBronze", "ArmorBronzeChest" }, outfit.Keys);
        }

        [Fact]
        public void KeepingSomethingForATakenSlotReplacesWhatWasThere()
        {
            var outfit = new Outfit();
            outfit.Keep("HelmetBronze", Slot.Head);
            outfit.Keep("HelmetIron", Slot.Head);

            Assert.Equal(new[] { "HelmetIron" }, outfit.Keys);
        }

        [Fact]
        public void ATwoHandedWeaponTakesBothHands()
        {
            var outfit = new Outfit();
            outfit.Keep("SwordIron", Slot.RightHand);
            outfit.Keep("ShieldWood", Slot.LeftHand);

            outfit.Keep("Battleaxe", Slot.BothHands);
            Assert.Equal(new[] { "Battleaxe" }, outfit.Keys);

            outfit.Keep("ShieldWood", Slot.LeftHand);
            Assert.Equal(new[] { "ShieldWood" }, outfit.Keys);
        }

        [Fact]
        public void TryingSomethingOnShowsItWithTheOutfitButKeepsNothing()
        {
            var outfit = new Outfit();
            outfit.Keep("HelmetBronze", Slot.Head);
            outfit.Keep("ArmorBronzeChest", Slot.Chest);

            var worn = outfit.With("HelmetIron", Slot.Head).ToList();

            Assert.Equal(new[] { "ArmorBronzeChest", "HelmetIron" }, worn);
            Assert.Equal(new[] { "HelmetBronze", "ArmorBronzeChest" }, outfit.Keys);
        }

        [Fact]
        public void TryingOnSomethingAlreadyKeptShowsItOnce()
        {
            var outfit = new Outfit();
            outfit.Keep("HelmetBronze", Slot.Head);

            Assert.Equal(new[] { "HelmetBronze" }, outfit.With("HelmetBronze", Slot.Head));
        }

        [Fact]
        public void KeepingTheSameItemAgainKeepsItOnce()
        {
            var outfit = new Outfit();
            outfit.Keep("Torch", Slot.RightHand);
            outfit.Keep("Torch", Slot.LeftHand);

            Assert.Equal(new[] { "Torch" }, outfit.Keys);
        }

        [Fact]
        public void TakingOffRemovesJustThatPiece()
        {
            var outfit = new Outfit();
            outfit.Keep("HelmetBronze", Slot.Head);
            outfit.Keep("ArmorBronzeChest", Slot.Chest);

            outfit.TakeOff("HelmetBronze");

            Assert.False(outfit.Contains("HelmetBronze"));
            Assert.Equal(new[] { "ArmorBronzeChest" }, outfit.Keys);
        }

        [Fact]
        public void SomethingThatCannotBeWornIsNeverKept()
        {
            var outfit = new Outfit();
            outfit.Keep("Wood", Slot.None);

            Assert.Empty(outfit.Keys);
        }
    }
}
