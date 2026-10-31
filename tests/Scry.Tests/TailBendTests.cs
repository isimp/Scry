using Xunit;

namespace Scry.Tests
{
    public class TailBendTests
    {
        // A Serpent's or a Leech's tail is bent by the game every frame, after its animator
        // (Tail.CustomLateUpdate): each joint trails its parent, sinking out of the water fast and
        // in it slowly, never more than an angle off the parent's line, kept its radius over the
        // ground, moving a share of the way a frame. These are the Serpent's numbers.

        private static readonly TailBend.Rules Serpent = new TailBend.Rules
        {
            MaxAngle = 60f, Gravity = 5f, GravityInWater = 0.1f, WaterCheck = true, GroundCheck = true, Smoothness = 0.5f, Radius = 0.5f,
        };

        // The parent at the origin, its line running along x: the joint belongs 2 m along it.
        private static readonly Vec3 Parent = new Vec3(0f, 0f, 0f);
        private static readonly Vec3 Along = new Vec3(1f, 0f, 0f);
        private const float Apart = 2f;

        private static Vec3 Next(Vec3 was, TailBend.Rules rules, float water, float ground, float dt = 0.1f) =>
            TailBend.Next(was, Parent, Along, Apart, water, ground, rules, dt);

        private static TailBend.Rules Whole(TailBend.Rules rules)
        {
            rules.Smoothness = 1f;
            return rules;
        }

        [Fact]
        public void AJointWhereItBelongsUnderWaterStaysThere()
        {
            var rules = Whole(Serpent);
            rules.GravityInWater = 0f;
            var next = Next(new Vec3(2f, 0f, 0f), rules, water: 10f, ground: -100f);
            Assert.Equal(2f, next.X, 3);
            Assert.Equal(0f, next.Y, 3);
            Assert.Equal(0f, next.Z, 3);
        }

        [Fact]
        public void OutOfTheWaterATailSagsFarMoreThanInIt()
        {
            var dry = Next(new Vec3(2f, 0f, 0f), Whole(Serpent), water: float.NaN, ground: -100f);
            var wet = Next(new Vec3(2f, 0f, 0f), Whole(Serpent), water: 10f, ground: -100f);
            // Sunk 0.5 m, then set back on its parent's line half its length from the middle: about 0.45 m down.
            Assert.Equal(-0.447f, dry.Y, 2);
            Assert.True(wet.Y > -0.01f && wet.Y < 0f, $"in water it sinks by a fiftieth as much: {wet.Y}");
            // Either way it keeps its length from its parent's middle.
            Assert.Equal(1f, Vec3.Distance(dry, new Vec3(1f, 0f, 0f)), 3);
        }

        [Fact]
        public void AboveTheSurfaceByItsRadiusItSinksAsOutOfTheWater()
        {
            // The surface 0.4 m over the joint, its radius 0.5 m: it reaches out, so it sinks fast.
            var reaching = Next(new Vec3(2f, 0f, 0f), Whole(Serpent), water: 0.4f, ground: -100f);
            Assert.True(reaching.Y < -0.4f, $"{reaching.Y}");
        }

        [Fact]
        public void WithoutTheWaterCheckItSinksFastEvenInWater()
        {
            var rules = Whole(Serpent);
            rules.WaterCheck = false;
            Assert.Equal(-0.447f, Next(new Vec3(2f, 0f, 0f), rules, water: 10f, ground: -100f).Y, 2);
        }

        [Fact]
        public void AJointBentFarOffItsParentsLineIsHeldAtTheMostAngle()
        {
            // Straight under its parent's middle, 90° off the line: held at 60°.
            var next = Next(new Vec3(1f, -10f, 0f), Whole(Serpent), water: 100f, ground: -100f);
            Assert.Equal(1.5f, next.X, 2);
            Assert.Equal(-0.866f, next.Y, 2);
        }

        [Fact]
        public void ItIsKeptItsRadiusOverTheGround()
        {
            var next = Next(new Vec3(2f, 0f, 0f), Whole(Serpent), water: float.NaN, ground: -0.3f);
            Assert.Equal(0.2f, next.Y, 3);
            var loose = Whole(Serpent);
            loose.GroundCheck = false;
            Assert.Equal(-0.447f, Next(new Vec3(2f, 0f, 0f), loose, water: float.NaN, ground: -0.3f).Y, 2);
        }

        [Fact]
        public void ItMovesItsShareOfTheWayAFrame()
        {
            // Half the way from where it sank to where its parent's line puts it.
            var whole = Next(new Vec3(2f, 0f, 0f), Whole(Serpent), water: float.NaN, ground: -100f);
            var half = Next(new Vec3(2f, 0f, 0f), Serpent, water: float.NaN, ground: -100f);
            Assert.Equal((-0.5f + whole.Y) / 2f, half.Y, 3);
            Assert.Equal((2f + whole.X) / 2f, half.X, 3);
        }

        [Fact]
        public void HeldStillATailSinksWhileSwimmingItStreamsOutBehindNearTheSurface()
        {
            // The game bends the tail of a creature that always swims. Held still on the stage for ten
            // seconds, the water's slow pull sinks a joint well under its parent's line; swimming
            // forward at the Serpent's speed (away from the joint, toward -x) keeps it on the line,
            // streaming out behind.
            var forward = new Vec3(-1f, 0f, 0f);
            var still = new Vec3(2f, 0f, 0f);
            var swimming = still;
            for (var frame = 0; frame < 600; frame++)
            {
                still = Next(still, Serpent, water: 10f, ground: -100f, dt: 1f / 60f);
                swimming = Next(TailBend.Swum(swimming, forward, 10f, 1f / 60f), Serpent, water: 10f, ground: -100f, dt: 1f / 60f);
            }
            Assert.True(still.Y < -0.5f, $"held still it sank to {still.Y}");
            Assert.True(swimming.Y > -0.05f, $"swimming it sank to {swimming.Y}");
            Assert.True(swimming.X > 2f, $"swimming it stands at {swimming.X}, not streamed out behind");
            // A frame at 10 m/s leaves it a sixtieth of ten metres behind.
            Assert.Equal(2f + 10f / 60f, TailBend.Swum(new Vec3(2f, 0f, 0f), forward, 10f, 1f / 60f).X, 4);
            // Not swimming, nothing goes by: the joint stays where it was.
            Assert.Equal(still.X, TailBend.Swum(still, forward, 0f, 1f / 60f).X, 5);
        }

        [Fact]
        public void AShareOfTheWayBeyondAllOrNoneIsTakenAsAllOrNone()
        {
            // The game moves a joint by a share it holds between none and all (Vector3.Lerp), as a mod's tail may set any.
            var whole = Next(new Vec3(2f, 0f, 0f), Whole(Serpent), water: float.NaN, ground: -100f);
            var over = Serpent;
            over.Smoothness = 3f;
            var under = Serpent;
            under.Smoothness = -1f;
            var sank = new Vec3(2f, -0.5f, 0f);
            Assert.Equal(whole.Y, Next(new Vec3(2f, 0f, 0f), over, water: float.NaN, ground: -100f).Y, 3);
            Assert.Equal(sank.Y, Next(new Vec3(2f, 0f, 0f), under, water: float.NaN, ground: -100f).Y, 3);
        }
    }
}
