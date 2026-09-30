using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class LocationTests
    {
        // ZoneSystem.GenerateLocationsTimeSliced tries points in the location's biomes and turns
        // each away that breaks one of its rules. Altitude is the ground's height less 30, the sea's.

        private static LocationRules Crypt() => new LocationRules
        {
            Quantity = 200, Biomes = "Black Forest", BiomeArea = "Everything", MinAltitude = 1f, MaxAltitude = 1000f,
            MinSimilar = 128f, ExteriorRadius = 12f, MaxTerrainDelta = 2f,
        };

        private static string Row(LocationRules rules, string label) => LocationWords.Rows(rules).FirstOrDefault(r => r.Key == label).Value;

        [Fact]
        public void APlacesRulesAreToldEachOnItsOwnRow()
        {
            var rules = Crypt();
            Assert.Equal("up to 200", Row(rules, "Per world"));
            Assert.Equal("Black Forest", Row(rules, "Biome"));
            Assert.Equal("at least 1 m", Row(rules, "Above the sea"));
            Assert.Equal("at least 128 m from another of its kind", Row(rules, "Apart"));
            Assert.Equal("rising at most 2 m within 12 m of it", Row(rules, "Ground"));
        }

        [Fact]
        public void RulesAtTheirDefaultsAreLeftOut()
        {
            var rules = new LocationRules { Quantity = 10, Biomes = "Meadows", BiomeArea = "Everything", MinAltitude = -1000f, MaxAltitude = 1000f, MaxTerrainDelta = 2f, ExteriorRadius = 10f };
            var labels = LocationWords.Rows(rules).Select(r => r.Key).ToList();
            Assert.DoesNotContain("Above the sea", labels);
            Assert.DoesNotContain("Apart", labels);
            Assert.DoesNotContain("From the centre", labels);
            Assert.DoesNotContain("Placed", labels);
        }

        [Fact]
        public void TheBiomesAreaSaysWhetherAwayFromOrAtItsEdges()
        {
            var rules = Crypt();
            rules.BiomeArea = "Median";
            Assert.Equal("Black Forest, away from its edges", Row(rules, "Biome"));
            rules.BiomeArea = "Edge";
            Assert.Equal("Black Forest, only at its edges", Row(rules, "Biome"));
        }

        [Fact]
        public void DistanceFromTheCentreTakesTheTighterOfItsTwoRules()
        {
            var rules = Crypt();
            rules.MaxDistance = 1000f;
            Assert.Equal("within 1,000 m", Row(rules, "From the centre"));
            rules.MinDistance = 3000f;
            rules.MaxDistance = 8000f;
            Assert.Equal("3,000–8,000 m", Row(rules, "From the centre"));
            rules.MaxDistance = 0f;
            rules.MinDistanceFromCenter = 4000f;
            Assert.Equal("beyond 4,000 m", Row(rules, "From the centre"));

            var both = Crypt();
            both.MaxDistance = 8000f;
            both.MaxDistanceFromCenter = 5000f;
            Assert.Equal("within 5,000 m", Row(both, "From the centre"));
        }

        [Fact]
        public void AUniquePlaceSaysOnlyThat()
        {
            var rules = Crypt();
            rules.Unique = true;
            Assert.Equal("one only, the first reached keeps its place", Row(rules, "Placed"));
        }

        [Fact]
        public void AltitudeReadsAsARangeOrAnEitherSide()
        {
            var rules = Crypt();
            rules.MinAltitude = 150f;
            rules.MaxAltitude = 500f;
            Assert.Equal("150–500 m", Row(rules, "Above the sea"));
            rules.MinAltitude = -1000f;
            rules.MaxAltitude = 60f;
            Assert.Equal("at most 60 m", Row(rules, "Above the sea"));
        }

        [Fact]
        public void SpacingNamesTheGroupWhenItKeepsApartFromAGroup()
        {
            var rules = Crypt();
            rules.MinSimilar = 3000f;
            rules.Group = "Bossstones";
            Assert.Equal("at least 3,000 m from another of the group \"Bossstones\"", Row(rules, "Apart"));
            rules.MaxSimilar = 200f;
            rules.GroupMax = "Village";
            Assert.Equal("within 200 m of one of the group \"Village\"", Row(rules, "Near"));
        }

        [Fact]
        public void GroundCanAskForASlope()
        {
            var rules = Crypt();
            rules.MinTerrainDelta = 5f;
            rules.MaxTerrainDelta = 1000f;
            Assert.Equal("rising at least 5 m within 12 m of it", Row(rules, "Ground"));
            rules.MaxTerrainDelta = 10f;
            Assert.Equal("rising 5–10 m within 12 m of it", Row(rules, "Ground"));
        }

        [Fact]
        public void HowItIsPlacedTellsPriorityUniquenessAndTheCentreFirst()
        {
            var rules = Crypt();
            rules.Prioritized = true;
            rules.Unique = true;
            rules.CenterFirst = true;
            Assert.Equal("before the others with five times the tries; outward from the world's centre; one only, the first reached keeps its place", Row(rules, "Placed"));
        }

        [Fact]
        public void SeveralRuleSetsForOnePrefabReadAsOneLineEach()
        {
            var rules = Crypt();
            rules.MinSimilar = 0f;
            Assert.Equal("up to 200 in Black Forest, at least 1 m above the sea, rising at most 2 m within 12 m of it", LocationWords.Line(rules));
            rules.MaxDistance = 1000f;
            Assert.Equal("up to 200 in Black Forest, within 1,000 m from the centre, at least 1 m above the sea, rising at most 2 m within 12 m of it", LocationWords.Line(rules));
        }

        [Theory]
        [InlineData("forestcrypt_Corridor2", "Corridor 2")]
        [InlineData("gobvill_hut02", "Hut 02")]
        [InlineData("sunkencrypt_EndCap", "End cap")]
        [InlineData("dvergtown_room_1", "Room 1")]
        [InlineData("Wide", "Wide")]
        public void ARoomIsNamedByItsPrefabWithoutItsDungeonsTag(string prefab, string name)
        {
            // Room prefabs start with their dungeon's tag, which the group they are listed under already says.
            Assert.Equal(name, LocationWords.RoomName(prefab));
        }

        [Fact]
        public void LocationsAreGroupedByBiomeInTheOrderPlayersMeetThem()
        {
            var meadows = LocationWords.Group(new[] { "Meadows" });
            var forest = LocationWords.Group(new[] { "BlackForest" });
            var swamp = LocationWords.Group(new[] { "Swamp" });
            var north = LocationWords.Group(new[] { "DeepNorth" });
            var several = LocationWords.Group(new[] { "Meadows", "BlackForest" });
            Assert.True(meadows.Order < forest.Order && forest.Order < swamp.Order && swamp.Order < north.Order);
            Assert.Equal("In several biomes", several.Name);
            Assert.True(several.Order > north.Order);
        }

        [Fact]
        public void ABiomeAModAddedComesAfterTheGamesOwn()
        {
            var modded = LocationWords.Group(new[] { "CustomBiome" });
            Assert.True(modded.Order > LocationWords.Group(new[] { "Ocean" }).Order);
            Assert.True(modded.Order < LocationWords.Group(new[] { "Meadows", "Swamp" }).Order);
        }

        [Fact]
        public void RoomsComeAfterEveryPlaceUnderTheirDungeon()
        {
            var rooms = LocationWords.RoomGroup("Burial Chamber");
            Assert.Equal("Burial Chamber rooms", rooms.Name);
            Assert.True(rooms.Order > LocationWords.Group(new[] { "Meadows", "Swamp" }).Order);
        }
    }
}
