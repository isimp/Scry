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

        [Theory]
        [InlineData(Kind.Effect)]
        [InlineData(Kind.Sound)]
        public void AnEffectLeftWhenSomethingIsDestroyedSaysWhatLeavesItAndStaysAnEffectUnderItsOwnName(object leftKind)
        {
            var kind = (Kind)leftKind;
            var catalog = WithLeftovers();
            catalog.Add(E("stone_wall_destruction", kind, "Stone dust"));

            Leftovers.Pair(catalog, new[] { new Leftover("stone_wall_destruction", "stone_wall", "broken") });

            var effect = Find(catalog, "stone_wall_destruction");
            Assert.Equal(new[] { "stone_wall" }, effect.LeftBy);
            Assert.Contains("stone_wall_destruction", Find(catalog, "stone_wall").LeavesBehind);
            Assert.Equal(kind, effect.Kind);
            Assert.False(effect.KindFromOwners);
            Assert.Equal("Stone dust", effect.DisplayName);
        }

        [Fact]
        public void AnEffectLeftBehindStaysInItsOwnGroup()
        {
            var catalog = WithLeftovers();
            var wall = Find(catalog, "stone_wall");
            wall.Group = "Building";
            wall.GroupOrder = 3;
            var dust = E("stone_wall_destruction", Kind.Effect);
            dust.Group = "Deaths and destruction";
            dust.GroupOrder = 3;
            catalog.Add(dust);
            Leftovers.Pair(catalog, new[] { new Leftover("stone_wall_destruction", "stone_wall", "broken") });

            Leftovers.JoinOwnersGroups(catalog);

            Assert.Equal("Deaths and destruction", dust.Group);
        }

        [Fact]
        public void WhatIsLeftBehindIsListedInTheGroupOfWhatLeavesIt()
        {
            var catalog = WithLeftovers();
            var troll = Find(catalog, "Troll");
            troll.Group = "Forest monsters";
            troll.GroupOrder = 2;
            var ragdoll = Find(catalog, "Troll_ragdoll");
            ragdoll.Group = "Remains";
            ragdoll.GroupOrder = 2;
            Leftovers.Pair(catalog, new[] { new Leftover("Troll_ragdoll", "Troll", "ragdoll") });

            Leftovers.JoinOwnersGroups(catalog);

            Assert.Equal("Forest monsters", ragdoll.Group);
            Assert.Equal(2, ragdoll.GroupOrder);
        }

        [Fact]
        public void ALogLeftByItsTreeKeepsItsOwnGroupAsItWasAResourceAlready()
        {
            var catalog = WithLeftovers();
            var oak = Find(catalog, "Oak1");
            oak.Kind = Kind.Resource;
            oak.Group = "Trees";
            oak.GroupOrder = 1;
            var log = Find(catalog, "Oak_log");
            log.Kind = Kind.Resource;
            log.Group = "Logs";
            log.GroupOrder = 2;
            Leftovers.Pair(catalog, new[] { new Leftover("Oak_log", "Oak1", "log") });

            Leftovers.JoinOwnersGroups(catalog);

            Assert.Equal("Logs", log.Group);
        }

        [Fact]
        public void WhatStaysItsOwnKindKeepsItsOwnGroup()
        {
            var catalog = WithLeftovers();
            var debris = Find(catalog, "wood_debris");
            debris.Group = "Scenery";
            debris.GroupOrder = 5;
            Leftovers.Pair(catalog, new[] { new Leftover("wood_debris", "stone_wall", "debris"), new Leftover("wood_debris", "Troll", "debris") });

            Leftovers.JoinOwnersGroups(catalog);

            Assert.Equal("Scenery", debris.Group);
        }

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
