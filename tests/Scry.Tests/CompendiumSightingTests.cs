using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    /// <summary>
    /// What the character saw of a creature up close, its stars and what it carried, is kept with
    /// the book, and under the strict rules the stage offers those stars and looks alone.
    /// </summary>
    public class CompendiumSightingTests
    {
        [Fact]
        public void ASightingKeepsTheStarsAndGearSeenApartFromLevelsAndPairs()
        {
            var book = new CompendiumBook();
            var version = book.PairsVersion;
            Assert.True(book.Sighted("Draugr", 1, new[] { "draugr_axe", "ShieldWood" }));
            Assert.False(book.Sighted("Draugr", 1, new[] { "draugr_axe" }));
            Assert.True(book.Sighted("Draugr", 0, null));
            Assert.Equal(new[] { 0, 1 }, book.StarsSeen("Draugr").OrderBy(s => s));
            Assert.Equal(new[] { "ShieldWood", "draugr_axe" }, book.WornSeen("Draugr").OrderBy(i => i, System.StringComparer.Ordinal));
            Assert.Empty(book.StarsSeen("Troll"));
            Assert.Empty(book.WornSeen(null));
            Assert.True(book.PairsVersion > version);
            Assert.True(book.Changed);
            Assert.False(book.Sighted("", 1, null));
            Assert.False(book.Sighted("Draugr", -1, new[] { "" }));
            Assert.Equal(KnownLevel.Unknown, book.Level("Draugr"));
        }

        [Fact]
        public void SightingsAreKeptWithTheBook()
        {
            var book = new CompendiumBook();
            book.Sighted("Draugr", 2, new[] { "draugr_bow" });
            var text = book.Save();
            Assert.Contains("sight\tDraugr\tstars:2\n", text);
            Assert.Contains("sight\tDraugr\twears:draugr_bow\n", text);
            var read = CompendiumBook.Load(text);
            Assert.Equal(new[] { 2 }, read.StarsSeen("Draugr"));
            Assert.Equal(new[] { "draugr_bow" }, read.WornSeen("Draugr"));
            Assert.Empty(CompendiumBook.Load("sight\tDraugr\tstars:many\nsight\tDraugr\n").StarsSeen("Draugr"));
        }

        [Fact]
        public void TheWorldsRulesSayHowNearACreatureIsSeenUpClose()
        {
            // The game names a creature and shows its stars within 10 m; a server may set it.
            Assert.Equal(10f, WorldRules.SeenReachSetting.Default);
            var rules = WorldRules.From(WorldRule.PlayersChoose, true, true, _ => SourceLevel.WithInsides, seenReach: 25f);
            Assert.Equal(25f, rules.SeenReach);
            Assert.Equal(25f, rules.Copy().SeenReach);
            Assert.False(rules.SameAs(WorldRules.Open));
            Assert.True(rules.SameAs(rules.Copy()));
            Assert.Equal(0f, WorldRules.From(WorldRule.PlayersChoose, true, true, _ => SourceLevel.WithInsides, seenReach: -5f).SeenReach);
        }

        private sealed class World
        {
            public readonly Entry Draugr = E("Draugr", Kind.Creature);
            public readonly Entry Axe = E("draugr_axe", Kind.Item);
            public readonly Entry Bow = E("draugr_bow", Kind.Item);
            public readonly Entry Forest = E("biome:BlackForest", Kind.Biome);
            public readonly Entry Crypt = E("loc:Crypt2", Kind.Location);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Draugr.ExtraLevels = 2;
                var all = new[] { Draugr, Axe, Bow, Forest, Crypt }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                Book.Raise(Draugr.Key, KnownLevel.Known);
                Book.Raise(Axe.Key, KnownLevel.Seen);
                Book.Raise(Bow.Key, KnownLevel.Seen);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void UnderStrictTheStageOffersTheStarsSeenUpCloseAndNoOthers()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Draugr);
            // Without stars is always offered: it is the creature itself.
            Assert.Equal(new[] { 1 }, world.Explorer.StarsOffered(world.Draugr));
            Assert.Equal(1, world.Explorer.StarLevelFor(world.Draugr.Key, 3));

            world.Book.Sighted(world.Draugr.Key, 2, null);
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 1, 3 }, world.Explorer.StarsOffered(world.Draugr));
            Assert.Equal(3, world.Explorer.StarLevelFor(world.Draugr.Key, 3));
            Assert.Equal(1, world.Explorer.StarLevelFor(world.Draugr.Key, 2));
            Assert.Equal(1, world.Explorer.StarLevelFor("Troll", 3));

            // Under the loose rules, off and while peeking every star is offered.
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { 1, 2, 3 }, world.Explorer.StarsOffered(world.Draugr));
            Assert.Equal(2, world.Explorer.StarLevelFor(world.Draugr.Key, 2));
            world.Rule(CompendiumRule.Off);
            Assert.Equal(new[] { 1, 2, 3 }, world.Explorer.StarsOffered(world.Draugr));
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Peeking = true;
            Assert.Equal(new[] { 1, 2, 3 }, world.Explorer.StarsOffered(world.Draugr));
        }

        [Fact]
        public void APageShownWholeByKnownUpToOffersEveryStarAndLook()
        {
            // Known up to shows a tier's pages whole under the strict rules, the stage with them.
            var world = new World();
            world.View.Milestones(new System.Collections.Generic.Dictionary<string, int> { [world.Draugr.Key] = 1 }, 1);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Draugr);
            Assert.Equal(new[] { 1, 2, 3 }, world.Explorer.StarsOffered(world.Draugr));
            Assert.Equal(3, world.Explorer.StarLevelFor(world.Draugr.Key, 3));
            Assert.True(world.Explorer.WearsShown(world.Draugr.Key, "draugr_bow"));
            world.View.Milestones(new System.Collections.Generic.Dictionary<string, int> { [world.Draugr.Key] = 1 }, 0);
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 1 }, world.Explorer.StarsOffered(world.Draugr));
        }

        [Fact]
        public void UnderStrictALookShowsOnlyWithEveryItemSeenCarried()
        {
            // Under the loose rules a look of met items is offered, as before; under the strict
            // rules a look is offered once the creature was seen carrying each of its items.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Draugr);
            string[][] looks = { new string[0], new[] { "draugr_axe" }, new[] { "draugr_bow" }, new[] { "draugr_bow", "NotInTheCatalog" } };
            Assert.Equal(new[] { 0, 1, 2, 3 }, world.Explorer.LooksShown(looks.Length, look => looks[look]));
            Assert.True(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));

            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 0 }, world.Explorer.LooksShown(looks.Length, look => looks[look]));
            Assert.False(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));
            world.Book.Sighted(world.Draugr.Key, 0, new[] { "draugr_axe" });
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 0, 1 }, world.Explorer.LooksShown(looks.Length, look => looks[look]));
            Assert.True(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));
            Assert.False(world.Explorer.WearsShown(world.Draugr.Key, "draugr_bow"));
            // An item the catalog lacks is no bar under either rules once the rest was seen.
            world.Book.Sighted(world.Draugr.Key, 0, new[] { "draugr_bow" });
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 0, 1, 2, 3 }, world.Explorer.LooksShown(looks.Length, look => looks[look]));
        }

        [Fact]
        public void AnAttackTakenOrSeenBegunCountsAsItsWeaponSeenCarried()
        {
            // The clip of an attack taken plays once the attack is proved; the stage dresses the
            // creature with the weapon it hit with, and the page says it was seen carrying it.
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Draugr);
            Assert.False(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));
            Assert.Empty(world.Explorer.SeenLines(world.Draugr));

            Assert.True(world.Book.FoundOut(world.Draugr.Key, EntryKeys.Attack("draugr_axe")));
            world.Rule(CompendiumRule.Strict);
            Assert.True(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));
            Assert.False(world.Explorer.WearsShown(world.Draugr.Key, "draugr_bow"));
            var lines = world.Explorer.SeenLines(world.Draugr);
            Assert.Equal(new[] { "Seen carrying" }, lines.Select(l => l.Label));
            Assert.Equal(world.Axe.ShownName, lines[0].Value);
            // Seen carrying the bow up close too: each once, in name order, after the stars seen.
            world.Book.Sighted(world.Draugr.Key, 0, new[] { "draugr_bow", "draugr_axe" });
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(world.Axe.ShownName + ", " + world.Bow.ShownName, world.Explorer.SeenLines(world.Draugr).Single(l => l.Label == "Seen carrying").Value);
        }

        [Fact]
        public void ASeenPageTellsWhatSightGave()
        {
            var world = new World();
            world.Book.Sighted(world.Draugr.Key, 1, new[] { "draugr_axe" });
            world.Book.Sighted(world.Draugr.Key, 0, null);
            world.Book.Witness(world.Draugr.Key, world.Forest.Key);
            world.Book.Witness(world.Draugr.Key, world.Crypt.Key);
            world.Rule(CompendiumRule.Strict);

            var lines = world.Explorer.SeenLines(world.Draugr);
            Assert.Equal(new[] { "Seen with", "Seen carrying", "Seen in" }, lines.Select(l => l.Label));
            Assert.Equal("no stars, 1 star", lines[0].Value);
            Assert.Equal(world.Axe.ShownName, lines[1].Value);
            Assert.Equal(world.Forest.ShownName + ", " + world.Crypt.ShownName, lines[2].Value);

            Assert.Empty(world.Explorer.SeenLines(world.Axe));
            world.Rule(CompendiumRule.Off);
            Assert.Empty(world.Explorer.SeenLines(world.Draugr));
        }
    }
}
