using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CrowdLayoutTests
    {
        // A raid's wave on the stage: the copies there have no bodies to push each other apart as
        // live creatures do, so each group is laid out with room for every body, spread at least
        // as wide as the game spreads a group (m_groupRadius), and the groups kept apart.

        private static double Distance((int Group, float X, float Z) a, (int Group, float X, float Z) b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(20)]
        [InlineData(40)]
        public void NoTwoOfAGroupStandInEachOther(int count)
        {
            var placed = CrowdLayout.Place(new[] { new CrowdGroup { Count = count, Body = 0.8f, Spread = 0f } });

            Assert.Equal(count, placed.Count);
            for (var i = 0; i < placed.Count; i++)
                for (var j = i + 1; j < placed.Count; j++)
                    Assert.True(Distance(placed[i], placed[j]) >= 2 * 0.8 + CrowdLayout.Gap - 1e-3, $"{i} and {j}: {Distance(placed[i], placed[j]):0.00}");
        }

        [Theory]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(19)]
        [InlineData(20)]
        public void AGroupSpreadsAsWideAsTheGameSpreadsIt(int count)
        {
            var placed = CrowdLayout.Place(new[] { new CrowdGroup { Count = count, Body = 0.5f, Spread = 12f } });

            var widest = placed.Max(p => Math.Sqrt(p.X * p.X + p.Z * p.Z));
            Assert.Equal(12 - 0.5, widest, 2);
        }

        [Fact]
        public void ALoneGroupStandsInTheMiddle()
        {
            var placed = CrowdLayout.Place(new[] { new CrowdGroup { Count = 1, Body = 1f, Spread = 3f } });
            Assert.Equal(0f, placed[0].X, 3);
            Assert.Equal(0f, placed[0].Z, 3);
        }

        [Fact]
        public void GroupsKeepApartHoweverUnlikeTheirSizes()
        {
            var groups = new[]
            {
                new CrowdGroup { Count = 30, Body = 1.2f, Spread = 0f },
                new CrowdGroup { Count = 1, Body = 0.4f, Spread = 0f },
                new CrowdGroup { Count = 30, Body = 1.2f, Spread = 8f },
                new CrowdGroup { Count = 1, Body = 0.4f, Spread = 0f },
                new CrowdGroup { Count = 5, Body = 2.5f, Spread = 0f },
            };
            var placed = CrowdLayout.Place(groups);

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, placed.Select(p => p.Group).Distinct());
            for (var i = 0; i < placed.Count; i++)
                for (var j = i + 1; j < placed.Count; j++)
                {
                    if (placed[i].Group == placed[j].Group) continue;
                    var need = groups[placed[i].Group].Body + groups[placed[j].Group].Body + CrowdLayout.Gap;
                    Assert.True(Distance(placed[i], placed[j]) >= need - 1e-3, $"{placed[i].Group} and {placed[j].Group}: {Distance(placed[i], placed[j]):0.00}");
                }
        }

        [Fact]
        public void EachCreatureIsPlacedInTheOrderGiven()
        {
            var placed = CrowdLayout.Place(new[] { new CrowdGroup { Count = 2, Body = 0.5f }, new CrowdGroup { Count = 3, Body = 0.5f } });
            Assert.Equal(new[] { 0, 0, 1, 1, 1 }, placed.Select(p => p.Group));
        }
    }
}
