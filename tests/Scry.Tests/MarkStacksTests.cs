using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MarkStacksTests
    {
        // Marks kept on the map that stand so near on the screen that they touch are a stack:
        // at rest each stands where it lies, with a pip under it for each mark there; pointed at,
        // the stack fans its marks out round their spot and its words name every kind there.

        private static MarkStacks Gathered(float touch, params (float X, float Z)[] spots)
        {
            var stacks = new MarkStacks();
            stacks.Gather(spots.Length, i => spots[i], touch);
            return stacks;
        }

        [Fact]
        public void MarksThatTouchOnTheScreenAreOneStackAndPartAsTheMapZoomsIn()
        {
            // Ribs and skull 3 m apart: a dot spans 90 m far out, 0.5 m near in.
            var far = Gathered(90f, (0f, 0f), (3f, 0.5f), (400f, 0f));
            Assert.Equal(far.AnchorOf(0), far.AnchorOf(1));
            Assert.Equal(2, far.CountAt(far.AnchorOf(0)));
            Assert.Equal(1, far.CountAt(far.AnchorOf(2)));
            // Wherever they lie on the world's grid, below and left of its middle too.
            var across = Gathered(10f, (9f, -1f), (11f, 1f), (-31f, -29f), (-29f, -31f));
            Assert.Equal(across.AnchorOf(0), across.AnchorOf(1));
            Assert.Equal(across.AnchorOf(2), across.AnchorOf(3));
            var near = Gathered(0.5f, (0f, 0f), (3f, 0.5f));
            Assert.NotEqual(near.AnchorOf(0), near.AnchorOf(1));
            Assert.Equal(1, near.CountAt(near.AnchorOf(0)));
        }

        [Fact]
        public void MarksStackOnlyOnceTheyHideAThirdOfEachOther()
        {
            // A mark 20 m wide on the map: 16 m apart the two still read apart, 12 m apart they stack.
            const float width = 20f;
            var touch = width * MarkStacks.Touching;
            var apart = Gathered(touch, (0f, 0f), (width * 0.8f, 0f));
            Assert.NotEqual(apart.AnchorOf(0), apart.AnchorOf(1));
            var close = Gathered(touch, (0f, 0f), (width * 0.6f, 0f));
            Assert.Equal(close.AnchorOf(0), close.AnchorOf(1));
        }

        [Fact]
        public void AStackGathersRoundItsFirstMarkSoARowOfMarksIsNotOneLongStack()
        {
            // Ten marks in a row, each touching the next: stacks round the first mark of each, never all ten as one.
            var row = Enumerable.Range(0, 10).Select(i => (i * 8f, 0f)).ToArray();
            var stacks = Gathered(10f, row);
            Assert.True(stacks.CountAt(stacks.AnchorOf(0)) < 10);
            Assert.All(Enumerable.Range(0, 10), i => Assert.True(Math.Abs(row[stacks.AnchorOf(i)].Item1 - row[i].Item1) < 10f));
            // Every mark is in exactly one stack, and a stack's members are told first to last.
            Assert.Equal(10, Enumerable.Range(0, 10).Select(stacks.AnchorOf).Distinct().Sum(stacks.CountAt));
            Assert.Equal(new[] { 0, 1 }, stacks.MembersOf(stacks.AnchorOf(0)));
        }

        [Fact]
        public void GatheringAgainForgetsTheStacksBefore()
        {
            var stacks = Gathered(90f, (0f, 0f), (3f, 0f));
            stacks.Gather(2, i => i == 0 ? (0f, 0f) : (3f, 0f), 0.5f);
            Assert.Equal(1, stacks.CountAt(stacks.AnchorOf(0)));
            Assert.Equal(new[] { 1 }, stacks.MembersOf(stacks.AnchorOf(1)));
            // Zoomed out again, the second mark heads no stack of its own any more.
            stacks.Gather(2, i => i == 0 ? (0f, 0f) : (3f, 0f), 90f);
            Assert.Equal(0, stacks.CountAt(1));
            Assert.Empty(stacks.MembersOf(1));
            Assert.Equal(new[] { 0, 1 }, stacks.MembersOf(0));
        }

        [Fact]
        public void ZoomingInAndOutAgainCountsEveryMarkOnceInItsOwnStack()
        {
            var random = new Random(3);
            var spots = Enumerable.Range(0, 60).Select(_ => ((float)random.NextDouble() * 400f, (float)random.NextDouble() * 400f)).ToArray();
            var stacks = new MarkStacks();
            foreach (var touch in new[] { 5f, 60f, 5f, 30f, 120f, 2f, 60f })
            {
                stacks.Gather(spots.Length, i => spots[i], touch);
                for (var i = 0; i < spots.Length; i++)
                {
                    var anchor = stacks.AnchorOf(i);
                    Assert.Contains(i, stacks.MembersOf(anchor));
                    if (anchor != i) Assert.Equal(0, stacks.CountAt(i));
                }
                Assert.Equal(spots.Length, Enumerable.Range(0, spots.Length).Where(i => stacks.AnchorOf(i) == i).Sum(stacks.CountAt));
            }
        }

        [Fact]
        public void AMarkTouchingTwoStacksJoinsTheFirst()
        {
            var stacks = Gathered(10f, (0f, 0f), (15f, 0f), (7.5f, 0f));
            Assert.Equal(0, stacks.AnchorOf(2));
            Assert.Equal(1, stacks.CountAt(1));
            // Wherever the two stacks lie on the world's grid.
            foreach (var (a, b, mark) in new[] { (12f, 25f, 18.5f), (-25f, -12f, -18.5f), (5f, 18f, 11.5f) })
            {
                stacks = Gathered(10f, (a, 3f), (b, 3f), (mark, 3f));
                Assert.Equal(0, stacks.AnchorOf(2));
                stacks = Gathered(10f, (3f, a), (3f, b), (3f, mark));
                Assert.Equal(0, stacks.AnchorOf(2));
            }
        }

        [Fact]
        public void MarksTouchingAcrossAnyEdgeOfTheGridAreOneStack()
        {
            foreach (var (a, b) in new[] { (19f, 21f), (21f, 19f), (-21f, -19f), (-19f, -21f), (39.5f, 40.5f), (-0.5f, 0.5f) })
            {
                var across = Gathered(10f, (a, 5f), (b, 5f));
                Assert.Equal(0, across.AnchorOf(1));
                var down = Gathered(10f, (5f, a), (5f, b));
                Assert.Equal(0, down.AnchorOf(1));
                var corner = Gathered(10f, (a, a), (b, b));
                Assert.Equal(0, corner.AnchorOf(1));
            }
        }

        [Fact]
        public void AStackShowsAPipForEachMarkUpToFourAndALoneMarkNone()
        {
            Assert.Equal(0, MarkStacks.Pips(1));
            Assert.Equal(2, MarkStacks.Pips(2));
            Assert.Equal(3, MarkStacks.Pips(3));
            Assert.Equal(4, MarkStacks.Pips(9));
        }

        [Fact]
        public void PipsStandInARowUnderTheMarkClearOfIt()
        {
            const float size = 20f;
            var pips = Enumerable.Range(0, 3).Select(i => MarkStacks.PipAt(i, 3, size)).ToList();
            // Under the mark, past its ring, centred, in order left to right and apart.
            Assert.All(pips, p => Assert.True(p.Y <= -size / 2f));
            Assert.Equal(0f, pips.Sum(p => p.X), 3);
            for (var i = 1; i < pips.Count; i++) Assert.True(pips[i].X - pips[i - 1].X >= MarkStacks.PipSize(size) * 1.2f);
            // Big enough to see on the minimap's small marks.
            Assert.True(MarkStacks.PipSize(8f) >= 2.5f);
        }

        [Fact]
        public void ASquareTooSmallForItsCountShowsAPipForEachPlaceInsteadUpToFour()
        {
            // Its tab counting them stands where the square has room; zoomed out, the pips do.
            Assert.Equal(0, MarkStacks.SquarePips(3, tabShows: true));
            Assert.Equal(3, MarkStacks.SquarePips(3, tabShows: false));
            Assert.Equal(4, MarkStacks.SquarePips(12, tabShows: false));
            Assert.Equal(0, MarkStacks.SquarePips(1, tabShows: false));
        }

        [Fact]
        public void ASquaresPipsStandInARowInItsMiddleInsideIt()
        {
            foreach (var side in new[] { 20f, 31f })
            {
                var pips = Enumerable.Range(0, 4).Select(i => MarkStacks.SquarePipAt(i, 4, side)).ToList();
                var width = MarkStacks.SquarePipSize(side);
                Assert.Equal(0f, pips.Sum(p => p.X), 3);
                Assert.All(pips, p => Assert.Equal(0f, p.Y));
                // The whole row inside the square, the pips apart.
                Assert.True(pips[3].X + width / 2f <= side / 2f && pips[0].X - width / 2f >= -side / 2f, $"{side}");
                for (var i = 1; i < pips.Count; i++) Assert.True(pips[i].X - pips[i - 1].X > width, $"{side}");
            }
            // Never so small it cannot be seen, nor grown past a pip in a square zoomed near.
            Assert.True(MarkStacks.SquarePipSize(4f) >= 3f);
            Assert.True(MarkStacks.SquarePipSize(200f) <= 6f);
        }

        [Fact]
        public void APointedStackFansItsMarksRoundTheirSpotWithoutTouching()
        {
            const float size = 20f;
            foreach (var n in new[] { 2, 3, 5, 12 })
            {
                var fan = Enumerable.Range(0, n).Select(i => MarkStacks.FanAt(i, n, size)).ToList();
                // Each clear of the spot itself, which stays marked, and of every other.
                Assert.All(fan, f => Assert.True(Math.Sqrt(f.X * f.X + f.Y * f.Y) >= size * 0.9f, $"{n}: {f}"));
                for (var i = 0; i < n; i++)
                {
                    for (var j = i + 1; j < n; j++)
                    {
                        var (dx, dy) = (fan[i].X - fan[j].X, fan[i].Y - fan[j].Y);
                        Assert.True(Math.Sqrt(dx * dx + dy * dy) >= size, $"{n}: {i} and {j}");
                    }
                }
            }
            // Two stand left and right of their spot, as the eye reads them.
            var (left, right) = (MarkStacks.FanAt(0, 2, size), MarkStacks.FanAt(1, 2, size));
            Assert.True(left.X < 0 && right.X > 0 && Math.Abs(left.Y) < 0.01f && Math.Abs(right.Y) < 0.01f);
        }

        [Fact]
        public void AFanKeepsEachMarkOnTheSideItLies()
        {
            // Two side by side spread to their own sides, whichever comes first.
            var two = MarkStacks.FanAngles(new[] { (-1f, 0f), (1f, 0f) });
            Assert.True(Math.Cos(two[0]) < -0.9 && Math.Cos(two[1]) > 0.9, $"{two[0]}, {two[1]}");
            var swapped = MarkStacks.FanAngles(new[] { (1f, 0f), (-1f, 0f) });
            Assert.True(Math.Cos(swapped[0]) > 0.9 && Math.Cos(swapped[1]) < -0.9);
            // One above another, up and down.
            var upright = MarkStacks.FanAngles(new[] { (0f, -2f), (0.1f, 3f) });
            Assert.True(Math.Sin(upright[0]) < -0.9 && Math.Sin(upright[1]) > 0.9);
            // Four round their middle each keep near their own way, and stay evenly apart.
            var bearings = new[] { 3.4, 0.3, 4.8, 1.9 };
            var four = MarkStacks.FanAngles(bearings.Select(b => ((float)Math.Cos(b), (float)Math.Sin(b))).ToArray());
            for (var i = 0; i < 4; i++)
            {
                var off = Math.Abs(Math.IEEERemainder(four[i] - bearings[i], 2 * Math.PI));
                Assert.True(off < Math.PI / 4, $"{i}: {four[i]} for {bearings[i]}");
            }
            var sorted = four.Select(a => (a % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI)).OrderBy(a => a).ToArray();
            for (var i = 1; i < 4; i++) Assert.Equal(Math.PI / 2, sorted[i] - sorted[i - 1], 3);
            // All on one spot: the fan as it stands with no way to go by.
            var same = MarkStacks.FanAngles(new[] { (0f, 0f), (0f, 0f) });
            Assert.True(Math.Abs(Math.Cos(same[0]) - Math.Cos(same[1])) > 1.9);
        }

        [Fact]
        public void AFanStaysWhileThePointerIsOnItAndClosesPastIt()
        {
            const float size = 20f;
            var reach = MarkStacks.FanRadius(3, size);
            Assert.True(MarkStacks.StaysFanned(0f, 0f, 3, size));
            Assert.True(MarkStacks.StaysFanned(reach + size / 2f - 1f, 0f, 3, size));
            Assert.False(MarkStacks.StaysFanned(reach + size, 0f, 3, size));
        }

        [Fact]
        public void APointedStackNamesEveryKindThereNearestFirst()
        {
            var words = SpotWords.StackHover(new[]
            {
                new StackedMark("Giant skull", false, 160f, "north-east"),
                new StackedMark("Giant ribs", false, 150f, "east"),
                new StackedMark("Giant ribs", false, 140f, "north-east"),
                new StackedMark("Copper deposit", true, 300f, "north"),
            });
            Assert.Equal(
                "4 marks here, point at one to name it alone\n" +
                "2 Giant ribs · nearest 140 m north-east\n" +
                "Giant skull · 160 m north-east\n" +
                "Copper deposit · the server says one stands here · 300 m north",
                words);
            // One told of beside you says so.
            // One told of and one seen of a kind are two lines, as their dots differ.
            var lines = SpotWords.StackHover(new[] { new StackedMark("Tin", true, 2f, "north"), new StackedMark("Tin", false, 3f, "north") }).Split('\n');
            Assert.Equal(new[] { "Tin · the server says one stands here, beside you", "Tin · beside you" }, lines.Skip(1));
        }
    }

    /// <summary>The gathering timed with no other test running beside it, as the search's load is.</summary>
    [Collection(nameof(SearchLoadTests))]
    public class MarkStacksLoadTests
    {
        [Fact]
        public void TwoThousandMarksAreGatheredWellInsideAFrame()
        {
            var random = new Random(7);
            var spots = Enumerable.Range(0, 2000).Select(_ => ((float)random.NextDouble() * 10000f, (float)random.NextDouble() * 10000f)).ToArray();
            var stacks = new MarkStacks();
            stacks.Gather(spots.Length, i => spots[i], 90f);
            // The quickest of ten, so a pause of the test process is not taken for the gathering's own cost.
            var quickest = double.MaxValue;
            for (var i = 0; i < 10; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                stacks.Gather(spots.Length, j => spots[j], 90f);
                quickest = Math.Min(quickest, watch.Elapsed.TotalMilliseconds);
            }
            // A quarter of a frame here, the rest of the suite's work still going beside it; some
            // 0.8 ms alone. The self-test times the same in the game's own runtime.
            Assert.True(quickest < 4.0, $"{quickest:0.00} ms");
        }
    }
}
