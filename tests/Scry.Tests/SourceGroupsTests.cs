using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class SourceGroupsTests
    {
        // Stone comes out of some seventy things, most of them rocks alike but for their biome.
        // Where it comes from reads as a row for each way, each giver a chip, givers going by one
        // name one chip saying how many, its amount the range across them.

        private static SourceFacts Mined(string giver, string shown, int min, int max, TableOf table = TableOf.Rock)
        {
            var drop = new DropInfo("Stone", min, max, 1f);
            var rolls = new DropTableInfo { Drops = new List<DropInfo> { drop } };
            var source = SourceFacts.Of(SourceWay.Table, giver, shown);
            source.Table = table;
            source.Rolls = rolls;
            source.Drop = drop;
            return source;
        }

        private static SourceFacts Dropped(string giver, string shown, int min, int max, bool onePerPlayer = false) =>
            SourceFacts.Dropped(giver, shown, min, max, onePerPlayer, 1f);

        [Fact]
        public void RocksAlikeButForTheirBiomeShareOneChipSayingHowMany()
        {
            var groups = SourceGroups.Of(new[]
            {
                Mined("rock4_heath_frac", "Rock heath", 1, 2),
                Mined("rock4_coast_frac", "Rock coast", 1, 2),
                Mined("Rock_7", "Rock", 2, 3),
                Mined("MineRock_Copper", "Copper deposit", 1, 1),
            });

            Assert.Equal(new[] { "Mined from" }, groups.Select(g => g.Lead).Distinct().ToArray());
            var rock = groups.Single(g => g.Name == "Rock");
            Assert.Equal(new[] { "rock4_heath_frac", "rock4_coast_frac", "Rock_7" }, rock.Givers.Select(g => g.Giver).ToArray());
            Assert.Equal("1–3", rock.Amount);
            Assert.Equal("Rock ×3", rock.Chip);
            var copper = groups.Single(g => g.Name == "Copper deposit");
            Assert.Equal("1", copper.Amount);
            Assert.Equal("Copper deposit", copper.Chip);
        }

        [Fact]
        public void EachWayKeepsItsOwnRowInTheOrderFirstGiven()
        {
            var picked = SourceFacts.Of(SourceWay.Picked, "Pickable_Stone", "Stone");
            var groups = SourceGroups.Of(new[]
            {
                picked,
                Mined("rock1", "Rock", 1, 1),
                Dropped("Greydwarf", "Greydwarf", 1, 2),
                Mined("Rock_destructible", "Rock", 1, 1, TableOf.Broken),
                Mined("rock2", "Rock", 1, 1),
            });

            Assert.Equal(new[] { "Picked from", "Mined from", "Dropped by", "Broken out of" }, groups.Select(g => g.Lead).Distinct().ToArray());
            // A rock broken is not a rock mined: each way its own chip.
            Assert.Equal(2, groups.Single(g => g.Lead == "Mined from").Givers.Count);
            Assert.Single(groups.Single(g => g.Lead == "Broken out of").Givers);
            Assert.Null(groups.Single(g => g.Lead == "Picked from").Amount);
            Assert.Equal("1", groups.Single(g => g.Lead == "Dropped by").Amount);

            // A way's chips stand together however its givers came; a sapling's way has its own words.
            var together = SourceGroups.Of(new[]
            {
                Mined("rock1", "Rock", 1, 1),
                Dropped("Greydwarf", "Greydwarf", 1, 2),
                Mined("MineRock_Copper", "Copper deposit", 1, 1),
                SourceFacts.Of(SourceWay.GrowsFrom, "sapling_barley", "Barley seeds"),
            });
            Assert.Equal(new[] { "Mined from", "Mined from", "Dropped by", "Grows from" }, together.Select(g => g.Lead).ToArray());
        }

        [Fact]
        public void AWayWithAPaceAPriceOrABirthKeepsItsOwnLine()
        {
            Assert.True(SourceGroups.Groups(Mined("rock1", "Rock", 1, 1)));
            Assert.True(SourceGroups.Groups(Dropped("Troll", "Troll", 1, 2)));
            Assert.True(SourceGroups.Groups(SourceFacts.Of(SourceWay.Picked, "Pickable_Stone", "Stone")));
            Assert.True(SourceGroups.Groups(SourceFacts.Of(SourceWay.GrowsFrom, "sapling_barley", "Barley seeds")));
            Assert.False(SourceGroups.Groups(SourceFacts.Of(SourceWay.Made, "Beehive", "Beehive")));
            Assert.False(SourceGroups.Groups(SourceFacts.Of(SourceWay.Sold, "Haldor", "Haldor")));
            Assert.False(SourceGroups.Groups(SourceFacts.Of(SourceWay.Born, "Lox", "Lox")));
            Assert.False(SourceGroups.Groups(null));
            Assert.Empty(SourceGroups.Of(new[] { SourceFacts.Of(SourceWay.Sold, "Haldor", "Haldor") }));
        }

        [Fact]
        public void AnAmountTheGiversShareIsToldAsItIsAndOneNotCountedAlikeIsLeftOut()
        {
            var same = SourceGroups.Of(new[] { Dropped("Troll", "Troll", 1, 3), Dropped("Troll_Summoned", "Troll", 1, 3) });
            Assert.Equal("1–2", same.Single().Amount);
            // One for each player cannot be put in a range with a number.
            var mixed = SourceGroups.Of(new[] { Dropped("Troll", "Troll", 1, 3), Dropped("Troll_Summoned", "Troll", 1, 2, onePerPlayer: true) });
            Assert.Null(mixed.Single().Amount);
        }

        [Fact]
        public void EachGiversLineIsToldOnHoverTheFirstFewAndHowManyMore()
        {
            var givers = Enumerable.Range(1, 10).Select(i => Mined($"rock{i}", "Rock", 1, 1)).ToArray();
            var group = SourceGroups.Of(givers).Single();
            var tip = group.Tip(s => "Mined from Rock, 1");
            var lines = tip.Split('\n');
            Assert.Equal("Mined from Rock, 1 · rock1", lines[0]);
            Assert.Equal(SourceGroups.TipLines + 1, lines.Length);
            Assert.Equal("and 2 more", lines[lines.Length - 1]);

            // As many as it tells, it tells them all.
            var eight = SourceGroups.Of(givers.Take(SourceGroups.TipLines)).Single();
            Assert.Equal(SourceGroups.TipLines, eight.Tip(s => "Mined from Rock, 1").Split('\n').Length);
        }
    }
}
