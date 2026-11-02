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
        // a whole film of it takes long: a creature or an item films in a millisecond or two and
        // stays sharp turning, spinning or settling; a big dungeon's tens of milliseconds go lighter.
        // Each film measures it again, a lighter one by what it would have taken whole, so one slow
        // film (a first one drawing what was never drawn, a hitch) does not keep a quick stage soft.

        [Fact]
        public void AStageQuickToFilmStaysSharpWhileItsCameraMoves()
        {
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: 1.5, wasLighter: false, fromMs: StageFilm.LighterFromMs));
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: 0, wasLighter: false, fromMs: StageFilm.LighterFromMs));
        }

        [Fact]
        public void AStageSlowToFilmIsFilmedLighterWhileItsCameraMoves()
        {
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: 35, wasLighter: false, fromMs: StageFilm.LighterFromMs));
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: StageFilm.LighterFromMs, wasLighter: false, fromMs: StageFilm.LighterFromMs));
        }

        [Fact]
        public void AStillCameraOrAFastStageIsFilmedAsItIs()
        {
            Assert.False(StageFilm.Lighter(cameraMoved: false, fast: false, wholeMs: 35, wasLighter: false, fromMs: StageFilm.LighterFromMs));
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: true, wholeMs: 35, wasLighter: false, fromMs: StageFilm.LighterFromMs));
        }

        [Fact]
        public void ThePlayerSetsFromHowLongAFilmGoesLighter()
        {
            // Nought films every moving stage lighter, the most none.
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: 0.2, wasLighter: false, fromMs: 0f));
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: 5000, wasLighter: false, fromMs: StageFilm.LighterNeverMs));
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: 3, wasLighter: false, fromMs: 2f));
        }
        [Fact]
        public void OneSlowFilmDoesNotKeepAQuickStageLighter()
        {
            // A quick stage's lighter film, scaled to whole, is far under: the next film is whole.
            var whole = StageFilm.AsWhole(filmMs: 0.9, lighter: true);
            Assert.InRange(whole, 1.5, 1.7);
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: whole, wasLighter: true, fromMs: StageFilm.LighterFromMs));
        }

        [Fact]
        public void ASlowStageStaysLighterAsItKeepsMoving()
        {
            // A dungeon's lighter film of 25 ms would have taken some 44 whole.
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: StageFilm.AsWhole(25, true), wasLighter: true, fromMs: StageFilm.LighterFromMs));
            Assert.Equal(25, StageFilm.AsWhole(25, lighter: false));
        }

        [Fact]
        public void AStageNearTheLineDoesNotFlickerBetweenSharpAndSoft()
        {
            // Once lighter, it goes whole again only well under the line, so a stage costing about
            // the line does not change from frame to frame.
            Assert.True(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: 7, wasLighter: true, fromMs: 8f));
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: 7, wasLighter: false, fromMs: 8f));
            Assert.False(StageFilm.Lighter(cameraMoved: true, fast: false, wholeMs: 5, wasLighter: true, fromMs: 8f));
        }
    }
}