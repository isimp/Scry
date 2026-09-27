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
        [InlineData("Helmet", "Armour and capes")]
        [InlineData("Shoulder", "Armour and capes")]
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
        public void ThePiecesOfTheHammerGoByItsTabsAndOtherToolsByTheirName()
        {
            Assert.Equal("Furniture", Groups.Piece("Hammer", 0, true, "Furniture", 3, 5).Name);
            Assert.Equal("Cultivator", Groups.Piece("Cultivator", 2, false, "Misc", 0, 1).Name);
            Assert.Equal("Serving tray: Meads", Groups.Piece("Serving tray", 3, false, "Meads", 1, 2).Name);
        }

        [Fact]
        public void PiecesComeByToolThenTabThenThoseInNoMenu()
        {
            var hammerLast = Groups.Piece("Hammer", 0, true, "Misc", 8, 9);
            var cultivator = Groups.Piece("Cultivator", 1, false, "Misc", 0, 1);
            Assert.True(Groups.Piece("Hammer", 0, true, "Crafting", 0, 9).Order < hammerLast.Order);
            Assert.True(hammerLast.Order < cultivator.Order);
            Assert.True(cultivator.Order < Groups.InNoMenu.Order);
            Assert.Equal("In no build menu", Groups.InNoMenu.Name);
        }

        [Fact]
        public void EffectsAndSoundsGoByWhatPlaysThemCreaturesFirst()
        {
            Assert.Equal("Played by creatures", Groups.ByUsers(new[] { Kind.Piece, Kind.Creature }).Name);
            Assert.Equal("Played by creatures", Groups.ByUsers(new[] { Kind.Creature, Kind.Piece }).Name);
            Assert.Equal("Played by items", Groups.ByUsers(new[] { Kind.Item, Kind.Other }).Name);
            Assert.Equal("Played by pieces", Groups.ByUsers(new[] { Kind.Piece }).Name);
            Assert.Equal("Played by other things", Groups.ByUsers(new[] { Kind.Other }).Name);
            Assert.Equal("Played by nothing listed", Groups.ByUsers(new Kind[0]).Name);
            Assert.True(Groups.ByUsers(new[] { Kind.Creature }).Order < Groups.ByUsers(new[] { Kind.Item }).Order);
            Assert.True(Groups.ByUsers(new[] { Kind.Other }).Order < Groups.ByUsers(new Kind[0]).Order);
        }
    }
}
