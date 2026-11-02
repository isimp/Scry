using System;
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
        public void GrassGrowsShortRoundWhatStandsOnTheStageAndWhatLiesSmallInIt()
        {
            // A tree or a wall: over its footprint and a margin round it, as tall as it likes.
            Assert.Equal(2.7f, StageGround.ShortGrassOut(footprint: 2f, height: 10f, GrassStand.Thing), 3);
            Assert.Equal(1.5f, StageGround.ShortGrassOut(footprint: 1f, height: 1.5f, GrassStand.Thing), 3);
            // A sword lying down or a pebble: low enough for grass to hide it, so a wider ring.
            Assert.Equal(1.5f, StageGround.ShortGrassOut(footprint: 0.6f, height: 0.1f, GrassStand.Thing), 3);
            Assert.Equal(1.5f, StageGround.ShortGrassOut(footprint: 0.05f, height: 0.05f, GrassStand.Thing), 3);
            // Something small but standing taller than grass needs only its own margin.
            Assert.Equal(0.54f, StageGround.ShortGrassOut(footprint: 0.2f, height: 1f, GrassStand.Thing), 3);
        }

        // The stage opens looking down 12 degrees; a stem of meadow grass grows 0.8 m whole.
        private static readonly float Rise = (float)Math.Tan(12.0 * Math.PI / 180.0);

        /// <summary>How tall a stem grows, whole and at a point, under the rules for what is shown.</summary>
        private static float Grown(GrassShort rules, float away, float tallWhole = 0.8f) => rules.Stem(away, 1f, tallWhole) * tallWhole;

        [Fact]
        public void NoStemBetweenTheCameraAndAThingLyingLowRisesIntoTheLineItLooksAlong()
        {
            // Wood lying on the meadow, its middle 0.15 m up; a coin, 1 cm.
            foreach (var middle in new[] { 0.15f, 0.01f })
            {
                var rules = StageGround.Short(footprint: 0.65f, height: middle * 2f, middle, GrassStand.Thing, 12f);
                for (var away = 0f; away <= 6f; away += 0.25f)
                    Assert.True(Grown(rules, away) <= middle + away * Rise, $"{away} m out from a thing {middle} m up");
            }
        }

        [Fact]
        public void PastWhereTheLineRisesOverTheGrassItGrowsWhole()
        {
            // The line from the camera to the wood's middle passes over 0.8 m stems some 3 m out: from
            // there on, and the ring of short grass passed, they grow whole.
            var rules = StageGround.Short(footprint: 0.65f, height: 0.3f, 0.15f, GrassStand.Thing, 12f);
            Assert.Equal(0.8f, Grown(rules, 5f), 3);
            Assert.Equal(0.8f, Grown(rules, 9f), 3);
            // Closer, under the line, stems still grow: no bare ground round it, each as tall as keeps
            // its tip a fifth of the line's height under it.
            Assert.True(Grown(rules, 1f) > 0.1f);
            Assert.Equal(GrassShort.SightRoom * (0.15f + Rise), Grown(rules, 1f), 3);
        }

        [Fact]
        public void AThingStandingOverTheGrassKeepsItsGrassAsBefore()
        {
            // A workbench, its middle 0.9 m up: the line it is looked along runs over every stem.
            var rules = StageGround.Short(footprint: 1f, height: 1.8f, 0.9f, GrassStand.Thing, 12f);
            for (var away = 0f; away <= 6f; away += 0.5f)
                Assert.Equal(StageGround.StemScale(away, rules.Out) * 0.8f, Grown(rules, away), 4);
        }

        [Fact]
        public void AFigureOrAPlaceStandsInItsGrassAsBefore()
        {
            foreach (var stand in new[] { GrassStand.Figure, GrassStand.Place })
            {
                var rules = StageGround.Short(footprint: 0.3f, height: 0.3f, 0.15f, stand, 12f);
                Assert.Equal(StageGround.ShortGrassOut(0.3f, 0.3f, stand), rules.Out, 4);
                for (var away = 0f; away <= 6f; away += 0.5f)
                    Assert.Equal(StageGround.StemScale(away, rules.Out) * 0.8f, Grown(rules, away), 4);
            }
        }

        [Fact]
        public void AStemKeepsItsOwnSizeWhereItStaysUnderTheLine()
        {
            // A small stem of a kind half as tall grows as it would; only what would rise into the line is cut.
            var rules = StageGround.Short(footprint: 0.65f, height: 0.3f, 0.15f, GrassStand.Thing, 12f);
            Assert.Equal(StageGround.StemScale(1f, rules.Out) * 0.7f, rules.Stem(1f, 0.7f, 0.1f), 4);
            // A stem whose kind's height is not known grows as it would.
            Assert.Equal(StageGround.StemScale(1f, rules.Out), rules.Stem(1f, 1f, 0f), 4);
        }

        [Fact]
        public void AStemStandingRaisedOffTheGroundStillKeepsItsTipUnderTheLine()
        {
            // A kind the game sets a little up or down where it grows: its tip, not its size, stays under.
            var rules = StageGround.Short(footprint: 0.65f, height: 0.3f, 0.15f, GrassStand.Thing, 12f);
            foreach (var lift in new[] { 0.1f, -0.1f, 0.5f })
            {
                for (var away = 0.5f; away <= 3f; away += 0.5f)
                {
                    // One grown to nothing shows nothing.
                    var grown = rules.Stem(away, 1f, 0.8f, lift) * 0.8f;
                    Assert.True(grown == 0f || lift + grown <= 0.15f + away * Rise + 1e-4f, $"lifted {lift} at {away} m");
                }
            }
            // Lifted so far that no stem fits under, it is not grown at all.
            Assert.Equal(0f, rules.Stem(1f, 1f, 0.8f, lift: 2f), 4);
            // One whose kind's height is not known grows as rolled however high it stands.
            Assert.Equal(StageGround.StemScale(1f, rules.Out), rules.Stem(1f, 1f, 0f, lift: 2f), 4);
        }

        [Fact]
        public void GrassIsLaidAgainOnlyAsItsRulesChangeEnoughToShow()
        {
            var wood = StageGround.Short(footprint: 0.65f, height: 0.3f, 0.15f, GrassStand.Thing, 12f);
            Assert.True(wood.Same(StageGround.Short(footprint: 0.66f, height: 0.3f, 0.16f, GrassStand.Thing, 12f)));
            Assert.False(wood.Same(StageGround.Short(footprint: 0.65f, height: 0.3f, 0.4f, GrassStand.Thing, 12f)));
            // Made bigger, its middle 9 cm higher: a line that much higher would let stems over the lower one.
            Assert.False(wood.Same(StageGround.Short(footprint: 0.65f, height: 0.3f, 0.24f, GrassStand.Thing, 12f)));
            // Looked at from lower, the line is laid anew.
            Assert.False(wood.Same(StageGround.Short(footprint: 0.65f, height: 0.3f, 0.15f, GrassStand.Thing, 8f)));
            Assert.False(wood.Same(StageGround.Short(footprint: 2f, height: 0.3f, 0.15f, GrassStand.Thing, 12f)));
            Assert.False(wood.Same(StageGround.Short(footprint: 0.65f, height: 0.3f, 0.15f, GrassStand.Figure, 12f)));
            Assert.False(StageGround.Short(footprint: 0.65f, height: 0.3f, 0.15f, GrassStand.Figure, 12f).Same(wood));
            var figure = StageGround.Short(footprint: 0.3f, height: 2f, 1f, GrassStand.Figure, 12f);
            Assert.True(figure.Same(StageGround.Short(footprint: 0.3f, height: 2f, 1.5f, GrassStand.Figure, 12f)));
        }

        [Fact]
        public void NearAThingTheGrassIsShortNeverABareCircle()
        {
            // At its feet a fifth as tall, growing to full height at the ring's edge: stems stand
            // everywhere, so no bare disc of ground shows round it, and none hides it.
            Assert.Equal(StageGround.ShortStem, StageGround.StemScale(away: 0f, shortOut: 1.5f), 3);
            Assert.True(StageGround.ShortStem > 0f && StageGround.ShortStem <= 0.25f);
            Assert.InRange(StageGround.StemScale(away: 0.75f, shortOut: 1.5f), 0.5f, 0.7f);
            Assert.Equal(1f, StageGround.StemScale(away: 1.5f, shortOut: 1.5f), 3);
            Assert.Equal(1f, StageGround.StemScale(away: 9f, shortOut: 1.5f), 3);
            // Where none is short, every stem is whole.
            Assert.Equal(1f, StageGround.StemScale(away: 0f, shortOut: 0f), 3);
            var last = 0f;
            for (var away = 0f; away <= 1.5f; away += 0.1f)
            {
                var scale = StageGround.StemScale(away, 1.5f);
                Assert.True(scale >= last);
                last = scale;
            }
        }

        [Fact]
        public void TheStageGrowsAsManyPlantsAsTheGameAtThePlayersGrassQuality()
        {
            // The game's vegetation setting: a quarter on Low, half on Medium, all on High, none Off,
            // times the system's scale (ClutterSystem.GenerateVegPatch).
            Assert.Equal(0, StageGround.Amount(amount: 80, quality: 0, scale: 1f));
            Assert.Equal(20, StageGround.Amount(amount: 80, quality: 1, scale: 1f));
            Assert.Equal(40, StageGround.Amount(amount: 80, quality: 2, scale: 1f));
            Assert.Equal(80, StageGround.Amount(amount: 80, quality: 3, scale: 1f));
            Assert.Equal(20, StageGround.Amount(amount: 80, quality: 2, scale: 0.5f));
            // A whole number as the game divides it: 5 on Low is 1.
            Assert.Equal(1, StageGround.Amount(amount: 5, quality: 1, scale: 1f));
        }

        [Fact]
        public void GrassGrowsUpToACreaturesFeetUnlessItIsLowerThanTheGrass()
        {
            // As in the game, a troll or a dragon stands in the grass, however wide its arms or wings.
            Assert.Equal(0f, StageGround.ShortGrassOut(footprint: 2.5f, height: 4f, GrassStand.Figure), 3);
            Assert.Equal(0f, StageGround.ShortGrassOut(footprint: 9f, height: 5f, GrassStand.Figure), 3);
            // One lower than the grass keeps its ring, or the grass would hide it.
            Assert.Equal(1.5f, StageGround.ShortGrassOut(footprint: 0.4f, height: 0.4f, GrassStand.Figure), 3);
        }

        [Fact]
        public void ARaidsCrowdATraderAndAPersonStandInTheGrassAsACreatureDoes()
        {
            // Every figure the stage shows stands in the grass as the game's creatures do: a raid's
            // crowd twelve metres across, a trader, the person wearing a status effect.
            Assert.Equal(GrassStand.Figure, StageGround.StandsAs(Kind.Creature));
            Assert.Equal(GrassStand.Figure, StageGround.StandsAs(Kind.Raid));
            Assert.Equal(GrassStand.Figure, StageGround.StandsAs(Kind.Trader));
            Assert.Equal(GrassStand.Figure, StageGround.StandsAs(Kind.StatusEffect));
            Assert.Equal(0f, StageGround.ShortGrassOut(footprint: 6f, height: 2.5f, StageGround.StandsAs(Kind.Raid)), 3);
        }

        [Fact]
        public void APlaceLeavesItsGrassToTheGroundItPaints()
        {
            // A camp or any location clears its grass where its own ground is painted, as the game
            // clears it, never in a ring round all of it: a camp forty metres across stands in the
            // meadow between its tents, however low or high it is.
            Assert.Equal(GrassStand.Place, StageGround.StandsAs(Kind.Location));
            Assert.Equal(0f, StageGround.ShortGrassOut(footprint: 20f, height: 8f, GrassStand.Place), 3);
            Assert.Equal(0f, StageGround.ShortGrassOut(footprint: 3f, height: 0.3f, GrassStand.Place), 3);
            // Anything else keeps the ring round its footprint, nothing shown standing in no grass.
            Assert.Equal(GrassStand.Thing, StageGround.StandsAs(Kind.Item));
            Assert.Equal(GrassStand.Thing, StageGround.StandsAs(Kind.Piece));
            Assert.Equal(GrassStand.Thing, StageGround.StandsAs(Kind.Spawner));
            Assert.Equal(GrassStand.Thing, StageGround.StandsAs(null));
        }

        [Fact]
        public void GrassFadesOutWithTheGroundItGrowsOn()
        {
            // Grass grows out to where the ground has faded away, thinning as the ground fades, so
            // the two go together rather than the grass ending in a ring.
            Assert.Equal(76f, StageGround.GrassOut(160f), 3);
            Assert.Equal(1f, StageGround.GrassKept(0f), 3);
            Assert.Equal(1f, StageGround.GrassKept(0.6f), 3);
            Assert.Equal(0f, StageGround.GrassKept(0.95f), 3);
            for (var share = 0f; share <= 1f; share += 0.05f) Assert.Equal(1f - StageGround.Fade(share), StageGround.GrassKept(share), 4);
            Assert.True(StageGround.GrassKept(0.7f) > StageGround.GrassKept(0.85f));
        }

        [Fact]
        public void NoTwoPatchesOfGrassAroundTheStageAreScatteredAlike()
        {
            // The game seeds a patch by its place in the world, its numbers multiplied; round the
            // stage's middle those numbers are small, so a cross of patches and mirrored pairs came
            // out alike, a tiled look. Seeded as if out in the world, none is.
            for (var kind = 0; kind < 24; kind++)
            {
                var seeds = new System.Collections.Generic.HashSet<int>();
                for (var x = -12; x <= 12; x++)
                    for (var z = -12; z <= 12; z++)
                        Assert.True(seeds.Add(StageGround.PatchSeed(x, z, kind)), $"kind {kind}: patch {x}, {z} seeded as another");
            }
            Assert.NotEqual(StageGround.PatchSeed(1, 2, 0), StageGround.PatchSeed(2, 1, 0));
            Assert.NotEqual(StageGround.PatchSeed(0, 3, 0), StageGround.PatchSeed(0, -3, 0));
        }

        [Fact]
        public void ANewPlayerFirstSeesTheBiomesGroundUnderTheSky()
        {
            // The stage's own ground and sky, not a plain field, until a backdrop is picked and kept.
            Assert.Equal("Sky and ground", StageGround.Backdrops[StageGround.FirstBackdrop]);
            Assert.True(StageGround.Ground(StageGround.FirstBackdrop, underground: false));
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

        // Much spawns or grows nowhere of its own: a ship, a creature only a place's spawn points
        // bring (the Charred in Ashlands fortresses), an item, a piece, a raid that may come
        // anywhere. Each stands where the game has it: a ship on the sea, a place's creature in
        // that place's biome, an item where what drops or yields it lives, a crafted thing in the
        // biome of its tier, a raid where its creatures live.

        private static StageGround.Home Nothing => new StageGround.Home { Tier = Tiers.None };

        [Fact]
        public void WhatSpawnsOrGrowsStandsWhereItDoesFirst()
        {
            var home = Nothing;
            home.Own = new[] { "Swamp", "BlackForest" };
            home.Places = new[] { "Mountain" };
            home.Givers = new[] { "Plains" };
            home.Tier = 5;
            Assert.Equal("BlackForest", StageGround.HomeOf(home));
            Assert.Equal("Meadows", StageGround.HomeOf(Nothing));
        }

        [Fact]
        public void AShipStandsOnTheSea()
        {
            var ship = Nothing;
            ship.Floats = true;
            ship.Tier = 2;
            Assert.Equal("Ocean", StageGround.HomeOf(ship));
        }

        [Fact]
        public void ACreatureOnlyAPlaceBringsStandsInThatPlacesBiome()
        {
            var charred = Nothing;
            charred.Places = new[] { "AshLands" };
            charred.Tier = 7;
            Assert.Equal("AshLands", StageGround.HomeOf(charred));
        }

        [Fact]
        public void AnItemStandsWhereWhatDropsOrYieldsItLives()
        {
            var item = Nothing;
            item.Givers = new[] { "Mistlands", "Plains" };
            item.Tier = 6;
            Assert.Equal("Plains", StageGround.HomeOf(item));
            // Lying in a place, it stands in the place's biome before its givers'.
            item.Places = new[] { "Swamp" };
            Assert.Equal("Swamp", StageGround.HomeOf(item));
        }

        [Theory]
        [InlineData(1, "Meadows")]
        [InlineData(3, "Swamp")]
        [InlineData(5, "Plains")]
        [InlineData(7, "AshLands")]
        // No tier (the Ocean's, the Deep North's, a mod's lone item), or none known yet: Meadows.
        [InlineData(Tiers.None, "Meadows")]
        [InlineData(0, "Meadows")]
        public void ACraftedThingStandsInTheBiomeOfItsTier(int tier, string biome)
        {
            var crafted = Nothing;
            crafted.Tier = tier;
            Assert.Equal(biome, StageGround.HomeOf(crafted));
        }

        [Fact]
        public void ARaidStandsWhereItsCreaturesLiveOfWhereItMayCome()
        {
            var goblins = Nothing;
            goblins.Raid = true;
            goblins.Own = new[] { "Meadows", "BlackForest", "Plains", "Mistlands" };
            goblins.Brought = new[] { "Plains" };
            Assert.Equal("Plains", StageGround.HomeOf(goblins));
            // Of its creatures' biomes, only those it may come in count: then the first of its own.
            goblins.Brought = new[] { "Swamp" };
            Assert.Equal("Meadows", StageGround.HomeOf(goblins));
            // Creatures living nowhere of their own: the first it may come in, as before.
            goblins.Brought = new string[0];
            Assert.Equal("Meadows", StageGround.HomeOf(goblins));
            // A raid kept from the Meadows, its creatures elsewhere: the first it may come in.
            var drakes = Nothing;
            drakes.Raid = true;
            drakes.Own = new[] { "Swamp", "Mountain" };
            drakes.Brought = new[] { "Plains" };
            Assert.Equal("Swamp", StageGround.HomeOf(drakes));
        }

        [Fact]
        public void ABiomeLeftUnnamedTellsNothingAndTheNextWayIsAsked()
        {
            var unnamed = Nothing;
            unnamed.Own = new[] { "" };
            unnamed.Places = new[] { "AshLands" };
            Assert.Equal("AshLands", StageGround.HomeOf(unnamed));
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
