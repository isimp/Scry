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
    }
}
