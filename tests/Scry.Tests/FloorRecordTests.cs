using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FloorRecordTests
    {
        // Where a place's floors are is found by casting rays down over a copy of it, which needs
        // the copy awake: a dungeon room woken to be read costs a frame its size. So what the rays
        // met is kept for each kind of place, in its own frame, each surface with the part it is
        // on: any roll of the place whose parts a probe has seen tells its floors from the record,
        // a part only that roll brings, so far unseen, sends it to be probed again, and what that
        // probe sees is added. The floors told from a record are those its rays would find.

        // A room 2 m across: rays every half metre make a 5 x 5 grid.
        private static FloorRecord Empty(params (string Name, bool Solid)[] parts) =>
            FloorRecord.New("crypt2_Room_skull", true, "stamp", 0f, 2f, 0f, 2f, 6f, -2f, parts.Select(p => new RayPart { Name = p.Name, Solid = p.Solid }).ToList());

        private static readonly (string, bool)[] Parts = { ("floor", true), ("platform", true), ("cobweb", false), ("roof", true) };
        private const int Floor = 0, Platform = 1, Cobweb = 2, Roof = 3;

        private static RaySurface At(float height, int part, bool flat = true) => new RaySurface { Height = height, Flat = flat, Part = part };

        /// <summary>What a probe of one roll saw: every ray meeting these surfaces, top down, and under each spawn point its own.</summary>
        private static ProbeSeen Seen(int[] present, RaySurface[] everyRay, Dictionary<string, RaySurface[]> spawns = null)
        {
            var seen = new ProbeSeen();
            foreach (var part in present) seen.Present.Add(part);
            for (var ray = 0; ray < 25; ray++) seen.Rays[ray] = everyRay.ToList();
            if (spawns != null) foreach (var pair in spawns) seen.Spawns[pair.Key] = pair.Value.ToList();
            return seen;
        }

        private static ISet<int> Roll(params int[] present) => new HashSet<int>(present);

        [Fact]
        public void ARollWhosePartsWereSeenTellsItsFloorsFromTheRecord()
        {
            // Rolled with its platform: the platform at 2 m and the floor under it.
            var record = Empty(Parts).With(Seen(new[] { Floor, Platform }, new[] { At(2f, Platform), At(0f, Floor) }));

            Assert.True(record.Covers(Roll(Floor, Platform), new string[0]));
            var landings = record.Landings(Roll(Floor, Platform));
            Assert.Equal(50, landings.Count);
            Assert.All(landings.Where(l => l.Height == 2f), l => Assert.True(l.Open));
            Assert.All(landings.Where(l => l.Height == 0f), l => Assert.False(l.Open));
        }

        [Fact]
        public void ARollWithoutAPartTellsWhatIsLeftWithoutIt()
        {
            var record = Empty(Parts).With(Seen(new[] { Floor, Platform }, new[] { At(2f, Platform), At(0f, Floor) }));

            // Rolled without the platform: the floor alone, now with nothing over it.
            Assert.True(record.Covers(Roll(Floor), new string[0]));
            var landings = record.Landings(Roll(Floor));
            Assert.Equal(25, landings.Count);
            Assert.All(landings, l => Assert.Equal(0f, l.Height));
            Assert.All(landings, l => Assert.True(l.Open));
        }

        [Fact]
        public void ARollBringingAPartNeverSeenIsProbedAndWhatItSeesIsAdded()
        {
            var record = Empty(Parts).With(Seen(new[] { Floor }, new[] { At(0f, Floor) }));
            Assert.False(record.Covers(Roll(Floor, Roof), new string[0]));

            var more = record.With(Seen(new[] { Floor, Roof }, new[] { At(4f, Roof, flat: false), At(0f, Floor) }));
            Assert.True(more.Covers(Roll(Floor, Roof), new string[0]));
            Assert.True(more.Covers(Roll(Floor), new string[0]));

            // The floor once, not again for being seen twice; under the roof it is not open.
            var landings = more.Landings(Roll(Floor, Roof));
            Assert.Equal(25, landings.Count);
            Assert.All(landings, l => Assert.False(l.Open));
            // The record it was added to holds what it held.
            Assert.False(record.Covers(Roll(Floor, Roof), new string[0]));
        }

        [Fact]
        public void WhatLeansIsNoFloorButCoversWhatIsUnderIt()
        {
            var record = Empty(Parts).With(Seen(new[] { Floor, Roof }, new[] { At(3f, Roof, flat: false), At(0f, Floor) }));
            var landings = record.Landings(Roll(Floor, Roof));
            Assert.All(landings, l => Assert.Equal(0f, l.Height));
            Assert.All(landings, l => Assert.False(l.Open));
        }

        [Fact]
        public void UnderARoofAFloorWithARugOnItIsCoveredLikeTheRug()
        {
            // Whether a surface is open is told by the topmost thing on its ray, not by what lies on it.
            var record = Empty(Parts).With(Seen(new[] { Floor, Platform, Roof }, new[] { At(4f, Roof, flat: false), At(0.1f, Platform), At(0f, Floor) }));
            var landings = record.Landings(Roll(Floor, Platform, Roof));
            Assert.Equal(50, landings.Count);
            Assert.All(landings, l => Assert.False(l.Open));
        }

        [Fact]
        public void EachLandingKnowsHowFarOverItItsCeilingIs()
        {
            // A roof 4 m up over a floor with a rug on it: the rug is too near to hide the floor,
            // the roof hides both; under the open sky nothing does.
            var record = Empty(Parts).With(Seen(new[] { Floor, Platform, Roof }, new[] { At(4f, Roof, flat: false), At(0.02f, Platform), At(0f, Floor) }));
            var landings = record.Landings(Roll(Floor, Platform, Roof));
            Assert.All(landings.Where(l => l.Height == 0f), l => Assert.Equal(4f, l.Over, 3));
            Assert.All(landings.Where(l => l.Height == 0.02f), l => Assert.Equal(3.98f, l.Over, 3));
            Assert.All(record.Landings(Roll(Floor, Platform)), l => Assert.Equal(0f, l.Over));

            // Under a platform 2 m up and the roof over it, the nearer hides it.
            var under = Empty(Parts).With(Seen(new[] { Floor, Platform, Roof }, new[] { At(4f, Roof, flat: false), At(2f, Platform), At(0f, Floor) }));
            Assert.All(under.Landings(Roll(Floor, Platform, Roof)).Where(l => l.Height == 0f), l => Assert.Equal(2f, l.Over, 3));
        }

        // A cabin's wall of half-metre logs under its roof: rolled with the roof or without it.
        private static readonly (string, bool)[] Cabin = { ("log", true), ("top log", true), ("roof", true), ("rug", true) };
        private const int Log = 0, TopLog = 1, CabinRoof = 2, Rug = 3;

        [Fact]
        public void WhatARayPassesJustUnderAPartIsToldOnlyInARollWithoutThatPart()
        {
            // A ray goes on two centimetres past each surface it meets, so it passes over the top
            // log 7 mm under the roof resting on it. A probe keeps what its rays pass over too.
            var record = Empty(Cabin).With(Seen(new[] { Log, TopLog, CabinRoof }, new[] { At(2.507f, CabinRoof, flat: false), At(2.5f, TopLog), At(2f, Log) }));

            // With the roof the ray meets the roof and the log under the top one, as its rays do.
            var roofed = Roll(Log, TopLog, CabinRoof);
            Assert.Equal(new[] { 2.507f, 2f }, record.On(0, roofed).Select(s => s.Height));
            Assert.All(record.Landings(roofed), l => Assert.Equal(2f, l.Height));
            Assert.All(record.Landings(roofed), l => Assert.False(l.Open));
            Assert.All(record.Landings(roofed), l => Assert.Equal(0f, l.Over));

            // Without it the top log is met, open, and the log under it covered.
            var bare = Roll(Log, TopLog);
            Assert.Equal(new[] { 2.5f, 2f }, record.On(0, bare).Select(s => s.Height));
            var landings = record.Landings(bare);
            Assert.Equal(50, landings.Count);
            Assert.All(landings.Where(l => l.Part == TopLog), l => Assert.True(l.Open));
            Assert.All(landings.Where(l => l.Part == Log), l => Assert.False(l.Open));
        }

        [Fact]
        public void WhatLiesMoreThanARaysStepUnderAPartIsMetUnderIt()
        {
            // A rug 3 cm under a table top is met under it; a board 1 cm under the rug is passed,
            // as the ray steps past the rug it met, not past the table top.
            var record = Empty(Cabin).With(Seen(new[] { Log, Rug, TopLog }, new[] { At(0.53f, Log), At(0.5f, Rug), At(0.49f, TopLog) }));
            Assert.Equal(new[] { 0.53f, 0.5f }, record.On(0, Roll(Log, Rug, TopLog)).Select(s => s.Height));
            Assert.Equal(50, record.Landings(Roll(Log, Rug, TopLog)).Count);
        }

        [Theory]
        [InlineData(2.5f, 2f)]
        [InlineData(2.4999f, 2.0001f)]
        [InlineData(2.5002f, 1.9999f)]
        [InlineData(2.4996f, 2f)]
        public void ALogRightUnderTheTopOneIsCoveredHoweverItsHeightsLastDigitsFall(float top, float under)
        {
            // A wall's logs are half a metre high, as far as a surface must be under the topmost
            // on its ray to be covered: the log under the top one is covered whichever way the
            // rays' error falls, and as kept to the millimetre.
            var record = Empty(Cabin).With(Seen(new[] { Log, TopLog }, new[] { At(top, TopLog), At(under, Log) }));
            foreach (var told in new[] { record, FloorRecord.Load(record.Save()) })
            {
                var landings = told.Landings(Roll(Log, TopLog));
                Assert.Equal(50, landings.Count);
                Assert.All(landings.Where(l => l.Part == TopLog), l => Assert.True(l.Open));
                Assert.All(landings.Where(l => l.Part == Log), l => Assert.False(l.Open));
            }
        }

        [Fact]
        public void WhatStandsLessThanHalfAMetreOverAFloorLeavesItOpen()
        {
            // A bench 48 cm high.
            var record = Empty(Cabin).With(Seen(new[] { Log, Rug }, new[] { At(0.48f, Rug), At(0f, Log) }));
            Assert.All(record.Landings(Roll(Log, Rug)), l => Assert.True(l.Open));
        }

        [Fact]
        public void ARayKeepsWhatItPassesJustUnderTheSurfaceItMeets()
        {
            // One cast hits each collider under it once. By how far along each hit is and its part:
            // the log, a rug 15 mm under the roof, the top log 7 mm under it, the roof, another
            // room's floor beside the copy (-1), and the roof's second collider.
            var hits = new List<(float Distance, int Part)> { (1f, Log), (0.515f, Rug), (0.507f, TopLog), (0.5f, CabinRoof), (0.49f, -1), (0.51f, CabinRoof) };
            var kept = new List<int> { 9 };
            FloorRecord.StepKeeps(hits, kept);

            // The roof is met; the top log and the rug, passed, kept after it nearest first.
            Assert.Equal(new[] { 3, 2, 1 }, kept);

            FloorRecord.StepKeeps(new List<(float, int)> { (0.3f, -1) }, kept);
            Assert.Empty(kept);
        }

        [Fact]
        public void APlacesTopIsTheHighestItsRaysMetOfTheRoll()
        {
            var record = Empty(Parts).With(Seen(new[] { Floor, Platform, Roof }, new[] { At(4f, Roof, flat: false), At(2f, Platform), At(0f, Floor) }));
            Assert.Equal(4f, record.Highest(Roll(Floor, Platform, Roof)));
            Assert.Equal(2f, record.Highest(Roll(Floor, Platform)));
            Assert.True(float.IsNaN(record.Highest(Roll(Cobweb))));

            // Over a place whose rays meet it at different heights, the highest of them all.
            var tower = new ProbeSeen();
            tower.Present.UnionWith(new[] { Floor, Roof });
            tower.Rays[0] = new List<RaySurface> { At(9f, Roof, flat: false), At(0f, Floor) };
            tower.Rays[1] = new List<RaySurface> { At(3f, Roof, flat: false), At(0f, Floor) };
            Assert.Equal(9f, Empty(Parts).With(tower).Highest(Roll(Floor, Roof)));
        }

        [Fact]
        public void LandingsKeepTheirPlaceOnTheGridAndItsGround()
        {
            var record = Empty(Parts).With(Seen(new[] { Floor }, new[] { At(0f, Floor) }));
            var landings = record.Landings(Roll(Floor));
            Assert.Equal(record.Grid.Select(g => (g.I, g.J, g.X, g.Z)), landings.Select(l => (l.I, l.J, l.X, l.Z)));
            Assert.Equal(FloorFinder.Grid(0f, 2f, 0f, 2f).Select(g => (g.I, g.J)), record.Grid.Select(g => (g.I, g.J)));
            Assert.Equal(FloorFinder.CellArea(0f, 2f, 0f, 2f, 25), record.Cell);
            Assert.Equal(25 * record.Cell, record.Ground);
        }

        [Fact]
        public void RaysGoBesideTheLinesARoomIsBuiltOn()
        {
            // A room's walls and floors stand on half-metre lines from its edge. A ray right on an
            // edge meets or misses it by the last digit of where the copy stands, so one copy's
            // rays found a wall's top that another's did not: the rays go a little beside them.
            var record = FloorRecord.New("forestcrypt_Corridor3", true, "stamp", 0f, 8f, -2f, 2f, 6f, -2f, new[] { new RayPart { Name = "stonewall" } });
            bool OffTheLines(float along)
            {
                var past = along % 0.5f;
                return past >= 0.01f && past <= 0.49f;
            }

            Assert.Equal(17 * 9, record.Grid.Count);
            foreach (var (i, j, x, z) in record.Grid)
            {
                Assert.InRange(x, 0f, 8f);
                Assert.InRange(z, -2f, 2f);
                Assert.True(OffTheLines(x) && OffTheLines(z + 2f), $"{i},{j} at {x}, {z}");
            }
        }

        [Fact]
        public void ACreatureStandsOnTheFirstSolidPartUnderItThatTheRollBrings()
        {
            var under = new Dictionary<string, RaySurface[]> { ["4.0"] = new[] { At(1.5f, Cobweb), At(1f, Platform), At(0f, Floor) } };
            var record = Empty(Parts).With(Seen(new[] { Floor, Platform, Cobweb }, new[] { At(0f, Floor) }, under));

            // A cobweb is not solid to what drops: the platform under it is.
            Assert.True(record.GroundUnder("4.0", Roll(Floor, Platform, Cobweb), out var ground));
            Assert.Equal(1f, ground);
            Assert.True(record.GroundUnder("4.0", Roll(Floor, Cobweb), out ground));
            Assert.Equal(0f, ground);
            Assert.False(record.GroundUnder("4.0", Roll(Cobweb), out _));
            Assert.False(record.GroundUnder("9.9", Roll(Floor), out _));
        }

        [Fact]
        public void ASpawnPointIsCoveredOnlyWithThePartsItWasProbedWith()
        {
            var under = new Dictionary<string, RaySurface[]> { ["4.0"] = new[] { At(0f, Floor) } };
            var record = Empty(Parts)
                .With(Seen(new[] { Floor }, new[] { At(0f, Floor) }, under))
                .With(Seen(new[] { Floor, Platform }, new[] { At(2f, Platform), At(0f, Floor) }));

            // Seen with the floor alone; never with the platform that may stand over it.
            Assert.True(record.Covers(Roll(Floor), new[] { "4.0" }));
            Assert.False(record.Covers(Roll(Floor, Platform), new[] { "4.0" }));
            Assert.False(record.Covers(Roll(Floor), new[] { "5.1" }));

            // Probed again with the platform there, it is covered with it too.
            var again = record.With(Seen(new[] { Floor, Platform }, new[] { At(2f, Platform), At(0f, Floor) }, new Dictionary<string, RaySurface[]> { ["4.0"] = new[] { At(2f, Platform), At(0f, Floor) } }));
            Assert.True(again.Covers(Roll(Floor, Platform), new[] { "4.0" }));
            Assert.True(again.GroundUnder("4.0", Roll(Floor, Platform), out var ground));
            Assert.Equal(2f, ground);
        }

        [Fact]
        public void ARecordTellsWhetherAPlaceStillHasItsParts()
        {
            var record = Empty(Parts);
            Assert.True(record.Fits(Parts.Select(p => new RayPart { Name = p.Item1, Solid = p.Item2 }).ToList()));
            Assert.False(record.Fits(Parts.Take(3).Select(p => new RayPart { Name = p.Item1, Solid = p.Item2 }).ToList()));
            Assert.False(record.Fits(Parts.Select(p => new RayPart { Name = p.Item1 == "roof" ? "tower" : p.Item1, Solid = p.Item2 }).ToList()));
            Assert.False(record.Fits(Parts.Select(p => new RayPart { Name = p.Item1, Solid = !p.Item2 }).ToList()));
        }

        [Fact]
        public void WhatWasKeptOfAPlacesRaysComesBackWholeTheNextSession()
        {
            var under = new Dictionary<string, RaySurface[]> { ["4.0"] = new[] { At(1.5f, Cobweb), At(0.125f, Floor) } };
            var record = Empty(Parts)
                .With(Seen(new[] { Floor, Cobweb }, new[] { At(0.125f, Floor) }, under))
                .With(Seen(new[] { Floor, Roof }, new[] { At(4f, Roof, flat: false), At(0.125f, Floor) }));
            var text = record.Save();
            var back = FloorRecord.Load(text);

            Assert.NotNull(back);
            Assert.Equal(("crypt2_Room_skull", true, "stamp"), (back.Prefab, back.Room, back.Stamp));
            Assert.Equal((0f, 2f, 0f, 2f, 6f, -2f), (back.MinX, back.MaxX, back.MinZ, back.MaxZ, back.Top, back.Bottom));
            Assert.Equal(new[] { "floor", "platform", "cobweb", "roof" }, back.Parts.Select(p => p.Name));
            Assert.False(back.Parts[Cobweb].Solid);
            Assert.True(back.Covers(Roll(Floor, Roof), new string[0]));
            Assert.False(back.Covers(Roll(Floor, Platform), new string[0]));
            Assert.True(back.Covers(Roll(Floor, Cobweb), new[] { "4.0" }));
            Assert.False(back.Covers(Roll(Floor, Roof), new[] { "4.0" }));
            Assert.Equal(record.Landings(Roll(Floor, Roof)).Select(l => (l.I, l.J, l.Height, l.Open)), back.Landings(Roll(Floor, Roof)).Select(l => (l.I, l.J, l.Height, l.Open)));
            Assert.True(back.GroundUnder("4.0", Roll(Floor, Cobweb), out var ground));
            Assert.Equal(0.125f, ground);
            Assert.Equal(text, back.Save());
        }

        [Fact]
        public void EachKindOfPlaceIsKeptInAFileOfItsOwnWhateverItsName()
        {
            Assert.Equal(FloorRecord.FileName("Crypt2", false), FloorRecord.FileName("Crypt2", false));
            Assert.NotEqual(FloorRecord.FileName("Crypt2", false), FloorRecord.FileName("Crypt2", true));
            Assert.NotEqual(FloorRecord.FileName("Crypt2", false), FloorRecord.FileName("Crypt3", false));
            var odd = FloorRecord.FileName("a/b:c*?\"<>|\t", true);
            Assert.EndsWith(".dat", odd);
            Assert.True(odd.All(c => char.IsLetterOrDigit(c) || c == '.'), odd);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Scry places 1\n")]
        [InlineData("Scry floors 999\nplace\tx\t0\ts\nend\n")]
        public void AFileOfAnotherKindOrNoneGivesNoRecord(string text)
        {
            Assert.Null(FloorRecord.Load(text));
        }

        [Fact]
        public void ARecordKeptBeforeRaysKeptWhatTheyPassIsProbedAfresh()
        {
            // Such a record lacks what its rays passed just under another part.
            var text = Empty(Parts).With(Seen(new[] { Floor }, new[] { At(0f, Floor) })).Save();
            Assert.NotNull(FloorRecord.Load(text));
            Assert.Null(FloorRecord.Load("Scry floors 1" + text.Substring(text.IndexOf('\n'))));
        }

        [Fact]
        public void ARecordCutShortOrWithALineThatCannotBeReadGivesNone()
        {
            var text = Empty(Parts).With(Seen(new[] { Floor }, new[] { At(0f, Floor) })).Save();
            Assert.NotNull(FloorRecord.Load(text));
            Assert.Null(FloorRecord.Load(text.Substring(0, text.IndexOf("end", System.StringComparison.Ordinal))));
            Assert.Null(FloorRecord.Load(text.Replace("ray\t3\t", "ray\tthree\t")));
            Assert.Null(FloorRecord.Load(text.Replace("ray\t3\t0:1:0", "ray\t3\t0:1:9")));
            Assert.Null(FloorRecord.Load(text.Replace("ray\t3\t", "ray\t25\t")));
            Assert.Null(FloorRecord.Load(string.Join("\n", text.Split('\n').Where(l => !l.StartsWith("grid\t", System.StringComparison.Ordinal) && !l.StartsWith("ray\t", System.StringComparison.Ordinal)))));
            Assert.Null(FloorRecord.Load(string.Join("\n", text.Split('\n').Where(l => !l.StartsWith("place\t", System.StringComparison.Ordinal)))));
        }
    }
}
