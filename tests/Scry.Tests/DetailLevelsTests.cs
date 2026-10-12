using Xunit;

namespace Scry.Tests
{
    // The game culls a part's last level of detail once it is small on screen (a cave room's rocks
    // below a tenth to a third of the picture's height), which a whole place zoomed out on the
    // stage soon is, so its rooms went piece by piece. What stands on the stage keeps its last
    // level at any size, still turning to its lesser ones as it shrinks.
    public class DetailLevelsTests
    {
        [Fact]
        public void TheLastLevelIsNeverCulledAndTheOthersTurnAsBefore()
        {
            Assert.Equal(new[] { 0.6f, 0.3f, 0f }, DetailLevels.NeverCulled(new[] { 0.6f, 0.3f, 0.151f }));
        }

        [Fact]
        public void ASingleLevelIsAlwaysDrawn()
        {
            Assert.Equal(new[] { 0f }, DetailLevels.NeverCulled(new[] { 0.203f }));
        }

        [Fact]
        public void NoLevelsOrOneNeverCulledAlreadyIsLeftAsItIs()
        {
            Assert.Null(DetailLevels.NeverCulled(new float[0]));
            Assert.Null(DetailLevels.NeverCulled(new[] { 0.5f, 0f }));
        }
    }
}
