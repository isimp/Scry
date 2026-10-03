using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FirstOfNameTests
    {
        // A mod can put its status effect into the game's list twice (as it sets up again for a
        // world loaded a second time). The game only ever finds the first of a name, so the
        // catalog has one entry for it, that one.

        private static (string Name, int Copy) S(string name, int copy) => (name, copy);

        [Fact]
        public void SomethingListedTwiceIsReadOnceTheFirst()
        {
            var read = FirstOfName.Each(new[] { S("SE_Rested", 1), S("SE_Wet", 1), S("SE_Rested", 2) }, s => s.Name).ToList();
            Assert.Equal(new[] { S("SE_Rested", 1), S("SE_Wet", 1) }, read);
        }

        [Fact]
        public void EverythingElseIsReadInItsOrder()
        {
            var read = FirstOfName.Each(new[] { S("c", 1), S("a", 1), S("b", 1), S("a", 2), S("c", 2) }, s => s.Name).Select(s => s.Name);
            Assert.Equal(new[] { "c", "a", "b" }, read);
        }

        [Fact]
        public void NamesDifferingInCaseAreTwo()
        {
            // The game hashes a name as it is written.
            var read = FirstOfName.Each(new[] { S("Wolf", 1), S("wolf", 1) }, s => s.Name);
            Assert.Equal(2, read.Count());
        }

        [Fact]
        public void SomethingWithNoNameIsLeftOut()
        {
            var read = FirstOfName.Each(new[] { S(null, 1), S("", 1), S("SE_Wet", 1) }, s => s.Name).Select(s => s.Name);
            Assert.Equal(new[] { "SE_Wet" }, read);
        }
    }
}
