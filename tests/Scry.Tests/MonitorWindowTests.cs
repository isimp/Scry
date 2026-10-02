using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MonitorWindowTests
    {
        // The resource monitor keeps Scry's last frames: its own time and the frame's, what it
        // allocated and the memory cleanups that ran in its work, for its figures over the last
        // seconds and a little graph of them; and each part of its work smoothed over a couple
        // of seconds, the largest first.

        [Fact]
        public void ItTellsScrysTimeNowOnAverageAndAtTheMost()
        {
            var window = new MonitorWindow(4);
            Assert.Equal(0, window.Count);
            Assert.Equal(0.0, window.Mean, 6);
            window.Add(1.0, 16.0, 0, 0);
            window.Add(3.0, 16.0, 0, 0);
            Assert.Equal(2, window.Count);
            Assert.Equal(3.0, window.Last, 6);
            Assert.Equal(2.0, window.Mean, 6);
            Assert.Equal(3.0, window.Max, 6);
            // Its share of the frames' time.
            Assert.Equal(4.0 / 32.0, window.Share, 6);
        }

        [Fact]
        public void ItKeepsOnlyItsLastFrames()
        {
            var window = new MonitorWindow(3);
            foreach (var ms in new[] { 9.0, 1.0, 1.0, 1.0 }) window.Add(ms, 10.0, 0, 0);
            Assert.Equal(3, window.Count);
            Assert.Equal(1.0, window.Max, 6);
            Assert.Equal(1.0, window.Mean, 6);
        }

        [Fact]
        public void ItTellsWhatScryAllocatesASecondAndTheCleanupsInItsWork()
        {
            var window = new MonitorWindow(10);
            window.Add(1.0, 500.0, 2048, 0);
            window.Add(1.0, 500.0, 2048, 1);
            // 4 KB over a second.
            Assert.Equal(4096.0, window.BytesPerSecond, 3);
            Assert.Equal(1, window.Cleanups);
            Assert.Equal(1.0, window.Seconds, 6);
        }

        [Fact]
        public void ItsGraphHasTheMostOfEachStretchOldestFirst()
        {
            var window = new MonitorWindow(6);
            foreach (var ms in new[] { 5.0, 1.0, 2.0, 2.0, 7.0, 0.5 }) window.Add(ms, 16.0, 0, 0);
            Assert.Equal(new[] { 5.0, 2.0, 7.0 }, window.Columns(3));
            // Fewer frames than columns: the frames there are, at the right.
            var young = new MonitorWindow(6);
            young.Add(4.0, 16.0, 0, 0);
            Assert.Equal(new[] { 0.0, 0.0, 4.0 }, young.Columns(3));
        }

        [Fact]
        public void ItsPartsAreSmoothedAndTheLargestComeFirst()
        {
            var window = new MonitorWindow(10, smoothing: 0.5);
            window.AddPart("update", 1.0);
            window.AddPart("panel", 3.0);
            window.AddPart("panel", 1.0);
            window.EndParts();
            Assert.Equal(new[] { "panel", "update" }, window.Parts(5).Select(p => p.Name));
            Assert.Equal(2.0, window.Parts(5)[0].Ms, 6);

            // A part not seen in a frame fades; one seen again takes its share of it.
            window.AddPart("update", 1.0);
            window.EndParts();
            Assert.Equal(1.0, window.Parts(5).First(p => p.Name == "panel").Ms, 6);
            Assert.Equal(0.75, window.Parts(5).First(p => p.Name == "update").Ms, 6);
            Assert.Single(window.Parts(1));

            // Faded to nothing, a part is no longer told.
            var quick = new MonitorWindow(10, smoothing: 1.0);
            quick.AddPart("once", 2.0);
            quick.EndParts();
            Assert.Single(quick.Parts(5));
            quick.EndParts();
            Assert.Empty(quick.Parts(5));
        }

        [Fact]
        public void ItsPercentileIsOfItsFrames()
        {
            var window = new MonitorWindow(100);
            for (var i = 1; i <= 100; i++) window.Add(i, 100.0, 0, 0);
            Assert.Equal(95.0, window.Percentile(0.95), 6);
            Assert.Equal(0.0, new MonitorWindow(5).Percentile(0.95), 6);
        }
    }
}
