using Xunit;

namespace Scry.Tests
{
    public class ConfigSectionsTests
    {
        // A mod's settings for an item are those of a section of its config named after the item,
        // whole or as one part of the section's name, by the name the game shows or its prefab's.

        [Fact]
        public void ASectionNamedAfterTheItemBehindAKindIsItsOwn()
        {
            Assert.True(ConfigSections.About("Backpack: Arctic Sherpa Pack", "Arctic Sherpa Pack", "BackpackMountains"));
            Assert.True(ConfigSections.About("2 - Satchel", "Satchel", "BackpackMeadows"));
            Assert.True(ConfigSections.About("Items | Iron sword", "Iron sword", "SwordIron"));
        }

        [Fact]
        public void ANameOfThreeLettersIsEnoughAndOneInBracketsAfterOtherWordsCounts()
        {
            Assert.True(ConfigSections.About("Weapons: Bow", "Bow", "Bow"));
            Assert.True(ConfigSections.About("Recipe [Satchel]", "Satchel", "BackpackMeadows"));
        }

        [Fact]
        public void ASectionNamedAsItsPrefabIsItsOwn() =>
            Assert.True(ConfigSections.About("BackpackMountains", "Arctic Sherpa Pack", "BackpackMountains"));

        [Fact]
        public void CaseSpacesAndPunctuationMakeNoDifference()
        {
            Assert.True(ConfigSections.About("Backpack:arctic  SHERPA-pack", "Arctic Sherpa Pack", "BackpackMountains"));
            Assert.True(ConfigSections.About("[Lox Hide Knappsack]", "Lox Hide Knappsack", "BackpackPlains"));
        }

        [Fact]
        public void ASectionHoldingTheNameAmongOtherWordsIsNotItsOwn()
        {
            // A building mod's "Wood farm" is not about wood, nor an effect's section about the pack.
            Assert.False(ConfigSections.About("Wood farm", "Wood", "Wood"));
            Assert.False(ConfigSections.About("Effect: Frost Resistance", "Frost", "Frost"));
            Assert.False(ConfigSections.About("Backpack: Arctic Sherpa Pack", "Sherpa", "Sherpa"));
        }

        [Fact]
        public void ASectionBehindAnotherKindOfThingIsThatThingsNotAnItemsOfItsName()
        {
            // A backpack mod's "Effect: Demister" sets which packs give the effect; the item whose
            // prefab is called Demister is not what it is about, nor is any item named after an effect.
            Assert.False(ConfigSections.About("Effect: Demister", "Wisplight", "Demister"));
            Assert.False(ConfigSections.About("Effect: Feather Fall", "Feather Fall", "FeatherFall"));
            Assert.False(ConfigSections.About("Status Effects - Frost Resistance", "Frost Resistance", "FrostResistance"));
            Assert.False(ConfigSections.About("Creature: Boar", "Boar", "Boar"));
            Assert.False(ConfigSections.About("Pieces | Lantern", "Lantern", "Lantern"));
            Assert.False(ConfigSections.About("Spawner: Greydwarf nest", "Greydwarf nest", "Spawner_GreydwarfNest"));
            Assert.False(ConfigSections.About("Projectile: Arrow", "Arrow", "Arrow"));
            Assert.False(ConfigSections.About("Sound: Horn", "Horn", "Horn"));
            Assert.False(ConfigSections.About("Biome: Mistlands", "Mistlands", "Mistlands"));
            Assert.False(ConfigSections.About("Location: Crypt", "Crypt", "Crypt"));
            Assert.False(ConfigSections.About("Raid – Army of the dead", "Army of the dead", "Army"));
            Assert.Empty(ConfigSections.NamesOf("Effect: Demister"));
            // The kind ends at the first mark, whichever it is.
            Assert.False(ConfigSections.About("Effect | Demister: lights", "Demister", "Demister"));
            // A mark opening the name ends no kind.
            Assert.False(ConfigSections.About("|Effect: Demister", "Demister", "Demister"));
        }

        [Fact]
        public void ASectionBehindItemsOrResourcesOrAnyOtherWordStaysTheItems()
        {
            // Mods call materials resources, and a kind of their own is no other thing's.
            Assert.True(ConfigSections.About("Item: Wisplight", "Wisplight", "Demister"));
            Assert.True(ConfigSections.About("Resources: Copper", "Copper", "Copper"));
            Assert.True(ConfigSections.About("6 - Cultivator", "Cultivator", "Cultivator"));
            // A kind's word that only ends a section's name is no kind it is behind.
            Assert.True(ConfigSections.About("Lantern: Effect", "Lantern", "Lantern"));
            // A kind's word alone is a name, not a kind something is behind.
            Assert.True(ConfigSections.About("Sounds", "Sounds", "Sounds"));
        }

        [Fact]
        public void ANameTooShortToTellOrNoneIsNoSectionsOwn()
        {
            Assert.False(ConfigSections.About("General: Ax", "Ax", "Ax"));
            Assert.False(ConfigSections.About("General", null, ""));
            Assert.False(ConfigSections.About("", "Satchel", "BackpackMeadows"));
            Assert.False(ConfigSections.About(null, "Satchel", "BackpackMeadows"));
        }

        [Fact]
        public void ASectionIsFoundByEveryNameItsPartsMake()
        {
            Assert.Equal(new[] { "backpack arctic sherpa pack", "backpack", "arctic sherpa pack" }, ConfigSections.NamesOf("Backpack: Arctic Sherpa Pack"));
            // A section of one part goes by it once; one of nothing but marks by nothing.
            Assert.Equal(new[] { "satchel" }, ConfigSections.NamesOf("Satchel"));
            Assert.Empty(ConfigSections.NamesOf(": - |"));
            Assert.Equal("arctic sherpa pack", ConfigSections.Plain("Arctic Sherpa Pack"));
        }

        // How they are told: the mod's settings, as its config holds them.

        [Fact]
        public void TheRowNamesTheModAndTheSection() =>
            Assert.Equal("In the settings of Adventure Backpacks, under Backpack: Arctic Sherpa Pack", ModSettingWords.Title("Adventure Backpacks", "Backpack: Arctic Sherpa Pack"));

        [Fact]
        public void ALongValueIsCutShort()
        {
            var value = new string('x', 300);
            var shown = ModSettingWords.Value(value);
            Assert.True(shown.Length <= ModSettingWords.LongestValue + 1);
            Assert.EndsWith("…", shown);
            Assert.Equal("{\"x\":3.0,\"y\":3.0}", ModSettingWords.Value("{\"x\":3.0,\"y\":3.0}"));
            Assert.Equal("(empty)", ModSettingWords.Value(""));
            Assert.Equal("(empty)", ModSettingWords.Value(null));
        }

        [Fact]
        public void ItsColumnsAreTheSettingAndItsValue() =>
            Assert.Equal(new[] { "Setting", "Value" }, ModSettingWords.Columns);
    }
}
