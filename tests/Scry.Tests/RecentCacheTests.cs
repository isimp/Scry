using Xunit;

namespace Scry.Tests
{
    public class RecentCacheTests
    {
        // What Scry works out of a page is kept for the next time it is shown, but no more pages
        // than so many: a sweep over every entry, or a long session, kept them all, and the
        // game's memory cleanup, which goes through all it holds, came to take a large part of a
        // second. The page shown longest ago is let go first.

        [Fact]
        public void ItKeepsNoMoreThanItsSizeLettingGoOfTheLeastRecentFirst()
        {
            var cache = new RecentCache<string, int>(3);
            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.Set("c", 3);
            cache.Set("d", 4);

            Assert.Equal(3, cache.Count);
            Assert.False(cache.TryGet("a", out _));
            Assert.True(cache.TryGet("d", out var d) && d == 4);
        }

        [Fact]
        public void APageShownAgainCountsAsRecent()
        {
            var cache = new RecentCache<string, int>(3);
            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.Set("c", 3);
            Assert.True(cache.TryGet("a", out _));
            cache.Set("d", 4);

            Assert.True(cache.TryGet("a", out var a) && a == 1);
            Assert.False(cache.TryGet("b", out _));
        }

        [Fact]
        public void SetAgainReplacesAndCountsAsRecent()
        {
            var cache = new RecentCache<string, int>(2);
            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.Set("a", 10);
            cache.Set("c", 3);

            Assert.Equal(2, cache.Count);
            Assert.True(cache.TryGet("a", out var a) && a == 10);
            Assert.False(cache.TryGet("b", out _));
        }

        [Fact]
        public void OneCanBeLetGoOfAndAllForgotten()
        {
            var cache = new RecentCache<string, int>(4);
            cache.Set("a", 1);
            cache.Set("b", 2);
            Assert.True(cache.Remove("a"));
            Assert.False(cache.Remove("a"));
            Assert.Equal(1, cache.Count);
            cache.Clear();
            Assert.Equal(0, cache.Count);
            Assert.False(cache.TryGet("b", out _));
            // Let go of, a key's place among the recent goes too: filling up again lets go of the oldest.
            cache.Set("x", 1);
            cache.Set("y", 2);
            cache.Set("z", 3);
            cache.Set("w", 4);
            cache.Set("v", 5);
            Assert.False(cache.TryGet("x", out _));
        }

        [Fact]
        public void ASizeBelowOneKeepsOne()
        {
            var cache = new RecentCache<string, int>(0);
            cache.Set("a", 1);
            cache.Set("b", 2);
            Assert.Equal(1, cache.Count);
            Assert.True(cache.TryGet("b", out _));
        }
    }
}
