using Xunit;

namespace Scry.Tests
{
    public class WorldWordsTests
    {
        [Fact]
        public void AWorldAtTheGamesOwnSettingsNeedsNoNote()
        {
            Assert.Null(WorldWords.Note(0, 2f, 1f, health: true, drops: true));
        }

        [Fact]
        public void AWorldLevelTellsHowMuchMoreHealthEnemiesHave()
        {
            Assert.Equal("health ×2 (world level 1), not counted in these figures", WorldWords.Note(1, 2f, 1f, health: true, drops: true));
            Assert.Equal("health ×4 (world level 2), not counted in these figures", WorldWords.Note(2, 2f, 1f, health: true, drops: false));
        }

        [Fact]
        public void AResourceRateTellsHowMuchMoreMostDropsGive()
        {
            Assert.Equal("most drops ×1.5 (resource rate), not counted in these figures", WorldWords.Note(0, 2f, 1.5f, health: true, drops: true));
        }

        [Fact]
        public void BothAreToldTogether()
        {
            Assert.Equal("health ×2 (world level 1) and most drops ×0.5 (resource rate), not counted in these figures", WorldWords.Note(1, 2f, 0.5f, health: true, drops: true));
        }

        [Fact]
        public void OnlyWhatTheEntryShowsIsTold()
        {
            Assert.Null(WorldWords.Note(3, 2f, 1f, health: false, drops: true));
            Assert.Equal("most drops ×2 (resource rate), not counted in these figures", WorldWords.Note(3, 2f, 2f, health: false, drops: true));
            Assert.Null(WorldWords.Note(0, 2f, 2f, health: true, drops: false));
        }
    }
}
