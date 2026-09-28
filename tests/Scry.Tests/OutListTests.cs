using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class OutListTests
    {
        [Fact]
        public void EachPinnedCopyIsItsOwnRowSoOneOfTwoCanBeTakenAway()
        {
            var rows = OutList.Rows(new[] { (OutPlace.Pinned, "Troll"), (OutPlace.Pinned, "Troll") });

            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] { 0, 1 }, rows.Select(r => r.Nth).ToArray());
            Assert.All(rows, r => Assert.Equal(1, r.Count));
        }

        [Fact]
        public void WhatPlaysForAWhileIsOneRowPerThingWithHowManyAreOut()
        {
            // An effect played twice is one line saying two are out, not a line per copy.
            var rows = OutList.Rows(new[] { (OutPlace.Playing, "vfx_troll_death"), (OutPlace.Playing, "sfx_troll_death"), (OutPlace.Playing, "vfx_troll_death") });

            Assert.Equal(new[] { ("vfx_troll_death", 2), ("sfx_troll_death", 1) }, rows.Select(r => (r.Key, r.Count)).ToArray());
        }

        [Fact]
        public void RowsGoInTheOrderOfWhatStaysLongestFirst()
        {
            var rows = OutList.Rows(new[]
            {
                (OutPlace.Playing, "vfx_fire"), (OutPlace.Status, "Rested"), (OutPlace.Sound, "sfx_bow_fire"),
                (OutPlace.Pinned, "Troll"), (OutPlace.Shown, "Deer"),
            });

            Assert.Equal(new[] { OutPlace.Shown, OutPlace.Pinned, OutPlace.Sound, OutPlace.Status, OutPlace.Playing }, rows.Select(r => r.Place).ToArray());
        }

        [Fact]
        public void NothingOutIsNoRows()
        {
            Assert.Empty(OutList.Rows(new (OutPlace, string)[0]));
        }
    }
}
