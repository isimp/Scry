using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class StationTests
    {
        private static List<Entry> Catalog()
        {
            var sword = E("SwordIron", Kind.Item, "Iron sword");
            sword.Stations = new[] { new StationUse("forge", "Forge", 2) };

            var mace = E("MaceSilver", Kind.Item, "Frostner");
            mace.Stations = new[] { new StationUse("forge", "Forge", 4) };

            var club = E("Club", Kind.Item, "Club");
            club.Stations = new[] { new StationUse("piece_workbench", "Workbench", 1) };

            var wall = E("stone_wall", Kind.Piece, "Stone wall");
            wall.Stations = new[] { new StationUse("piece_stonecutter", "Stonecutter", 1) };

            var wood = E("Wood", Kind.Item, "Wood");

            var bench = E("piece_chair", Kind.Piece, "Chair");
            bench.Stations = new[] { new StationUse("piece_artisanstation", "Artisan table", 1) };

            return new List<Entry> { sword, mace, club, wall, wood, bench };
        }

        private static List<string> Find(string text)
        {
            return Search.Run(Catalog(), new Query { Text = text }, new List<string>()).Select(e => e.Name).ToList();
        }

        [Fact]
        public void AStationFindsEverythingMadeAtIt()
        {
            Assert.Equal(new[] { "MaceSilver", "SwordIron" }, Find("station:forge"));
        }

        [Fact]
        public void AStationWithALevelFindsWhatThatLevelCanMake()
        {
            Assert.Equal(new[] { "SwordIron" }, Find("station:forge3"));
            Assert.Equal(new[] { "MaceSilver", "SwordIron" }, Find("station:forge4"));
        }

        [Fact]
        public void AStationIsFoundByTheNameTheGameShowsToo()
        {
            Assert.Equal(new[] { "Club" }, Find("station:workbench"));
            Assert.Equal(new[] { "stone_wall" }, Find("station:stonecut"));
            Assert.Equal(new[] { "piece_chair" }, Find("station:table"));
        }

        [Fact]
        public void WhatNoStationMakesIsNotFoundByOne()
        {
            Assert.DoesNotContain("Wood", Find("station:forge"));
            Assert.DoesNotContain("Wood", Find("station:work"));
        }

        [Fact]
        public void LeavingAStationOutWorksLikeAnyTerm()
        {
            Assert.Equal(new[] { "Club", "piece_chair", "stone_wall", "Wood" }, Find("-station:forge"));
        }
    }
}
