using Xunit;

namespace Scry.Tests
{
    public class CodeGiversTests
    {
        // Some status effects no prefab names: the game's own code puts them on you. A bed gives
        // Rested on waking in it (Player.SetSleeping); a fire's heat (an EffectArea of the Heat
        // type, EffectArea.CustomFixedUpdate) makes you warm and lets you rest, as does a seat by
        // one (Player.UpdateEnvStatusEffects); and a status effect that names another gives it
        // after a while, as Resting gives Rested (SE_Cozy.UpdateStatusEffect).

        [Fact]
        public void ABedGivesRestedOnWaking()
        {
            Assert.Equal(new[] { ("Rested", "waking in it") }, CodeGivers.Of(bed: true, heat: false, seat: false));
        }

        [Fact]
        public void AFiresHeatMakesYouWarmAndLetsYouRestAsASeatByOneDoes()
        {
            Assert.Equal(new[] { ("CampFire", "standing near it"), ("Resting", "sitting or sheltered near it") }, CodeGivers.Of(bed: false, heat: true, seat: false));
            Assert.Equal(new[] { ("Resting", "sitting in it near a fire") }, CodeGivers.Of(bed: false, heat: false, seat: true));
            Assert.Empty(CodeGivers.Of(bed: false, heat: false, seat: false));
        }

        [Fact]
        public void AStatusEffectGivesWhatItNamesAfterAWhile()
        {
            Assert.Equal("after a while", CodeGivers.AfterAWhile);
        }
    }
}
