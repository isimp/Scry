using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class RoomOutlineTests
    {
        private static readonly Vec3[] Square = { new Vec3(0f, 5f, 0f), new Vec3(4f, 5f, 0f), new Vec3(4f, 5f, 4f), new Vec3(0f, 5f, 4f) };

        private static string Edge(Vec3 a, Vec3 b)
        {
            var one = $"{a.X},{a.Y},{a.Z}";
            var two = $"{b.X},{b.Y},{b.Z}";
            return string.CompareOrdinal(one, two) < 0 ? one + "|" + two : two + "|" + one;
        }

        /// <summary>The segments a path draws, a step in place left out.</summary>
        private static HashSet<string> Edges(IReadOnlyList<Vec3> path)
        {
            var edges = new HashSet<string>();
            for (var i = 0; i + 1 < path.Count; i++)
            {
                if (path[i].X == path[i + 1].X && path[i].Y == path[i + 1].Y && path[i].Z == path[i + 1].Z) continue;
                edges.Add(Edge(path[i], path[i + 1]));
            }
            return edges;
        }

        /// <summary>The twelve edges of a box over a square of corners, from one height to another.</summary>
        private static HashSet<string> BoxEdges(Vec3[] corners, float low, float high)
        {
            var edges = new HashSet<string>();
            for (var i = 0; i < 4; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % 4];
                edges.Add(Edge(new Vec3(a.X, low, a.Z), new Vec3(b.X, low, b.Z)));
                edges.Add(Edge(new Vec3(a.X, high, a.Z), new Vec3(b.X, high, b.Z)));
                edges.Add(Edge(new Vec3(a.X, low, a.Z), new Vec3(a.X, high, a.Z)));
            }
            return edges;
        }

        [Fact]
        public void TheLineTracesEveryEdgeOfTheRoomsBoxFromItsFloorToItsTop()
        {
            var path = RoomOutline.Path(Square, 1f, 3f);

            var edges = Edges(path);
            Assert.True(BoxEdges(Square, 1f, 3f).SetEquals(edges));
            Assert.All(path, p => Assert.True(p.Y == 1f || p.Y == 3f));
            Assert.Equal(4, path.Where(p => p.Y == 1f).Select(p => (p.X, p.Z)).Distinct().Count());
            Assert.Equal(4, path.Where(p => p.Y == 3f).Select(p => (p.X, p.Z)).Distinct().Count());
            // Each corner of the floor seen from above stands under one of the top's.
            Assert.All(Square, c => Assert.Contains(path, p => p.X == c.X && p.Z == c.Z && p.Y == 3f));
        }

        [Fact]
        public void UnderASlopingCutTheOutlinesTopFollowsTheSlopeCornerByCorner()
        {
            // Each corner's top under the cut there, or the room's own top where that is lower.
            var tops = new[] { 2f, 3f, 9f, 4f };
            var path = RoomOutline.Path(Square, 1f, tops);
            var edges = Edges(path);
            for (var i = 0; i < 4; i++)
            {
                var a = Square[i];
                var b = Square[(i + 1) % 4];
                Assert.Contains(Edge(new Vec3(a.X, 1f, a.Z), new Vec3(a.X, tops[i], a.Z)), edges);
                Assert.Contains(Edge(new Vec3(a.X, tops[i], a.Z), new Vec3(b.X, tops[(i + 1) % 4], b.Z)), edges);
                Assert.Contains(Edge(new Vec3(a.X, 1f, a.Z), new Vec3(b.X, 1f, b.Z)), edges);
            }
            Assert.Equal(12, edges.Count);
        }

        [Fact]
        public void WhereTheSlopeMeetsTheFloorAtOneCornerTheOtherCornersStillStandUp()
        {
            // The cut reaches the ground at the first corner only.
            var path = RoomOutline.Path(Square, 1f, new[] { 0.5f, 3f, 3f, 3f });
            Assert.Contains(path, p => p.Y == 3f);
            Assert.DoesNotContain(path, p => p.Y < 1f);
        }

        [Fact]
        public void TheOutlineTracesTheRoomsWholeBoxHoweverLowTheCutWhereItIsDrawnPastTheCut()
        {
            Assert.Equal(10f, RoomOutline.Top(2.5f, 10f, pastCut: true), 4);
            Assert.Equal(10f, RoomOutline.Top(12f, 10f, pastCut: true), 4);
        }

        [Fact]
        public void DrawnWithTheCutTheOutlinesTopRunsJustUnderItSoTheCutNeverClipsIt()
        {
            Assert.Equal(2.5f - RoomOutline.UnderCut, RoomOutline.Top(2.5f, 10f, pastCut: false), 4);
            // A room lower than the cut is outlined to its own top.
            Assert.Equal(10f, RoomOutline.Top(12f, 10f, pastCut: false), 4);
        }

        [Fact]
        public void WithItsTopNoHigherThanItsFloorOnlyTheFloorIsOutlined()
        {
            var path = RoomOutline.Path(Square, 1f, 1f);

            Assert.Equal(5, path.Count);
            Assert.All(path, p => Assert.Equal(1f, p.Y));
            Assert.Equal(4, Edges(path).Count);
        }

        [Fact]
        public void ARoomWithoutFourCornersIsNotOutlined()
        {
            Assert.Empty(RoomOutline.Path(Square.Take(3).ToArray(), 1f, 3f));
        }

        [Fact]
        public void TheOutlinesPixelsAreCountedByTheirAccentColour()
        {
            // Amber within the tolerance on every channel counts; anything else does not.
            // The last three lie just at the tolerance, one channel each.
            var film = new[] { 0xF5B857, 0xF0B050, 0xF5B857 + 0x000040, 0x20242C, 0xEDE8DE, 0xF5B857 + 0x000010, 0xE5B857, 0xF5A857 };

            Assert.Equal(5, AccentPixels.Count(film, 0xF5B857, 16));
            Assert.Equal(0, AccentPixels.Count(new int[0], 0xF5B857, 16));
        }

        [Fact]
        public void WhatTheOutlineAddsToAFilmIsTheChangeFromTheSameFilmWithoutIt()
        {
            // Two pixels turned amber, one barely changed, one by just the threshold, the rest the same: the outline's are the two.
            var without = new[] { 0x202020, 0x303030, 0x404040, 0x505050, 0x606060 };
            var with = new[] { 0xF5B857, 0xF3B655, 0x424242, 0x505050, 0x7E7E7E };

            var (changed, average) = AccentPixels.Changed(with, without, 30);

            Assert.Equal(2, changed);
            Assert.Equal(0xF4B756, average);
            Assert.Equal((0, -1), AccentPixels.Changed(new[] { 0xFFFFFF }, new[] { 0, 0 }, 30));
            Assert.Equal((0, -1), AccentPixels.Changed(without, without, 30));
        }
    }
}
