using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class SeenDropsTests
    {
        // What creatures are seen to drop as you play, whichever mod put it there: each death
        // whose loot came out on this machine counts as a kill of that creature, and each item
        // in its loot is counted with how many kills it came in and how many at a time. It is
        // kept from session to session, and a line that cannot be read is passed over.

        private static SeenDrops Greydwarfs()
        {
            var seen = new SeenDrops();
            seen.Record("Greydwarf", new[] { ("Wood", 2), ("Resin", 1) });
            seen.Record("Greydwarf", new[] { ("Wood", 1), ("GemstoneRed_JC", 1) });
            seen.Record("Greydwarf", new (string, int)[0]);
            seen.Record("Troll", new[] { ("TrollHide", 5) });
            return seen;
        }

        [Fact]
        public void EachDeathCountsAsAKillOfItsCreatureLootOrNot()
        {
            var seen = Greydwarfs();

            Assert.Equal(3, seen.Kills("Greydwarf"));
            Assert.Equal(1, seen.Kills("Troll"));
            Assert.Equal(0, seen.Kills("Boar"));
        }

        [Fact]
        public void ACreatureTellsWhatItWasSeenToDropTheMostOftenFirst()
        {
            var drops = Greydwarfs().Of("Greydwarf");

            Assert.Equal(new[] { "Wood", "GemstoneRed_JC", "Resin" }, drops.Select(d => d.Item));
            var wood = drops[0];
            Assert.Equal((2, 3, 1, 2), (wood.Times, wood.Total, wood.Least, wood.Most));
        }

        [Fact]
        public void AnItemInOneKillTwiceCountsAsOneKill()
        {
            var seen = new SeenDrops();
            seen.Record("Neck", new[] { ("NeckTail", 1), ("NeckTail", 1) });

            var tail = Assert.Single(seen.Of("Neck"));
            Assert.Equal((1, 2, 2, 2), (tail.Times, tail.Total, tail.Least, tail.Most));
        }

        [Fact]
        public void TheMostAtATimeGrowsWithALargerDrop()
        {
            var seen = new SeenDrops();
            seen.Record("Troll", new[] { ("TrollHide", 1) });
            seen.Record("Troll", new[] { ("TrollHide", 3) });

            var hide = Assert.Single(seen.Of("Troll"));
            Assert.Equal((1, 3), (hide.Least, hide.Most));
        }

        [Fact]
        public void ANamelessCreatureOrItemIsNotRecorded()
        {
            var seen = new SeenDrops();
            seen.Record("", new[] { ("Wood", 1) });
            Assert.False(seen.Changed);
            Assert.Equal(0, seen.Kills(""));

            seen.Record("Boar", new[] { ("", 1), ("RawMeat", 0) });
            Assert.Equal(1, seen.Kills("Boar"));
            Assert.Empty(seen.Of("Boar"));
        }

        [Fact]
        public void AnItemTellsTheCreatureThatDropsItMostOftenFirst()
        {
            var seen = new SeenDrops();
            seen.Record("Greydwarf", new[] { ("Resin", 1) });
            seen.Record("Skeleton", new[] { ("Resin", 1) });
            seen.Record("Skeleton", new[] { ("Resin", 1) });

            Assert.Equal(new[] { "Skeleton", "Greydwarf" }, seen.Sources("Resin").Select(s => s.Creature));
        }

        [Fact]
        public void AnItemTellsEveryCreatureSeenToDropIt()
        {
            var seen = Greydwarfs();
            seen.Record("Skeleton", new[] { ("Resin", 3) });

            var sources = seen.Sources("Resin");

            Assert.Equal(new[] { "Greydwarf", "Skeleton" }, sources.Select(s => s.Creature));
            Assert.Equal(3, sources[0].Kills);
            Assert.Empty(seen.Sources("Coins"));
        }

        [Fact]
        public void WhatWasSeenIsKeptAndReadBack()
        {
            var text = Greydwarfs().Save();
            var back = SeenDrops.Load(text);

            Assert.Equal(3, back.Kills("Greydwarf"));
            Assert.Equal(new[] { "Wood", "GemstoneRed_JC", "Resin" }, back.Of("Greydwarf").Select(d => d.Item));
            Assert.Equal((2, 3, 1, 2), (back.Of("Greydwarf")[0].Times, back.Of("Greydwarf")[0].Total, back.Of("Greydwarf")[0].Least, back.Of("Greydwarf")[0].Most));
        }

        [Fact]
        public void ALineThatCannotBeReadIsPassedOver()
        {
            var back = SeenDrops.Load("kills\tGreydwarf\t4\nnonsense\ndrop\tGreydwarf\tWood\tx\t1\t1\t1\ndrop\tGreydwarf\tResin\t2\t3\t1\t2\n\nkills\tTroll\nkills\t5\nkills\t\t3");

            Assert.Equal(4, back.Kills("Greydwarf"));
            Assert.Equal(new[] { "Resin" }, back.Of("Greydwarf").Select(d => d.Item));
            Assert.Equal(0, back.Kills("Troll"));
            Assert.Equal(0, back.Kills("5"));
            Assert.Equal(0, back.Kills(""));
        }

        [Fact]
        public void NothingSeenLoadsEmpty()
        {
            Assert.Equal(0, SeenDrops.Load(null).Kills("Greydwarf"));
            Assert.Equal(0, SeenDrops.Load("").Kills("Greydwarf"));
        }

        [Fact]
        public void RecordingMarksItChangedUntilSaved()
        {
            var seen = new SeenDrops();
            Assert.False(seen.Changed);
            seen.Record("Boar", new[] { ("RawMeat", 1) });
            Assert.True(seen.Changed);
            seen.Save();
            Assert.False(seen.Changed);
        }

        [Theory]
        [InlineData(3, 40, 1, 1, "in 3 of 40 kills")]
        [InlineData(1, 1, 5, 5, "in 1 of 1 kill, 5 each time")]
        [InlineData(12, 40, 1, 3, "in 12 of 40 kills, 1 to 3 each time")]
        public void AnItemSeenIsToldByHowOftenAndHowMany(int times, int kills, int least, int most, string told)
        {
            Assert.Equal(told, SeenWords.Amount(new SeenDrop { Times = times, Least = least, Most = most }, kills));
        }

        [Theory]
        [InlineData(3, 40, 1, 1, "3 of 40")]
        [InlineData(12, 40, 1, 3, "1 to 3, 12 of 40")]
        [InlineData(1, 1, 5, 5, "5, 1 of 1")]
        public void AChipInTheRowTellsHowManyAndInHowManyKills(int times, int kills, int least, int most, string told)
        {
            Assert.Equal(told, SeenWords.Chip(new SeenDrop { Times = times, Least = least, Most = most }, kills));
        }

        [Fact]
        public void TheRowAndLinesSayItWasSeenInYourPlay()
        {
            Assert.Equal("Seen dropping in your play (40 kills)", SeenWords.Title(40));
            Assert.Equal("Seen dropping in your play (1 kill)", SeenWords.Title(1));
            Assert.Equal("Seen dropped by Greydwarf in your play, in 3 of 40 kills", SeenWords.Line("Greydwarf", new SeenDrop { Times = 3, Least = 1, Most = 1 }, 40));
        }
    }
}
