namespace Scry
{
    /// <summary>
    /// What on the stage moves by itself and so is filmed every frame: grass the wind bends, as
    /// the game's grass sways with its wind wherever it is drawn, the stage's too.
    /// </summary>
    internal static class StageFilm
    {
        /// <summary>The wind's strength (<c>EnvMan.GetWindIntensity</c>, nought to one) at or under which grass is taken as still.</summary>
        public const float StillWind = 0.05f;

        /// <summary>Whether grass on the stage moves in the wind: some is shown and the wind is more than still.</summary>
        public static bool GrassMoves(bool grassShown, float windIntensity) => grassShown && windIntensity > StillWind;

        /// <summary>From how many milliseconds a still film takes a moving camera films lighter, unless the player sets otherwise: half a frame at sixty a second.</summary>
        public const float LighterFromMs = 8f;

        /// <summary>The most the player can set it to, which no film reaches, so none is lighter.</summary>
        public const float LighterNeverMs = 1000f;

        /// <summary>
        /// Whether a film is made lighter: the camera moves, the stage is not already filmed fast,
        /// and its last still film took at least <paramref name="fromMs"/>, so a stage quick to
        /// film stays sharp as it turns and only a slow one trades its edges for smoothness.
        /// </summary>
        public static bool Lighter(bool cameraMoved, bool fast, double stillFilmMs, float fromMs) =>
            cameraMoved && !fast && fromMs < LighterNeverMs && stillFilmMs >= fromMs;
    }
}
