using System;
using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class StageCameraTests
    {
        // The stage camera circles a point it looks at from a distance. The wheel zooms toward
        // what is under the pointer, which stays under it; a drag with the right button moves the
        // view so what was grabbed follows the pointer, along the floor where one is opened; with
        // a floor opened the camera looks at that floor, framed on what stands on it. A floor's
        // cut leaves the creatures whole: drawn again, they keep only what is above the cut.

        private static void Near(Vec3 expected, Vec3 actual, float within = 0.001f) =>
            Assert.True(Vec3.Distance(expected, actual) <= within, $"expected {expected}, found {actual}");

        private static Vec3 Unit(Vec3 v) => v * (1f / v.Length);

        [Fact]
        public void ZoomingTowardAPointKeepsItWhereItIsInThePicture()
        {
            var pivot = new Vec3(0f, 0f, 0f);
            var forward = Unit(new Vec3(0f, -0.6f, 0.8f));
            var distance = 50f;
            var eye = pivot - forward * distance;
            var point = new Vec3(12f, -3f, 20f);

            var factor = 0.5f;
            var after = StageCamera.ZoomToward(pivot, point, factor);
            var eyeAfter = after - forward * (distance * factor);

            // The point is seen the same way round as before, from nearer.
            Near(Unit(point - eye), Unit(point - eyeAfter));
            Assert.Equal(Vec3.Distance(eye, point) * factor, Vec3.Distance(eyeAfter, point), 3);
        }

        [Fact]
        public void ZoomingOutBacksAwayFromThePointTheSameWay()
        {
            var pivot = new Vec3(4f, 1f, -2f);
            var point = new Vec3(10f, 0f, 6f);
            Near(new Vec3(-2f, 2f, -10f), StageCamera.ZoomToward(pivot, point, 2f));
            Near(pivot, StageCamera.ZoomToward(pivot, point, 1f));
        }

        [Fact]
        public void TheWheelComesAsNearAsTwoMetresHoweverBigWhatIsFramed()
        {
            Assert.Equal(0.02f, StageCamera.LeastZoom(100f), 4);
            Assert.Equal(0.004f, StageCamera.LeastZoom(500f), 4);
            // Never nearer than a five-hundredth, nor less near than a model a seventh of its framing.
            Assert.Equal(0.002f, StageCamera.LeastZoom(5000f), 4);
            Assert.Equal(0.15f, StageCamera.LeastZoom(5f), 4);
            // Before anything is framed.
            Assert.Equal(0.15f, StageCamera.LeastZoom(0f), 4);
            Assert.Equal(0.15f, StageCamera.LeastZoom(-1f), 4);
        }

        [Fact]
        public void ARayMeetsAFloorBelowIt()
        {
            var at = StageCamera.OnLevel(new Vec3(0f, 10f, 0f), new Vec3(0f, -1f, 1f), 2f, 100f);
            Assert.NotNull(at);
            Near(new Vec3(0f, 2f, 8f), at.Value);
        }

        [Fact]
        public void ARayMeetsNoFloorItRunsAwayFromOrAlongOrTooFarOff()
        {
            // Up from above it, level with it, or meeting it farther than is looked.
            Assert.Null(StageCamera.OnLevel(new Vec3(0f, 10f, 0f), new Vec3(0f, 1f, 1f), 2f, 100f));
            Assert.Null(StageCamera.OnLevel(new Vec3(0f, 10f, 0f), new Vec3(0f, 0f, 1f), 2f, 100f));
            Assert.Null(StageCamera.OnLevel(new Vec3(0f, 10f, 0f), new Vec3(0f, -0.01f, 1f), 2f, 100f));
            // From below a floor, looking down, it never meets it.
            Assert.Null(StageCamera.OnLevel(new Vec3(0f, 1f, 0f), new Vec3(0f, -1f, 1f), 2f, 100f));
        }

        [Fact]
        public void ARayMeetsThePlaneThroughWhatIsLookedAtFacingTheCamera()
        {
            var forward = new Vec3(0f, 0f, 1f);
            var at = StageCamera.OnFacing(new Vec3(0f, 0f, -10f), new Vec3(0.5f, 0.25f, 1f), new Vec3(3f, 3f, 0f), forward);
            Assert.NotNull(at);
            Near(new Vec3(5f, 2.5f, 0f), at.Value);
            // However long the ray as given.
            Near(new Vec3(5f, 2.5f, 0f), StageCamera.OnFacing(new Vec3(0f, 0f, -10f), new Vec3(1f, 0.5f, 2f), new Vec3(3f, 3f, 0f), forward).Value);
            // A ray looking away from it or along it meets nothing, nor one with it behind the camera.
            Assert.Null(StageCamera.OnFacing(new Vec3(0f, 0f, -10f), new Vec3(0f, 0f, -1f), new Vec3(3f, 3f, 0f), forward));
            Assert.Null(StageCamera.OnFacing(new Vec3(0f, 0f, -10f), new Vec3(1f, 0f, 0f), new Vec3(3f, 3f, 0f), forward));
            Assert.Null(StageCamera.OnFacing(new Vec3(0f, 0f, 10f), new Vec3(0.5f, 0.25f, 1f), new Vec3(3f, 3f, 0f), forward));
        }

        [Fact]
        public void DraggingAlongAFloorKeepsWhatWasGrabbedUnderThePointer()
        {
            var eye = new Vec3(0f, 20f, -20f);
            var before = new Vec3(0f, -1f, 1f);
            var now = new Vec3(0.3f, -1f, 1.2f);
            var move = StageCamera.DragOnLevel(eye, before, now, 0f, 500f);
            Assert.NotNull(move);
            // Along the floor only.
            Assert.Equal(0f, move.Value.Y, 4);

            // Moved with the camera, the pointer now finds what it grabbed.
            var grabbed = StageCamera.OnLevel(eye, before, 0f, 500f).Value;
            var found = StageCamera.OnLevel(eye + move.Value, now, 0f, 500f).Value;
            Near(grabbed, found);
        }

        [Fact]
        public void DraggingOffTheFloorMovesNothingAlongIt()
        {
            var eye = new Vec3(0f, 20f, -20f);
            Assert.Null(StageCamera.DragOnLevel(eye, new Vec3(0f, -1f, 1f), new Vec3(0f, 0.2f, 1f), 0f, 500f));
        }

        [Fact]
        public void DraggingAcrossThePictureMovesTheViewAPointerWidthAtItsDistance()
        {
            // At 10 m with a field of view of 90 degrees the picture is 20 m tall, 40 m wide at 2 to 1.
            var (right, up) = StageCamera.Drag(0.1f, -0.25f, 10f, 90f, 2f);
            // What is under the pointer goes with it: the view moves the other way.
            Assert.Equal(-4f, right, 3);
            Assert.Equal(5f, up, 3);
        }

        [Fact]
        public void AFloorIsFramedOnWhatStandsOnIt()
        {
            var corners = new List<Vec3> { new Vec3(-10f, 0f, 2f), new Vec3(30f, 4f, 2f), new Vec3(30f, -2f, 32f), new Vec3(-10f, 0f, 32f) };
            var frame = StageCamera.Across(corners);
            Assert.NotNull(frame);
            Assert.Equal(10f, frame.Value.X, 3);
            Assert.Equal(17f, frame.Value.Z, 3);
            // Half the way from corner to corner across: 40 by 30 m.
            Assert.Equal(25f, frame.Value.Radius, 3);
            Assert.Null(StageCamera.Across(new List<Vec3>()));
        }

        // The creatures' second picture keeps only what is above the cut: its depth runs from the
        // camera's near distance to the cut itself, so what lies below it falls past the far end.
        // Points in the camera's own space, looking along -Z; the depth a point gets is the row
        // times the point, over its distance ahead.
        private static float Depth((float X, float Y, float Z, float W) row, Vec3 v) =>
            (row.X * v.X + row.Y * v.Y + row.Z * v.Z + row.W) / -v.Z;

        [Fact]
        public void AboveTheCutTheCreaturesAreDrawnBelowItNot()
        {
            // The cut 3 m under a camera looking level.
            var row = StageCamera.AboveCutRow(0f, 1f, 0f, 3f, 0.1f).Value;
            Assert.Equal(1f, Depth(row, new Vec3(0f, -3f, -10f)), 4);
            var above = Depth(row, new Vec3(0.5f, -2f, -10f));
            Assert.True(above < 1f && above > -1f, $"{above}");
            Assert.True(Depth(row, new Vec3(0f, -4f, -10f)) > 1f);
            // Its near end is where the camera's would be.
            Assert.Equal(-1f, Depth(row, new Vec3(0f, 0f, -0.1f)), 4);
            Assert.True(Depth(row, new Vec3(0f, 0f, -0.05f)) < -1f);
        }

        [Fact]
        public void AboveATiltedCutNearerStillHidesFarther()
        {
            // Looking down 40 degrees onto a cut 20 m below: in the camera's space the world's up
            // leans back toward the camera.
            var a = (float)(40.0 * Math.PI / 180.0);
            var up = new Vec3(0f, (float)Math.Cos(a), (float)Math.Sin(a));
            var row = StageCamera.AboveCutRow(up.X, up.Y, up.Z, 20f, 0.2f).Value;

            var ray = Unit(new Vec3(0.1f, 0.05f, -1f));
            float Height(Vec3 v) => Vec3.Dot(up, v) + 20f;
            var near = ray * 5f;
            var far = ray * 15f;
            Assert.True(Height(near) > 0f && Height(far) > 0f);
            var nearDepth = Depth(row, near);
            var farDepth = Depth(row, far);
            Assert.True(nearDepth < farDepth && farDepth < 1f && nearDepth > -1f, $"{nearDepth}, {farDepth}");

            // Where the ray passes the cut is the far end, and past it nothing is drawn.
            var t = 20f / -Vec3.Dot(up, ray);
            Assert.Equal(1f, Depth(row, ray * t), 3);
            Assert.True(Depth(row, ray * (t + 1f)) > 1f);
        }

        [Fact]
        public void ACutLeaningSidewaysIsFollowedToo()
        {
            // The cut leaning to one side of the picture, 5 m under the camera.
            var up = Unit(new Vec3(0.3f, 0.9f, 0.1f));
            var row = StageCamera.AboveCutRow(up.X, up.Y, up.Z, 5f, 0.1f).Value;
            float Height(Vec3 v) => Vec3.Dot(up, v) + 5f;

            // On it, at either side of the picture, is its far end.
            var left = new Vec3(-8f, 0f, -10f);
            left.Y = (-5f - up.X * left.X - up.Z * left.Z) / up.Y;
            var right = new Vec3(8f, 0f, -10f);
            right.Y = (-5f - up.X * right.X - up.Z * right.Z) / up.Y;
            Assert.Equal(0f, Height(left), 3);
            Assert.Equal(1f, Depth(row, left), 3);
            Assert.Equal(1f, Depth(row, right), 3);
            Assert.True(Depth(row, new Vec3(left.X, left.Y + 0.5f, left.Z)) < 1f);
            Assert.True(Depth(row, new Vec3(right.X, right.Y - 0.5f, right.Z)) > 1f);
        }

        [Fact]
        public void WithACutLaidTheCameraStaysAboveIt()
        {
            // Looking down 30 degrees at a point 4 m under the cut, it stays at least 9 m off, half
            // a metre over the cut: 4.5 m up over sin 30.
            Assert.Equal(9f, StageCamera.OverCut(4f, 30f), 3);
            // A cut below what it looks at asks nothing; looking level or from below, nothing either.
            Assert.Equal(0f, StageCamera.OverCut(-2f, 30f), 3);
            Assert.Equal(0f, StageCamera.OverCut(4f, 2f), 3);
            Assert.Equal(0f, StageCamera.OverCut(4f, -10f), 3);
        }

        [Fact]
        public void BelowTheCutTheCameraKeepsNoSuchPicture()
        {
            Assert.Null(StageCamera.AboveCutRow(0f, 1f, 0f, 0f, 0.1f));
            Assert.Null(StageCamera.AboveCutRow(0f, 1f, 0f, -2f, 0.1f));
        }
    }
}
