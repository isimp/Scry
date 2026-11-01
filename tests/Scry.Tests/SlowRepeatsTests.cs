using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Over a whole run, a frame of Scry's own work past a quarter of a second fails only where the
    /// same part of Scry took most of more than one such frame: a part that comes back is a hot
    /// spot, a frame that stands alone is most likely a stall of the machine or a memory cleanup,
    /// and is noted with its part rather than judged.
    /// </summary>
    public class SlowRepeatsTests
    {
        private static (double Ms, string Part, double PartMs, string Outer, double OuterMs, int Cleanups) Frame(double ms, string part, double partMs, int cleanups = 0) =>
            (ms, part, partMs, "update", ms, cleanups);

        [Fact]
        public void APartTakingMostOfMoreThanOneSlowFrameFailsAndOneFrameAloneIsNoted()
        {
            var frames = new[]
            {
                Frame(702, "facts station", 431),
                Frame(384, "update previews", 300),
                Frame(373, "compendium told", 173, 1),
                Frame(336, "update previews", 306, 1),
                Frame(120, "update previews", 100),
            };
            var judged = SlowRepeats.Of(frames, 250);
            Assert.Equal(new[] { ("update previews", 2, 384.0) }, judged.Repeated);
            Assert.Equal(new[] { "facts station", "compendium told" }, judged.Once.Select(f => f.Part));
        }

        [Fact]
        public void NoSlowFrameIsNothingToJudge()
        {
            var judged = SlowRepeats.Of(new[] { Frame(120, "update previews", 100) }, 250);
            Assert.Empty(judged.Repeated);
            Assert.Empty(judged.Once);
        }

        [Fact]
        public void TheRepeatsAreToldPartByPartTheMostFirst()
        {
            Assert.Equal("update previews in 3 frames, 412 ms at the most; facts station in 2 frames, 300 ms at the most",
                SlowRepeats.Told(new List<(string, int, double)> { ("facts station", 2, 300), ("update previews", 3, 412) }));
        }
    }
}
