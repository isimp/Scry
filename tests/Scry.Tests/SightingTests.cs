using Xunit;

namespace Scry.Tests
{
    public class SightingTests
    {
        // A creature in view is seen as far as its size makes it out: one of a person's size or
        // smaller within 30 m, a bigger one farther as it looks bigger, up to a limit.

        [Fact]
        public void ACreatureOfAPersonsSizeOrSmallerIsSeenWithin30Metres()
        {
            Assert.Equal(30f, Sighting.Reach(1.8f));
            Assert.Equal(30f, Sighting.Reach(0.3f));
            Assert.Equal(30f, Sighting.Reach(0f));
        }

        [Fact]
        public void ABiggerCreatureIsSeenAsMuchFartherAsItIsBigger()
        {
            // A troll some 4.5 m tall looks at 75 m as a person does at 30 m.
            Assert.Equal(75f, Sighting.Reach(4.5f), 3);
            Assert.Equal(60f, Sighting.Reach(3.6f), 3);
        }

        [Fact]
        public void HoweverBigNothingIsSeenBeyond150Metres()
        {
            Assert.Equal(150f, Sighting.Reach(9f), 3);
            Assert.Equal(150f, Sighting.Reach(40f));
        }
    }
}
