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
        public void AThingThatIsNoCreatureIsSeenOnlyWithinTheWorldsSeenReach()
        {
            // The game names an item, a piece or a resource only on hover, so sight makes one out
            // only as near as a creature's stars: within the world's seen reach, whatever its size.
            Assert.Equal(10f, Sighting.ThingReach(Sighting.Reach(1.8f), 10f));
            Assert.Equal(10f, Sighting.ThingReach(Sighting.Reach(9f), 10f));
            Assert.Equal(30f, Sighting.ThingReach(Sighting.Reach(0.3f), 150f));
            // A seen reach of 0 keeps no stars or gear, but cannot blind the sight of things: it falls back to the size's reach.
            Assert.Equal(30f, Sighting.ThingReach(Sighting.Reach(1.8f), 0f));
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

        [Fact]
        public void ABossesAttackIsSeenBegunAsMuchFartherAsTheGameShowsItsHealthBar()
        {
            // The game names a creature within 10 m and shows a boss's health bar within 100 m,
            // so Yagluth's taunt, which hurts no one, is seen begun from where its fight is fought.
            Assert.Equal(100f, Sighting.AttackReach(true, 10f, 10f, 100f), 3);
            Assert.Equal(10f, Sighting.AttackReach(false, 10f, 10f, 100f), 3);
            // A world that sees up close nearer sees a boss's attacks as much nearer.
            Assert.Equal(50f, Sighting.AttackReach(true, 5f, 10f, 100f), 3);
            // One keeping no attacks seen up close keeps none of a boss's either.
            Assert.Equal(0f, Sighting.AttackReach(true, 0f, 10f, 100f));
            // Without the game's hud to read, a boss's attack is seen as near as any.
            Assert.Equal(10f, Sighting.AttackReach(true, 10f, 0f, 100f));
        }

        [Fact]
        public void AnAttackIsSeenBegunFromAsFarAsItsCreatureUsesIt()
        {
            // A Seeker soldier taunts from up to 20 m (its taunt's m_aiAttackRange), and a taunt
            // hurts no one, so it is seen begun from there, not only within the 10 m seen reach.
            Assert.Equal(20f, Sighting.AttackReach(false, 10f, 10f, 100f, attackRange: 20f), 3);
            // A Gjall's taunt from up to 50 m.
            Assert.Equal(50f, Sighting.AttackReach(false, 10f, 10f, 100f, attackRange: 50f), 3);
            // A bite used from 4 m is still seen within the seen reach.
            Assert.Equal(10f, Sighting.AttackReach(false, 10f, 10f, 100f, attackRange: 4f), 3);
            // A boss's attack used from nearer than its health bar shows keeps the health bar's reach.
            Assert.Equal(100f, Sighting.AttackReach(true, 10f, 10f, 100f, attackRange: 30f), 3);
            // A world keeping no attacks seen keeps none from afar either.
            Assert.Equal(0f, Sighting.AttackReach(false, 0f, 10f, 100f, attackRange: 20f));
        }
    }
}
