using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a weather does to whoever is out in it, as read from its <c>EnvSetup</c>.</summary>
    internal struct WeatherFacts
    {
        public bool Wet, Cold, ColdAtNight, Freezing, FreezingAtNight, AlwaysDark;

        /// <summary>The wind's range, from 0 to 1 (<c>m_windMin</c>, <c>m_windMax</c>).</summary>
        public float WindMin, WindMax;
    }

    /// <summary>
    /// A weather in words: wet soaks you outside without a roof (<c>Player.UpdateEnvStatusEffects</c>
    /// adds Wet while <c>EnvMan.IsWet</c> and not under a roof); cold and freezing give their
    /// statuses, freezing outranking cold, each by day or only at night (<c>EnvMan.CalculateCold</c>,
    /// <c>CalculateFreezing</c>); some are always dark; each blows within a range of wind.
    /// </summary>
    internal static class WeatherWords
    {
        public static string Line(string name, WeatherFacts weather) => $"{name}: {Effects(weather)}";

        public static string Effects(WeatherFacts weather)
        {
            var parts = new List<string>();
            if (weather.Wet) parts.Add("wet outside without a roof");
            AddTemperature(parts, weather);
            if (weather.AlwaysDark) parts.Add("always dark");
            parts.Add(weather.WindMax <= 0f ? "no wind" : $"wind {Numbers.Amount(weather.WindMin * 100.0, 0)}–{Numbers.Percent(weather.WindMax)}");
            return string.Join(", ", parts);
        }

        /// <summary>
        /// What a biome's weathers put on you in all, the worst of them by day and at night:
        /// "Wet, cold, freezing at night"; "Nothing" where none puts anything on you.
        /// </summary>
        public static string PutsOnYou(IEnumerable<WeatherFacts> weathers)
        {
            var all = new WeatherFacts();
            foreach (var weather in weathers)
            {
                all.Wet |= weather.Wet;
                all.Cold |= weather.Cold;
                all.ColdAtNight |= weather.ColdAtNight;
                all.Freezing |= weather.Freezing;
                all.FreezingAtNight |= weather.FreezingAtNight;
            }
            var parts = new List<string>();
            if (all.Wet) parts.Add("wet");
            AddTemperature(parts, all);
            return parts.Count == 0 ? "Nothing" : Naming.Capital(string.Join(", ", parts));
        }

        /// <summary>Cold or freezing, by day and at night, freezing outranking cold.</summary>
        private static void AddTemperature(List<string> parts, WeatherFacts weather)
        {
            string Temperature(bool night)
            {
                if (weather.Freezing || night && weather.FreezingAtNight) return "freezing";
                if (weather.Cold || night && weather.ColdAtNight) return "cold";
                return null;
            }
            var day = Temperature(false);
            var night = Temperature(true);
            if (day == night) { if (day != null) parts.Add(day); }
            else if (day == null) parts.Add(night + " at night");
            else parts.Add($"{day}, {night} at night");
        }
    }
}
