namespace Scry
{
    /// <summary>
    /// Whether the weather now is the biome's own: the game puts another in its place inside a
    /// dungeon or a forcing zone, during a raid or a boss fight, in an altered stretch of a biome
    /// and in the intro (<c>EnvMan.m_forceEnv</c>, <c>EnvMan.GetEnvironmentOverride</c>), which is
    /// none of the weathers the biome's own table brings.
    /// </summary>
    internal static class BiomeWeather
    {
        /// <summary>Whether a weather is the biome's own, by the weather a zone forces and the one put in the biome's place, none where nothing is.</summary>
        public static bool Own(string forced, string overriding) => string.IsNullOrEmpty(forced) && string.IsNullOrEmpty(overriding);
    }
}
