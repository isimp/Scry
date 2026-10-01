using Xunit;

namespace Scry.Tests
{
    public class ModAttributionTests
    {
        // Which mod added something is found from several clues: a registry naming it, the mod
        // whose assembly holds its scripts, the mod shipping a bundle that holds it by name, or
        // the bundles holding the sounds, icons and materials it uses. A clue naming one mod is
        // taken; clues naming several leave it unnamed, as a guess would mislead.

        [Fact]
        public void ABundleHoldingItByNameNamesItsMod()
        {
            Assert.Equal("Bamboozled", ModAttribution.Pick("Bamboozled", new[] { "OdinArchitect" }));
        }

        [Fact]
        public void WhatItUsesNamesItsModWhenAllAgree()
        {
            Assert.Equal("OdinHorse", ModAttribution.Pick("", new[] { "OdinHorse", "", "OdinHorse", null }));
            Assert.Equal("OdinHorse", ModAttribution.Pick(null, new[] { "OdinHorse" }));
        }

        [Fact]
        public void CluesNamingSeveralModsLeaveItUnnamed()
        {
            Assert.Null(ModAttribution.Pick("", new[] { "OdinHorse", "Bamboozled" }));
            Assert.Null(ModAttribution.Pick("", new string[0]));
            Assert.Null(ModAttribution.Pick("", null));
            Assert.Null(ModAttribution.Pick("", new[] { "", null }));
        }

        [Fact]
        public void ARecipeOrConversionAnotherModAddedSaysWhich()
        {
            // An item made by its own mod's recipe needs no word of it; a recipe another mod
            // added for it, the game's item or a mod's, says so.
            Assert.Equal("Made at Workbench, added by RecipeManager", ModWords.AddedBy("Made at Workbench", "RecipeManager", ""));
            Assert.Equal("Made at Workbench, added by RecipeManager", ModWords.AddedBy("Made at Workbench", "RecipeManager", "Bamboozled"));
            Assert.Equal("Made at Workbench", ModWords.AddedBy("Made at Workbench", "Bamboozled", "Bamboozled"));
            Assert.Equal("Made at Workbench", ModWords.AddedBy("Made at Workbench", "", "Bamboozled"));
            Assert.Equal("Made at Workbench", ModWords.AddedBy("Made at Workbench", null, null));
        }
    }
}
