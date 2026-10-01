using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class RunestoneTests
    {
        // A location's runestones, as its page tells them: every text a stone can give, with its
        // title. RuneStone.Interact shows m_text, or one of m_randomTexts picked by
        // RuneStone.GetRandomText, which seeds the pick with the stone's place, so a stone always
        // gives the same one.

        private static PlaceRunestone Stone(params string[] texts)
        {
            var stone = new PlaceRunestone { Name = "Rune stone" };
            stone.Texts.AddRange(texts.Select(t => new RuneText { Topic = "About " + t, Text = t }));
            return stone;
        }

        [Fact]
        public void AStoneWithManyTextsSaysItGivesOneAlwaysTheSameForWhereItStands()
        {
            Assert.Equal("Each stone gives one of these 3, always the same one for where it stands.", RuneWords.Caption(Stone("a", "b", "c")));
            Assert.Null(RuneWords.Caption(Stone("a")));
        }

        [Fact]
        public void AStoneIsToldOnceHoweverOftenTheLocationHoldsIt()
        {
            var stones = new List<PlaceRunestone>();
            PlaceRunes.Add(stones, Stone("a", "b"));
            PlaceRunes.Add(stones, Stone("a", "b"));
            PlaceRunes.Add(stones, Stone("c"));
            PlaceRunes.Add(stones, Stone("d", "e"));
            Assert.Equal(3, stones.Count);
            Assert.Equal(new[] { "a", "b" }, stones[0].Texts.Select(t => t.Text));
            Assert.Equal(new[] { "c" }, stones[1].Texts.Select(t => t.Text));
            Assert.Equal(new[] { "d", "e" }, stones[2].Texts.Select(t => t.Text));
        }

        [Fact]
        public void AStoneWithNothingToReadIsLeftOut()
        {
            var stones = new List<PlaceRunestone>();
            PlaceRunes.Add(stones, Stone());
            PlaceRunes.Add(stones, Stone(""));
            Assert.Empty(stones);
        }

        [Fact]
        public void ATextWithNoTitleGoesByItsStonesName()
        {
            Assert.Equal("Rune stone", RuneWords.Title(new RuneText { Topic = "", Text = "x" }, "Rune stone"));
            Assert.Equal("The boar", RuneWords.Title(new RuneText { Topic = "The boar", Text = "x" }, "Rune stone"));
        }
    }
}
