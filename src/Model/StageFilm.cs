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

        /// <summary>How much of the stage's width and height a lighter film takes.</summary>
        public const float MotionShare = 0.75f;

        /// <summary>How far under the line a lighter film's stage must come to be filmed whole again while it moves, so one costing about the line keeps one look.</summary>
        public const float StayLighterShare = 0.75f;

        /// <summary>What a film would have taken whole: a lighter one's time over the share of the pixels it drew, which a film's cost follows at the most.</summary>
        public static double AsWhole(double filmMs, bool lighter) => lighter ? filmMs / (MotionShare * MotionShare) : filmMs;

        /// <summary>
        /// Whether a film is made lighter: the camera moves, the stage is not already filmed fast,
        /// and its last film took, whole (<see cref="AsWhole"/>), at least <paramref name="fromMs"/>,
        /// or a <see cref="StayLighterShare"/> of it while it was lighter, so a stage quick to film
        /// stays sharp as it turns, only a slow one trades its edges for smoothness, and each film
        /// measures it again, so one slow film does not keep a quick stage soft.
        /// </summary>
        public static bool Lighter(bool cameraMoved, bool fast, double wholeMs, bool wasLighter, float fromMs) =>
            cameraMoved && !fast && fromMs < LighterNeverMs && wholeMs >= (wasLighter ? fromMs * StayLighterShare : fromMs);
    }
}
