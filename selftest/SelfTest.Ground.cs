using System.Collections;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for the grass on a biome's ground.</summary>
    internal static partial class SelfTest
    {
        private static (int Backdrop, bool Spin, string Choice)? _groundBefore;

        /// <summary>
        /// On a meadow with the sky, a small creature's grass grows past forty metres, out to where
        /// the ground has faded away and no farther, thinning as it goes; a big creature stands in
        /// the grass up to its feet, none kept off round it however wide it is. Each is pictured,
        /// and the backdrop, spin and ground put back.
        /// </summary>
        private static IEnumerator GroundGrass(Probe p)
        {
            var small = Pick(Kind.Creature, "Boar", "Deer");
            var big = Pick(Kind.Creature, "Dragon", "Troll");
            if (small == null || big == null) p.Skip("there are no creatures to stand on the ground");
            _groundBefore = (Stage.BackdropIndex, Stage.Spin, Stage.GroundChoice);
            Stage.Spin = false;
            Stage.GroundChoice = "Meadows";
            Stage.BackdropIndex = 5;

            Select(small);
            yield return Until(() => CopyOf(small) != null && Stage.GroundShown == "Meadows" && Stage.GrassCount > 0, 10);
            yield return new Wait(1);
            var spread = Stage.GrassSpread;
            var fadesAt = Mathf.Min(StageGround.GrassOut(spread.Reach * 2f), 80f);
            if (p.Check(Stage.GrassCount > 0, "a meadow grows grass", Stage.GroundShown ?? "no ground laid"))
            {
                p.Check(spread.Farthest > 40f && spread.Farthest <= fadesAt + 0.01f, "with the sky its grass grows past forty metres, out to where the ground fades away and no farther",
                    $"the farthest stem {Numbers.Fixed(spread.Farthest, 1)} m out, the ground faded away at {Numbers.Fixed(fadesAt, 1)} m, {Numbers.Count(Stage.GrassCount)} stems");
            }
            yield return Picture(p, "ground meadow");

            Select(big);
            yield return Until(() => CopyOf(big) != null && Stage.GrassCount > 0, 15);
            yield return new Wait(1);
            spread = Stage.GrassSpread;
            var size = Stage.SubjectSize;
            var footprint = Mathf.Max(size.x, size.z) / 2f;
            p.Check(spread.Cleared == 0f && spread.Nearest < footprint, $"{big.Name} stands in the grass up to its feet, none kept off round it",
                $"kept off {Numbers.Fixed(spread.Cleared, 1)} m round it, the nearest stem {Numbers.Fixed(spread.Nearest, 1)} m from its middle, half its widest side {Numbers.Fixed(footprint, 1)} m");
            yield return Picture(p, "ground big creature");
            GroundAsItWas();
        }

        /// <summary>The backdrop, spin and ground put back as they were before <see cref="GroundGrass"/>.</summary>
        private static void GroundAsItWas()
        {
            if (_groundBefore is (int backdrop, bool spin, string choice))
            {
                Stage.BackdropIndex = backdrop;
                Stage.Spin = spin;
                Stage.GroundChoice = choice;
            }
            _groundBefore = null;
        }
    }
}
