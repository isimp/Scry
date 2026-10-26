using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class GroupByTests
    {
        // A kind's tab can be grouped another way than its own (creatures by faction, items by
        // type): creatures by biome or by what taming allows, items by biome. One way at a time,
        // each thing under one heading; by biome under the first biome players reach, its row
        // telling how many more. With the compendium's strict rules a biome groups a thing only
        // where the character met it there.

        private sealed class World
        {
            public readonly Entry Lox = Creature("Lox", "Plains monsters", Deed.Tamed, Deed.Born, Deed.Ridden);
            public readonly Entry Boar = Creature("Boar", "Animals", Deed.Tamed, Deed.Born);
            public readonly Entry Wolf = Creature("Wolf", "Mountain monsters", Deed.Tamed);
            public readonly Entry Skeleton = Creature("Skeleton_Friendly", Groups.PlayersGroup);
            public readonly Entry Player = Creature("Player", Groups.PlayersGroup);
            public readonly Entry Troll = Creature("Troll", "Forest monsters");
            public readonly Entry Perch = E("Fish1", Kind.Item, "Perch");
            public readonly Entry Tuna = E("Fish3", Kind.Item, "Tuna");
            public readonly Entry Hammer = E("Hammer", Kind.Item, "Hammer");
            public readonly Entry Meadows = E("Meadows", Kind.Biome, "Meadows");
            public readonly Entry Forest = E("BlackForest", Kind.Biome, "Black Forest");
            public readonly Entry Plains = E("Plains", Kind.Biome, "Plains");
            public readonly Entry Ocean = E("Ocean", Kind.Biome, "Ocean");
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Perch.Biomes = new[] { "Plains", "BlackForest", "Meadows" };
                Tuna.Biomes = new[] { "Ocean" };
                Lox.Biomes = new[] { "Plains" };
                Troll.Biomes = new[] { "BlackForest" };
                // An item no world spawn places groups by the biomes of what gives it.
                Hammer.GiverBiomes = new string[0];
                Player.Components = new[] { "Player" };
                var all = new[] { Lox, Boar, Wolf, Skeleton, Player, Troll, Perch, Tuna, Hammer, Meadows, Forest, Plains, Ocean }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
            }

            private static Entry Creature(string name, string group, params Deed[] deeds)
            {
                var e = E(name, Kind.Creature, name);
                e.Group = group;
                e.Deeds = deeds;
                return e;
            }

            public World Rule(CompendiumRule rule)
            {
                foreach (var e in Explorer.Entries.All) Book.Raise(e.Key, KnownLevel.Known);
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }

            public string Group(Entry entry) => Explorer.ListGroupOf(entry).Name;
        }

        [Fact]
        public void EachKindOffersItsOwnWaysToGroup()
        {
            Assert.Equal(new[] { GroupLens.Own, GroupLens.Biome, GroupLens.Taming }, ListGrouping.Lenses(Kind.Creature));
            Assert.Equal(new[] { GroupLens.Own, GroupLens.Biome }, ListGrouping.Lenses(Kind.Item));
            Assert.Equal(new[] { GroupLens.Own }, ListGrouping.Lenses(Kind.Piece));
            Assert.Equal("Faction", ListGrouping.LensName(GroupLens.Own, Kind.Creature));
            Assert.Equal("Type", ListGrouping.LensName(GroupLens.Own, Kind.Item));
        }

        [Fact]
        public void CreaturesGroupByWhatTamingAllowsTheMostFirst()
        {
            var world = new World();
            world.Explorer.KindFilter = Kind.Creature;
            Assert.Equal("Plains monsters", world.Group(world.Lox));
            world.Explorer.Lens = GroupLens.Taming;
            Assert.Equal(ListGrouping.Ridden, world.Group(world.Lox));
            Assert.Equal(ListGrouping.TamedAndBred, world.Group(world.Boar));
            Assert.Equal(ListGrouping.Tamed, world.Group(world.Wolf));
            Assert.Equal(ListGrouping.Summoned, world.Group(world.Skeleton));
            Assert.Equal(ListGrouping.NeverTamed, world.Group(world.Player));
            Assert.Equal(ListGrouping.NeverTamed, world.Group(world.Troll));
            var order = world.Explorer.Results.Select(world.Group).Distinct().ToArray();
            Assert.Equal(new[] { ListGrouping.Ridden, ListGrouping.TamedAndBred, ListGrouping.Tamed, ListGrouping.Summoned, ListGrouping.NeverTamed }, order);
            // What can never be tamed starts folded away.
            Assert.True(ListGrouping.FoldedAtFirst(GroupLens.Taming, ListGrouping.NeverTamed));
            Assert.False(ListGrouping.FoldedAtFirst(GroupLens.Taming, ListGrouping.Tamed));
        }

        [Fact]
        public void ByBiomeAThingStandsUnderTheFirstBiomePlayersReach()
        {
            var world = new World();
            world.Explorer.KindFilter = Kind.Item;
            world.Explorer.Lens = GroupLens.Biome;
            Assert.Equal("Meadows", world.Group(world.Perch));
            Assert.Equal(2, world.Explorer.ListGroupOf(world.Perch).More);
            Assert.Equal("Ocean", world.Group(world.Tuna));
            Assert.Equal(ListGrouping.NoBiome, world.Group(world.Hammer));
            var order = world.Explorer.Results.Select(world.Group).Distinct().ToArray();
            Assert.Equal(new[] { "Meadows", "Ocean", ListGrouping.NoBiome }, order);
            // A way the tab does not offer is not taken.
            world.Explorer.Lens = GroupLens.Taming;
            Assert.Equal(GroupLens.Biome, world.Explorer.Lens);
            // Each kind keeps its own way: creatures still by faction.
            world.Explorer.KindFilter = Kind.Creature;
            Assert.Equal(GroupLens.Own, world.Explorer.Lens);
            world.Explorer.KindFilter = Kind.Item;
            Assert.Equal(GroupLens.Biome, world.Explorer.Lens);
        }

        [Fact]
        public void AnItemNoSpawnPlacesGroupsByTheBiomesOfWhatGivesIt()
        {
            var world = new World();
            world.Hammer.GiverBiomes = new[] { "BlackForest" };
            world.Explorer.KindFilter = Kind.Item;
            world.Explorer.Lens = GroupLens.Biome;
            Assert.Equal("Black Forest", world.Group(world.Hammer));
        }

        [Fact]
        public void UnderStrictRulesABiomeGroupsOnlyWhereTheCharacterMetIt()
        {
            var world = new World();
            world.Book.SawWith(world.Perch.Key, world.Forest.Key, Tie.Where);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.KindFilter = Kind.Item;
            world.Explorer.Lens = GroupLens.Biome;
            Assert.Equal("Black Forest", world.Group(world.Perch));
            Assert.Equal(0, world.Explorer.ListGroupOf(world.Perch).More);
            Assert.Equal(ListGrouping.NotMetInABiome, world.Group(world.Tuna));
            world.Rule(CompendiumRule.Loose);
            Assert.Equal("Meadows", world.Group(world.Perch));
        }
    }
}
