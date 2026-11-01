using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A creature's footsteps in words (<c>FootStep</c>): how it moves, by the game's names of its
    /// gaits, on what ground, by the game's names of its grounds, its plain one "plain ground".
    /// </summary>
    internal static class StepWords
    {
        /// <summary>A footstep's note: "walk/run on stone", "walk on any ground", or that it never plays.</summary>
        public static string Note(IReadOnlyList<string> gaits, IReadOnlyList<string> grounds, bool anyGround)
        {
            // A step set for no gait or no ground matches no footfall (FootStep.FindBestStepEffect), so it never plays.
            if (gaits.Count == 0) return "set for no gait, so it never plays";
            var gait = string.Join("/", gaits.Select(g => g.ToLowerInvariant()));
            if (!anyGround && grounds.Count == 0) return $"{gait} on no ground, so it never plays";
            var ground = anyGround ? "any ground" : string.Join("/", grounds.Select(Ground));
            return $"{gait} on {ground}";
        }

        private static string Ground(string name) => name == "Default" ? "plain ground" : name == "GenericGround" ? "ground" : name.ToLowerInvariant();
    }
}
