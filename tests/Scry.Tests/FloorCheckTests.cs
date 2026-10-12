using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    // The self-test shows places twice and, the second time, checks the floors told from what was
    // kept against rays cast over the same roll: each surface where the rays put it, give or take
    // the rays' own error of a millimetre or so, and each spawn point's creature on the same ground.
    public class FloorCheckTests
    {
        private static readonly IReadOnlyList<RayPart> Parts = new[] { new RayPart { Name = "floor", Solid = true }, new RayPart { Name = "platform", Solid = true } };
        private const int Floor = 0, Platform = 1;
        private static readonly ISet<int> Both = new HashSet<int> { Floor, Platform };

        /// <summary>A 2 m room probed once: every ray meeting these surfaces, and under each spawn point its own.</summary>
        private static FloorRecord Probed(RaySurface[] everyRay, Dictionary<string, RaySurface[]> spawns = null)
        {
            var seen = new ProbeSeen();
            seen.Present.UnionWith(Both);
            for (var ray = 0; ray < 25; ray++) seen.Rays[ray] = everyRay.ToList();
            if (spawns != null) foreach (var pair in spawns) seen.Spawns[pair.Key] = pair.Value.ToList();
            return FloorRecord.New("forestcrypt_Corridor1", true, "stamp", 0f, 2f, 0f, 2f, 6f, -2f, Parts).With(seen);
        }

        /// <summary>The same room, each ray meeting what <paramref name="ray"/> gives for its cell (i, j), five by five.</summary>
        private static FloorRecord Probed(System.Func<int, int, RaySurface[]> ray)
        {
            var seen = new ProbeSeen();
            seen.Present.UnionWith(Both);
            for (var at = 0; at < 25; at++) seen.Rays[at] = ray(at / 5, at % 5).ToList();
            return FloorRecord.New("forestcrypt_Corridor1", true, "stamp", 0f, 2f, 0f, 2f, 6f, -2f, Parts).With(seen);
        }

        private static RaySurface At(float height, int part) => new RaySurface { Height = height, Flat = true, Part = part };

        [Fact]
        public void FloorsWhereTheRaysPutThemWithinTheirErrorAreTheSame()
        {
            // 2.695 against 2.6949: one rounds to 2.7, the other to 2.69.
            var kept = Probed(new[] { At(2.695f, Platform), At(0f, Floor) });
            var rays = Probed(new[] { At(2.6949f, Platform), At(0.002f, Floor) });

            Assert.True(FloorCheck.Of(kept, rays, Both, new string[0]).Same);
        }

        [Fact]
        public void AFloorACoupleOfCentimetresOffIsToldWhereEachPutIt()
        {
            var kept = Probed(new[] { At(2.7f, Platform), At(0f, Floor) });
            var rays = Probed(new[] { At(2.72f, Platform), At(0f, Floor) });

            var check = FloorCheck.Of(kept, rays, Both, new string[0]);

            Assert.False(check.Same);
            Assert.Equal(25, check.OnlyKept.Count);
            Assert.Equal(25, check.OnlyRays.Count);
            Assert.Contains("0,0 at 2.7 open", check.OnlyKept);
            Assert.Contains("0,0 at 2.72 open", check.OnlyRays);
        }

        [Fact]
        public void AFloorOnlyOneOfThemFoundIsTold()
        {
            var kept = Probed(new[] { At(2.7f, Platform), At(0f, Floor) });
            var rays = Probed(new[] { At(0f, Floor) });

            var check = FloorCheck.Of(kept, rays, Both, new string[0]);

            // The platform only the record has, and the floor under it covered there, open by the rays.
            Assert.False(check.Same);
            Assert.Equal(50, check.OnlyKept.Count);
            Assert.Contains("0,0 at 2.7 open", check.OnlyKept);
            Assert.Contains("0,0 at 0", check.OnlyKept);
            Assert.Equal(25, check.OnlyRays.Count);
            Assert.All(check.OnlyRays, l => Assert.EndsWith(" at 0 open", l));
        }

        [Fact]
        public void AFloorAtTheSameHeightInOtherCellsIsTold()
        {
            // The platform along one side in the record, along the other by the rays.
            RaySurface[] Platform(bool there) => there ? new[] { At(2.7f, FloorCheckTests.Platform), At(0f, Floor) } : new[] { At(0f, Floor) };
            var acrossI = FloorCheck.Of(Probed((i, j) => Platform(i == 0)), Probed((i, j) => Platform(i == 4)), Both, new string[0]);
            var acrossJ = FloorCheck.Of(Probed((i, j) => Platform(j == 0)), Probed((i, j) => Platform(j == 4)), Both, new string[0]);

            Assert.Contains("0,1 at 2.7 open", acrossI.OnlyKept);
            Assert.Contains("4,1 at 2.7 open", acrossI.OnlyRays);
            Assert.Contains("1,0 at 2.7 open", acrossJ.OnlyKept);
            Assert.Contains("1,4 at 2.7 open", acrossJ.OnlyRays);
        }

        [Fact]
        public void AFloorOnlyTheRaysFoundIsNotTheSame()
        {
            // A floor under the one the record has, covered by it.
            var kept = Probed(new[] { At(0f, Floor) });
            var rays = Probed(new[] { At(0f, Floor), At(-1f, Platform) });

            var check = FloorCheck.Of(kept, rays, Both, new string[0]);

            Assert.Empty(check.OnlyKept);
            Assert.Equal(25, check.OnlyRays.Count);
            Assert.False(check.Same);
        }

        [Fact]
        public void AFloorOpenInOneAndCoveredInTheOtherIsTold()
        {
            // With the platform over it the floor is covered; without, open.
            var kept = Probed(new[] { At(0f, Floor) });
            var rays = Probed(new[] { At(2.7f, Platform), At(0f, Floor) });

            var check = FloorCheck.Of(kept, rays, new HashSet<int> { Floor, Platform }, new string[0]);

            Assert.Contains("0,0 at 0 open", check.OnlyKept);
            Assert.Contains("0,0 at 0", check.OnlyRays);
        }

        [Fact]
        public void ACreatureStandsOnTheSameGroundWithinTheRaysError()
        {
            var kept = Probed(new[] { At(0f, Floor) }, new Dictionary<string, RaySurface[]> { ["0.3"] = new[] { At(0.5f, Floor) } });
            var rays = Probed(new[] { At(0f, Floor) }, new Dictionary<string, RaySurface[]> { ["0.3"] = new[] { At(0.501f, Floor) } });

            Assert.True(FloorCheck.Of(kept, rays, Both, new[] { "0.3" }).Same);
        }

        [Fact]
        public void ACreatureOnGroundInOneAndOnNothingInTheOtherIsTold()
        {
            var kept = Probed(new[] { At(0f, Floor) }, new Dictionary<string, RaySurface[]> { ["0.3"] = new[] { At(0.5f, Floor) } });
            var rays = Probed(new[] { At(0f, Floor) }, new Dictionary<string, RaySurface[]> { ["0.3"] = new RaySurface[0] });

            var check = FloorCheck.Of(kept, rays, Both, new[] { "0.3" });
            var back = FloorCheck.Of(rays, kept, Both, new[] { "0.3" });

            Assert.False(check.Same);
            Assert.False(check.Minor);
            Assert.Equal(new[] { "spawn 0.3 on 0.5" }, check.OnlyKept);
            Assert.Equal(new[] { "spawn 0.3 on nothing" }, check.OnlyRays);
            Assert.Equal(new[] { "spawn 0.3 on nothing" }, back.OnlyKept);
            Assert.Equal(new[] { "spawn 0.3 on 0.5" }, back.OnlyRays);
        }

        [Fact]
        public void ACreatureACoupleOfCentimetresOffIsTold()
        {
            var kept = Probed(new[] { At(0f, Floor) }, new Dictionary<string, RaySurface[]> { ["0.3"] = new[] { At(0.5f, Floor) } });
            var rays = Probed(new[] { At(0f, Floor) }, new Dictionary<string, RaySurface[]> { ["0.3"] = new[] { At(0.52f, Floor) } });

            var check = FloorCheck.Of(kept, rays, Both, new[] { "0.3" });

            Assert.Equal(new[] { "spawn 0.3 on 0.5" }, check.OnlyKept);
            Assert.Equal(new[] { "spawn 0.3 on 0.52" }, check.OnlyRays);
        }

        // A ray steps two centimetres past each surface it meets, so a surface less than that
        // under another part, or under another room's collider beside the copy, goes unmet by it.
        // What a record keeps comes from whichever copy was probed, with other parts and rooms
        // about it, so such a surface is the same floor whichever of them found it.

        /// <summary>What each fresh ray met, on every ray: a part of the copy, or another copy's collider (-1).</summary>
        private static Dictionary<int, List<(float Height, int Part)>> Met(params (float Height, int Part)[] hits) =>
            Enumerable.Range(0, 25).ToDictionary(ray => ray, ray => hits.ToList());

        private const int Beside = -1;

        [Fact]
        public void AFloorWithinTwoCentimetresUnderAnotherPartTheRaysMetIsTheSame()
        {
            var kept = Probed(new[] { At(0.02f, Platform), At(0f, Floor) });
            var rays = Probed(new[] { At(0.02f, Platform) });

            Assert.True(FloorCheck.Of(kept, rays, Both, new string[0], Met((0.02f, Platform))).Same);
            Assert.False(FloorCheck.Of(kept, rays, Both, new string[0]).Same);
        }

        [Fact]
        public void AFloorUnderAnotherRoomsColliderTheRaysMetIsTheSame()
        {
            var kept = Probed(new[] { At(0f, Floor) });
            var rays = Probed(new RaySurface[0]);

            Assert.True(FloorCheck.Of(kept, rays, Both, new string[0], Met((0.01f, Beside))).Same);
            Assert.True(FloorCheck.Of(kept, rays, Both, new string[0], Met((0f, Beside))).Same);
        }

        [Fact]
        public void AFloorTheRecordHasUnderAnotherOfItsPartsIsTheSameAsTheRaysMissedIt()
        {
            var kept = Probed(new[] { At(0.02f, Platform) });
            var rays = Probed(new[] { At(0.02f, Platform), At(0f, Floor) });

            Assert.True(FloorCheck.Of(kept, rays, Both, new string[0], Met((0.02f, Platform), (0f, Floor))).Same);
        }

        [Fact]
        public void AFloorTheRecordMissedUnderAPartAnotherRollBroughtIsTheSame()
        {
            // The record kept the floor's part from a roll with the platform 2 cm over it there;
            // this roll has no platform, so its rays meet the floor the record never kept.
            var kept = Probed(new[] { At(0.02f, Platform) });
            var rays = Probed(new[] { At(0f, Floor) });

            Assert.True(FloorCheck.Of(kept, rays, new HashSet<int> { Floor }, new string[0], Met((0f, Floor))).Same);
        }

        [Fact]
        public void WhatIsMetHigherUpOrBelowOrIsTheFloorItselfHidesNothing()
        {
            var kept = Probed(new[] { At(0f, Floor) });
            var rays = Probed(new RaySurface[0]);

            Assert.False(FloorCheck.Of(kept, rays, Both, new string[0], Met((0.05f, Beside))).Same);
            Assert.False(FloorCheck.Of(kept, rays, Both, new string[0], Met((-0.01f, Beside))).Same);
            Assert.False(FloorCheck.Of(kept, rays, Both, new string[0], Met((0.01f, Floor))).Same);
        }

        [Fact]
        public void ACellOrTwoOfDifferenceIsMinorAndMoreIsNot()
        {
            // A ray meets a curved part's mesh, or misses it, by its last digit at a seam: a cell
            // or two apart is told, a part found in one and not the other differs across many.
            RaySurface[] Arch(bool there) => there ? new[] { At(2.7f, Platform), At(0f, Floor) } : new[] { At(0f, Floor) };
            var one = FloorCheck.Of(Probed((i, j) => Arch(i == 0 && j == 0)), Probed((i, j) => Arch(false)), Both, new string[0]);
            var many = FloorCheck.Of(Probed((i, j) => Arch(i == 0)), Probed((i, j) => Arch(false)), Both, new string[0]);

            Assert.False(one.Same);
            Assert.True(one.Minor, one.Told);
            Assert.False(many.Minor, many.Told);
        }

        [Fact]
        public void ADifferenceTellsHowManySurfacesItsFreshRayMet()
        {
            // A ray stops after its most surfaces: one that met as many may have stopped short.
            var kept = Probed(new[] { At(0f, Floor) });
            var rays = Probed(new RaySurface[0]);

            var check = FloorCheck.Of(kept, rays, Both, new string[0], Met((0.5f, Platform), (0.4f, Platform)));

            Assert.Contains("; its ray met 2", check.Told);
        }

        [Fact]
        public void WhatDifferedIsToldInALineNamingThePlace()
        {
            var kept = Probed(new[] { At(2.7f, Platform), At(0f, Floor) });
            var rays = Probed(new[] { At(0f, Floor) });

            Assert.Equal("forestcrypt_Corridor1: only kept 50 (0,0 at 2.7 open; 0,0 at 0; 0,1 at 2.7 open), only by rays 25 (0,0 at 0 open; 0,1 at 0 open; 0,2 at 0 open); parts platform, floor",
                FloorCheck.Of(kept, rays, Both, new string[0]).Told);
        }
    }
}
