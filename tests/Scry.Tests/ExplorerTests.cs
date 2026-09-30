using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class ExplorerTests
    {
        private static Explorer Open()
        {
            return new Explorer(Game(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
        }

        private static Entry Named(Explorer explorer, string name) => explorer.Catalog.First(e => e.Name == name);

        [Fact]
        public void TheResourcesTabListsTreesThenLogsThenRocksThenPlantsThenBushesThenTheRest()
        {
            Entry R(string name, ResourceGroup group) => new Entry { Name = name, Kind = Kind.Resource, Origin = Origin.Vanilla, Group = Kinds.GroupLabel(group), GroupOrder = (int)group };
            var catalog = new System.Collections.Generic.List<Entry>
            {
                R("barrel", ResourceGroup.Other), R("RaspberryBush", ResourceGroup.BushesAndPickables), R("rock4_copper", ResourceGroup.RocksAndOre),
                R("Oak_log", ResourceGroup.Logs), R("Beech1", ResourceGroup.Trees), R("sapling_turnip", ResourceGroup.Plants),
                R("BlueberryBush", ResourceGroup.BushesAndPickables), R("Birch1", ResourceGroup.Trees), E("wood_wall", Kind.Piece),
            };
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "favourites.txt")));

            explorer.KindFilter = Kind.Resource;

            Assert.Equal(new[] { "Beech1", "Birch1", "Oak_log", "rock4_copper", "sapling_turnip", "BlueberryBush", "RaspberryBush", "barrel" },
                explorer.Results.Select(e => e.Name).ToArray());
        }

        [Fact]
        public void TwoGroupsThatComeInTheSamePlaceStillListTheirEntriesTogether()
        {
            Entry R(string name, string group, int order) => new Entry { Name = name, Kind = Kind.Resource, Origin = Origin.Vanilla, Group = group, GroupOrder = order };
            var catalog = new System.Collections.Generic.List<Entry> { R("a1", "Bushes", 5), R("b1", "Scenery", 5), R("c1", "Bushes", 5), R("d1", "Scenery", 5) };
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "favourites.txt")));

            explorer.KindFilter = Kind.Resource;

            Assert.Equal(new[] { "Bushes", "Bushes", "Scenery", "Scenery" }, explorer.Results.Select(e => e.Group).ToArray());
        }

        [Fact]
        public void WithinAGroupWhatRanksFirstComesFirstThenByName()
        {
            // A dungeon's group: the location, then entrances, then rooms, then end caps, each by name.
            Entry L(string name, int rank) => new Entry { Name = name, Kind = Kind.Location, Origin = Origin.Vanilla, Group = "Black Forest · Burial Chambers", GroupOrder = 11, GroupRank = rank };
            var catalog = new System.Collections.Generic.List<Entry> { L("zz_endcap", 3), L("aa_room", 2), L("Crypt3", 0), L("mm_entrance", 1), L("Crypt2", 0), L("bb_room", 2) };
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "favourites.txt")));

            explorer.KindFilter = Kind.Location;

            Assert.Equal(new[] { "Crypt2", "Crypt3", "mm_entrance", "aa_room", "bb_room", "zz_endcap" }, explorer.Results.Select(e => e.Name).ToArray());
        }

        [Fact]
        public void ARankOrdersOnlyWithinItsGroupNeverAcrossGroups()
        {
            Entry L(string name, string group, int rank) => new Entry { Name = name, Kind = Kind.Location, Origin = Origin.Vanilla, Group = group, GroupOrder = 11, GroupRank = rank };
            var catalog = new System.Collections.Generic.List<Entry>
            {
                L("GoblinCamp2", "Black Forest · Fuling camp", 0), L("forestcrypt_EndCap", "Black Forest · Burial Chambers", 3), L("Crypt2", "Black Forest · Burial Chambers", 0),
            };
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "favourites.txt")));

            explorer.KindFilter = Kind.Location;

            Assert.Equal(new[] { "Crypt2", "forestcrypt_EndCap", "GoblinCamp2" }, explorer.Results.Select(e => e.Name).ToArray());
        }

        [Fact]
        public void OpeningShowsEverythingWithNothingSelected()
        {
            var explorer = Open();

            Assert.Equal(Game().Count, explorer.Results.Count);
            Assert.Null(explorer.Selected);
        }

        [Fact]
        public void SearchingFromALinkClearsEveryOtherFilterAndFindsWhatTheTextAloneFinds()
        {
            // A biome or mod chip searches with every other filter off, in one go rather than a
            // refresh of the whole catalog for each filter it clears.
            var explorer = Open();
            explorer.KindFilter = Kind.Piece;
            explorer.FavouritesOnly = true;
            explorer.RecentOnly = true;
            explorer.Origin = OriginFilter.Vanilla;

            explorer.SearchEverything("troll");

            var alone = Open();
            alone.Text = "troll";
            Assert.Null(explorer.KindFilter);
            Assert.False(explorer.FavouritesOnly);
            Assert.False(explorer.RecentOnly);
            Assert.Equal(OriginFilter.All, explorer.Origin);
            Assert.Equal("troll", explorer.Text);
            Assert.Equal(alone.Results.Select(e => e.Name), explorer.Results.Select(e => e.Name));
            Assert.Equal(alone.CountOf(Kind.Creature), explorer.CountOf(Kind.Creature));
        }

        [Fact]
        public void KindChipsCountWhatMatchesTheSearch()
        {
            var explorer = Open();
            explorer.Text = "troll";

            Assert.Equal(3, explorer.CountOf(Kind.Creature));
            Assert.Equal(1, explorer.CountOf(Kind.Item));
            Assert.Equal(0, explorer.CountOf(Kind.StatusEffect));
            Assert.Equal(explorer.Results.Count, explorer.CountAll);
        }

        [Fact]
        public void PickingAKindKeepsTheOtherChipsCounting()
        {
            var explorer = Open();
            explorer.Text = "troll";
            explorer.KindFilter = Kind.Creature;

            Assert.Equal(3, explorer.Results.Count);
            Assert.Equal(1, explorer.CountOf(Kind.Item));
            Assert.True(explorer.CountAll > explorer.Results.Count);
        }

        [Fact]
        public void ASearchTheOpenTabHasNothingForListsEveryMatchAndKeepsTheTabPicked()
        {
            // Typing past what the open tab holds is no dead end: the matches of every kind show,
            // listed as the All tab lists them, while the tab stays the player's pick.
            var explorer = Open();
            explorer.KindFilter = Kind.Projectile;
            explorer.Text = "troll";

            var alone = Open();
            alone.Text = "troll";
            Assert.Equal(Kind.Projectile, explorer.KindFilter);
            Assert.True(explorer.ShowingEveryKind);
            Assert.Equal(alone.Results.Select(e => e.Name), explorer.Results.Select(e => e.Name));
            Assert.Equal(0, explorer.CountOf(Kind.Projectile));
        }

        [Fact]
        public void EveryMatchShownForAnEmptyTabIsInTheAllTabsOrderNotByGroup()
        {
            Entry C(string name, string group, int order) => new Entry { Name = name, Kind = Kind.Creature, Origin = Origin.Vanilla, Group = group, GroupOrder = order };
            var catalog = new System.Collections.Generic.List<Entry> { C("Troll", "Zeta", 5), C("TrollKing", "Alpha", 1), E("arrow_wood_projectile", Kind.Projectile) };
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            explorer.KindFilter = Kind.Projectile;

            explorer.Text = "troll";

            Assert.Equal(new[] { "Troll", "TrollKing" }, explorer.Results.Select(e => e.Name).ToArray());
        }

        [Fact]
        public void OnceTheOpenTabHasMatchesAgainTheListGoesBackToThem()
        {
            var explorer = Open();
            explorer.KindFilter = Kind.Projectile;
            explorer.Text = "troll";

            explorer.Text = "wood";

            Assert.False(explorer.ShowingEveryKind);
            Assert.Equal(new[] { "arrow_wood_projectile" }, explorer.Results.Select(e => e.Name).ToArray());
        }

        [Fact]
        public void ASearchNothingMatchesStillShowsNothing()
        {
            var explorer = Open();
            explorer.KindFilter = Kind.Item;
            explorer.Text = "dragonfruit";

            Assert.False(explorer.ShowingEveryKind);
            Assert.Empty(explorer.Results);
        }

        [Fact]
        public void AnEntryListedFromEveryKindCanBeSelectedAndKeptWhileTyping()
        {
            var explorer = Open();
            explorer.KindFilter = Kind.Projectile;
            explorer.Text = "troll";
            var troll = Named(explorer, "MountainTroll");

            explorer.Select(troll);
            explorer.Text = "mountain";

            Assert.Same(troll, explorer.Selected);
            Assert.Equal(explorer.Results.ToList().IndexOf(troll), explorer.SelectedIndex);
        }

        [Fact]
        public void TheSelectionStaysWhileNarrowingTheSearchIfItStillMatches()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "MountainTroll"));

            explorer.Text = "mountain";

            Assert.Equal("MountainTroll", explorer.Selected?.Name);
            Assert.Equal(0, explorer.SelectedIndex);
        }

        [Fact]
        public void ASelectionTheSearchNoLongerListsIsLetGo()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "Bow"));

            explorer.Text = "troll";

            Assert.Null(explorer.Selected);
            Assert.Equal(-1, explorer.SelectedIndex);
        }

        [Fact]
        public void ArrowingDownWithNothingSelectedStartsAtTheTop()
        {
            var explorer = Open();
            explorer.Text = "troll";

            explorer.Move(1);

            Assert.Equal("Troll", explorer.Selected?.Name);
        }

        [Fact]
        public void ArrowingStopsAtEitherEndOfTheList()
        {
            var explorer = Open();
            explorer.Text = "troll";

            explorer.Move(100);
            Assert.Same(explorer.Results.Last(), explorer.Selected);

            explorer.Move(-100);
            Assert.Same(explorer.Results.First(), explorer.Selected);
        }

        [Fact]
        public void ArrowingInAnEmptyListSelectsNothing()
        {
            var explorer = Open();
            explorer.Text = "dragonfruit";

            explorer.Move(1);

            Assert.Null(explorer.Selected);
        }

        [Fact]
        public void ChoosingAnotherPrefabStartsItAsItIs()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "Troll"));
            explorer.Modifiers.Scale = 4f;

            explorer.Select(Named(explorer, "MountainTroll"));

            Assert.Equal(1f, explorer.Modifiers.Scale);
        }

        [Fact]
        public void EachNewSelectionIsNoticeable()
        {
            var explorer = Open();
            var before = explorer.SelectionVersion;

            explorer.Select(Named(explorer, "Troll"));
            var afterFirst = explorer.SelectionVersion;
            explorer.Select(Named(explorer, "Troll"));

            Assert.NotEqual(before, afterFirst);
            Assert.Equal(afterFirst, explorer.SelectionVersion);
        }

        [Fact]
        public void StarringAPrefabPutsItInTheFavourites()
        {
            var explorer = Open();
            explorer.ToggleFavourite(Named(explorer, "Bow"));

            explorer.FavouritesOnly = true;

            Assert.Equal(new[] { "Bow" }, explorer.Results.Select(e => e.Name));
        }

        [Fact]
        public void UnstarringTheLastFavouriteEmptiesTheFavouritesList()
        {
            var explorer = Open();
            explorer.FavouritesOnly = true;
            explorer.ToggleFavourite(Named(explorer, "Bow"));
            Assert.Single(explorer.Results);

            explorer.ToggleFavourite(Named(explorer, "Bow"));

            Assert.Empty(explorer.Results);
        }
    }
}
