using System.Collections;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Scry's marks and dots stand on Valheim's map only where it shows the ground, as the game
    /// tells explored ground (<c>Minimap.IsExplored</c>): the pixel a position maps to is the
    /// character's own explored or explored on a map shared with it. A dungeon's entrance seen
    /// from farther than the map explores waits until that ground is explored.
    /// </summary>
    public class MarksOnExploredGroundTests
    {
        // A map of 8 pixels a side at 10 m each, as the game's is of 2,048 at 12 m: the middle pixel at the world's middle.
        private const int Size = 8;
        private const float Pixel = 10f;

        private static BitArray Map(params (int X, int Y)[] explored)
        {
            var bits = new BitArray(Size * Size);
            foreach (var (x, y) in explored) bits[y * Size + x] = true;
            return bits;
        }

        [Fact]
        public void APositionIsOnThePixelTheGameMapsItTo()
        {
            // px = round(x / pixel + size / 2), as Minimap.WorldToPixel works it out.
            Assert.Equal(4 * Size + 4, ExploredGrid.PixelAt(0f, 0f, Size, Pixel));
            Assert.Equal(2 * Size + 7, ExploredGrid.PixelAt(31f, -19f, Size, Pixel));
            Assert.Equal(-1, ExploredGrid.PixelAt(40f, 0f, Size, Pixel));
            Assert.Equal(-1, ExploredGrid.PixelAt(0f, -46f, Size, Pixel));
        }

        [Fact]
        public void AMarkStandsOnGroundTheCharacterExplored()
        {
            Assert.True(ExploredGrid.ShownAt(31f, -19f, Size, Pixel, Map((7, 2)), null));
        }

        [Fact]
        public void AMarkStandsOnGroundAMapSharedWithTheCharacterShows()
        {
            Assert.True(ExploredGrid.ShownAt(31f, -19f, Size, Pixel, Map(), Map((7, 2))));
        }

        [Fact]
        public void AMarkWaitsWhereTheMapShowsNoGround()
        {
            // An entrance seen far off, beyond what the map explored around the character.
            Assert.False(ExploredGrid.ShownAt(31f, -19f, Size, Pixel, Map((4, 4), (6, 2)), Map((7, 3))));
            Assert.False(ExploredGrid.ShownAt(100f, 0f, Size, Pixel, Map((7, 4)), null));
            Assert.False(ExploredGrid.ShownAt(0f, 0f, Size, Pixel, null, null));
            // A map smaller than its size says holds nothing past its end.
            Assert.False(ExploredGrid.ShownAt(31f, 19f, Size, Pixel, new BitArray(3, true), null));
        }
    }
}
