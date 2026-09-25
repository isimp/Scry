using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class LookTests
    {
        private static Modifiers For(Entry entry)
        {
            var modifiers = new Modifiers();
            modifiers.ResetFor(entry);
            return modifiers;
        }

        [Fact]
        public void APrefabWithSeveralLooksOffersThemAll()
        {
            var fire = E("fire_pit", Kind.Piece);
            fire.Looks = new[] { "Unlit", "Low", "Lit" };

            var modifiers = For(fire);

            Assert.True(modifiers.LookAvailable);
            Assert.Equal(new[] { "Unlit", "Low", "Lit" }, modifiers.LookNames);
        }

        [Fact]
        public void APrefabWithOneLookOrNoneOffersNothingToSwitch()
        {
            var rock = E("Rock_3", Kind.Other);
            var single = E("sign", Kind.Piece);
            single.Looks = new[] { "Only" };

            Assert.False(For(rock).LookAvailable);
            Assert.False(For(single).LookAvailable);
        }

        [Fact]
        public void ALookStaysAmongThoseOffered()
        {
            var fire = E("fire_pit", Kind.Piece);
            fire.Looks = new[] { "Unlit", "Lit" };
            var modifiers = For(fire);

            modifiers.Look = 5;
            Assert.Equal(1, modifiers.Look);

            modifiers.Look = -2;
            Assert.Equal(0, modifiers.Look);
        }

        [Fact]
        public void APrefabStartsInItsOwnDefaultLookAndResetReturnsToIt()
        {
            var draugr = E("Draugr", Kind.Creature);
            draugr.Looks = new[] { "No gear", "Gear" };
            draugr.DefaultLook = 1;
            var modifiers = For(draugr);
            Assert.Equal(1, modifiers.Look);

            modifiers.Look = 0;
            modifiers.Reset();

            Assert.Equal(1, modifiers.Look);
        }

        [Fact]
        public void ChangingTheLookIsNoticeable()
        {
            var fire = E("fire_pit", Kind.Piece);
            fire.Looks = new[] { "Unlit", "Lit" };
            var modifiers = For(fire);
            var before = modifiers.Version;

            modifiers.Look = 1;

            Assert.NotEqual(before, modifiers.Version);
        }
    }
}
