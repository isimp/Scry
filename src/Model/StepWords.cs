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
        /// <summary>A footstep's note: "walk/run on stone", "any gait on any ground".</summary>
        public static string Note(IReadOnlyList<string> gaits, IReadOnlyList<string> grounds, bool anyGround)
        {
            var gait = gaits.Count > 0 ? string.Join("/", gaits.Select(g => g.ToLowerInvariant())) : "any gait";
            var ground = anyGround ? "any ground" : grounds.Count > 0 ? string.Join("/", grounds.Select(Ground)) : "nothing";
            return $"{gait} on {ground}";
        }

        private static string Ground(string name) => name == "Default" ? "plain ground" : name == "GenericGround" ? "ground" : name.ToLowerInvariant();
    }
}
