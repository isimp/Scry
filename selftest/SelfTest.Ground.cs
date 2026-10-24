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
        /// the grass up to its feet, none kept off round it however wide it is, and so do a raid's
        /// crowd and a camp, whose own painted ground alone is clear. Each is pictured, and the
        /// backdrop, spin and ground put back.
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
            yield return GrassFor(small);
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
            yield return GrassFor(big);
            yield return new Wait(1);
            spread = Stage.GrassSpread;
            var size = Stage.SubjectSize;
            var footprint = Mathf.Max(size.x, size.z) / 2f;
            p.Check(spread.ShortOut == 0f && spread.Nearest < footprint, $"{big.Name} stands in the grass up to its feet, none short round it",
                $"short {Numbers.Fixed(spread.ShortOut, 1)} m round it, the nearest stem {Numbers.Fixed(spread.Nearest, 1)} m from its middle, half its widest side {Numbers.Fixed(footprint, 1)} m");
            yield return Picture(p, "ground big creature");

            // A raid's crowd stands in the grass as one creature does, and a camp in the meadow
            // between its tents, its grass cleared only where its own ground is painted.
            var raid = Pick(Kind.Raid, "army_eikthyr", "foresttrolls", "army_theelder");
            if (raid != null) yield return StandsInGrass(p, raid, "ground raid");
            var camp = LocationNamed("WoodFarm1", "WoodVillage1", "GoblinCamp2");
            if (camp != null)
            {
                Select(camp);
                yield return Until(() => CopyOf(camp) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
                yield return StandsInGrass(p, camp, "ground camp");
            }
            // A thing lying low in the meadow: the grass short at its feet, no bare circle round it.
            var lying = Pick(Kind.Item, "SwordIron", "AxeBronze", "Wood");
            if (lying != null)
            {
                Select(lying);
                yield return GrassFor(lying);
                yield return new Wait(1);
                var low = Stage.GrassSpread;
                p.Check(low.ShortOut >= 1.5f && low.Nearest < 0.5f, $"{lying.Name} lies in short grass, stems growing at its feet, no bare circle round it",
                    $"short {Numbers.Fixed(low.ShortOut, 1)} m round it, the nearest stem {Numbers.Fixed(low.Nearest, 2)} m from its middle");
                yield return Picture(p, "ground thing lying low");
            }
            GroundAsItWas();
        }

        /// <summary>One shown stands in the grass, none kept off round it and a stem within half its widest side of its middle; pictured.</summary>
        private static IEnumerator StandsInGrass(Probe p, Entry entry, string picture)
        {
            Select(entry);
            yield return GrassFor(entry);
            yield return new Wait(1);
            var spread = Stage.GrassSpread;
            var size = Stage.SubjectSize;
            var footprint = Mathf.Max(size.x, size.z) / 2f;
            p.Check(spread.ShortOut == 0f && spread.Nearest < footprint, $"{entry.Name} stands in the grass, none short round all of it",
                $"short {Numbers.Fixed(spread.ShortOut, 1)} m round it, the nearest stem {Numbers.Fixed(spread.Nearest, 1)} m from its middle, half its widest side {Numbers.Fixed(footprint, 1)} m");
            yield return Picture(p, picture);
        }

        /// <summary>Waits for an entry's copy, then for the grass scattered for it, a few milliseconds a frame, the last grass shown till then.</summary>
        private static IEnumerator GrassFor(Entry entry)
        {
            yield return Until(() => CopyOf(entry) != null && Stage.GroundShown == "Meadows", 15);
            yield return null;
            yield return null;
            yield return Until(() => !Stage.GrassScattering && Stage.GrassCount > 0, 10);
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
