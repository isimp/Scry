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
        public void AThingIsSeenByItsNearestPartNotItsMiddle()
        {
            // The game names a thing where the eye's ray meets it (Player.FindHoverObject), so a
            // beech 29.5 m tall, whose middle stands 14 m up, is near for one standing 8 m from its
            // trunk (a 3 m crown round it): its nearest part 5 m off at the eye's height.
            var eye = new Vec3(8f, 1.7f, 0f);
            var foot = new Vec3(0f, 0f, 0f);
            Assert.Equal(25f, Sighting.NearestSq(eye, foot, bottom: 0f, top: 29.5f, radius: 3f), 3);
            // Under its crown, nothing between.
            Assert.Equal(0f, Sighting.NearestSq(new Vec3(1f, 1.7f, 0f), foot, 0f, 29.5f, 3f), 3);
            // Above a low stone, the height between counts.
            Assert.Equal(1f + 16f, Sighting.NearestSq(new Vec3(1.5f, 4.5f, 0f), foot, 0f, 0.5f, 0.5f), 3);
            // A thing whose box starts below its foot, as a rock half in the ground.
            Assert.Equal(0f, Sighting.NearestSq(new Vec3(0f, -0.5f, 0f), foot, -1f, 2f, 1f), 3);
        }

        [Fact]
        public void AThingIsLookedAtOnItsOwnUprightAtTheEyesHeight()
        {
            // The line of sight goes to the trunk at the eye's height, within the thing's own height.
            var foot = new Vec3(4f, 10f, 2f);
            var aim = Sighting.Aim(new Vec3(0f, 11.7f, 0f), foot, bottom: 0f, top: 29.5f);
            Assert.Equal(4f, aim.X);
            Assert.Equal(11.7f, aim.Y, 3);
            Assert.Equal(2f, aim.Z);
            // Looking down on a small stone, the line goes to its top; from below a cliff, to its foot.
            Assert.Equal(10.5f, Sighting.Aim(new Vec3(0f, 20f, 0f), foot, 0f, 0.5f).Y, 3);
            Assert.Equal(9f, Sighting.Aim(new Vec3(0f, 0f, 0f), foot, -1f, 40f).Y, 3);
        }

        [Fact]
        public void ACreatureIsSeenAsFarAsItsSizeMakesItOutItsStarsAndGearUpClose()
        {
            // A creature in view is seen by its size whatever the world's seen reach, which keeps
            // only its stars, gear and attacks begun near: a Gjall 4 m tall makes out at about 67 m.
            Assert.Equal(Sighting.Reach(4f), Sighting.CreatureReach(4f, seenReach: 10f), 3);
            Assert.Equal(30f, Sighting.CreatureReach(1.8f, seenReach: 10f), 3);
            // A world seeing up close farther than a creature's size reach sees it that far.
            Assert.Equal(40f, Sighting.CreatureReach(1.8f, seenReach: 40f), 3);
        }

        [Fact]
        public void APlaceStandingTallIsSeenAsFarAsItsHeightMakesItOut()
        {
            // A place is seen within its radius and 30 m beyond, or as far as its height makes it
            // out: the Mistlands' rock spire, 4 m round and some 40 m tall, from 150 m.
            Assert.Equal(34f, Sighting.PlaceReach(radius: 4f, height: 1.8f), 3);
            Assert.Equal(150f, Sighting.PlaceReach(radius: 4f, height: 40f), 3);
            Assert.Equal(54f, Sighting.PlaceReach(radius: 24f, height: 3f), 3);
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
        public void APlaceStandingTallIsLookedForHighUpToo()
        {
            // A rock spire 40 m tall shows over a hill that hides its foot: looked for three
            // quarters of the way up as well; a low place is looked for as before.
            var tall = Sighting.PlacePoints(4f, height: 40f);
            Assert.Equal(6, tall.Length);
            Assert.Equal((0f, 30f, 0f), tall[5]);
            Assert.Equal(5, Sighting.PlacePoints(12f, height: 3f).Length);
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
