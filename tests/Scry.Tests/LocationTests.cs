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

        // A place's music is told with when the game plays it: its own source on coming near
        // (MusicLocation), one of the game's pieces by name on stepping inside, at its chance
        // (MusicVolume), or the music of the weather it sets inside (EnvZone). Enter plays it.

        private static PlaceMusic Tune(string name, MusicWhen when, float chance = 1f) => new PlaceMusic { Name = name, When = when, Chance = chance };

        [Fact]
        public void APlacesOwnMusicPlaysWhenYouComeNear()
        {
            Assert.Equal("Music_FulingCamp when you come near. Enter plays it", LocationWords.Music(new[] { Tune("Music_FulingCamp", MusicWhen.Near) }));
        }

        [Fact]
        public void MusicOnSteppingInsideTellsHowOftenItPlays()
        {
            Assert.Equal("Location_Crypt each time you step inside. Enter plays it", LocationWords.Music(new[] { Tune("Location_Crypt", MusicWhen.Inside) }));
            Assert.Equal("Location_Crypt on 70% of the times you step inside. Enter plays it", LocationWords.Music(new[] { Tune("Location_Crypt", MusicWhen.Inside, 0.7f) }));
        }

        [Fact]
        public void TheMusicOfTheWeatherInsidePlaysWhileYouAreInside()
        {
            Assert.Equal("crypt while you are inside. Enter plays it", LocationWords.Music(new[] { Tune("crypt", MusicWhen.Weather) }));
        }

        [Fact]
        public void SeveralPiecesOfMusicAreToldInTurnAndEnterPlaysTheFirst()
        {
            Assert.Equal("Music_StoneHenge when you come near; crypt while you are inside. Enter plays the first",
                LocationWords.Music(new[] { Tune("Music_StoneHenge", MusicWhen.Near), Tune("crypt", MusicWhen.Weather) }));
        }

        [Fact]
        public void APlaceWithoutMusicTellsNone()
        {
            Assert.Null(LocationWords.Music(new PlaceMusic[0]));
            Assert.Null(LocationWords.Music(null));
        }

        // A location's model is loaded when it is selected; until it is in, the stage and the
        // details say so, and a model that could not be loaded is not promised again.

        [Fact]
        public void WhileItsModelLoadsTheStageAndDetailsSaySo()
        {
            Assert.Equal("Loading its model", LocationWords.StageNote(PlaceLoad.Loading));
            Assert.Equal("read once its model has loaded", LocationWords.HoldsNote(PlaceLoad.Loading));
        }

        [Fact]
        public void AModelThatCouldNotBeLoadedIsToldAsSuch()
        {
            Assert.Equal("Its model could not be loaded.", LocationWords.StageNote(PlaceLoad.Failed));
            Assert.Equal("not known, its model could not be loaded", LocationWords.HoldsNote(PlaceLoad.Failed));
        }

        [Fact]
        public void ALoadedModelLeavesTheStageToItsCopy()
        {
            Assert.Null(LocationWords.StageNote(PlaceLoad.Ready));
        }

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

        // WorldGenerator.GetForestFactor is noise from 0, the thickest woods, to about 2.2; the
        // Meadows count as wooded below 1.15 (InForest). A location in woods keeps to a range of it.

        private const string ForestScale = " (0 is the thickest woods, about 2.2 the most open; the Meadows are wooded below 1.15)";

        [Fact]
        public void AWoodsRuleSaysItsRangeOfTheForestFactor()
        {
            var rules = Crypt();
            Assert.Null(Row(rules, "Woods"));
            rules.InForest = true;
            rules.ForestMin = 0.5f;
            rules.ForestMax = 1f;
            Assert.Equal("forest factor 0.5–1" + ForestScale, Row(rules, "Woods"));
            rules.ForestMin = 0f;
            Assert.Equal("forest factor at most 1" + ForestScale, Row(rules, "Woods"));
            rules.ForestMin = 1f;
            rules.ForestMax = 5f;
            Assert.Equal("forest factor at least 1" + ForestScale, Row(rules, "Woods"));
            rules.ForestMin = 0f;
            Assert.Null(Row(rules, "Woods"));
        }

        // The ground value the generator reads where it places a location is the Ashlands' lava
        // (from 0.6 it counts as lava, ZoneSystem.IsLavaPreHeightmap), the Mistlands' growth, and
        // 0 in every other biome (WorldGenerator.GetBiomeHeight). It must be over the minimum and
        // under the maximum.

        private static LocationRules In(params string[] biomes)
        {
            var rules = Crypt();
            rules.BiomeKeys = biomes;
            return rules;
        }

        [Fact]
        public void InTheAshlandsTheGroundRuleIsAboutLava()
        {
            var rules = In("AshLands");
            Assert.Null(Row(rules, "Lava"));
            rules.MaxVegetation = 0.1f;
            Assert.Equal("only on ground less than 10% lava (the game counts 60% and more as lava)", Row(rules, "Lava"));
            rules.MinVegetation = 0.6f;
            rules.MaxVegetation = 1f;
            Assert.Equal("only on ground more than 60% lava (the game counts 60% and more as lava)", Row(rules, "Lava"));
            rules.MinVegetation = 0.2f;
            rules.MaxVegetation = 0.5f;
            Assert.Equal("only on ground 20–50% lava (the game counts 60% and more as lava)", Row(rules, "Lava"));
        }

        [Fact]
        public void InTheMistlandsTheGroundRuleIsAboutGrowth()
        {
            var rules = In("Mistlands");
            rules.MaxVegetation = 0.3f;
            Assert.Equal("only where the Mistlands' growth value, which their plants grow by, is under 0.3", Row(rules, "Growth"));
            rules.MinVegetation = 0.1f;
            Assert.Equal("only where the Mistlands' growth value, which their plants grow by, is 0.1–0.3", Row(rules, "Growth"));
            rules.MaxVegetation = 1f;
            Assert.Equal("only where the Mistlands' growth value, which their plants grow by, is over 0.1", Row(rules, "Growth"));
        }

        [Fact]
        public void WhereTheGroundHasNoValueAMinimumIsNeverMetAndAMaximumAlwaysIs()
        {
            var rules = In("Meadows");
            rules.MaxVegetation = 0.5f;
            Assert.DoesNotContain(LocationWords.Rows(rules), r => r.Key == "Ground value");
            rules.MinVegetation = 0.2f;
            Assert.Equal("never met: it asks for lava or Mistlands growth, which its biome does not have", Row(rules, "Ground value"));
        }

        [Fact]
        public void SeveralBiomesTellTheGroundValueForEach()
        {
            var rules = In("AshLands", "Mistlands");
            rules.MaxVegetation = 0.1f;
            Assert.Equal("under 0.1 (lava in the Ashlands, growth in the Mistlands, none elsewhere)", Row(rules, "Ground value"));
        }

        // ZoneSystem.GenerateLocationsTimeSliced sums the ground value round a spot and keeps only
        // spots at least so far from the average of those that met the other rules to the most found.

        [Fact]
        public void TheSurroundingsAreComparedWithTheOtherSpotsTried()
        {
            var rules = In("AshLands");
            rules.SurroundCheck = true;
            rules.SurroundDistance = 30f;
            Assert.Equal("more lava within 30 m than the average spot that meets its other rules", Row(rules, "Surroundings"));
            rules.SurroundBetter = 0.5f;
            Assert.Equal("lava within 30 m at least 50% of the way from the average spot that meets its other rules to the most found", Row(rules, "Surroundings"));
        }

        [Fact]
        public void SurroundingsWithNothingToMeasureAreLeftOut()
        {
            var rules = In("Mountain");
            rules.SurroundCheck = true;
            rules.SurroundDistance = 40f;
            Assert.Null(Row(rules, "Surroundings"));
        }

        [Fact]
        public void TheLineOfARuleSetTellsTheWoodsAndGroundToo()
        {
            var rules = In("AshLands");
            rules.MaxVegetation = 0.1f;
            rules.InForest = true;
            rules.ForestMax = 1f;
            rules.SurroundCheck = true;
            var line = LocationWords.Line(rules);
            Assert.Contains("more lava within 20 m than the average spot", line);
            Assert.Contains("forest factor at most 1", line);
            Assert.Contains("on ground less than 10% lava", line);
            Assert.DoesNotContain("thickest", line);
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

        [Fact]
        public void APlaceKeptToOnePartOfItsBiomeSaysWhich()
        {
            Assert.Equal("the mountain part of its biome", LocationWords.OnlyInPart("Mountain"));
            Assert.Equal("1 in the world, only in the black forest part of it", LocationWords.WithPart("1 in the world", "BlackForest"));
            Assert.Equal("1 in the world", LocationWords.WithPart("1 in the world", ""));
        }

        [Fact]
        public void EachSetAfterTheFirstIsAlsoPlaced()
        {
            Assert.Equal("Placed", LocationWords.Placed(first: true));
            Assert.Equal("Also placed", LocationWords.Placed(first: false));
        }

        [Fact]
        public void NothingIsBuiltNearAPlaceThatForbidsIt() => Assert.Equal("not within 1,500 m", LocationWords.NoBuild(1500f));
    }
}
