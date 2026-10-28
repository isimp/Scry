using Xunit;

namespace Scry.Tests
{
    public class PartWindowTests
    {
        private static FrameTimes Frame(params (string Part, double Ms)[] parts)
        {
            var frame = new FrameTimes();
            foreach (var (part, ms) in parts) frame.Add(part, ms, 0, 0);
            return frame;
        }

        [Fact]
        public void TheLineTellsScrysWorkAFrameOverTheWindowWithItsOuterPartsSlowestFirst()
        {
            var window = new PartWindow();
            window.Add(Frame(("update", 1.0), ("links", 0.2)));
            window.Add(Frame(("update", 0.6), ("links", 0.2), ("marks", 0.4)));

            Assert.Equal("In the last 30 s Scry's work took 1.20 ms a frame over 2 frames, 1.20 ms at most: update 0.80, links 0.20, marks 0.20.", window.Line(30));
        }

        [Fact]
        public void PartsInsideAnotherAreToldApartAfterTheOuterOnesAndNotCountedInTheWhole()
        {
            // "update compendium" runs inside "update": summing both would count it twice.
            var window = new PartWindow();
            window.Add(Frame(("update", 2.0), ("update compendium", 1.5), ("update dungeon walks", 0.3)));
            window.Add(Frame(("update", 2.0), ("update compendium", 1.1), ("update dungeon walks", 0.5)));

            Assert.Equal("In the last 30 s Scry's work took 2.00 ms a frame over 2 frames, 2.00 ms at most: update 2.00; inside them update compendium 1.30, update dungeon walks 0.40.", window.Line(30));
        }

        [Fact]
        public void ASingleInnerPartIsToldToo()
        {
            var window = new PartWindow();
            window.Add(Frame(("update", 1.0), ("update locations", 0.75)));

            Assert.Equal("In the last 30 s Scry's work took 1.00 ms a frame over 1 frame, 1.00 ms at most: update 1.00; inside them update locations 0.75.", window.Line(30));
        }

        [Fact]
        public void TheSlowestFrameIsToldBesideTheAverageSoAnOddHitchIsNotMistakenForSteadyCost()
        {
            var window = new PartWindow();
            for (var i = 0; i < 4; i++) window.Add(Frame(("update", 0.5)));
            window.Add(Frame(("update", 45.5)));
            for (var i = 0; i < 5; i++) window.Add(Frame(("update", 0.5)));

            Assert.StartsWith("In the last 30 s Scry's work took 5.00 ms a frame over 10 frames, 45.50 ms at most: ", window.Line(30));
        }

        [Fact]
        public void APartTooSmallToTellAtTwoDecimalsIsLeftOut()
        {
            var window = new PartWindow();
            window.Add(Frame(("update", 1.0), ("update key", 0.001), ("render", 0.002)));

            Assert.Equal("In the last 30 s Scry's work took 1.00 ms a frame over 1 frame, 1.00 ms at most: update 1.00.", window.Line(30));
        }

        [Fact]
        public void OnlyTheSlowestInnerPartsAreToldSoTheLineStaysReadable()
        {
            var window = new PartWindow();
            var frame = new FrameTimes();
            frame.Add("update", 20, 0, 0);
            for (var i = 1; i <= 12; i++) frame.Add("update part" + i, i, 0, 0);
            window.Add(frame);

            var line = window.Line(30);
            Assert.Contains("inside them update part12 12.00, update part11 11.00", line);
            Assert.Contains("update part5 5.00.", line);
            Assert.DoesNotContain("update part4 ", line);
        }

        [Fact]
        public void ResetStartsTheNextWindowEmpty()
        {
            var window = new PartWindow();
            window.Add(Frame(("update", 3.0)));
            window.Reset();
            window.Add(Frame(("links", 0.5)));

            Assert.Equal("In the last 30 s Scry's work took 0.50 ms a frame over 1 frame, 0.50 ms at most: links 0.50.", window.Line(30));
        }

        [Fact]
        public void AWindowWithoutFramesSaysSo()
        {
            Assert.Equal("In the last 30 s Scry did no work it times.", new PartWindow().Line(30));
        }
    }
}
