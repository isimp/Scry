using System;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class GroundShapeTests
    {
        // A place digs and raises the ground it stands on with its terrain modifiers, as the game
        // shapes its terrain under it (Heightmap.ApplyModifier): a level sets the ground flat at its
        // height inside its radius, a smooth pulls it toward that height by 1 - (d/r)^power, each in
        // the order the game sorts them, on the terrain's one-metre grid. The stage's ground follows,
        // so what lies in a pit shows instead of standing under a flat floor.

        // TarPit1 as the game files have it: flatten pulls toward -1 m over 19 m, then lower toward -5 m over 12 m.
        private static readonly GroundShaping[] TarPit =
        {
            new GroundShaping { Y = -1f, Smooth = true, SmoothRadius = 19f, SmoothPower = 1f, Order = 0 },
            new GroundShaping { Y = -5f, Smooth = true, SmoothRadius = 12f, SmoothPower = 1f, Order = 1 },
        };

        // Burial Chambers 3's two sinks: level to 3 m down within 3 m, then smooth toward it over 5 m.
        private static GroundShaping Sink(float z) => new GroundShaping
        {
            Z = z, LevelOffset = -3f, Level = true, LevelRadius = 3f, Smooth = true, SmoothRadius = 5f, SmoothPower = 3f, Order = 2,
        };

        [Fact]
        public void ATarPitIsDugIntoTheBowlTheGameDigs()
        {
            var shape = GroundShape.Of(TarPit);
            Assert.False(shape.Flat);
            // At its middle the second pull has its whole weight: the pit's floor.
            Assert.Equal(-5f, shape.HeightAt(0f, 0f), 3);
            // Six metres out: flatten leaves -13/19, lower pulls half of the way to -5.
            var flattened = -1f * (1f - 6f / 19f);
            Assert.Equal(flattened + (-5f - flattened) * 0.5f, shape.HeightAt(6f, 0f), 3);
            // Past both reaches the ground is as it was.
            Assert.Equal(0f, shape.HeightAt(20f, 0f), 3);
            Assert.Equal(0f, shape.HeightAt(0f, -25f), 3);
            // At the very edge of the second pull it still weighs in, by what is left of its reach.
            var edge = (float)Math.Sqrt(4 * 4 + 11 * 11);
            var flat = -1f * (1f - edge / 19f);
            Assert.Equal(flat + (-5f - flat) * (1f - edge / 12f), shape.HeightAt(4f, 11f), 4);
        }

        [Fact]
        public void NothingIsShapedPastALevelsReach()
        {
            var shape = GroundShape.Of(new[] { new GroundShaping { Level = true, LevelRadius = 2f, Y = 2f } });
            Assert.Equal(2f, shape.HeightAt(2f, 0f), 3);
            Assert.Equal(0f, shape.HeightAt(3.5f, 0f), 3);
            Assert.Equal(0f, shape.HeightAt(0f, -3.9f), 3);
        }

        [Fact]
        public void ALevelSetsItsCircleFlatAndItsSmoothBlendsTheEdge()
        {
            var shape = GroundShape.Of(new[] { Sink(0f) });
            Assert.Equal(-3f, shape.HeightAt(0f, 0f), 3);
            Assert.Equal(-3f, shape.HeightAt(3f, 0f), 3);
            // Four metres out, past the level: the smooth's cubic weight from the ground as it was.
            Assert.Equal(-3f * (1f - (float)Math.Pow(4f / 5f, 3)), shape.HeightAt(4f, 0f), 3);
            Assert.Equal(0f, shape.HeightAt(6f, 0f), 3);
        }

        [Fact]
        public void ASquareLevelTakesItsCorners()
        {
            var round = GroundShape.Of(new[] { new GroundShaping { Level = true, LevelRadius = 2f, LevelOffset = 1f } });
            var square = GroundShape.Of(new[] { new GroundShaping { Level = true, LevelRadius = 2f, LevelOffset = 1f, Square = true } });
            Assert.Equal(0f, round.HeightAt(2f, 2f), 3);
            Assert.Equal(1f, square.HeightAt(2f, 2f), 3);
        }

        [Fact]
        public void ModifiersApplyInTheGamesOrderWhatEverOrderTheyAreRead()
        {
            var low = new GroundShaping { Level = true, LevelRadius = 2f, Y = -2f, Order = 0 };
            var high = new GroundShaping { Level = true, LevelRadius = 2f, Y = 2f, Order = 1 };
            Assert.Equal(2f, GroundShape.Of(new[] { high, low }).HeightAt(0f, 0f), 3);
            Assert.Equal(2f, GroundShape.Of(new[] { low, high }).HeightAt(0f, 0f), 3);
            // Of one order, the one read first goes first, so the later one wins.
            var also = new GroundShaping { Level = true, LevelRadius = 2f, Y = -1f, Order = 1 };
            Assert.Equal(-1f, GroundShape.Of(new[] { high, also }).HeightAt(0f, 0f), 3);
        }

        [Fact]
        public void TheGroundRunsStraightBetweenTheGridsPoints()
        {
            // A level of half a metre lands on its nearest point, as the game rounds a position to its grid.
            var shape = GroundShape.Of(new[] { new GroundShaping { X = 0.4f, Level = true, LevelRadius = 0.2f, Y = -2f } });
            Assert.Equal(-2f, shape.HeightAt(0f, 0f), 3);
            Assert.Equal(-1f, shape.HeightAt(0.5f, 0f), 3);
            Assert.Equal(0f, shape.HeightAt(1f, 0f), 3);
            var rounded = GroundShape.Of(new[] { new GroundShaping { X = 0.5f, Level = true, LevelRadius = 0.2f, Y = -2f } });
            Assert.Equal(-2f, rounded.HeightAt(1f, 0f), 3);
        }

        [Fact]
        public void APlaceWithoutModifiersStandsOnFlatGround()
        {
            var shape = GroundShape.Of(Array.Empty<GroundShaping>());
            Assert.True(shape.Flat);
            Assert.Equal(0f, shape.HeightAt(3f, -7f));
            Assert.True(GroundShape.Of(null).Flat);
            // A modifier that only paints shapes nothing.
            Assert.True(GroundShape.Of(new[] { new GroundShaping { LevelRadius = 4f, SmoothRadius = 4f } }).Flat);
        }

        [Fact]
        public void TheGroundsTiltIsTold()
        {
            Assert.Equal(0f, GroundShape.Of(null).TiltAt(1f, 1f), 3);
            var shape = GroundShape.Of(TarPit);
            // On the bowl's side the ground leans; at its flat floor and far out it does not.
            Assert.InRange(shape.TiltAt(6f, 0f), 15f, 45f);
            Assert.Equal(0f, shape.TiltAt(30f, 0f), 3);
            // East of the pit's middle the ground climbs eastward, so it faces up and back west.
            var (x, y, z) = shape.NormalAt(6f, 0f);
            Assert.True(x < 0f && y > 0f);
            Assert.Equal(0f, z, 3);
            Assert.Equal(1f, x * x + y * y + z * z, 3);
            Assert.Equal((0f, 1f, 0f), GroundShape.Of(null).NormalAt(2f, 2f));
        }

        [Fact]
        public void TheShapedGroundReachesAsFarAsItsModifiers()
        {
            Assert.Equal(0f, GroundShape.Of(null).Reach);
            Assert.InRange(GroundShape.Of(TarPit).Reach, 19f, 21f);
            Assert.InRange(GroundShape.Of(new[] { Sink(8f) }).Reach, 13f, 15f);
        }
    }

    public class LiquidSettleTests
    {
        // A place's liquid (LiquidVolume), tar in a tar pit, starts as a block over its middle
        // (m_initialArea, m_initialVolume) and flows until it lies level: it fills the ground's
        // lowest reaches joined to where it started, its depth at each point of its grid what lies
        // between its level and the ground there.

        [Fact]
        public void TheTarFillsThePitToTheLevelItsVolumeReaches()
        {
            var bowl = GroundShape.Of(new[]
            {
                new GroundShaping { Y = -1f, Smooth = true, SmoothRadius = 19f, SmoothPower = 1f, Order = 0 },
                new GroundShaping { Y = -5f, Smooth = true, SmoothRadius = 12f, SmoothPower = 1f, Order = 1 },
            });
            // TarPit1's tar: 64 m wide grid, a metre a point, 400 over the 8 by 8 middle (the game fills 9 by 9).
            var tar = LiquidSettle.Of(64, 1f, 8, 400f, 20f, (x, z) => bowl.HeightAt(x, z));
            Assert.Equal(81f * 400f / 64f, tar.Volume, 1);
            Assert.InRange(tar.Level, -1.1f, -0.85f);
            // Its depths hold its volume, and lie only where the ground is under its level.
            Assert.Equal(tar.Volume, tar.Depths.Sum(), 0);
            Assert.True(tar.DepthAt(0, 0) > 3.5f);
            Assert.Equal(0f, tar.DepthAt(25, 0));
            Assert.Equal(tar.Level - bowl.HeightAt(3f, 0f), tar.DepthAt(3, 0), 2);
        }

        [Fact]
        public void LiquidOnFlatGroundSpreadsThin()
        {
            var tar = LiquidSettle.Of(64, 1f, 8, 400f, 20f, (x, z) => 0f);
            Assert.InRange(tar.Level, 0.1f, 0.15f);
            Assert.True(tar.DepthAt(-32, 32) > 0.1f);
        }

        [Fact]
        public void TheGroundIsHeldWithinTheLiquidsReach()
        {
            // The game holds the ground's height within its reach up and down (m_maxDepth): far deeper ground lies at its floor.
            var tar = LiquidSettle.Of(64, 1f, 8, 400f, 20f, (x, z) => -50f);
            Assert.Equal(-20f + 81f * 400f / 64f / (65f * 65f), tar.Level, 2);
        }

        [Fact]
        public void ALowPlaceTheLiquidCannotReachStaysDry()
        {
            // A ring wall higher than the liquid ever rises keeps it off a hollow past it.
            Func<float, float, float> ground = (x, z) =>
            {
                var d = Math.Sqrt(x * x + z * z);
                if (d < 6) return -2f;
                if (d < 8) return 5f;
                return d > 14 && d < 18 ? -6f : 0f;
            };
            var pool = LiquidSettle.Of(64, 1f, 4, 64f, 20f, ground);
            Assert.True(pool.DepthAt(0, 0) > 0f);
            Assert.Equal(0f, pool.DepthAt(16, 0));
            Assert.True(pool.Level < 5f);
        }

        [Fact]
        public void TheLiquidsMeshStandsAsTheGamesDoes()
        {
            // A point under the liquid stands at least a tenth of a metre above its ground, a dry one on it and unseen.
            Assert.Equal((-4.9f, true), LiquidSettle.Point(-5f, 0.05f));
            Assert.Equal((-1f, true), LiquidSettle.Point(-5f, 4f));
            Assert.Equal((2f, false), LiquidSettle.Point(2f, 0f));
        }
    }

    public class PartRestTests
    {
        // What the game lets settle comes to rest on the shaped ground as it would in the world:
        // a part that falls (StaticPhysics.m_fall) drops to the ground under it, one pushed up
        // (m_pushUp) rises out of it, one that checks solids keeps its height as the stage has no
        // solids of the world's, and one that floats (Floating) rides a liquid's level where one lies
        // over the ground.

        [Fact]
        public void BigTarFallsToThePitsFloor()
        {
            Assert.Equal(-3.9f, PartRest.Settled(-2.6f, -3.9f, null, PartRest.Fall), 3);
            // Within the game's twentieth of a metre it stays.
            Assert.Equal(-3.86f, PartRest.Settled(-3.86f, -3.9f, null, PartRest.Fall), 3);
            // A falling part under the ground stays under it: only pushing up raises it.
            Assert.Equal(-6f, PartRest.Settled(-6f, -3.9f, null, PartRest.Fall), 3);
            Assert.Equal(-3.9f, PartRest.Settled(-6f, -3.9f, null, PartRest.PushUp), 3);
            Assert.Equal(-2f, PartRest.Settled(-2f, -3.9f, null, PartRest.PushUp), 3);
        }

        [Fact]
        public void APartCheckingSolidsKeepsItsHeight()
        {
            Assert.Equal(1f, PartRest.Settled(1f, -3f, null, PartRest.Fall | PartRest.Solids), 3);
            Assert.Equal(-3f, PartRest.Settled(-5f, -3f, null, PartRest.Fall | PartRest.Solids | PartRest.PushUp), 3);
        }

        [Fact]
        public void SmallTarFloatsOnTheTar()
        {
            Assert.Equal(-0.86f, PartRest.Settled(0f, -3.5f, -0.96f, PartRest.Floats, 0.1f), 3);
            // Where no liquid lies over the ground it is left as it stands.
            Assert.Equal(0f, PartRest.Settled(0f, 0.2f, -0.96f, PartRest.Floats, 0.1f), 3);
            Assert.Equal(0f, PartRest.Settled(0f, -3.5f, null, PartRest.Floats, 0.1f), 3);
        }

        [Fact]
        public void APartWithNoSettlingStaysPut()
        {
            Assert.Equal(-4f, PartRest.Settled(-4f, 0f, null, 0), 3);
        }
    }
}
