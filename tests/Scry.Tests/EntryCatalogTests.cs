using Xunit;

namespace Scry.Tests
{
    public class EntryCatalogTests
    {
        // A world's catalog: its entries in the order they were read, each found by its key at
        // once; what reads the world and what shows it find entries this way, never by going
        // through all of them.

        private static Entry E(string name, Kind kind = Kind.Item) => new Entry { Name = name, Kind = kind };

        [Fact]
        public void AnEntryIsFoundByItsKey()
        {
            var wolf = E("Wolf", Kind.Creature);
            var catalog = new EntryCatalog(new[] { E("Wood"), wolf });
            Assert.Same(wolf, catalog.Find(wolf.Key));
        }

        [Fact]
        public void WhereSeveralShareAKeyTheFirstReadIsFound()
        {
            var first = E("Wood");
            var catalog = new EntryCatalog(new[] { first, E("Wood") });
            Assert.Same(first, catalog.Find(first.Key));
        }

        [Fact]
        public void NoKeyOrOneNotInTheCatalogFindsNothing()
        {
            var catalog = new EntryCatalog(new[] { E("Wood") });
            Assert.Null(catalog.Find(null));
            Assert.Null(catalog.Find("Stone"));
        }

        [Fact]
        public void EveryEntryIsKeptInTheOrderItWasRead()
        {
            var entries = new[] { E("c"), E("a"), E("b") };
            var catalog = new EntryCatalog(entries);
            Assert.Equal(entries, catalog.All);
        }

        [Fact]
        public void WhatShowsTheEntriesHearsWhenTheirGroupsChange()
        {
            var catalog = new EntryCatalog(new[] { E("Wood") });
            var heard = 0;
            catalog.Regrouped += () => heard++;
            catalog.Regroup();
            Assert.Equal(1, heard);
        }

        [Fact]
        public void TheExplorerFindsInTheCatalogItWasMadeWith()
        {
            var wood = E("Wood");
            var explorer = new Explorer(new[] { wood }, new Favourites(null));
            Assert.Same(wood, explorer.Find(wood.Key));
            Assert.Same(explorer.Entries.All, explorer.Catalog);
        }

        [Fact]
        public void TheExplorerListsAgainWhenTheGroupsChange()
        {
            // What the locations were found to hold moves a resource to another group.
            Entry R(string name, int order) => new Entry { Name = name, Kind = Kind.Resource, Origin = Origin.Vanilla, Group = "Group " + order, GroupOrder = order };
            var a = R("a", 1);
            var b = R("b", 2);
            var explorer = new Explorer(new[] { a, b }, new Favourites(null)) { KindFilter = Kind.Resource };
            Assert.Equal(new[] { a, b }, explorer.Results);

            a.GroupOrder = 3;
            a.Group = "Group 3";
            explorer.Entries.Regroup();
            Assert.Equal(new[] { b, a }, explorer.Results);
        }
    }
}
