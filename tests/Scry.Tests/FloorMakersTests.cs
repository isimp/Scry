using Xunit;

namespace Scry.Tests
{
    public class FloorMakersTests
    {
        // For the self-test to tell what each floor found stands on: the parts its rays landed
        // on within half a metre of it, the most landed on first, with their shares.

        [Fact]
        public void EachFloorNamesWhatItsRaysLandedOn()
        {
            var heights = new[] { 0f, 0.1f, 0.2f, 0.1f, 6.3f, 6.2f, 6.4f };
            var names = new[] { "stone_floor", "stone_floor", "rug", "stone_floor", "rock_top", "rock_top", "rock_top" };

            var told = FloorMakers.Tell(heights, names, new[] { 6.3f, 0f });

            Assert.Equal(new[] { "rock_top 100%", "stone_floor 75%, rug 25%" }, told);
        }

        [Fact]
        public void OnlyTheMostLandedOnAreNamed()
        {
            var heights = new[] { 0f, 0f, 0f, 0f, 0f, 0f };
            var names = new[] { "a", "a", "a", "b", "b", "c" };

            Assert.Equal(new[] { "a 50%, b 33%" }, FloorMakers.Tell(heights, names, new[] { 0f }));
        }

        [Fact]
        public void AFloorNoRayLandedNearIsTheGround()
        {
            // A location's own ground is a floor where none was found near it.
            Assert.Equal(new[] { "rug 100%", "the ground" }, FloorMakers.Tell(new[] { 3f }, new[] { "rug" }, new[] { 3f, 0f }));
            Assert.Equal(new[] { "the ground" }, FloorMakers.Tell(new[] { 0.6f }, new[] { "rug" }, new[] { 0f }));
        }
    }
}
