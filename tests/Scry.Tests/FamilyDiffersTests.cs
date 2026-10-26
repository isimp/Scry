using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class FamilyDiffersTests
    {
        // A family's page sets its kinds side by side where they differ, each line once: what all
        // share stays the page's own. Only the members the character met have a column, one only
        // seen an empty one, its page closed; a value the rules keep back counts as nothing, so the
        // table never tells that something kept back differs.

        private static List<KeyValuePair<string, string>> Lines(params string[] labelValues)
        {
            var lines = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < labelValues.Length; i += 2) lines.Add(new KeyValuePair<string, string>(labelValues[i], labelValues[i + 1]));
            return lines;
        }

        [Fact]
        public void KindsStandSideBySideOnlyWhereTheyDiffer()
        {
            var one = Lines("Biome", "Plains", "Reaches", "20 m", "Tar to pick", "12");
            var two = Lines("Biome", "Plains", "Reaches", "25 m");
            var rows = FamilyDiffers.Rows(new[] { one, two }, CompendiumWords.Hidden);
            Assert.Equal(new[] { "Reaches", "Tar to pick" }, rows.Select(r => r.Label));
            Assert.Equal(new[] { "20 m", "25 m" }, rows[0].Values);
            // A line one kind has not, said so.
            Assert.Equal(new[] { "12", FamilyDiffers.Missing }, rows[1].Values);
            // All alike: nothing to set side by side.
            Assert.Empty(FamilyDiffers.Rows(new[] { one, one }, CompendiumWords.Hidden));
        }

        [Fact]
        public void AClosedPageOrAValueKeptBackNeverTellsADifference()
        {
            var one = Lines("Reaches", "20 m", "Health", CompendiumWords.Hidden);
            var two = Lines("Reaches", "20 m", "Health", "300");
            // Kept back on one, only one value shows: no row.
            Assert.Empty(FamilyDiffers.Rows(new[] { one, two }, CompendiumWords.Hidden));
            // A page only seen has an empty column and makes no row of its own.
            var three = Lines("Reaches", "25 m", "Health", "300");
            var rows = FamilyDiffers.Rows(new[] { one, null, three }, CompendiumWords.Hidden);
            var reach = Assert.Single(rows);
            Assert.Equal(new[] { "20 m", null, "25 m" }, reach.Values);
            Assert.Empty(FamilyDiffers.Rows(new[] { two, null }, CompendiumWords.Hidden));
        }

        private sealed class World
        {
            public readonly Entry Pit1 = E("TarPit1", Kind.Location, "Tar pit 1");
            public readonly Entry Pit2 = E("TarPit2", Kind.Location, "Tar pit 2");
            public readonly Entry Pit3 = E("TarPit3", Kind.Location, "Tar pit 3");
            public readonly Entry Tar = E("Tar", Kind.Item, "Tar");
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Explorer = new Explorer(new[] { Pit1, Pit2, Pit3, Tar }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void APageSwitchesAmongTheKindsMetAndStaysOnItsFamilysRow()
        {
            var world = new World();
            world.Book.Raise(world.Pit1.Key, KnownLevel.Known);
            world.Book.Raise(world.Pit3.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Pit1);
            Assert.Equal(new[] { world.Pit1, world.Pit3 }, world.Explorer.MembersMet(world.Pit1));
            world.Explorer.SwitchTo(world.Pit3);
            Assert.Same(world.Pit3, world.Explorer.Selected);
            Assert.Equal(0, world.Explorer.SelectedIndex);
            // Switching within a page is no step to go back from.
            Assert.False(world.Explorer.CanGoBack);
            // One alone met, or no family: no switch.
            Assert.Empty(world.Explorer.MembersMet(world.Tar));
            world.Rule(CompendiumRule.Off);
            Assert.Empty(world.Explorer.MembersMet(world.Pit1));
        }

        [Fact]
        public void ACopysSwitchNamesWhoCarriesItOnlyOnceTheCharacterMetThem()
        {
            Entry Item(string name, bool crafted)
            {
                var e = E(name, Kind.Item, "Blackmetal tower shield");
                e.Tokens = new[] { "$item_shield_blackmetal_tower" };
                if (crafted) e.Stations = new[] { new StationUse("forge", "Forge", 2) };
                return e;
            }
            var shield = Item("ShieldBlackmetalTower", true);
            var copy = Item("SP_ShieldBlackmetalTower", false);
            var bearer = E("ShadowPerson", Kind.Creature, "Shadow Person");
            copy.Links.Add(new Link { Group = LinkWords.CarriedBy, Target = bearer.Key, Back = true });
            var explorer = new Explorer(new[] { shield, copy, bearer }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(shield.Key, KnownLevel.Known);
            book.Raise(copy.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Loose);
            explorer.Know(view);
            Assert.Equal("SP", explorer.KindLabel(copy));
            Assert.Equal("Blackmetal tower shield", explorer.KindLabel(shield));
            book.Raise(bearer.Key, KnownLevel.Seen);
            view.Tell(book, CompendiumRule.Loose);
            explorer.Know(view);
            Assert.Equal("Shadow Person", explorer.KindLabel(copy));
        }

        [Fact]
        public void UnderStrictRulesEachKindKeepsBackWhatItsOwnPageWould()
        {
            var world = new World();
            world.Book.Raise(world.Pit1.Key, KnownLevel.Known);
            world.Book.Raise(world.Pit2.Key, KnownLevel.Known);
            world.Book.Raise(world.Tar.Key, KnownLevel.Known);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Pit1);
            var proof = EntryKeys.Did(Deed.Killed);
            Assert.True(world.Explorer.KeepsBackOn(world.Pit2, proof));
            world.Book.Prove(world.Pit2.Key, proof);
            world.Rule(CompendiumRule.Strict);
            Assert.False(world.Explorer.KeepsBackOn(world.Pit2, proof));
            // Tar was never seen together with the second pit: its name is left out there.
            Assert.True(world.Explorer.LeavesOutOn(world.Pit2, world.Tar.Key, false));
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.KeepsBackOn(world.Pit2, proof));
            Assert.False(world.Explorer.LeavesOutOn(world.Pit2, world.Tar.Key, false));
        }
    }
}
