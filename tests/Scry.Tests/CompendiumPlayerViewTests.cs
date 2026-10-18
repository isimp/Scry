using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumPlayerViewTests
    {
        // With the compendium on, the list is of the world a player meets: its creatures, items,
        // pieces, places and the rest, not the effects, sounds, projectiles and mods the game is
        // built of, which come back with it off or while peeking. A name several of one kind share
        // says which of them each is; the commands that spawn or give a thing are for the
        // compendium off; a table whose every line waits to be found out is one faint line.

        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature, "Troll");
            public readonly Entry Hit = E("vfx_troll_hit", Kind.Effect);
            public readonly Entry Grunt = E("sfx_troll_grunt", Kind.Sound);
            public readonly Entry Rock = E("troll_throw_projectile", Kind.Projectile);
            public readonly Entry Ragdoll = E("Troll_ragdoll", Kind.Other);
            public readonly Entry Mod = E("Monstrum", Kind.Mod);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                var all = new[] { Troll, Hit, Grunt, Rock, Ragdoll, Mod }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                foreach (var entry in all) Book.Raise(entry.Key, KnownLevel.Known);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Theory]
        [InlineData("Creature", true)]
        [InlineData("Item", true)]
        [InlineData("Piece", true)]
        [InlineData("Resource", true)]
        [InlineData("Spawner", true)]
        [InlineData("StatusEffect", true)]
        [InlineData("Biome", true)]
        [InlineData("Location", true)]
        [InlineData("Raid", true)]
        [InlineData("Effect", false)]
        [InlineData("Sound", false)]
        [InlineData("Projectile", false)]
        [InlineData("Other", false)]
        [InlineData("Mod", false)]
        public void AKindIsOfTheWorldAPlayerMeetsOrOfHowTheGameIsBuilt(string kind, bool forPlayers)
        {
            Assert.Equal(forPlayers, Kinds.ForPlayers((Kind)System.Enum.Parse(typeof(Kind), kind)));
        }

        [Fact]
        public void WithTheCompendiumOnTheListHoldsOnlyTheWorldAPlayerMeets()
        {
            var world = new World().Rule(CompendiumRule.Loose);

            Assert.Equal(new[] { world.Troll }, world.Explorer.Results.ToArray());
            Assert.Equal(1, world.Explorer.CountAll);
            Assert.Equal(0, world.Explorer.CountOf(Kind.Effect));
            Assert.Equal(0, world.Explorer.CountOf(Kind.Mod));
            // Picking such a kind lists none of it.
            world.Explorer.KindFilter = Kind.Sound;
            Assert.DoesNotContain(world.Grunt, world.Explorer.Results);
        }

        [Fact]
        public void OffOrPeekingTheListHoldsEveryKindAgain()
        {
            var world = new World().Rule(CompendiumRule.Off);
            Assert.Equal(6, world.Explorer.CountAll);

            world.Rule(CompendiumRule.Strict);
            Assert.Equal(1, world.Explorer.CountAll);
            world.Explorer.Peeking = true;
            Assert.Equal(6, world.Explorer.CountAll);
            Assert.Contains(world.Grunt, world.Explorer.Results);
            Assert.Equal(6, world.Explorer.LearnedCount);
        }

        [Fact]
        public void WhatWasLearnedLatelyCountsOnlyWhatTheListShows()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            Assert.Equal(1, world.Explorer.LearnedCount);

            var news = new LearnedNotice().New(world.Explorer);
            Assert.Equal(new[] { world.Troll }, news.ToArray());
        }

        [Fact]
        public void ANameSeveralOfOneKindShareSaysWhichOfThemEachIs()
        {
            var cabins = new[] { "AbandonedLogCabin04", "AbandonedLogCabin02", "AbandonedLogCabin03" }.Select(n => E(n, Kind.Location, "Abandoned log cabin")).ToList();
            var item = E("Barley", Kind.Item, "Barley");
            var crop = E("Pickable_Barley", Kind.Resource, "Barley");
            var all = cabins.Concat(new[] { item, crop }).ToList();

            Search.OrderNames(all);

            // In the order of their prefabs.
            Assert.Equal("1 of 3", cabins[1].Alike);
            Assert.Equal("2 of 3", cabins[2].Alike);
            Assert.Equal("3 of 3", cabins[0].Alike);
            // One of each kind going by a name tells nothing more: its kind's colour does.
            Assert.Null(item.Alike);
            Assert.Null(crop.Alike);

            // Names alike but for their case read as one name.
            var big = E("Rock_7", Kind.Resource, "Rock");
            var small = E("rock1", Kind.Resource, "rock");
            Search.OrderNames(new[] { big, small });
            Assert.Equal(new[] { "1 of 2", "2 of 2" }, new[] { big.Alike, small.Alike }.OrderBy(a => a).ToArray());

            // Renamed, it is told again as its names now are.
            cabins[0].DisplayName = "Collapsed log cabin";
            Search.OrderNames(all);
            Assert.Null(cabins[0].Alike);
            Assert.Equal("2 of 2", cabins[2].Alike);
        }

        [Fact]
        public void TheCommandsThatSpawnOrGiveAThingAreForTheCompendiumOff()
        {
            var world = new World().Rule(CompendiumRule.Off);
            Assert.True(world.Explorer.ShowsCommands);

            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.ShowsCommands);
            world.Explorer.Peeking = true;
            Assert.True(world.Explorer.ShowsCommands);
        }

        [Fact]
        public void ATableWhoseEveryLineWaitsToBeFoundOutWaitsAsOne()
        {
            // A creature's stars table: its health by stars once one was killed, the rest once a starred one fell.
            var world = new World();
            var proofs = new Dictionary<int, string> { { 0, EntryKeys.Did(Deed.Killed) }, { 1, EntryKeys.Did(Deed.StarredFell) }, { 2, EntryKeys.Did(Deed.StarredFell) } };
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            // Nothing found out: the whole table waits for what shows its first line.
            Assert.Equal(EntryKeys.Did(Deed.Killed), world.Explorer.KeepsBackWhole(proofs, 3));

            // Killed, its first line shows, so the table is drawn with the rest kept back.
            world.Book.FoundOut(world.Troll.Key, EntryKeys.Did(Deed.Killed));
            world.Rule(CompendiumRule.Strict);
            Assert.Null(world.Explorer.KeepsBackWhole(proofs, 3));

            // A line with nothing to wait for shows as it is.
            var fresh = new World().Rule(CompendiumRule.Strict);
            fresh.Explorer.Select(fresh.Troll);
            Assert.Null(fresh.Explorer.KeepsBackWhole(new Dictionary<int, string> { { 0, EntryKeys.Did(Deed.Killed) } }, 2));
            Assert.Null(fresh.Explorer.KeepsBackWhole(new Dictionary<int, string>(), 0));
            fresh.Rule(CompendiumRule.Loose);
            Assert.Null(fresh.Explorer.KeepsBackWhole(proofs, 3));
        }
    }
}
