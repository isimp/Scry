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
        public void FootstepsSoundOnTheGroundTheStageShows()
        {
            // The plain floor is the game's default ground; a biome's ground by the game's rule for flat ground.
            Assert.Equal(StepGround.Default, StageGround.Footsteps("Meadows", groundShown: false));
            Assert.Equal(StepGround.Snow, StageGround.Footsteps("Mountain", groundShown: true));
            Assert.Equal(StepGround.Snow, StageGround.Footsteps("DeepNorth", groundShown: true));
            Assert.Equal(StepGround.Mud, StageGround.Footsteps("Swamp", groundShown: true));
            Assert.Equal(StepGround.Grass, StageGround.Footsteps("Meadows", groundShown: true));
            Assert.Equal(StepGround.Grass, StageGround.Footsteps("BlackForest", groundShown: true));
            Assert.Equal(StepGround.Ashlands, StageGround.Footsteps("AshLands", groundShown: true));
            Assert.Equal(StepGround.GenericGround, StageGround.Footsteps("Plains", groundShown: true));

            // On the sea's floor the feet stand in its water, and the game steps on water before any ground.
            Assert.Equal(StepGround.Water, StageGround.Footsteps("Ocean", groundShown: true));
            Assert.Equal(StepGround.GenericGround, StageGround.Footsteps(null, groundShown: true));
        }

        [Fact]
        public void TheStepPlayedIsTheOneTheGameWouldPlay()
        {
            // Of the steps fitting the way of moving, the last made for the ground, wherever a default one stands.
            var made = new[] { StepGround.Default, StepGround.Snow, StepGround.Snow | StepGround.Mud, StepGround.Default };
            var fits = new[] { true, true, true, true };
            Assert.Equal(2, StageGround.GameStep(made, fits, StepGround.Snow));
            Assert.Equal(2, StageGround.StageStep(made, fits, StepGround.Snow));
            Assert.Equal(3, StageGround.GameStep(made, fits, StepGround.Default));

            // With none made for the ground, the first default one.
            Assert.Equal(0, StageGround.GameStep(made, fits, StepGround.Grass));
            Assert.Equal(0, StageGround.StageStep(made, fits, StepGround.Grass));

            // A step made for the ground but not for the way of moving is passed over for a default one that is.
            var running = new[] { StepGround.Snow, StepGround.Default };
            var walking = new[] { false, true };
            Assert.Equal(1, StageGround.GameStep(running, walking, StepGround.Snow));
            Assert.Equal(1, StageGround.StageStep(running, walking, StepGround.Snow));
        }

        [Fact]
        public void ACreatureWithNoStepForTheGroundIsNeverMuteOnTheStage()
        {
            // Where the game falls silent: ground in general, then any way of moving, then the first step there is.
            var general = new[] { StepGround.Grass, StepGround.GenericGround };
            Assert.Equal(-1, StageGround.GameStep(general, new[] { true, true }, StepGround.Snow));
            Assert.Equal(1, StageGround.StageStep(general, new[] { true, true }, StepGround.Snow));

            var runningOnly = new[] { StepGround.Grass, StepGround.Snow };
            Assert.Equal(-1, StageGround.GameStep(runningOnly, new[] { false, false }, StepGround.Snow));
            Assert.Equal(1, StageGround.StageStep(runningOnly, new[] { false, false }, StepGround.Snow));
            Assert.Equal(1, StageGround.StageStep(general, new[] { false, false }, StepGround.Snow));

            Assert.Equal(0, StageGround.StageStep(new[] { StepGround.Water, StepGround.Wood }, new[] { false, true }, StepGround.Snow));
            Assert.Equal(-1, StageGround.StageStep(new StepGround[0], new bool[0], StepGround.Snow));
        }

        [Fact]
        public void GrassKeepsOffWhatStandsOnTheStageAndWhatLiesSmallInIt()
        {
            // A bear: its footprint and a margin round it.
            Assert.Equal(1.5f, StageGround.ClearOfGrass(footprint: 1f, height: 1.5f), 3);
            // A tree: wider, and as tall as it likes.
            Assert.Equal(2.7f, StageGround.ClearOfGrass(footprint: 2f, height: 10f), 3);
            // A sword lying down or a pebble: low enough for grass to hide it, so a wider ring.
            Assert.Equal(1.5f, StageGround.ClearOfGrass(footprint: 0.6f, height: 0.1f), 3);
            Assert.Equal(1.5f, StageGround.ClearOfGrass(footprint: 0.05f, height: 0.05f), 3);
            // Something small but standing taller than grass needs only its own margin.
            Assert.Equal(0.54f, StageGround.ClearOfGrass(footprint: 0.2f, height: 1f), 3);
        }

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
        public void TheGroundsBiomeCanBePicked()
        {
            // Auto is the entry's own; any biome can stand in for it, cycled through in the order players meet them.
            Assert.Equal("Auto", StageGround.Choices[0]);
            Assert.Equal("Meadows", StageGround.Choices[1]);
            Assert.Equal("Ocean", StageGround.Choices[StageGround.Choices.Length - 1]);
            Assert.Equal("Swamp", StageGround.Chosen("Swamp", new[] { "Plains" }));
            Assert.Equal("Plains", StageGround.Chosen("Auto", new[] { "Plains" }));
            Assert.Equal("Plains", StageGround.Chosen(null, new[] { "Plains" }));
            Assert.Equal("Meadows", StageGround.Next("Auto"));
            Assert.Equal("BlackForest", StageGround.Next("Meadows"));
            Assert.Equal("Auto", StageGround.Next("Ocean"));
            Assert.Equal("Auto", StageGround.Next("Gone"));
        }

        [Fact]
        public void TheGroundReachesFarWithTheSkyAndFadesToItsEdge()
        {
            // Several times what is framed, out to a horizon with the sky; never less than some way.
            Assert.Equal(60f, StageGround.Across(10f, sky: false), 3);
            Assert.Equal(24f, StageGround.Across(1f, sky: false), 3);
            Assert.Equal(400f, StageGround.Across(20f, sky: true), 3);
            Assert.Equal(160f, StageGround.Across(1f, sky: true), 3);

            // Whole in its middle, fading over its outer part to nothing at its edge.
            Assert.Equal(0f, StageGround.Fade(0f), 3);
            Assert.Equal(0f, StageGround.Fade(0.5f), 3);
            Assert.Equal(1f, StageGround.Fade(0.95f), 3);
            Assert.Equal(1f, StageGround.Fade(1.2f), 3);
            var half = StageGround.Fade(0.775f);
            Assert.True(half > 0.3f && half < 0.7f, $"{half}");
            Assert.True(StageGround.Fade(0.7f) < StageGround.Fade(0.8f));
            // Eased in and out: a quarter of the way along, less than a quarter faded.
            Assert.Equal(0.156f, StageGround.Fade(0.6875f), 3);
        }

        [Fact]
        public void TheBiomesLightGoesWithTheTimeOfDayChosen()
        {
            // Studio and Day by day, Dusk by evening, Night by night; the cave keeps its own.
            Assert.Equal(StageGround.Time.Day, StageGround.TimeFor("Studio"));
            Assert.Equal(StageGround.Time.Day, StageGround.TimeFor("Day"));
            Assert.Equal(StageGround.Time.Evening, StageGround.TimeFor("Dusk"));
            Assert.Equal(StageGround.Time.Night, StageGround.TimeFor("Night"));
            Assert.Null(StageGround.TimeFor("Cave"));
            Assert.Null(StageGround.TimeFor("Other"));
        }

        [Fact]
        public void GrassIsScatteredAsAtAHeightTypicalOfTheBiome()
        {
            // The game scatters its grass by height over the sea; the stage stands at none, so
            // each biome's ground is taken at one typical of it: the mountains high, the swamp
            // low, the sea's floor under its water, the rest a little over the sea.
            Assert.Equal(120f, StageGround.Altitude("Mountain"), 3);
            Assert.Equal(120f, StageGround.Altitude("DeepNorth"), 3);
            Assert.Equal(2f, StageGround.Altitude("Swamp"), 3);
            Assert.Equal(-4f, StageGround.Altitude("Ocean"), 3);
            Assert.Equal(10f, StageGround.Altitude("Meadows"), 3);
            Assert.Equal(10f, StageGround.Altitude("ModBiome"), 3);
        }

        [Fact]
        public void APlacePaintsItsGroundAsTheGamePaintsItsTerrain()
        {
            // Each paint blends toward its colour by (1 - its distance over its reach) to the
            // tenth power and its strength, keeping the ground's fourth share unless it clears
            // the vegetation; later paints go over earlier ones.
            var ground = (0f, 0f, 0f, 1f);
            var dirt = new GroundPaint { X = 0f, Z = 0f, Radius = 4f, Strength = 1f, Color = (1f, 0f, 0f, 1f) };
            Assert.Equal((1f, 0f, 0f, 1f), StageGround.Painted(ground, 0f, 0f, new[] { dirt }));
            Assert.Equal(ground, StageGround.Painted(ground, 5f, 0f, new[] { dirt }));
            var near = StageGround.Painted(ground, 3.9f, 0f, new[] { dirt });
            Assert.True(near.R > 0.5f && near.R < 1f, $"{near.R}");

            var weak = new GroundPaint { X = 0f, Z = 0f, Radius = 4f, Strength = 0.5f, Color = (0f, 0f, 1f, 1f) };
            var both = StageGround.Painted(ground, 0f, 0f, new[] { dirt, weak });
            Assert.Equal(0.5f, both.R, 3);
            Assert.Equal(0.5f, both.B, 3);

            var bare = new GroundPaint { X = 0f, Z = 0f, Radius = 4f, Strength = 1f, Color = (0f, 0f, 0f, 0f), ClearsVegetation = true };
            Assert.Equal(0f, StageGround.Painted(ground, 0f, 0f, new[] { bare }).A, 3);
            var paved = new GroundPaint { X = 0f, Z = 0f, Radius = 4f, Strength = 1f, Color = (0f, 0f, 1f, 0.2f) };
            Assert.Equal(1f, StageGround.Painted(ground, 0f, 0f, new[] { paved }).A, 3);

            // A paint reaching nowhere paints nothing, even where it stands.
            var none = new GroundPaint { X = 0f, Z = 0f, Radius = 0f, Strength = 1f, Color = (1f, 0f, 0f, 1f) };
            Assert.Equal(ground, StageGround.Painted(ground, 0f, 0f, new[] { none }));
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

        // The ground's paint mask: two points while nothing is painted on it, else half a metre a
        // point, at least 32 and at most 256 across, however wide the ground.

        [Theory]
        [InlineData(0, 300f, 2)]
        [InlineData(3, 24f, 48)]
        [InlineData(3, 10f, 32)]
        [InlineData(3, 160f, 256)]
        [InlineData(1, 47.6f, 96)]
        public void TheMaskIsHalfAMetreAPointWhereSomethingIsPainted(int paints, float across, int size)
        {
            Assert.Equal(size, StageGround.MaskSize(paints, across));
        }
    }
}
