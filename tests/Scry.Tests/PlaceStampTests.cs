using Xunit;

namespace Scry.Tests
{
    public class PlaceStampTests
    {
        // A place's stamp says whether what was kept of it still holds: the build of Scry that read
        // it, the mods loaded then (any of which may change a place in memory, so a change of mods
        // reads every place again), and the file its bundle is in, by its size and when it was
        // written, which a game update that rewrites the bundle changes.

        private static readonly (string, string)[] Mods = { ("isimp.Scry", "0.3.0"), ("com.jotunn.jotunn", "2.24.3"), ("Azumatt.AzuCraftyBoxes", "1.8.4") };

        [Fact]
        public void TheSameBuildModsAndBundleGiveTheSameStamp()
        {
            Assert.Equal(PlaceStamps.Of("b1", Mods, "7f3a91c2 3043 1"), PlaceStamps.Of("b1", Mods, "7f3a91c2 3043 1"));
        }

        [Fact]
        public void TheModsCountWhateverOrderTheyLoadIn()
        {
            var shuffled = new[] { Mods[2], Mods[0], Mods[1] };
            Assert.Equal(PlaceStamps.Of("b1", Mods, "s"), PlaceStamps.Of("b1", shuffled, "s"));
        }

        [Fact]
        public void AnotherBuildAModChangedOrABundleRewrittenGiveAnotherStamp()
        {
            var stamp = PlaceStamps.Of("b1", Mods, "7f3a91c2 3043 1");
            Assert.NotEqual(stamp, PlaceStamps.Of("b2", Mods, "7f3a91c2 3043 1"));
            Assert.NotEqual(stamp, PlaceStamps.Of("b1", new[] { Mods[0], Mods[1], ("Azumatt.AzuCraftyBoxes", "1.8.5") }, "7f3a91c2 3043 1"));
            Assert.NotEqual(stamp, PlaceStamps.Of("b1", new[] { Mods[0], Mods[1] }, "7f3a91c2 3043 1"));
            Assert.NotEqual(stamp, PlaceStamps.Of("b1", Mods, "7f3a91c2 3043 2"));
            Assert.NotEqual(stamp, PlaceStamps.Of("b1", Mods, "7f3a91c2 3044 1"));
        }

        [Fact]
        public void AModsListIsNotTakenForAnotherSplitDifferently()
        {
            Assert.NotEqual(PlaceStamps.Of("b1", new[] { ("ab", "1") }, "s"), PlaceStamps.Of("b1", new[] { ("a", "b1") }, "s"));
            Assert.NotEqual(PlaceStamps.Of("b1", new[] { ("ab", "1"), ("c", "1") }, "s"), PlaceStamps.Of("b1", new[] { ("a", "1"), ("bc", "1") }, "s"));
        }

        [Fact]
        public void APlaceWhoseBundleIsNotKnownHasNoStamp()
        {
            Assert.Equal("", PlaceStamps.Of("b1", Mods, ""));
        }

        [Fact]
        public void ABundleIsKnownByItsNameSizeAndWhenItWasWritten()
        {
            Assert.Equal("7f3a91c2 3043 638640000000000000", PlaceStamps.Bundle("7f3a91c2", 3043, 638640000000000000));
        }
    }
}
