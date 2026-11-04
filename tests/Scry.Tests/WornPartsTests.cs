using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What of an item the stage hangs on a character, and where, by the slot the item is worn in
    /// and its children's names, as the game's <c>VisEquipment</c> hangs it: armour and utility
    /// items by every "attach_" part, held and head items by their first "attach" part.
    /// </summary>
    public class WornPartsTests
    {
        [Fact]
        public void ADvergrMagesStaffHangsInItsRightHandThoughItIsAUtilityItem()
        {
            // DvergerStaffFire: a utility item whose only part is attach_r.hand.
            var parts = WornParts.Of(Slot.Utility, new[] { "collider", "attach_r.hand" });

            Assert.Equal(new[] { (1, "r.hand") }, parts);
        }

        [Fact]
        public void ArmourHangsEveryAttachPartTheSkinOnTheBody()
        {
            var parts = WornParts.Of(Slot.Chest, new[] { "attach_skin", "log", "attach_Spine2" });

            Assert.Equal(new[] { (0, WornParts.Body), (2, "Spine2") }, parts);
            Assert.Equal(new[] { (0, WornParts.Body) }, WornParts.Of(Slot.Shoulders, new[] { "attach_skin" }));
            Assert.Equal(new[] { (1, "Hips") }, WornParts.Of(Slot.Legs, new[] { "attach", "attach_Hips" }));
        }

        [Fact]
        public void ArmourHasNoPartInAPlainAttachChild()
        {
            Assert.Empty(WornParts.Of(Slot.Chest, new[] { "attach" }));
            Assert.Empty(WornParts.Of(Slot.Utility, new[] { "attach", "default" }));
        }

        [Fact]
        public void AHeldOrHeadItemHangsItsFirstAttachPartInItsSlotsOwnPlace()
        {
            Assert.Equal(new[] { (2, (string)null) }, WornParts.Of(Slot.RightHand, new[] { "collider", "log", "attach", "attach_skin" }));
            Assert.Equal(new[] { (0, WornParts.Body) }, WornParts.Of(Slot.Head, new[] { "attach_skin", "attach" }));
            // An attach_ part for a bone is armour's way, not a held item's.
            Assert.Empty(WornParts.Of(Slot.LeftHand, new[] { "attach_r.hand" }));
            // What has no slot keeps its attach part, as an item stand hangs it.
            Assert.Equal(new[] { (0, (string)null) }, WornParts.Of(Slot.None, new[] { "attach" }));
        }

        [Fact]
        public void AnItemWithNoAttachPartHangsNothing()
        {
            Assert.Empty(WornParts.Of(Slot.Utility, new[] { "collider", "default" }));
            Assert.Empty(WornParts.Of(Slot.RightHand, new string[0]));
            Assert.Empty(WornParts.Of(Slot.Chest, null));
        }

        [Fact]
        public void OnlyArmourAndUtilitySlotsHangAsArmour()
        {
            var asArmour = System.Enum.GetValues(typeof(Slot)).Cast<Slot>().Where(WornParts.AsArmour).ToArray();

            Assert.Equal(new[] { Slot.Chest, Slot.Legs, Slot.Shoulders, Slot.Utility }, asArmour);
        }
    }
}
