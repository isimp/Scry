using Xunit;

namespace Scry.Tests
{
    public class ChipNamesTests
    {
        [Fact]
        public void NamesNoOtherChipSharesStayAsTheyAre()
        {
            Assert.Equal(new[] { "Crude bow", "Ash Fang" }, ChipNames.Apart(new[] { ("Bow", "Crude bow"), ("BowAshlands", "Ash Fang") }));
        }

        [Fact]
        public void ChipsSharingAShownNameAreToldApartByTheirPrefabNames()
        {
            // Four Draugr in a row read "Draugr" four times; the prefab names say which is which.
            var names = ChipNames.Apart(new[] { ("Draugr_Ranged", "Draugr"), ("Draugr_Elite", "Draugr"), ("Troll", "Troll") });

            Assert.Equal(new[] { "Draugr · Draugr_Ranged", "Draugr · Draugr_Elite", "Troll" }, names);
        }

        [Fact]
        public void APrefabNamedAsItIsShownNeedsNoSuffix()
        {
            var names = ChipNames.Apart(new[] { ("Draugr", "Draugr"), ("Draugr_Ranged", "Draugr") });

            Assert.Equal(new[] { "Draugr", "Draugr · Draugr_Ranged" }, names);
        }
    }
}
