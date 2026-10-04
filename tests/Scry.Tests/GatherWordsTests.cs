using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class GatherWordsTests
    {
        // What is felled, mined, broken, picked or grown tells what it takes, what it gives and
        // what it becomes, and what it resists grouped by how hard each damage type lands.

        [Fact]
        public void HealthIsToldForTheWholeOrForEachPieceMined()
        {
            Assert.Equal("120", GatherWords.Health(120f, perPiece: false));
            Assert.Equal("50 a piece", GatherWords.Health(50f, perPiece: true));
        }

        [Fact]
        public void AnyToolBreaksWhatNeedsNoTier()
        {
            Assert.Equal("any", GatherWords.ToolTier(0));
            Assert.Equal("2", GatherWords.ToolTier(2));
        }

        [Fact]
        public void AShellSaysWhatItBreaksIntoAndThatThatIsMinedAPieceAtATime()
        {
            Assert.Equal("Silver vein, mined a piece at a time", GatherWords.BreaksInto("Silver vein"));
        }

        [Fact]
        public void WhatBreaksAndGivesNothingSaysSo() => Assert.Equal("nothing when broken", GatherWords.NothingWhenBroken);

        [Fact]
        public void WhatIsPickedTellsItsYieldADayAndHowLongADayIs()
        {
            Assert.Equal("2 a day (a day is 20 min)", GatherWords.PerDay("2 a day", 1200f));
        }

        [Fact]
        public void APlantTellsTheGroundItNeedsAndTheWeatherItTolerates()
        {
            Assert.Equal("cultivated ground", GatherWords.CultivatedGround);
            Assert.Equal("heat, cold", GatherWords.Tolerates(heat: true, cold: true));
            Assert.Equal("cold", GatherWords.Tolerates(heat: false, cold: true));
            Assert.Null(GatherWords.Tolerates(heat: false, cold: false));
        }

        [Fact]
        public void ARowOfWhatIsPickedOrGrownSaysWhenItIsOneOfSeveral()
        {
            Assert.Equal("Picked", GatherWords.Picked(oneOf: false));
            Assert.Equal("Picked, one of", GatherWords.Picked(oneOf: true));
            Assert.Equal("Grows into", GatherWords.GrowsInto(oneOf: false));
            Assert.Equal("Grows into one of", GatherWords.GrowsInto(oneOf: true));
        }

        [Fact]
        public void ADropRowLeadsWithWhenItDrops()
        {
            Assert.Equal("When felled, drops 3 times", DropWords.Led(GatherWords.WhenFelled, "Drops 3 times"));
            Assert.Equal("Each piece drops", DropWords.Led(GatherWords.EachPiece, "Drops"));
            Assert.Equal("Drops 3 times", DropWords.Led(null, "Drops 3 times"));
        }

        [Theory]
        [InlineData(Degree.VeryWeak, "Very weak to")]
        [InlineData(Degree.Weak, "Weak to")]
        [InlineData(Degree.SlightlyWeak, "Slightly weak to")]
        [InlineData(Degree.SlightlyResistant, "Slightly resists")]
        [InlineData(Degree.Resistant, "Resists")]
        [InlineData(Degree.VeryResistant, "Strongly resists")]
        [InlineData(Degree.Immune, "Immune to")]
        [InlineData(Degree.Ignore, "Unaffected by")]
        public void EachDegreeReadsAsALabel(object degree, string label) => Assert.Equal(label, ResistWords.Label((Degree)degree));

        [Fact]
        public void ResistancesAreGroupedByDegreeFromTheMostHarmTakenToTheLeast()
        {
            var groups = ResistWords.ByDegree(new[]
            {
                ("fire", Degree.Resistant), ("blunt", Degree.Weak), ("slash", Degree.Normal), ("frost", Degree.Resistant), ("spirit", Degree.Immune), ("pierce", Degree.VeryWeak),
            });
            Assert.Equal(new[] { "Very weak to", "Weak to", "Resists", "Immune to" }, groups.Select(g => g.Words));
            Assert.Equal(new[] { "fire", "frost" }, groups[2].Types);
        }

        [Fact]
        public void NothingResistedTellsNoGroups() => Assert.Empty(ResistWords.ByDegree(new[] { ("slash", Degree.Normal) }));
    }
}
