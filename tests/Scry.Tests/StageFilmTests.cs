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

        // While the camera moves, a stage is filmed lighter (smaller, stretched, softer) only where
        // a still film of it takes long: a creature or an item films in a millisecond or two and
        // stays sharp turning, spinning or settling; a big dungeon's tens of milliseconds go lighter.

        [Fact]
        public void AStageQuickToFilmStaysSharpWhileItsCameraMoves()
        {
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: false, stillFilmMs: 1.5, fromMs: StageFilm.LighterFromMs));
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: false, stillFilmMs: 0, fromMs: StageFilm.LighterFromMs));
        }

        [Fact]
        public void AStageSlowToFilmIsFilmedLighterWhileItsCameraMoves()
        {
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, stillFilmMs: 35, fromMs: StageFilm.LighterFromMs));
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, stillFilmMs: StageFilm.LighterFromMs, fromMs: StageFilm.LighterFromMs));
        }

        [Fact]
        public void AStillCameraOrAFastStageIsFilmedAsItIs()
        {
            Assert.False(StageFilm.Lighter(cameraMoved: false, fast: false, stillFilmMs: 35, fromMs: StageFilm.LighterFromMs));
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: true, stillFilmMs: 35, fromMs: StageFilm.LighterFromMs));
        }

        [Fact]
        public void ThePlayerSetsFromHowLongAFilmGoesLighter()
        {
            // Nought films every moving stage lighter, the most none.
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, stillFilmMs: 0.2, fromMs: 0f));
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: false, stillFilmMs: 5000, fromMs: StageFilm.LighterNeverMs));
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, stillFilmMs: 3, fromMs: 2f));
        }
    }
}
