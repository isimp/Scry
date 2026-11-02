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

        // The game moves a joint a share of the way once a frame, so how far a swimming tail trails
        // depends on how long a frame took. The stage's frames run unevenly (a page being laid can
        // take a tenth of a second), and stepped once a frame the tail would stretch and spring
        // back with them. Stepped on a clock of its own it keeps one shape whatever the frames.

        private static Vec3 SwimThrough(float[] frames)
        {
            var forward = new Vec3(-1f, 0f, 0f);
            var joint = new Vec3(2f, 0f, 0f);
            var carried = 0f;
            foreach (var dt in frames)
            {
                var steps = TailBend.Steps(ref carried, dt, out _);
                for (var step = 0; step < steps; step++)
                    joint = Next(TailBend.Swum(joint, forward, 10f, TailBend.Step), Serpent, water: 10f, ground: -100f, dt: TailBend.Step);
            }
            return joint;
        }

        private static float[] Frames(int count, params float[] lengths)
        {
            var frames = new float[count];
            for (var i = 0; i < count; i++) frames[i] = lengths[i % lengths.Length];
            return frames;
        }

        [Fact]
        public void ASwimmingTailTrailsAsFarWhetherTheFramesRunEvenlyOrNot()
        {
            // Two seconds each way: sixty frames a second, then frames now short now long, then a
            // hitch of a quarter second among them.
            var even = SwimThrough(Frames(120, 1f / 60f));
            var uneven = SwimThrough(Frames(80, 1f / 120f, 1f / 24f));
            var hitched = SwimThrough(Frames(36, 1f / 60f, 1f / 60f, 1f / 60f, 1f / 60f, 1f / 60f, 0.25f));
            // At ten metres a second it trails a step's swim behind where it belongs, as at sixty frames a second.
            Assert.Equal(2f + 10f / 60f, even.X, 2);
            Assert.Equal(even.X, uneven.X, 3);
            Assert.Equal(even.X, hitched.X, 3);
        }

        [Theory]
        [InlineData(1f / 60f)]
        // A frame as measured at sixty a second, a hair short of a step.
        [InlineData(0.016666f)]
        public void FramesOfASixtiethTakeOneStepEach(float frameLength)
        {
            var carried = 0f;
            for (var frame = 0; frame < 600; frame++)
            {
                Assert.Equal(1, TailBend.Steps(ref carried, frameLength, out var share));
                Assert.True(share < 0.01f, $"frame {frame} stood {share} into the next step");
            }
        }

        [Fact]
        public void ShortFramesShareTheStepsAndShowHowFarIntoTheNextTheyStand()
        {
            var carried = 0f;
            var steps = 0;
            var lastShare = -1f;
            var rose = 0;
            for (var frame = 0; frame < 144; frame++)
            {
                var taken = TailBend.Steps(ref carried, 1f / 144f, out var share);
                Assert.InRange(taken, 0, 1);
                Assert.InRange(share, 0f, 1f);
                if (taken == 0 && share > lastShare) rose++;
                steps += taken;
                lastShare = share;
            }
            // A second of frames takes a second of steps.
            Assert.InRange(steps, 59, 60);
            // Between steps the share grows, for the joints to be shown part way.
            Assert.True(rose > 60, $"the share grew on {rose} frames");
        }

        [Fact]
        public void ALongFrameTakesTheStepsItSpansUpToTheMost()
        {
            var carried = 0f;
            Assert.Equal(15, TailBend.Steps(ref carried, 0.25f + 1e-4f, out _));
            // A frame far longer is let go past: the tail would have settled anyway.
            carried = 0f;
            Assert.Equal(TailBend.MostSteps, TailBend.Steps(ref carried, 2f, out var share));
            Assert.Equal(0f, share);
            Assert.Equal(1, TailBend.Steps(ref carried, 1f / 60f, out _));
            // No time, or time going back, takes no step.
            carried = 0f;
            Assert.Equal(0, TailBend.Steps(ref carried, 0f, out _));
            Assert.Equal(0, TailBend.Steps(ref carried, -1f, out _));
            Assert.Equal(0f, carried);
        }

        // The game stops a creature once what it attacks is in reach and starts it again after
        // (MonsterAI.StopMoving, Character.UpdateSwimming): a swimmer's speed halves each physics
        // step as it stops, and grows by half its acceleration each step as it starts again. A
        // Serpent: 10 m/s, acceleration 0.2, steps of 0.02 s.

        private static float SwimFor(float seconds, float speed, float target)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += TailBend.Step) speed = TailBend.Eased(speed, target, 0.2f, TailBend.Step, 0.02f);
            return speed;
        }

        [Fact]
        public void ASwimmerStoppingToAttackComesToRestInATenthOfASecond()
        {
            Assert.InRange(SwimFor(0.05f, 10f, 0f), 1f, 3f);
            Assert.True(SwimFor(0.2f, 10f, 0f) < 0.02f, $"{SwimFor(0.2f, 10f, 0f)} m/s after a fifth of a second");
        }

        [Fact]
        public void ASwimmerSetOffAgainTakesTwoSecondsToFullSpeedAndNoMore()
        {
            // Half of 0.2 m/s gained each 0.02 s: five metres a second each second.
            Assert.Equal(5f, SwimFor(1f, 0f, 10f), 1);
            Assert.Equal(10f, SwimFor(2.1f, 0f, 10f), 3);
            Assert.Equal(10f, SwimFor(5f, 0f, 10f), 3);
            // At full speed it stays there.
            Assert.Equal(10f, TailBend.Eased(10f, 10f, 0.2f, TailBend.Step, 0.02f), 5);
        }

        [Fact]
        public void WithoutAPhysicsStepOrTimeTheSpeedIsWhatItWasOrWhereItGoes()
        {
            Assert.Equal(4f, TailBend.Eased(4f, 10f, 0.2f, 0f, 0.02f), 5);
            Assert.Equal(10f, TailBend.Eased(4f, 10f, 0.2f, TailBend.Step, 0f), 5);
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
