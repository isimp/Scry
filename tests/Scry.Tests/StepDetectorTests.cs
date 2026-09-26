using System;
using Xunit;

namespace Scry.Tests
{
    public class StepDetectorTests
    {
        private const float Frame = 1f / 60f;

        /// <summary>How many steps a foot makes, given its height over time.</summary>
        private static int Steps(Func<float, float> height, float seconds)
        {
            var detector = new StepDetector();
            var steps = 0;
            for (var t = 0f; t <= seconds; t += Frame)
            {
                if (detector.Feed(t, height(t))) steps++;
            }
            return steps;
        }

        /// <summary>A foot lifted and set down once a second, 15 cm high.</summary>
        private static float Walk(float t) => 0.075f * (1f - (float)Math.Cos(2 * Math.PI * t));

        [Fact]
        public void AWalkingFootStepsOnceEachTimeItComesDown()
        {
            // Down at 1, 2 and 3 seconds; it starts down, before anything is known of its stride.
            Assert.Equal(3, Steps(Walk, 3.2f));
        }

        [Fact]
        public void AFootThatOnlySwaysDoesNotStep()
        {
            Assert.Equal(0, Steps(t => 0.004f * (float)Math.Sin(2 * Math.PI * t), 5f));
        }

        [Fact]
        public void AFootDippingHighInTheAirDoesNotStep()
        {
            // Lifted to 30 cm with a quick dip of 6 cm at the top, set down once, at 2 seconds.
            float Height(float t)
            {
                var lift = 0.15f * (1f - (float)Math.Cos(Math.PI * t));
                var dip = t > 0.9f && t < 1.1f ? -0.06f * (float)Math.Sin(Math.PI * (t - 0.9f) / 0.2f) : 0f;
                return lift + dip;
            }
            Assert.Equal(1, Steps(Height, 2.1f));
        }

        [Fact]
        public void AFootSettlingWithATremorStepsOnce()
        {
            // Down at 1 second, then trembling on the ground ten times a second for 0.15 seconds.
            float Height(float t)
            {
                var stride = Walk(t);
                var tremor = t > 1f && t < 1.15f ? 0.004f * (float)Math.Sin(20 * Math.PI * t) : 0f;
                return stride + tremor;
            }
            Assert.Equal(1, Steps(Height, 1.4f));
        }

        [Fact]
        public void AFootRestingOnTheGroundStepsOnlyWhenItLands()
        {
            // One stride, down at 1 second, then standing still for a second.
            Assert.Equal(1, Steps(t => t < 1f ? Walk(t) : 0f, 2f));
        }

        [Fact]
        public void AFastRunStepsEveryStride()
        {
            // Three strides a second for two seconds: down every third of a second after the start.
            Assert.Equal(6, Steps(t => Walk(t * 3f), 2.05f));
        }
    }
}
