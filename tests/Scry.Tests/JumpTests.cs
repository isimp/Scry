using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class JumpTests
    {
        private static Explorer Open() => new Explorer(Game(), new Favourites(Path.Combine(TempDir(), "f.txt")));

        [Fact]
        public void JumpingToAnIngredientSelectsIt()
        {
            var explorer = Open();

            Assert.True(explorer.Jump("Bow"));

            Assert.Equal("Bow", explorer.Selected?.Name);
            Assert.Equal(explorer.Results.ToList().IndexOf(explorer.Selected), explorer.SelectedIndex);
        }

        [Fact]
        public void JumpingKeepsTheSearchWhenTheItemIsAlreadyListed()
        {
            var explorer = Open();
            explorer.Text = "troll";

            explorer.Jump("TrollArmorChest");

            Assert.Equal("troll", explorer.Text);
            Assert.Equal("TrollArmorChest", explorer.Selected?.Name);
        }

        [Fact]
        public void JumpingToSomethingTheFiltersHideClearsThemSoItShows()
        {
            var explorer = Open();
            explorer.Text = "troll";
            explorer.KindFilter = Kind.Creature;
            explorer.FavouritesOnly = true;
            explorer.RecentOnly = true;
            explorer.Origin = OriginFilter.Mods;

            explorer.Jump("Bow");

            Assert.Equal("Bow", explorer.Selected?.Name);
            Assert.Contains(explorer.Selected, explorer.Results);
        }

        [Fact]
        public void JumpingToAStatusEffectsNameFindsThePrefabNotTheStatusEffect()
        {
            var catalog = Game();
            catalog.Add(E("Rested", Kind.Other));
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "f.txt")));

            explorer.Jump("Rested");

            Assert.Equal(Kind.Other, explorer.Selected?.Kind);
        }

        [Fact]
        public void JumpingToAStatusEffectFindsTheStatusEffect()
        {
            var catalog = Game();
            catalog.Add(E("Rested", Kind.Other));
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "f.txt")));

            Assert.True(explorer.Jump("Rested", statusEffect: true));

            Assert.Equal(Kind.StatusEffect, explorer.Selected?.Kind);
        }

        [Fact]
        public void ALinkToARaidGoesToTheRaid()
        {
            // A raid is no prefab: a creature's "Comes in the raid" line links to it by its key.
            var catalog = Game();
            catalog.Add(E("foresttrolls", Kind.Raid, "The ground is shaking"));
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "f.txt")));

            Assert.True(explorer.Jump("raid:foresttrolls"));

            Assert.Equal(Kind.Raid, explorer.Selected?.Kind);
        }

        [Fact]
        public void ARaidAndAPrefabOfTheSameNameStayApart()
        {
            var catalog = Game();
            catalog.Insert(0, E("Troll", Kind.Raid, "Trolls!"));
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "f.txt")));

            explorer.Jump("Troll");
            Assert.Equal(Kind.Creature, explorer.Selected?.Kind);

            explorer.Jump("raid:Troll");
            Assert.Equal(Kind.Raid, explorer.Selected?.Kind);
        }

        [Fact]
        public void AStatusEffectsKeyGoesToTheStatusEffect()
        {
            var catalog = Game();
            catalog.Add(E("Rested", Kind.Other));
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "f.txt")));

            Assert.True(explorer.Jump("se:Rested"));

            Assert.Equal(Kind.StatusEffect, explorer.Selected?.Kind);
        }

        [Fact]
        public void EachEntryIsKeptUnderAKeyOfItsOwnKind()
        {
            // Favourites, recent and links use the key: kinds that are no prefabs have a namespace.
            Assert.Equal("Troll", E("Troll", Kind.Creature).Key);
            Assert.Equal("se:Rested", E("Rested", Kind.StatusEffect).Key);
            Assert.Equal("raid:foresttrolls", E("foresttrolls", Kind.Raid).Key);
        }

        [Fact]
        public void JumpingToSomethingNotInTheCatalogChangesNothing()
        {
            var explorer = Open();
            explorer.Text = "troll";
            explorer.Select(explorer.Results[0]);

            Assert.False(explorer.Jump("NoSuchThing"));

            Assert.Equal("troll", explorer.Text);
            Assert.Equal("Troll", explorer.Selected?.Name);
        }
    }
}
