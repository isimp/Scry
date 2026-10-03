using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the notice under the panel's header says while part of Scry is off: a game update it
    /// does not know, or a part of it failing, needs a newer Scry either way, so it speaks of what
    /// is off rather than of the game.
    /// </summary>
    public static class OffWords
    {
        /// <summary>The notice's line, or null while nothing is off.</summary>
        public static string Line(IReadOnlyList<string> features)
        {
            if (features == null || features.Count == 0) return null;
            if (features.Count == 1)
            {
                var one = features[0];
                return char.ToUpperInvariant(one[0]) + one.Substring(1) + " is off until Scry is updated. Everything else works.";
            }
            var named = Naming.Joined(new List<string>(features));
            return $"{Numbers.Count(features.Count)} parts of Scry are off until it is updated: {named}. Everything else works.";
        }

        /// <summary>What the details card says above the list of what is off.</summary>
        public static string Details() =>
            "This version of Scry does not know something it relies on in this version of the game, or a part of it failed, so the parts below are off. " +
            "Everything else works as before. A newer Scry turns them back on; until then, BepInEx/LogOutput.log says what was not found, which helps when reporting it.";
    }
}
