using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class SearchSyntaxTests
    {
        private static List<Entry> Catalog()
        {
            var troll = E("Troll", Kind.Creature, "Troll");
            troll.Components = new[] { "Humanoid", "MonsterAI", "CharacterDrop" };
            troll.Biomes = new[] { "BlackForest" };

            var draugr = E("Draugr", Kind.Creature, "Draugr");
            draugr.Components = new[] { "Humanoid", "MonsterAI" };
            draugr.Biomes = new[] { "Swamp" };

            var blob = E("BlobElite", Kind.Creature, "Oozer");
            blob.Components = new[] { "MonsterAI", "Aoe" };
            blob.Biomes = new[] { "Swamp" };

            var statue = E("CoolMod_TrollStatue", Kind.Piece, "Troll statue", Origin.Mod);
            statue.ModName = "Cool Statues";
            statue.Components = new[] { "Piece", "WearNTear" };

            var death = E("vfx_troll_death", Kind.Effect);
            death.UsedBy.Add("Troll");
            death.Components = new[] { "ParticleSystem" };

            var rested = E("Rested", Kind.StatusEffect, "Rested");

            return new List<Entry> { troll, draugr, blob, statue, death, rested };
        }

        private static List<string> Find(string text)
        {
            return Search.Run(Catalog(), new Query { Text = text }, new List<string>()).Select(e => e.Name).ToList();
        }

        [Fact]
        public void HasFindsPrefabsCarryingAComponent()
        {
            Assert.Equal(new[] { "BlobElite" }, Find("has:aoe"));
        }

        [Fact]
        public void BiomeFindsWhatLivesThere()
        {
            // With no words typed the list is in name order, as everywhere else.
            Assert.Equal(new[] { "BlobElite", "Draugr" }, Find("biome:swamp"));
        }

        [Fact]
        public void KindNarrowsToOneKindByAnyReasonableName()
        {
            Assert.Equal(new[] { "CoolMod_TrollStatue" }, Find("troll kind:piece"));
            Assert.Equal(3, Find("kind:creature").Count);
            Assert.Equal(Find("kind:creature"), Find("kind:creatures"));
            Assert.Equal(new[] { "Rested" }, Find("kind:se"));
            Assert.Equal(new[] { "Rested" }, Find("kind:status"));
        }

        [Fact]
        public void ModFindsWhatANamedModAdded()
        {
            Assert.Equal(new[] { "CoolMod_TrollStatue" }, Find("mod:statues"));
        }

        [Fact]
        public void UsedFindsTheEffectsAPrefabPlays()
        {
            Assert.Equal(new[] { "vfx_troll_death" }, Find("used:troll"));
        }

        [Fact]
        public void AMinusLeavesThingsOut()
        {
            Assert.Equal(new[] { "Troll", "vfx_troll_death" }, Find("troll -statue"));
            Assert.Equal(new[] { "Draugr", "Troll" }, Find("kind:creature -has:aoe"));
        }

        [Fact]
        public void TermsCombineWithPlainWords()
        {
            Assert.Equal(new[] { "Draugr" }, Find("has:humanoid biome:swamp"));
            Assert.Equal(new[] { "BlobElite" }, Find("oozer biome:swamp"));
        }

        [Fact]
        public void AnUnknownTermIsSearchedAsPlainText()
        {
            var catalog = new List<Entry> { E("fx:glow", Kind.Effect), E("Troll", Kind.Creature) };

            var found = Search.Run(catalog, new Query { Text = "fx:glow" }, new List<string>());

            Assert.Equal("fx:glow", Assert.Single(found).Name);
        }

        [Fact]
        public void AnEmptyTermMatchesNothingInsteadOfEverything()
        {
            Assert.Empty(Find("has:"));
        }

        [Fact]
        public void TheKindChipsCountWhatTheTermsLetThrough()
        {
            var explorer = new Explorer(Catalog(), new Favourites(System.IO.Path.Combine(TempDir(), "f.txt")));

            explorer.Text = "biome:swamp";

            Assert.Equal(2, explorer.CountOf(Kind.Creature));
            Assert.Equal(0, explorer.CountOf(Kind.Piece));
        }
    }
}
