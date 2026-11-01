using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FedThenHeldTests
    {
        // A smelter turns copper ore into copper and tin ore into tin. Once the character fed copper
        // ore into a smelter, holding copper, wherever and whenever it came by it, shows the smelter
        // makes copper of copper ore; tin, never fed, shows nothing.

        private static readonly (string Station, string Input)[] Smelting = { ("smelter", "CopperOre"), ("blastfurnace", "CopperOre") };

        [Fact]
        public void HoldingWhatAStationMakesOfWhatWasFedIntoItPairsAllThree()
        {
            var pairs = FedThenHeld.Pairs("Copper", Smelting, (station, input) => station == "smelter" && input == "CopperOre").ToList();
            Assert.Contains(("smelter", "Copper", true), pairs);
            Assert.Contains(("CopperOre", "Copper", false), pairs);
            // A furnace never fed that ore shows nothing.
            Assert.DoesNotContain(pairs, p => p.A == "blastfurnace");
        }

        [Fact]
        public void HoldingItWithNothingFedPairsNothing()
        {
            Assert.Empty(FedThenHeld.Pairs("Copper", Smelting, (station, input) => false));
        }
    }
}
