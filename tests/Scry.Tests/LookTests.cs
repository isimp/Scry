using System.Collections.Generic;
using System.IO;
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
        public void APrefabsLooksAreWorkedOutOnlyOnceItIsSelected()
        {
            // Reading the looks off a prefab takes a while; a catalog of thousands only needs the selected one's.
            var asked = 0;
            var fire = E("fire_pit", Kind.Piece);
            fire.LooksFrom(() =>
            {
                asked++;
                return (new[] { "Unlit", "Low", "Lit" }, 2);
            });
            var explorer = new Explorer(new List<Entry> { fire, E("Rock_3", Kind.Other) }, new Favourites(Path.Combine(TempDir(), "f.txt")));
            explorer.Text = "fire";
            Assert.Equal(0, asked);

            explorer.Select(fire);

            Assert.Equal(1, asked);
            Assert.Equal(new[] { "Unlit", "Low", "Lit" }, explorer.Modifiers.LookNames);
            Assert.Equal(2, explorer.Modifiers.Look);

            explorer.Select(null);
            explorer.Select(fire);
            Assert.Equal(1, asked);
        }

        [Fact]
        public void LooksThatCannotBeWorkedOutOfferNoneAndTheSelectionStillWorks()
        {
            var broken = E("mod_thing", Kind.Piece);
            broken.LooksFrom(() => throw new System.InvalidOperationException("a mod's script broke"));
            var explorer = new Explorer(new List<Entry> { broken }, new Favourites(Path.Combine(TempDir(), "f.txt")));

            explorer.Select(broken);

            Assert.Same(broken, explorer.Selected);
            Assert.False(explorer.Modifiers.LookAvailable);
            Assert.Equal(0, explorer.Modifiers.Look);
        }

        [Fact]
        public void LooksSetOutrightReplaceTheOnesToBeWorkedOut()
        {
            var asked = 0;
            var fire = E("fire_pit", Kind.Piece);
            fire.LooksFrom(() =>
            {
                asked++;
                return (new[] { "Unlit", "Lit" }, 1);
            });

            fire.Looks = new[] { "Cold", "Warm", "Hot" };

            Assert.Equal(new[] { "Cold", "Warm", "Hot" }, For(fire).LookNames);
            Assert.Equal(0, asked);
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
