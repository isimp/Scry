using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ToolBookTests
    {
        // A build tool is any item with a build table: the hammer, the hoe, the cultivator and any
        // mod's own. A piece says which tool builds it and on which tab; a tool lists what it
        // builds, tab by tab in the tool's own order.

        private static ToolBook Book()
        {
            var book = new ToolBook();
            book.Add("Hammer", "Hammer", "piece_workbench", "Crafting", 2);
            book.Add("Hammer", "Hammer", "wood_wall", "Building", 1);
            book.Add("Hammer", "Hammer", "piece_chest_wood", "Furniture", 3);
            book.Add("Hammer", "Hammer", "wood_floor", "Building", 1);
            book.Add("OP_Bamboo_Hammer", "Bamboo Hammer", "OP_Bamboo_Build_Totem", "Bamboozled", 0);
            book.Add("OP_Bamboo_Hammer", "Bamboo Hammer", "OP_Bamboo_Pole", "Bamboozled", 0);
            book.Add("Cultivator", "Cultivator", "OP_Bamboo_Sapling", "Misc", 4);
            return book;
        }

        [Fact]
        public void APieceSaysWhichToolBuildsItAndOnWhichTab()
        {
            var tools = Book().ToolsOf("OP_Bamboo_Pole");

            var only = Assert.Single(tools);
            Assert.Equal("OP_Bamboo_Hammer", only.Tool);
            Assert.Equal("Bamboo Hammer", only.ToolName);
            Assert.Equal("Bamboozled", only.Tab);
        }

        [Fact]
        public void APieceInSeveralToolsNamesEachOnce()
        {
            var book = Book();
            book.Add("ModHammer", "Mod hammer", "wood_wall", "Walls", 0);
            book.Add("Hammer", "Hammer", "wood_wall", "Building", 1);

            Assert.Equal(new[] { "Hammer", "ModHammer" }, book.ToolsOf("wood_wall").Select(t => t.Tool));
        }

        [Fact]
        public void APieceOnTwoTabsOfOneToolNamesTheToolOnce()
        {
            var book = Book();
            book.Add("Hammer", "Hammer", "wood_wall", "Misc", 5);

            Assert.Single(book.ToolsOf("wood_wall"));
            Assert.Contains(book.PiecesOf("Hammer"), t => t.Tab == "Misc" && t.Pieces.Contains("wood_wall"));
        }

        [Fact]
        public void ANamelessToolOrPieceIsNotNoted()
        {
            var book = Book();
            book.Add("", "Nothing", "wood_wall", "Building", 1);
            book.Add("Hammer", "Hammer", "", "Building", 1);

            Assert.False(book.IsTool(""));
            Assert.Equal(new[] { "wood_wall", "wood_floor" }, book.PiecesOf("Hammer")[0].Pieces);
        }

        [Fact]
        public void AToolListsWhatItBuildsTabByTabInItsOwnOrder()
        {
            var tabs = Book().PiecesOf("Hammer");

            Assert.Equal(new[] { "Building", "Crafting", "Furniture" }, tabs.Select(t => t.Tab));
            Assert.Equal(new[] { "wood_wall", "wood_floor" }, tabs[0].Pieces);
            Assert.Equal(new[] { "piece_workbench" }, tabs[1].Pieces);
        }

        [Fact]
        public void AToolListsAPieceOncePerTab()
        {
            var book = Book();
            book.Add("Hammer", "Hammer", "wood_wall", "Building", 1);

            Assert.Equal(new[] { "wood_wall", "wood_floor" }, book.PiecesOf("Hammer")[0].Pieces);
        }

        [Fact]
        public void WhatNoToolBuildsAndWhatIsNoToolHaveNone()
        {
            var book = Book();

            Assert.Empty(book.ToolsOf("Beech1"));
            Assert.Empty(book.PiecesOf("SwordIron"));
            Assert.True(book.IsTool("Cultivator"));
            Assert.False(book.IsTool("SwordIron"));
        }

        [Fact]
        public void ABookAsBigAsPlanBuildsAnswersAtOnce()
        {
            // PlanBuild gives a plan of every piece, so the book holds thousands of pieces, and
            // the mod report asks for every mod piece's tools and every mod item's pieces.
            var book = new ToolBook();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 4000; i++)
            {
                book.Add("Hammer", "Hammer", "piece" + i, "Tab" + (i % 7), i % 7);
                book.Add("PlanHammer", "Plan hammer", "piece" + i + "_plan", "Plans" + (i % 9), i % 9);
                book.Add("PlanHammer", "Plan hammer", "piece" + i + "_plan", "Plans" + (i % 9), i % 9);
            }
            for (var i = 0; i < 8000; i++) book.ToolsOf("piece" + i);
            for (var i = 0; i < 2000; i++) book.PiecesOf(i % 2 == 0 ? "Hammer" : "Sword" + i);

            Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
            Assert.Equal("Hammer", Assert.Single(book.ToolsOf("piece17")).Tool);
            Assert.Equal(4000, book.PiecesOf("PlanHammer").Sum(t => t.Pieces.Count));
        }

        [Fact]
        public void ClearingForgetsEveryTool()
        {
            var book = Book();
            book.Clear();

            Assert.Empty(book.ToolsOf("wood_wall"));
            Assert.False(book.IsTool("Hammer"));
        }
    }
}
