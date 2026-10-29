using Xunit;

namespace Scry.Tests
{
    public class StageFilmTests
    {
        // The stage is filmed only while something on it moves; grass the wind bends moves on its
        // own, and filmed every few frames it jumped, the more the stronger the wind.

        [Fact]
        public void GrassTheWindBendsIsFilmedEveryFrame()
        {
            Assert.True(StageFilm.GrassMoves(grassShown: true, windIntensity: 0.6f));
            Assert.True(StageFilm.GrassMoves(grassShown: true, windIntensity: 0.1f));
        }

        [Fact]
        public void StillAirOrNoGrassLeavesTheStageAsItWas()
        {
            Assert.False(StageFilm.GrassMoves(grassShown: true, windIntensity: 0f));
            Assert.False(StageFilm.GrassMoves(grassShown: true, windIntensity: StageFilm.StillWind));
            Assert.False(StageFilm.GrassMoves(grassShown: false, windIntensity: 1f));
        }
    }
}
