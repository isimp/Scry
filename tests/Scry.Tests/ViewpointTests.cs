using System;
using Xunit;

namespace Scry.Tests
{
    public class ViewpointTests
    {
        // Things standing still rest while the character and the eye stay where they are, as
        // nothing then comes into reach or view; a step or a turn past a little wakes them, to be
        // looked at again from there. Each frame tells where the character stands, where the eye
        // is and which way it looks.

        private static readonly Vec3 Standing = new Vec3(0f, 0f, 0f);
        private static readonly Vec3 Eye = new Vec3(0f, 1.8f, -3f);
        private static readonly Vec3 Ahead = new Vec3(0f, 0f, 1f);

        private static Vec3 Along(Vec3 at, float x) => new Vec3(at.X + x, at.Y, at.Z);

        /// <summary>Ahead turned about the upright by so many degrees.</summary>
        private static Vec3 Turned(float degrees)
        {
            var radians = degrees * Math.PI / 180.0;
            return new Vec3((float)Math.Sin(radians), 0f, (float)Math.Cos(radians));
        }

        private static Viewpoint Taken()
        {
            var view = new Viewpoint();
            view.Moved(Standing, Eye, Ahead);
            return view;
        }

        [Fact]
        public void TheFirstLookIsFromSomewhereNew()
        {
            Assert.True(new Viewpoint().Moved(Standing, Eye, Ahead));
            // Even standing where nothing was taken, at the world's middle with the eye there too.
            Assert.True(new Viewpoint().Moved(Standing, Standing, Ahead));
        }

        [Fact]
        public void HalfAMetreOrFiveDegreesIsAMoveACentimetreOrHalfADegreeNone()
        {
            // A stride or a glance aside wakes the things resting; standing, breathing and the
            // camera's settling do not.
            var view = Taken();
            Assert.False(view.Moved(Along(Standing, 0.01f), Along(Eye, 0.01f), Turned(0.5f)));
            Assert.True(view.Moved(Along(Standing, 0.5f), Along(Eye, 0.5f), Ahead));

            view = Taken();
            Assert.True(view.Moved(Standing, Eye, Turned(5f)));
        }

        [Fact]
        public void StandingStillIsNoMove()
        {
            var view = Taken();
            for (var frame = 0; frame < 100; frame++) Assert.False(view.Moved(Standing, Eye, Ahead));
        }

        [Fact]
        public void SwayingALittleIsNoMove()
        {
            var view = Taken();
            Assert.False(view.Moved(Along(Standing, Viewpoint.Step * 0.8f), Along(Eye, -Viewpoint.Step * 0.8f), Turned(Viewpoint.Turn * 0.8f)));
            Assert.False(view.Moved(Standing, Eye, Ahead));
        }

        [Fact]
        public void CreepingIsAMoveOnceItAddsUpToAStep()
        {
            // A little at a time is told once it is a step from where the last move was told.
            var view = Taken();
            var step = Viewpoint.Step * 0.4f;

            Assert.False(view.Moved(Along(Standing, step), Along(Eye, step), Ahead));
            Assert.False(view.Moved(Along(Standing, 2 * step), Along(Eye, 2 * step), Ahead));
            Assert.True(view.Moved(Along(Standing, 3 * step), Along(Eye, 3 * step), Ahead));
            Assert.False(view.Moved(Along(Standing, 3 * step), Along(Eye, 3 * step), Ahead));
            Assert.False(view.Moved(Along(Standing, 4 * step), Along(Eye, 4 * step), Ahead));
        }

        [Fact]
        public void TurningTheViewPastALittleIsAMoveSlowlyToo()
        {
            var view = Taken();
            Assert.True(view.Moved(Standing, Eye, Turned(Viewpoint.Turn * 1.5f)));

            view = Taken();
            var turn = Viewpoint.Turn * 0.4f;
            Assert.False(view.Moved(Standing, Eye, Turned(turn)));
            Assert.False(view.Moved(Standing, Eye, Turned(2 * turn)));
            Assert.True(view.Moved(Standing, Eye, Turned(3 * turn)));
        }

        [Fact]
        public void TheEyeMovingAloneIsAMove()
        {
            // The camera drawn in or swung round the character standing still.
            var view = Taken();
            Assert.True(view.Moved(Standing, new Vec3(Eye.X, Eye.Y, Eye.Z + 1f), Ahead));
        }

        [Fact]
        public void TheCharacterMovingAloneIsAMove()
        {
            // How far a thing is seen is measured from the character, which can move with the eye held.
            var view = Taken();
            Assert.True(view.Moved(Along(Standing, 1f), Eye, Ahead));
        }

        [Fact]
        public void AViewWithNoWayToLookIsTakenAsAMove()
        {
            var view = Taken();
            Assert.True(view.Moved(Standing, Eye, new Vec3(0f, 0f, 0f)));
            Assert.True(view.Moved(Standing, Eye, new Vec3(float.NaN, 0f, 1f)));
        }
    }
}
