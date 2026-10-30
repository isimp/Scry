using Xunit;

namespace Scry.Tests
{
    public class HeadingClashTests
    {
        // Scry's own fixed words go through the name mask as the game's texts do, so a word of
        // them that is also the name of a thing not seen reads a smudge ("hook", the game's Hook).

        [Fact]
        public void AFixedHeadingHoldingAThingsWholeNameIsFoundWithThatName()
        {
            var clashes = HeadingClashes.Of(new[] { "Bites on, when it reaches the hook", "Hooked fish", "Drops" }, new[] { "Hook", "Drop", "Troll" });
            Assert.Equal(new[] { ("Bites on, when it reaches the hook", "Hook"), ("Drops", "Drop") }, clashes);
        }

        [Fact]
        public void NoHeadingClashesWhereNoNameIsWholeInIt()
        {
            Assert.Empty(HeadingClashes.Of(new[] { "Hooked fish", "Trolling" }, new[] { "Hook", "Troll" }));
            Assert.Empty(HeadingClashes.Of(new[] { "Bites on" }, new string[0]));
        }

        [Fact]
        public void AFishsAndABaitsBitingHeadingsNameNoHook()
        {
            // The game's Hook is an item a character may never have seen; knowing a rod and bait tells the rest.
            Assert.Empty(HeadingClashes.Of(new[] { FishWords.BitesOn, FishWords.CatchesOn }, new[] { "Hook" }));
        }
    }
}
