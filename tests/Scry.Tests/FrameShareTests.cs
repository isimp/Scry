using System;
using Xunit;

namespace Scry.Tests
{
    public class FrameShareTests
    {
        /// <summary>
        /// One frame of work: pieces begun for as long as the share allows, each taking what
        /// <paramref name="piece"/> says. How many were done and how long the frame's work took.
        /// </summary>
        private static (int Pieces, double Ms) Frame(FrameShare share, Func<double> piece)
        {
            var done = 0;
            var elapsed = 0.0;
            while (share.MayBegin(elapsed, done))
            {
                var took = piece();
                share.Took(took);
                elapsed += took;
                done++;
                if (done > 1000) break;
            }
            return (done, elapsed);
        }

        [Fact]
        public void EveryFrameDoesAPieceHoweverLongOneTakes()
        {
            // A person's animator can take longer for one piece than the whole share; the work
            // still has to go on, a piece a frame.
            var share = new FrameShare(4);
            for (var frame = 0; frame < 5; frame++) Assert.Equal(1, Frame(share, () => 10).Pieces);
        }

        [Fact]
        public void APieceThatWouldRunPastTheShareWaitsForTheNextFrame()
        {
            // As the log showed: pieces of about two milliseconds went on until the budget was
            // spent, then one more, and frames took 9-11 ms of an 8 ms budget.
            var share = new FrameShare(4);
            Frame(share, () => 2);
            for (var frame = 0; frame < 5; frame++)
            {
                var (pieces, ms) = Frame(share, () => 2.5);
                Assert.True(ms <= 4 || pieces == 1, $"a frame took {ms} ms over {pieces} pieces");
            }
        }

        [Fact]
        public void PiecesThatFitAreAllDoneInTheSameFrame()
        {
            var share = new FrameShare(4);
            Frame(share, () => 1);
            Assert.Equal(4, Frame(share, () => 1).Pieces);
        }

        [Fact]
        public void TheFirstFrameLearnsWhatAPieceTakesFromItsFirstPiece()
        {
            // Nothing is known before the first piece, so it is begun; the second is begun only
            // if one more like it fits.
            var share = new FrameShare(4);
            Assert.Equal(1, Frame(share, () => 3).Pieces);
        }

        [Fact]
        public void PiecesGrowingSlowerAreSoonBegunMoreSparingly()
        {
            // Quick pieces at first (a run's cheap bookkeeping), then the heavy ones: within a few
            // frames no frame runs past its share by more than one piece's worth.
            var share = new FrameShare(4);
            for (var frame = 0; frame < 3; frame++) Frame(share, () => 0.2);
            for (var frame = 0; frame < 3; frame++) Frame(share, () => 1.5);
            for (var frame = 0; frame < 5; frame++) Assert.True(Frame(share, () => 1.5).Ms <= 4);
        }

        [Fact]
        public void PiecesGrowingQuickerAreSoonBegunMoreOften()
        {
            var share = new FrameShare(4);
            for (var frame = 0; frame < 3; frame++) Frame(share, () => 3);
            for (var frame = 0; frame < 3; frame++) Frame(share, () => 0.5);
            Assert.True(Frame(share, () => 0.5).Pieces >= 7);
        }
    }
}
