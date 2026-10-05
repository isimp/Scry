using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The status effects the game's own code puts on you, which no prefab names: a bed gives
    /// Rested on waking in it (<c>Player.SetSleeping</c>); a fire's heat, an area of the Heat
    /// type (<c>EffectArea.CustomFixedUpdate</c>), makes you warm (the game's CampFire) and lets
    /// you rest, sitting or sheltered by it, as a seat by one does (<c>Player.UpdateEnvStatusEffects</c>).
    /// Each by the game's name for the effect, with how it is given, as the givers read from a
    /// prefab's fields are told.
    /// </summary>
    internal static class CodeGivers
    {
        /// <summary>How a status effect gives the one it names: Resting gives Rested once it has lasted its delay (<c>SE_Cozy.UpdateStatusEffect</c>).</summary>
        public const string AfterAWhile = "after a while";

        /// <summary>What a thing gives you by the game's code: a bed, a fire's heat, a seat.</summary>
        public static List<(string Effect, string How)> Of(bool bed, bool heat, bool seat)
        {
            var given = new List<(string, string)>();
            if (bed) given.Add(("Rested", "waking in it"));
            if (heat)
            {
                given.Add(("CampFire", "standing near it"));
                given.Add(("Resting", "sitting or sheltered near it"));
            }
            if (seat) given.Add(("Resting", "sitting in it near a fire"));
            return given;
        }
    }
}
