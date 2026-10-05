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
        public void AWetWeatherWetsYouOutsideWithoutARoof()
        {
            Assert.Equal(new[] { ("Wet", "outside in Rain") }, CodeGivers.OfWeather("Rain", wet: true, cold: false, coldAtNight: false, freezing: false, freezingAtNight: false));
            Assert.Empty(CodeGivers.OfWeather("Clear", false, false, false, false, false));
        }

        [Fact]
        public void ColdWeatherChillsYouAwayFromAFireAndFreezingWeatherFreezesYouAwayFromFireAndShelter()
        {
            // EnvMan.CalculateCold and CalculateFreezing, as Player.UpdateEnvStatusEffects applies them.
            Assert.Equal(new[] { ("Cold", "in Mist, away from a fire") }, CodeGivers.OfWeather("Mist", false, cold: true, coldAtNight: false, freezing: false, freezingAtNight: false));
            Assert.Equal(new[] { ("Cold", "in Clear at night, away from a fire") }, CodeGivers.OfWeather("Clear", false, false, coldAtNight: true, freezing: false, freezingAtNight: false));
            Assert.Equal(new[] { ("Freezing", "in Snow storm, away from fire and shelter"), ("Cold", "in Snow storm, with a fire or shelter but not both") },
                CodeGivers.OfWeather("Snow storm", false, false, false, freezing: true, freezingAtNight: false));
            // Cold and freezing all the time: freezing takes over, cold only with fire or shelter.
            Assert.Equal(2, CodeGivers.OfWeather("Blizzard", false, cold: true, coldAtNight: false, freezing: true, freezingAtNight: false).Count);
            // Cold by day and freezing at night.
            Assert.Equal(new[] { ("Freezing", "in Snow at night, away from fire and shelter"), ("Cold", "in Snow at night, with a fire or shelter but not both"), ("Cold", "in Snow, away from a fire") },
                CodeGivers.OfWeather("Snow", false, cold: true, coldAtNight: false, freezing: false, freezingAtNight: true));
        }

        [Fact]
        public void AStatusEffectGivesWhatItNamesAfterAWhile()
        {
            Assert.Equal("after a while", CodeGivers.AfterAWhile);
        }
    }
}
