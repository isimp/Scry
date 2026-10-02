using Xunit;

namespace Scry.Tests
{
    public class StageGroundTests
    {
        // With the Ground backdrop a model stands on its own biome's ground, painted by the world's
        // terrain as the game paints it: of the biomes it is in, the first players meet, Meadows
        // for none. A dungeon's inside or a room of one has no ground of the world's: there the
        // plain floor stays. Sky goes with ground as it goes with the grid.

        [Fact]
        public void TheBackdropsAreThePlainOnesTheGridAndTheGround()
        {
            Assert.Equal(new[] { "Plain", "Sky", "Grid", "Sky and grid", "Ground", "Sky and ground" }, StageGround.Backdrops);

            Assert.False(StageGround.Sky(0));
            Assert.True(StageGround.Sky(1));
            Assert.True(StageGround.Sky(3));
            Assert.True(StageGround.Sky(5));
            Assert.False(StageGround.Sky(4));

            Assert.True(StageGround.Grid(2));
            Assert.True(StageGround.Grid(3));
            Assert.False(StageGround.Grid(4));

            Assert.True(StageGround.Ground(4, underground: false));
            Assert.True(StageGround.Ground(5, underground: false));
            Assert.False(StageGround.Ground(1, underground: false));
            Assert.False(StageGround.Ground(3, underground: false));
        }

        [Fact]
        public void UndergroundThePlainFloorStays()
        {
            Assert.False(StageGround.Ground(4, underground: true));
            Assert.False(StageGround.Ground(5, underground: true));
            // The plain floor shows where neither the grid nor the ground does.
            Assert.True(StageGround.Floor(4, underground: true));
            Assert.False(StageGround.Floor(4, underground: false));
            Assert.False(StageGround.Floor(2, underground: false));
            Assert.True(StageGround.Floor(1, underground: false));
        }

        [Fact]
        public void TheGroundIsPaintedAsTheWorldBuildsItsOwn()
        {
            // The terrain's paint mask keeps more than what is painted on it: its fourth share is
            // the Ashlands' lava and the Mistlands' own (WorldGenerator.GetAshlandsHeight,
            // GetMistlandsHeight), whole elsewhere. Ashlands ground without lava, Mistlands' at its
            // most common, the rest as the world leaves it.
            Assert.Equal(0f, StageGround.MaskShare("AshLands"));
            Assert.Equal(0f, StageGround.MaskShare("Mistlands"));
            Assert.Equal(1f, StageGround.MaskShare("Meadows"));
            Assert.Equal(1f, StageGround.MaskShare("Mountain"));
            Assert.Equal(1f, StageGround.MaskShare("ModBiome"));

            // The sea's floor lies under water: a few metres of it over the ground.
            Assert.Equal(4f, StageGround.WaterOver("Ocean"));
            Assert.Null(StageGround.WaterOver("Meadows"));
            Assert.Null(StageGround.WaterOver("Swamp"));
        }

        [Fact]
        public void AModelStandsOnTheGroundOfTheFirstBiomePlayersMeetOfItsOwn()
        {
            Assert.Equal("BlackForest", StageGround.BiomeFor(new[] { "Swamp", "BlackForest" }));
            Assert.Equal("Mountain", StageGround.BiomeFor(new[] { "DeepNorth", "Mountain" }));
            Assert.Equal("Ocean", StageGround.BiomeFor(new[] { "Ocean" }));
            // A biome a mod adds comes after the game's; none, or nothing, is Meadows.
            Assert.Equal("Plains", StageGround.BiomeFor(new[] { "ModBiome", "Plains" }));
            Assert.Equal("ModBiome", StageGround.BiomeFor(new[] { "ModBiome" }));
            // Of two a mod adds, the first named; a name left empty is none.
            Assert.Equal("ModA", StageGround.BiomeFor(new[] { "ModA", "ModB" }));
            Assert.Equal("Meadows", StageGround.BiomeFor(new[] { "" }));
            Assert.Equal("Meadows", StageGround.BiomeFor(new string[0]));
            Assert.Equal("Meadows", StageGround.BiomeFor(null));
        }
    }
}
