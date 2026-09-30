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
            draugr.FoundIn = new[] { "Crypt", "Sunken crypt rooms" };

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

            var raid = E("foresttrolls", Kind.Raid, "The ground is shaking");
            raid.Biomes = new[] { "Meadows", "BlackForest" };

            return new List<Entry> { troll, draugr, blob, statue, death, rested, raid };
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
            // With no words typed the list is in the order of the names it shows, as everywhere else.
            Assert.Equal(new[] { "Draugr", "BlobElite" }, Find("biome:swamp"));
        }

        [Fact]
        public void InFindsWhatIsFoundInALocationOrDungeonWrittenWithoutItsSpaces()
        {
            Assert.Equal(new[] { "Draugr" }, Find("in:crypt"));
            Assert.Equal(new[] { "Draugr" }, Find("in:sunkencryptrooms"));
            Assert.Empty(Find("in:stonehenge"));
            Assert.Equal(2, Find("kind:creature -in:crypt").Count);
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
        public void RaidsAreFoundByNameKindAndBiomeLikeEverythingElse()
        {
            Assert.Equal(new[] { "foresttrolls" }, Find("kind:raid"));
            Assert.Equal(new[] { "foresttrolls" }, Find("kind:raids"));
            Assert.Equal(new[] { "foresttrolls" }, Find("ground shaking"));
            Assert.Equal(new[] { "foresttrolls", "Troll" }, Find("biome:blackforest"));
        }

        [Fact]
        public void LocationsAndRoomsAreFoundByKindAndByThePlaceTheyBelongTo()
        {
            var crypt = E("Crypt2", Kind.Location, "Burial Chambers");
            crypt.FoundIn = new[] { "Burial Chambers" };
            var room = E("forestcrypt_Bend1", Kind.Location, "Bend 1");
            room.FoundIn = new[] { "Burial Chambers" };
            var catalog = Catalog();
            catalog.Add(crypt);
            catalog.Add(room);
            List<string> In(string text) => Search.Run(catalog, new Query { Text = text }, new List<string>()).Select(e => e.Name).ToList();

            Assert.Equal(new[] { "forestcrypt_Bend1", "Crypt2" }, In("kind:loc"));
            Assert.Equal(new[] { "forestcrypt_Bend1", "Crypt2" }, In("kind:locations in:burial"));
        }

        [Fact]
        public void ModFindsWhatANamedModAdded()
        {
            Assert.Equal(new[] { "CoolMod_TrollStatue" }, Find("mod:statues"));
        }

        [Fact]
        public void PlayedByFindsTheEffectsAPrefabPlays()
        {
            Assert.Equal(new[] { "vfx_troll_death" }, Find("playedby:troll"));
        }

        [Fact]
        public void TheOldUsedTermStillFindsWhatAPrefabPlays()
        {
            // "used:" read as "what it is used for", which the details mean by crafting; it stays as another spelling.
            Assert.Equal(new[] { "vfx_troll_death" }, Find("used:troll"));
            Assert.Equal(Find("-playedby:troll"), Find("-used:troll"));
        }

        [Fact]
        public void AMinusLeavesThingsOut()
        {
            Assert.Equal(new[] { "Troll", "vfx_troll_death", "foresttrolls" }, Find("troll -statue"));
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
