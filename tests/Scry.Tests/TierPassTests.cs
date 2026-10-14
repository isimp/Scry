using Xunit;

namespace Scry.Tests
{
    public class TierPassTests
    {
        // Beating a boss opens everything of its tier to the milestone floor. An entry's tier is
        // worked out the way progression works: the cheapest way to come by it, each way costing
        // the highest tier of what it needs, so mods' content gets a tier too.

        [Fact]
        public void TheBiomesFollowTheBossesAndTheSeaAndTheNorthHaveNoTier()
        {
            Assert.Equal(1, Tiers.OfBiome("Meadows"));
            Assert.Equal(2, Tiers.OfBiome("BlackForest"));
            Assert.Equal(3, Tiers.OfBiome("Swamp"));
            Assert.Equal(4, Tiers.OfBiome("Mountain"));
            Assert.Equal(5, Tiers.OfBiome("Plains"));
            Assert.Equal(6, Tiers.OfBiome("Mistlands"));
            Assert.Equal(7, Tiers.OfBiome("AshLands"));
            Assert.Equal(Tiers.None, Tiers.OfBiome("Ocean"));
            Assert.Equal(Tiers.None, Tiers.OfBiome("DeepNorth"));
            Assert.Equal(Tiers.None, Tiers.OfBiome(null));
            Assert.Equal(1, Tiers.OfBoss("Eikthyr"));
            Assert.Equal(2, Tiers.OfBoss("gd_king"));
            Assert.Equal(7, Tiers.OfBoss("Fader"));
            Assert.Equal(Tiers.None, Tiers.OfBoss("Troll"));
            Assert.Equal(7, Tiers.Last);
            Assert.Equal("Bonemass", Tiers.BossOf(3));
            Assert.Equal("AshLands", Tiers.BiomeOf(7));
            Assert.Null(Tiers.BossOf(0));
            Assert.Null(Tiers.BiomeOf(8));
        }

        [Fact]
        public void ACreatureBelongsToTheEarliestBiomeItLivesIn()
        {
            var pass = new TierPass();
            pass.Add("Troll", Tiers.OfBiome("Mountain"));
            pass.Add("Troll", Tiers.OfBiome("BlackForest"));

            Assert.Equal(2, pass.Work()["Troll"]);
        }

        [Fact]
        public void AnItemCostsItsCheapestWayAndAWayItsDearestNeed()
        {
            // Iron: scrap from the swamp's crypts; a sword of it at the forge, made of iron and wood.
            var pass = new TierPass();
            pass.Add("Crypt", 3);
            pass.Add("Draugr", 3);
            pass.Add("IronScrap", 0, "Crypt");
            pass.Add("Iron", 0, "IronScrap", "Smelter");
            pass.Add("Smelter", 0, "SurtlingCore", "Stone");
            pass.Add("SurtlingCore", 2);
            pass.Add("Stone", 1);
            pass.Add("Wood", 1);
            pass.Add("SwordIron", 0, "Iron", "Wood", "Forge");
            pass.Add("Forge", 0, "Stone", "Wood");
            pass.Add("Coins", 0, "Draugr");
            pass.Add("Coins", 1);

            var tiers = pass.Work();

            Assert.Equal(3, tiers["SwordIron"]);
            Assert.Equal(2, tiers["Smelter"]);
            Assert.Equal(1, tiers["Forge"]);
            Assert.Equal(1, tiers["Coins"]);
        }

        [Fact]
        public void AWayNeedingWhatHasNoTierLeadsNowhere()
        {
            // A cheat item, or something only an admin hands out.
            var pass = new TierPass();
            pass.Add("SwordCheat", 0, "Nothing");
            pass.Add("Hammer", 0, "Wood", "Stone");
            pass.Add("Wood", 1);

            var tiers = pass.Work();

            Assert.False(tiers.ContainsKey("SwordCheat"));
            Assert.False(tiers.ContainsKey("Hammer"));
            Assert.False(tiers.ContainsKey("Nothing"));
        }

        [Fact]
        public void WaysThatNeedEachOtherSettleOnTheCheapestWayIn()
        {
            // A plant grows from its seed and gives the seed back.
            var pass = new TierPass();
            pass.Add("Turnip", 0, "TurnipSeeds");
            pass.Add("TurnipSeeds", 0, "Turnip");
            pass.Add("TurnipSeeds", 3);
            pass.Add("Loop", 0, "Loop");

            var tiers = pass.Work();

            Assert.Equal(3, tiers["Turnip"]);
            Assert.Equal(3, tiers["TurnipSeeds"]);
            Assert.False(tiers.ContainsKey("Loop"));
        }

        [Fact]
        public void AWaysOwnTierCountsWithWhatItNeeds()
        {
            // Sold by a trader once a boss has fallen: its tier at least, whatever coins cost.
            var pass = new TierPass();
            pass.Add("Coins", 1);
            pass.Add("Megingjord", Tiers.OfBoss("Bonemass"), "Coins");
            pass.Add("Lantern", 0, "Coins");

            var tiers = pass.Work();

            Assert.Equal(3, tiers["Megingjord"]);
            Assert.Equal(1, tiers["Lantern"]);
        }

        [Fact]
        public void AWayWithANoneTierLeadsNowhere()
        {
            // Met only at sea: no floor shows it.
            var pass = new TierPass();
            pass.Add("Serpent", Tiers.OfBiome("Ocean"));
            pass.Add("SerpentMeat", 0, "Serpent");

            var tiers = pass.Work();

            Assert.False(tiers.ContainsKey("Serpent"));
            Assert.False(tiers.ContainsKey("SerpentMeat"));
        }
    }
}
