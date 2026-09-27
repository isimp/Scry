using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class UsesTests
    {
        private static string Show(UseGroup group) =>
            $"{group.Kind} {group.Place ?? "-"}: " + string.Join(", ", group.Targets.Select(t => t.Amount > 0 ? $"{t.Target} x{t.Amount}" : t.Target));

        [Fact]
        public void AnItemShowsWhatItIsUsedToMakeWithHowManyEachTakes()
        {
            var book = new UseBook();
            book.Add("Resin", UseKind.Crafts, "Torch", 1, "hand");
            book.Add("Resin", UseKind.Crafts, "ArrowWood", 2, "piece_workbench");

            var groups = book.Of("Resin").Select(Show).ToList();

            Assert.Equal(new[] { "Crafts hand: Torch x1", "Crafts piece_workbench: ArrowWood x2" }, groups);
        }

        [Fact]
        public void TheSameUseNotedTwiceIsListedOnce()
        {
            // Mods register recipes again, and a recipe can appear in the list twice.
            var book = new UseBook();
            book.Add("Wood", UseKind.Builds, "wood_wall", 2, "piece_workbench");
            book.Add("Wood", UseKind.Builds, "wood_wall", 2, "piece_workbench");

            Assert.Equal(new[] { "Builds piece_workbench: wood_wall x2" }, book.Of("Wood").Select(Show));
        }

        [Fact]
        public void UsesComeInTheSameOrderEveryTimeWhateverOrderTheyWereFoundIn()
        {
            // Made things first, then built, then what stations turn it into, what burns it, and what eats it.
            var book = new UseBook();
            book.Add("Wood", UseKind.EatenBy, "Goat", 0);
            book.Add("Wood", UseKind.Fuels, "piece_fireplace", 0);
            book.Add("Wood", UseKind.TurnsInto, "Coal", 1, "charcoal_kiln");
            book.Add("Wood", UseKind.Builds, "wood_wall", 2, "piece_workbench");
            book.Add("Wood", UseKind.Crafts, "Club", 6, "hand");

            var kinds = book.Of("Wood").Select(g => g.Kind).ToList();

            Assert.Equal(new[] { UseKind.Crafts, UseKind.Builds, UseKind.TurnsInto, UseKind.Fuels, UseKind.EatenBy }, kinds);
        }

        [Fact]
        public void UsesOfOneKindAreGroupedByWhereTheyHappenInTheOrderFirstFound()
        {
            var book = new UseBook();
            book.Add("CopperOre", UseKind.TurnsInto, "Copper", 1, "smelter");
            book.Add("TinOre", UseKind.TurnsInto, "Tin", 1, "smelter");
            book.Add("CopperOre", UseKind.TurnsInto, "Copper", 1, "blastfurnace");

            Assert.Equal(new[] { "TurnsInto smelter: Copper x1", "TurnsInto blastfurnace: Copper x1" }, book.Of("CopperOre").Select(Show));
        }

        [Fact]
        public void AnItemNothingUsesHasNoUses()
        {
            var book = new UseBook();
            book.Add("Wood", UseKind.Fuels, "piece_fireplace", 0);

            Assert.Empty(book.Of("Ruby"));
            Assert.Empty(book.Of(null));
        }

        [Fact]
        public void AThingIsNotListedAsAUseOfItself()
        {
            // An upgrade recipe lists the item among its own ingredients.
            var book = new UseBook();
            book.Add("SwordIron", UseKind.Crafts, "SwordIron", 1, "forge");

            Assert.Empty(book.Of("SwordIron"));
        }

        [Fact]
        public void HowManyUsesThereAreIsKnownWithoutListingThem()
        {
            var book = new UseBook();
            book.Add("Wood", UseKind.Builds, "wood_wall", 2, "piece_workbench");
            book.Add("Wood", UseKind.Builds, "wood_floor", 2, "piece_workbench");
            book.Add("Wood", UseKind.Fuels, "piece_fireplace", 0);

            Assert.Equal(3, book.CountOf("Wood"));
            Assert.Equal(0, book.CountOf("Ruby"));
        }

        [Fact]
        public void StartingAgainForgetsEveryUse()
        {
            var book = new UseBook();
            book.Add("Wood", UseKind.Fuels, "piece_fireplace", 0);

            book.Clear();

            Assert.Empty(book.Of("Wood"));

            // The next world's catalog notes the same uses again.
            book.Add("Wood", UseKind.Fuels, "piece_fireplace", 0);
            Assert.Single(book.Of("Wood"));
        }
    }
}
