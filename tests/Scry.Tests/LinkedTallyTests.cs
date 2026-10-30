using Xunit;

namespace Scry.Tests
{
    public class LinkedTallyTests
    {
        // Before moving any group of LINKED into a topic of In the game, the self-test walks every
        // page and tells what LINKED holds on each kind of page: which groups, on how many pages,
        // going to what kinds of thing, with examples to look at.

        [Fact]
        public void EachKindOfPageTellsHowManyOfItsPagesHoldLinkedAndItsGroupsMostPagesFirst()
        {
            var tally = new LinkedTally();
            tally.Page(Kind.Item);
            tally.Page(Kind.Item);
            tally.Page(Kind.Item);
            tally.Add(Kind.Item, "Bow", "Shoots", new[] { ("ArrowWood", "Item"), ("ArrowFire", "Item") });
            tally.Add(Kind.Item, "BowFineWood", "Shoots", new[] { ("ArrowWood", "Item") });
            tally.Add(Kind.Item, "Bow", "Variants", new[] { ("Bow_npc", "Item") });

            Assert.Equal(new[]
            {
                "Item pages: 2 of 3 hold LINKED, 4 links",
                "  Shoots: 2 pages, 3 links, to Item 3; Bow: ArrowWood, ArrowFire; BowFineWood: ArrowWood",
                "  Variants: 1 page, 1 link, to Item 1; Bow: Bow_npc",
            }, tally.Lines());
        }

        [Fact]
        public void AGroupSaysWhatKindsOfThingItGoesToMostFirst()
        {
            var tally = new LinkedTally();
            tally.Page(Kind.Creature);
            tally.Add(Kind.Creature, "Troll", "Animation sounds and effects", new[] { ("sfx_troll_grunt", "Sound"), ("vfx_troll_hit", "Effect"), ("sfx_troll_roar", "Sound") });

            Assert.Equal("  Animation sounds and effects: 1 page, 3 links, to Sound 2, Effect 1; Troll: sfx_troll_grunt, vfx_troll_hit, sfx_troll_roar", tally.Lines()[1]);
        }

        [Fact]
        public void AGroupShowsAFewPagesAndAFewLinksOfEachAsExamples()
        {
            var tally = new LinkedTally();
            foreach (var page in new[] { "F", "B", "E", "A", "D", "C" })
            {
                tally.Page(Kind.Piece);
                tally.Add(Kind.Piece, page, "Spawns", new[] { ("a", "Other"), ("b", "Other"), ("c", "Other"), ("d", "Other"), ("e", "Other"), ("f", "Other") });
            }

            // Pages by name, the first four; links in their order, the first five and how many more.
            Assert.Equal("  Spawns: 6 pages, 36 links, to Other 36; A: a, b, c, d, e +1; B: a, b, c, d, e +1; C: a, b, c, d, e +1; D: a, b, c, d, e +1; +2 pages", tally.Lines()[1]);
        }

        [Fact]
        public void AGroupOnMorePagesComesFirstThoughItHoldsFewerLinksAndAnEmptyRowHoldsNothing()
        {
            // How widely a group shows is what moving it changes; a row with no chips draws nothing.
            var tally = new LinkedTally();
            tally.Page(Kind.Item);
            tally.Page(Kind.Item);
            tally.Page(Kind.Item);
            tally.Add(Kind.Item, "Bow", "Shoots", new[] { ("ArrowWood", "Item") });
            tally.Add(Kind.Item, "Crossbow", "Shoots", new[] { ("BoltBone", "Item") });
            tally.Add(Kind.Item, "Bow", "Variants", new[] { ("a", "Item"), ("b", "Item"), ("c", "Item") });
            tally.Add(Kind.Item, "Club", "Variants", new (string, string)[0]);

            var lines = tally.Lines();
            Assert.Equal("Item pages: 2 of 3 hold LINKED, 5 links", lines[0]);
            Assert.StartsWith("  Shoots: 2 pages, 2 links", lines[1]);
        }

        [Fact]
        public void KindsWithTheMostLinksComeFirstAndAKindWithoutLinkedSaysSo()
        {
            var tally = new LinkedTally();
            tally.Page(Kind.Biome);
            tally.Page(Kind.Piece);
            tally.Add(Kind.Piece, "forge", "Upgrades", new[] { ("forge_ext1", "Piece") });
            tally.Page(Kind.Creature);
            tally.Add(Kind.Creature, "Troll", "Footsteps", new[] { ("vfx_troll_step", "Effect"), ("sfx_troll_step", "Sound") });

            Assert.Equal(new[]
            {
                "Creature pages: 1 of 1 hold LINKED, 2 links",
                "  Footsteps: 1 page, 2 links, to Effect 1, Sound 1; Troll: vfx_troll_step, sfx_troll_step",
                "Piece pages: 1 of 1 hold LINKED, 1 link",
                "  Upgrades: 1 page, 1 link, to Piece 1; forge: forge_ext1",
                "Biome pages: 0 of 1 hold LINKED",
            }, tally.Lines());
            Assert.Equal(3, tally.Pages);
            Assert.Equal(3, tally.Links);
        }
    }
}
