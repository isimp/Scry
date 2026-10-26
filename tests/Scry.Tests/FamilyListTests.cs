using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class FamilyListTests
    {
        // With the compendium set, a family (Families) is one row of the list, shown by the member
        // the character knows best and named as the family goes; a member it met matching the
        // search finds that row, and what it never met is neither listed, found nor counted. With
        // the compendium off every prefab stands on its own, as the game is built.

        private sealed class World
        {
            public readonly Entry Shield = Item("ShieldBlackmetalTower", crafted: true);
            public readonly Entry Fallen = Item("FW_ShieldBlackmetalTower", crafted: false);
            public readonly Entry Shadow = Item("SP_ShieldBlackmetalTower", crafted: false);
            public readonly Entry Pit1 = E("TarPit1", Kind.Location, "Tar pit 1");
            public readonly Entry Pit2 = E("TarPit2", Kind.Location, "Tar pit 2");
            public readonly Entry Pit3 = E("TarPit3", Kind.Location, "Tar pit 3");
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                var all = new[] { Shield, Fallen, Shadow, Pit1, Pit2, Pit3 }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
            }

            private static Entry Item(string name, bool crafted)
            {
                var e = E(name, Kind.Item, "Blackmetal tower shield");
                e.Tokens = new[] { "$item_shield_blackmetal_tower" };
                if (crafted) e.Stations = new[] { new StationUse("forge", "Forge", 2) };
                return e;
            }

            public World Met(Entry entry, KnownLevel level)
            {
                Book.Raise(entry.Key, level);
                return this;
            }

            public World Met(System.Func<World, Entry> entry, KnownLevel level) => Met(entry(this), level);

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }

            public string[] Listed(string text = "")
            {
                Explorer.Text = text;
                return Explorer.Results.Select(e => e.Name).ToArray();
            }
        }

        [Theory]
        [InlineData("Loose")]
        [InlineData("Strict")]
        public void AFamilyIsOneRowShownByItsBestKnownMember(string ruleName)
        {
            var rule = (CompendiumRule)System.Enum.Parse(typeof(CompendiumRule), ruleName);
            var world = new World().Met(world_ => world_.Pit2, KnownLevel.Seen);
            world.Met(world.Pit3, KnownLevel.Known).Rule(rule);
            Assert.Equal(new[] { "TarPit3" }, world.Listed());
            Assert.Equal(1, world.Explorer.CountOf(Kind.Location));
            // Named as the family goes, telling only the kinds the character met.
            Assert.Equal("Tar pit", world.Explorer.ShownAs(world.Pit3));
            Assert.Equal(CompendiumWords.FamilyNote(2, 1), world.Explorer.FamilyNote(world.Pit3));
            world.Met(world.Pit1, KnownLevel.Known).Rule(rule);
            Assert.Equal(new[] { "TarPit1" }, world.Listed());
            Assert.Equal(CompendiumWords.FamilyNote(3, 2), world.Explorer.FamilyNote(world.Pit1));
        }

        [Fact]
        public void AMemberNeverMetIsNeitherListedNorFound()
        {
            var world = new World().Met(world_ => world_.Pit2, KnownLevel.Known).Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { "TarPit2" }, world.Listed());
            Assert.Empty(world.Listed("tarpit1"));
            Assert.Null(world.Explorer.FamilyNote(world.Pit2));
            // An item's copy it never met is not found by its prefab either.
            world.Met(world.Shield, KnownLevel.Known).Rule(CompendiumRule.Strict);
            Assert.Empty(world.Listed("SP_"));
        }

        [Fact]
        public void ACopyMetIsFoundByItsPrefabAndListedAsItsOriginal()
        {
            var world = new World().Met(w => w.Shield, KnownLevel.Known);
            world.Met(world.Fallen, KnownLevel.Known).Met(world.Shadow, KnownLevel.Seen).Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { "ShieldBlackmetalTower" }, world.Listed("shield"));
            Assert.Equal(new[] { "ShieldBlackmetalTower" }, world.Listed("FW_"));
            Assert.Equal(1, world.Explorer.CountOf(Kind.Item));
            // With only its copy met, the family shows by the copy.
            var alone = new World().Met(w => w.Fallen, KnownLevel.Seen).Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { "FW_ShieldBlackmetalTower" }, alone.Listed("shield"));
        }

        [Fact]
        public void AFamilyRanksByItsBestMatchingMember()
        {
            // The copy comes first, matching "shield" only inside its prefab; the original's prefab
            // begins with it. The family ranks as the original matches, before a short-named item
            // matching only by a word of its name.
            var fallen = E("FW_ShieldBlackmetalTower", Kind.Item, "Blackmetal tower");
            fallen.Tokens = new[] { "$item_shield_blackmetal_tower" };
            var shield = E("ShieldBlackmetalTower", Kind.Item, "Blackmetal tower");
            shield.Tokens = fallen.Tokens;
            shield.Stations = new[] { new StationUse("forge", "Forge", 2) };
            var buckler = E("Buckler", Kind.Item, "Bronze shield");
            var explorer = new Explorer(new[] { fallen, shield, buckler }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            foreach (var e in new[] { fallen, shield, buckler }) book.Raise(e.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Loose);
            explorer.Know(view);
            explorer.Text = "shield";
            Assert.Equal(new[] { "ShieldBlackmetalTower", "Buckler" }, explorer.Results.Select(e => e.Name).ToArray());
        }

        [Fact]
        public void TheListsOfWhatWasLearnedOrLookedAtTellEachMemberOnItsOwn()
        {
            var world = new World().Met(w => w.Pit1, KnownLevel.Known);
            world.Met(world.Pit2, KnownLevel.Known).Rule(CompendiumRule.Loose);
            world.Explorer.LearnedOnly = true;
            Assert.Equal(new[] { "TarPit1", "TarPit2" }, world.Explorer.Results.Select(e => e.Name).OrderBy(n => n).ToArray());
            Assert.Equal("Tar pit 2", world.Explorer.ShownAs(world.Pit2));
            Assert.Null(world.Explorer.FamilyNote(world.Pit2));
            world.Explorer.LearnedOnly = false;
            world.Explorer.Select(world.Pit1);
            world.Explorer.Select(world.Pit2);
            world.Explorer.RecentOnly = true;
            Assert.Equal(new[] { "TarPit1", "TarPit2" }, world.Explorer.Results.Select(e => e.Name).OrderBy(n => n).ToArray());
        }

        [Fact]
        public void WithTheCompendiumOffEveryPrefabStandsOnItsOwn()
        {
            var world = new World().Rule(CompendiumRule.Off);
            Assert.Equal(6, world.Listed().Length);
            Assert.Equal("Tar pit 2", world.Explorer.ShownAs(world.Pit2));
        }

        [Fact]
        public void AMembersPageKeepsItsFamilysRowLit()
        {
            var world = new World().Met(w => w.Shield, KnownLevel.Known);
            world.Met(world.Fallen, KnownLevel.Known).Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Fallen);
            Assert.Same(world.Fallen, world.Explorer.Selected);
            Assert.Equal(world.Explorer.Results.ToList().IndexOf(world.Shield), world.Explorer.SelectedIndex);
            // A search finding the family keeps the member's page.
            world.Listed("blackmetal");
            Assert.Same(world.Fallen, world.Explorer.Selected);
            Assert.True(world.Explorer.Jump(world.Fallen.Key));
            Assert.Equal("blackmetal", world.Explorer.Text);
        }
    }
}
