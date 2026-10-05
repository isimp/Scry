using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ChainTests
    {
        // What one page tells of a step, a chain tells whole on every page of it: a hen's egg, its
        // chick and the hen again; a seed, its sapling, the grown plant and its crop; an offering,
        // its altar, the boss, its trophy and its power.

        private static IReadOnlyList<string> Next(Dictionary<string, string[]> map, string key) =>
            map.TryGetValue(key, out var next) ? next : new string[0];

        private static string[] Shown(List<string[]> steps) => steps.Select(s => string.Join("/", s)).ToArray();

        [Fact]
        public void AChainFollowsEachStepToTheNextUntilThereIsNone()
        {
            var map = new Dictionary<string, string[]> { ["sapling_carrot"] = new[] { "Pickable_Carrot" }, ["Pickable_Carrot"] = new[] { "Carrot" } };
            Assert.Equal(new[] { "sapling_carrot", "Pickable_Carrot", "Carrot" }, Shown(ChainBook.Walk("sapling_carrot", k => Next(map, k))));
        }

        [Fact]
        public void ALifeCycleEndsWhereItBeganShowingItOnce()
        {
            var map = new Dictionary<string, string[]> { ["Hen"] = new[] { "ChickenEgg" }, ["ChickenEgg"] = new[] { "Chicken" }, ["Chicken"] = new[] { "Hen" } };
            Assert.Equal(new[] { "Hen", "ChickenEgg", "Chicken", "Hen" }, Shown(ChainBook.Walk("Hen", k => Next(map, k))));
        }

        [Fact]
        public void AStepOfSeveralShowsThemAllAndGoesOnFromTheFirst()
        {
            // A sapling grows into one of two trees; an Ashlands young grows into one of two looks.
            var map = new Dictionary<string, string[]> { ["Birch_Sapling"] = new[] { "Birch1", "Birch2" }, ["Birch1"] = new[] { "Wood" } };
            Assert.Equal(new[] { "Birch_Sapling", "Birch1/Birch2", "Wood" }, Shown(ChainBook.Walk("Birch_Sapling", k => Next(map, k))));
        }

        [Fact]
        public void AChainNeverGoesOnForeverNorRepeatsAStepInside()
        {
            var map = new Dictionary<string, string[]> { ["a"] = new[] { "b" }, ["b"] = new[] { "c" }, ["c"] = new[] { "b" } };
            // b comes back: the chain ends at its second b.
            Assert.Equal(new[] { "a", "b", "c", "b" }, Shown(ChainBook.Walk("a", k => Next(map, k))));
            var line = Enumerable.Range(0, 20).ToDictionary(i => "s" + i, i => new[] { "s" + (i + 1) });
            Assert.Equal(ChainBook.Most, ChainBook.Walk("s0", k => Next(line, k)).Count);
        }

        [Fact]
        public void EachPageOfAChainFindsItWithItsStepAndTheSameChainIsKeptOnce()
        {
            var book = new ChainBook();
            var steps = new List<string[]> { new[] { "Hen" }, new[] { "ChickenEgg" }, new[] { "Chicken" }, new[] { "Hen" } };
            book.Add(ChainWords.Breeding, steps);
            book.Add(ChainWords.Breeding, new List<string[]> { new[] { "Hen" }, new[] { "ChickenEgg" }, new[] { "Chicken" }, new[] { "Hen" } });
            var chain = Assert.Single(book.Of("ChickenEgg"));
            Assert.Equal(ChainWords.Breeding, chain.Title);
            Assert.Equal(1, chain.StepOf("ChickenEgg"));
            Assert.Empty(book.Of("Boar"));
            // A chain of one step tells nothing.
            book.Add(ChainWords.Planting, new List<string[]> { new[] { "Beech_Sapling" } });
            Assert.Empty(book.Of("Beech_Sapling"));
            book.Clear();
            Assert.Empty(book.Of("Hen"));
        }

        [Fact]
        public void EachChainIsTitledByWhatItFollows()
        {
            Assert.Equal("From parents to grown", ChainWords.Breeding);
            Assert.Equal("From seed to harvest", ChainWords.Planting);
            Assert.Equal("From offering to power", ChainWords.Summoning);
        }
    }
}
