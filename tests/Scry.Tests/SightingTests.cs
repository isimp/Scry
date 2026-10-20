using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class SightingTests
    {
        // A creature in view is seen as far as its size makes it out: one of a person's size or
        // smaller within 30 m, a bigger one farther as it looks bigger, up to a limit.

        [Fact]
        public void ACreatureOfAPersonsSizeOrSmallerIsSeenWithin30Metres()
        {
            Assert.Equal(30f, Sighting.Reach(1.8f));
            Assert.Equal(30f, Sighting.Reach(0.3f));
            Assert.Equal(30f, Sighting.Reach(0f));
        }

        [Fact]
        public void ABiggerCreatureIsSeenAsMuchFartherAsItIsBigger()
        {
            // A troll some 4.5 m tall looks at 75 m as a person does at 30 m.
            Assert.Equal(75f, Sighting.Reach(4.5f), 3);
            Assert.Equal(60f, Sighting.Reach(3.6f), 3);
        }

        [Fact]
        public void HoweverBigNothingIsSeenBeyond150Metres()
        {
            Assert.Equal(150f, Sighting.Reach(9f), 3);
            Assert.Equal(150f, Sighting.Reach(40f));
        }

        [Fact]
        public void APlaceIsSeenOnlyWhereALineReachesItsGround()
        {
            // Nothing in the way, or what stops the line stands in the place's own ground (its
            // walls, its roof, the ground in it): seen. Stopped short of it, by a hill or a tree
            // outside it: not seen.
            Assert.True(Sighting.PlaceInSight(blocked: false, 0f, 0f, 12f));
            Assert.True(Sighting.PlaceInSight(blocked: true, 6f, -8f, 12f));
            Assert.False(Sighting.PlaceInSight(blocked: true, 9f, 9f, 12f));
            Assert.False(Sighting.PlaceInSight(blocked: true, 30f, 0f, 12f));
        }

        [Fact]
        public void APlaceIsLookedForAtItsMiddleAndFourPointsHalfWayOut()
        {
            // A person's height over its ground, so a place in a dip or behind a low wall is
            // looked for where it stands up, and a big one half hidden is still found.
            var points = Sighting.PlacePoints(12f);
            Assert.Equal(5, points.Length);
            Assert.Equal((0f, 1.8f, 0f), points[0]);
            foreach (var (x, y, z) in points.Skip(1))
            {
                Assert.Equal(1.8f, y);
                Assert.Equal(6f, System.MathF.Sqrt(x * x + z * z), 3);
            }
        }
    }
}
