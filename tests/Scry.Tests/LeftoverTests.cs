using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class LeftoverTests
    {
        private static List<Entry> WithLeftovers()
        {
            var catalog = Game();
            catalog.Add(E("Troll_ragdoll", Kind.Other));
            catalog.Add(E("wood_debris", Kind.Other));
            catalog.Add(E("stone_wall", Kind.Piece, "Stone wall"));
            catalog.Add(E("Oak1", Kind.Piece, "Oak"));
            catalog.Add(E("Oak_log", Kind.Other));
            return catalog;
        }

        private static Entry Find(List<Entry> catalog, string name) => catalog.Single(e => e.Name == name);

        [Fact]
        public void ARagdollIsNamedAfterItsCreatureAndListedWithIt()
        {
            var catalog = WithLeftovers();

            Leftovers.Pair(catalog, new[] { new Leftover("Troll_ragdoll", "Troll", "ragdoll") });

            var ragdoll = Find(catalog, "Troll_ragdoll");
            Assert.Equal("Troll · ragdoll", ragdoll.DisplayName);
            Assert.Equal(Kind.Creature, ragdoll.Kind);
            Assert.Equal(new[] { "Troll" }, ragdoll.LeftBy);
        }

        [Fact]
        public void TheOwnerListsWhatItLeavesBehind()
        {
            var catalog = WithLeftovers();

            Leftovers.Pair(catalog, new[] { new Leftover("Oak_log", "Oak1", "log") });

            Assert.Equal(new[] { "Oak_log" }, Find(catalog, "Oak1").LeavesBehind);
        }

        [Fact]
        public void SearchingForTheOwnerFindsWhatItLeavesBehind()
        {
            var catalog = WithLeftovers();
            Leftovers.Pair(catalog, new[] { new Leftover("Troll_ragdoll", "Troll", "ragdoll") });
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "f.txt")));

            explorer.Text = "troll";
            explorer.KindFilter = Kind.Creature;

            Assert.Contains(explorer.Results, e => e.Name == "Troll_ragdoll");
        }

        [Fact]
        public void OwnersAlikeInNameShareTheLeftoverUnderThatName()
        {
            var catalog = WithLeftovers();

            Leftovers.Pair(catalog, new[]
            {
                new Leftover("Troll_ragdoll", "Troll", "ragdoll"),
                new Leftover("Troll_ragdoll", "Troll_Summoned", "ragdoll"),
            });

            var ragdoll = Find(catalog, "Troll_ragdoll");
            Assert.Equal("Troll · ragdoll", ragdoll.DisplayName);
            Assert.Equal(new[] { "Troll", "Troll_Summoned" }, ragdoll.LeftBy);
        }

        [Fact]
        public void ALeftoverOfManyDifferentOwnersIsNamedForWhatItIsAndLinksThemAll()
        {
            var catalog = WithLeftovers();

            Leftovers.Pair(catalog, new[]
            {
                new Leftover("wood_debris", "wood_wall", "debris"),
                new Leftover("wood_debris", "stone_wall", "debris"),
            });

            var debris = Find(catalog, "wood_debris");
            Assert.Equal("Debris of 2", debris.DisplayName);
            Assert.Equal(Kind.Piece, debris.Kind);
            Assert.Equal(new[] { "stone_wall", "wood_wall" }, debris.LeftBy);
        }

        [Fact]
        public void ALeftoverOfOwnersOfDifferentKindsStaysWhereItWas()
        {
            var catalog = WithLeftovers();

            Leftovers.Pair(catalog, new[]
            {
                new Leftover("wood_debris", "wood_wall", "debris"),
                new Leftover("wood_debris", "Troll", "debris"),
            });

            Assert.Equal(Kind.Other, Find(catalog, "wood_debris").Kind);
        }

        [Fact]
        public void ALogThatIsAResourceItselfIsStillNamedAfterItsTree()
        {
            // A log is chopped for wood as a tree is, and is a resource too.
            var catalog = WithLeftovers();
            Find(catalog, "Oak1").Kind = Kind.Resource;
            Find(catalog, "Oak_log").Kind = Kind.Resource;

            Leftovers.Pair(catalog, new[] { new Leftover("Oak_log", "Oak1", "log") });

            var log = Find(catalog, "Oak_log");
            Assert.Equal("Oak · log", log.DisplayName);
            Assert.Equal(Kind.Resource, log.Kind);
            Assert.Equal(new[] { "Oak1" }, log.LeftBy);
        }

        [Fact]
        public void OnlyWhatIsListedAsOtherIsPaired()
        {
            var catalog = WithLeftovers();

            Leftovers.Pair(catalog, new[] { new Leftover("MountainTroll", "Troll", "split") });

            var mountain = Find(catalog, "MountainTroll");
            Assert.Equal("Mountain troll", mountain.DisplayName);
            Assert.Empty(mountain.LeftBy);
            Assert.Empty(Find(catalog, "Troll").LeavesBehind);
        }

        [Fact]
        public void ALeftoverWhoseOwnerIsNotInTheCatalogStaysAsItIs()
        {
            var catalog = WithLeftovers();

            Leftovers.Pair(catalog, new[] { new Leftover("Troll_ragdoll", "NoSuchTroll", "ragdoll") });

            var ragdoll = Find(catalog, "Troll_ragdoll");
            Assert.Equal(Kind.Other, ragdoll.Kind);
            Assert.Equal("", ragdoll.DisplayName);
        }

        [Fact]
        public void AnOwnerWithoutAShownNameLendsItsPrefabName()
        {
            var catalog = WithLeftovers();
            catalog.Add(E("Rock_big", Kind.Piece));
            catalog.Add(E("Rock_big_frac", Kind.Other));

            Leftovers.Pair(catalog, new[] { new Leftover("Rock_big_frac", "Rock_big", "broken") });

            Assert.Equal("Rock_big · broken", Find(catalog, "Rock_big_frac").DisplayName);
        }

        [Fact]
        public void TheSameLeftoverNotedTwiceForAnOwnerIsListedOnce()
        {
            var catalog = WithLeftovers();

            Leftovers.Pair(catalog, new[]
            {
                new Leftover("Oak_log", "Oak1", "log"),
                new Leftover("Oak_log", "Oak1", "log"),
            });

            Assert.Single(Find(catalog, "Oak1").LeavesBehind);
            Assert.Single(Find(catalog, "Oak_log").LeftBy);
        }
    }
}
