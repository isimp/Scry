using System;
using Xunit;

namespace Scry.Tests
{
    public class StageFramingTests
    {
        // The stage frames what it shows: the camera stands far enough off for it to fill the
        // picture, glides to what it is to frame, and lays the floor, grid and sky around it.

        [Fact]
        public void TheCameraStandsFarEnoughForWhatIsFramedToFillThePicture()
        {
            // A sphere of ten metres seen through 60 degrees fills the picture from twenty off.
            Assert.Equal(20f, StageFraming.FramedDistance(10f, 60f), 3);
            Assert.Equal(20f, StageFraming.Distance(10f, 60f, zoom: 1f, overCut: 0f), 3);
            Assert.Equal(10f, StageFraming.Distance(10f, 60f, zoom: 0.5f, overCut: 0f), 3);
        }

        [Fact]
        public void NothingFramedYetHasNoDistance()
        {
            Assert.Equal(0f, StageFraming.FramedDistance(0f, 60f));
            Assert.Equal(0f, StageFraming.FramedDistance(-1f, 60f));
        }

        [Fact]
        public void OverAFloorsCutTheCameraStandsNoNearerThanKeepsItOverTheCut()
        {
            Assert.Equal(15f, StageFraming.Distance(10f, 60f, zoom: 0.1f, overCut: 15f), 3);
            Assert.Equal(20f, StageFraming.Distance(10f, 60f, zoom: 1f, overCut: 15f), 3);
        }

        [Fact]
        public void ThePictureDrawsFromAHundredthOfTheWayToWellPastWhatIsFramed()
        {
            var (near, far) = StageFraming.Clipping(100f, 10f);
            Assert.Equal(1f, near, 3);
            Assert.Equal(170f, far, 3);
            // Never nearer than a centimetre.
            Assert.Equal(0.01f, StageFraming.Clipping(0.5f, 0.1f).Near, 4);
        }

        [Fact]
        public void AModelsFramingTakesInWhatItPlaysUpToHalfAgainAndMoreItsOwnSize()
        {
            Assert.Equal(2.5f, StageFraming.ModelRadius(own: 2f, reach: 2.5f, followEffect: false), 3);
            Assert.Equal(3.2f, StageFraming.ModelRadius(own: 2f, reach: 10f, followEffect: false), 3);
        }

        [Fact]
        public void AnEffectsFramingFollowsItsParticlesUpToTwentyFiveMetres()
        {
            Assert.Equal(10f, StageFraming.ModelRadius(own: 2f, reach: 10f, followEffect: true), 3);
            Assert.Equal(25f, StageFraming.ModelRadius(own: 2f, reach: 40f, followEffect: true), 3);
        }

        [Fact]
        public void TheFramingWidensQuicklySoNothingLeavesThePicture()
        {
            var widened = StageFraming.Glide(10f, 20f, toFloor: false, seconds: 0.1f);
            Assert.Equal(10f + 10f * (1f - (float)Math.Exp(-0.5)), widened, 3);
        }

        [Fact]
        public void TheFramingNarrowsSlowlyOnceWhatIsFramedSettles()
        {
            var narrowed = StageFraming.Glide(20f, 10f, toFloor: false, seconds: 0.1f);
            Assert.Equal(20f - 10f * (1f - (float)Math.Exp(-0.1)), narrowed, 3);
        }

        [Fact]
        public void TheFramingGoesToAFloorSteppedToQuicklyEitherWay()
        {
            var narrowed = StageFraming.Glide(20f, 10f, toFloor: true, seconds: 0.1f);
            Assert.Equal(20f - 10f * (1f - (float)Math.Exp(-0.5)), narrowed, 3);
        }

        [Fact]
        public void TheFramingArrivesHoweverLongAFrameTakes()
        {
            Assert.Equal(10f, StageFraming.Glide(10f, 20f, toFloor: false, seconds: 0f), 3);
            Assert.Equal(20f, StageFraming.Glide(10f, 20f, toFloor: false, seconds: 60f), 3);
            Assert.Equal(1f - (float)Math.Exp(-0.6), StageFraming.CentreShare(0.1f), 4);
            Assert.Equal(1f, StageFraming.CentreShare(60f), 4);
        }

        [Fact]
        public void TheCameraIsDoneGlidingWithinAHalfPercentAndTwoCentimetres()
        {
            Assert.False(StageFraming.StillGliding(10.04f, 10f, 0f));
            Assert.True(StageFraming.StillGliding(10.06f, 10f, 0f));
            Assert.True(StageFraming.StillGliding(9.94f, 10f, 0f));
            Assert.True(StageFraming.StillGliding(10f, 10f, 0.03f));
            Assert.False(StageFraming.StillGliding(10f, 10f, 0.01f));
            // A small framing is done within a centimetre.
            Assert.False(StageFraming.StillGliding(0.509f, 0.5f, 0f));
            Assert.True(StageFraming.StillGliding(0.512f, 0.5f, 0f));
        }

        [Fact]
        public void TheGridIsWholeTensOfMetresSoAFiveMetreLineRunsUnderTheMiddle()
        {
            // Ten at least, before anything is framed too.
            Assert.Equal(10f, StageFraming.GridMetres(0f));
            Assert.Equal(10f, StageFraming.GridMetres(1f));
            Assert.Equal(10f, StageFraming.GridMetres(2.5f));
            Assert.Equal(20f, StageFraming.GridMetres(3f));
            Assert.Equal(40f, StageFraming.GridMetres(10f));
        }

        [Fact]
        public void TheFloorAndTheSkyReachPastWhatIsFramed()
        {
            Assert.Equal(32f, StageFraming.FloorSize(10f), 3);
            // The sky fills the picture at its depth, with a little to spare.
            var tall = 2f * 100f * (float)Math.Tan(30.0 * Math.PI / 180.0);
            Assert.Equal(tall * 1.05f, StageFraming.SkyHeight(100f, 60f), 2);
        }

        [Fact]
        public void AFloorOpenedIsFramedAtLeastTwoMetresAcross()
        {
            Assert.Equal(2f, StageFraming.FloorReach(0.5f), 3);
            Assert.Equal(12f, StageFraming.FloorReach(12f), 3);
        }

        [Fact]
        public void ThePersonStandsALittleToTheLeftOfTheModel()
        {
            // Its right side 40 cm left of the model's left side.
            Assert.Equal(-3.7f, StageFraming.PersonX(modelLeft: -3f, personHalfWidth: 0.3f), 3);
        }
    }
}
