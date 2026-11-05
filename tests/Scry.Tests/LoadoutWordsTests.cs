using Xunit;

namespace Scry.Tests
{
    public class LoadoutWordsTests
    {
        // A creature's gear rows in Adjust are named by the game's list each rolls from, unless
        // the list holds none of its kind: the game rolls a Dvergr's hair from its shield list,
        // male hair typed as a helmet, female hair as legs. Such a row is named by the item type
        // its choices share, else "Rolled". Each choice goes by its game name, except where that
        // name is set on the prefab, not one the game translates, and another choice shares it:
        // "Iron plate armor" is the name of every Dvergr's and Jotun's hair and every goblin's
        // armour, which the game never shows; those go by their prefab's words.

        private static readonly (string Prefab, string Shown, bool Translated, string Type)[] DvergrHair =
        {
            ("DvergerHairMale", "Iron plate armor", false, "Helmet"),
            ("DvergerHairFemale", "Iron plate armor", false, "Legs"),
        };

        [Fact]
        public void ARowOfItsOwnKindKeepsTheListsName()
        {
            Assert.Equal("Shield", LoadoutWords.RowLabel(Loadout.Row.Shield, new[] { "Shield", "Shield" }));
            Assert.Equal("Armour", LoadoutWords.RowLabel(Loadout.Row.Armour, new[] { "Chest", "Helmet" }));
            Assert.Equal("Weapon", LoadoutWords.RowLabel(Loadout.Row.Weapon, new[] { "OneHandedWeapon", "Bow" }));
            Assert.Equal("Holding", LoadoutWords.RowLabel(Loadout.Row.Holding, new[] { "TwoHandedWeapon", "Torch" }));
            // With no item it can read the type of, the list's name too.
            Assert.Equal("Shield", LoadoutWords.RowLabel(Loadout.Row.Shield, new string[0]));
            // One of its kind among others is still that list's row.
            Assert.Equal("Shield", LoadoutWords.RowLabel(Loadout.Row.Shield, new[] { "Shield", "Helmet" }));
        }

        [Fact]
        public void ARowOfNoneOfItsKindIsNamedByTheTypeItsChoicesShareElseRolled()
        {
            // A Jotun's hair, every one typed as a helmet.
            Assert.Equal("Helmet", LoadoutWords.RowLabel(Loadout.Row.Shield, new[] { "Helmet", "Helmet" }));
            Assert.Equal("Two handed weapon", LoadoutWords.RowLabel(Loadout.Row.Armour, new[] { "TwoHandedWeapon" }));
            // A Dvergr's, a helmet and legs.
            Assert.Equal("Rolled", LoadoutWords.RowLabel(Loadout.Row.Shield, new[] { "Helmet", "Legs" }));
            Assert.Equal("Rolled", LoadoutWords.RowLabel(Loadout.Row.Weapon, new[] { "Shield", "Legs" }));
        }

        [Fact]
        public void AChoiceWhoseUntranslatedNameAnotherSharesGoesByItsPrefab()
        {
            Assert.Equal(new[] { "Dverger hair male", "Dverger hair female" }, LoadoutWords.Choices(DvergrHair));
            // A name the game translates stays, every one of a repeated name telling its prefab.
            var shields = new[] { ("ShieldWood", "Wood shield", true, "Shield"), ("ShieldWood_old", "Wood shield", true, "Shield"), ("ShieldBronzeBuckler", "Bronze buckler", true, "Shield") };
            Assert.Equal(new[] { "Wood shield (ShieldWood)", "Wood shield (ShieldWood_old)", "Bronze buckler" }, LoadoutWords.Choices(shields));
            // An untranslated name no other choice shares stays as it is, as a goblin's "Club".
            var clubs = new[] { ("GoblinClub", "Club", false, "OneHandedWeapon"), ("GoblinSpear", "Flint spear", false, "OneHandedWeapon") };
            Assert.Equal(new[] { "Club", "Flint spear" }, LoadoutWords.Choices(clubs));
            // Nothing chosen reads so, and one prefab listed twice is one name.
            var withNothing = new[] { (null, null, false, null), ("GoblinClub", "Club", false, "OneHandedWeapon"), ("GoblinClub", "Club", false, "OneHandedWeapon") };
            Assert.Equal(new[] { "Nothing", "Club", "Club" }, LoadoutWords.Choices(withNothing));
            // A prefab with no name at all goes by its prefab.
            Assert.Equal(new[] { "Dverger hair male" }, LoadoutWords.Choices(new[] { ("DvergerHairMale", "", false, "Helmet") }));
        }
    }
}
