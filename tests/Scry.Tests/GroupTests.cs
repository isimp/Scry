using Xunit;

namespace Scry.Tests
{
    public class GroupTests
    {
        [Theory]
        [InlineData("OneHandedWeapon", "Weapons")]
        [InlineData("TwoHandedWeaponLeft", "Weapons")]
        [InlineData("Bow", "Weapons")]
        [InlineData("Attach_Atgeir", "Weapons")]
        [InlineData("Shield", "Shields")]
        [InlineData("Helmet", "Helmets")]
        [InlineData("Chest", "Chest armour")]
        [InlineData("Legs", "Leg armour")]
        [InlineData("Hands", "Gloves")]
        [InlineData("Shoulder", "Capes")]
        [InlineData("Utility", "Belts and trinkets")]
        [InlineData("Trinket", "Belts and trinkets")]
        [InlineData("AmmoNonEquipable", "Ammo")]
        [InlineData("Consumable", "Food and meads")]
        [InlineData("Material", "Materials")]
        [InlineData("Torch", "Tools")]
        [InlineData("Fish", "Fish")]
        [InlineData("Trophy", "Trophies")]
        [InlineData("Customization", "Other")]
        [InlineData("SomeModType", "Other")]
        public void ItemsAreGroupedByTheGamesOwnItemType(string type, string group)
        {
            Assert.Equal(group, Groups.Item(type).Name);
        }

        [Fact]
        public void ItemGroupsComeWeaponsFirstAndOtherLast()
        {
            Assert.True(Groups.Item("Bow").Order < Groups.Item("Shield").Order);
            Assert.True(Groups.Item("Shield").Order < Groups.Item("Helmet").Order);
            Assert.True(Groups.Item("Helmet").Order < Groups.Item("Chest").Order);
            Assert.True(Groups.Item("Chest").Order < Groups.Item("Legs").Order);
            Assert.True(Groups.Item("Legs").Order < Groups.Item("Shoulder").Order);
            Assert.True(Groups.Item("Trophy").Order < Groups.Item("Misc").Order);
        }

        [Fact]
        public void CreaturesAreGroupedByFactionWithBossesTogether()
        {
            Assert.Equal("Forest monsters", Groups.Creature("ForestMonsters", false).Name);
            Assert.Equal("Animals", Groups.Creature("AnimalsVeg", false).Name);
            Assert.Equal("Bosses", Groups.Creature("Boss", false).Name);
            Assert.Equal("Bosses", Groups.Creature("PlainsMonsters", true).Name);
            Assert.Equal("Other factions", Groups.Creature("17", false).Name);
        }

        [Fact]
        public void ThePiecesOfTheHammerGoByItsTabsAndOtherToolsAreOneGroupEach()
        {
            Assert.Equal("Furniture", Groups.Piece("Hammer", 0, true, "Furniture", 3).Name);
            Assert.Equal("Cultivator", Groups.Piece("Cultivator", 2, false, "Misc", 0).Name);
            Assert.Equal("Serving tray", Groups.Piece("Serving tray", 3, false, "Meads", 1).Name);
            // A mod's tool with many tabs of its own (PlanBuild's plans) is one group, not one a tab.
            Assert.Equal("Plan hammer", Groups.Piece("Plan hammer", 4, false, "Furniture", 4).Name);
        }

        [Fact]
        public void PiecesComeByToolThenTabThenThoseInNoMenu()
        {
            var hammerLast = Groups.Piece("Hammer", 0, true, "Misc", 8);
            var cultivator = Groups.Piece("Cultivator", 1, false, "Misc", 0);
            Assert.True(Groups.Piece("Hammer", 0, true, "Crafting", 0).Order < hammerLast.Order);
            Assert.True(hammerLast.Order < cultivator.Order);
            Assert.True(cultivator.Order < Groups.InNoMenu.Order);
            Assert.Equal("In no build menu", Groups.InNoMenu.Name);
        }
    }
}
