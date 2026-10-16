using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>The places whose rooms climb with no real floor: The Hole's tubes and the sunken crypts' stairs slope; the caves' holes are shafts.</summary>
        private static readonly string[] ClimbingPlaces = { "TheHole01", "SunkenCrypt4", "MountainCave02", "Hildir_cave" };

        /// <summary>
        /// A room gone into that has no real floor and climbs more than a storey between its
        /// doorways is cut along its slope, a head's height over the way through it, its outline's
        /// top following the slope; one that climbs as a shaft is cut flat over its top. For each
        /// place with such rooms, one of each kind is gone into from above: the cut is laid as
        /// expected, and how many of its parts reach under the cut is told, at least a quarter of
        /// them for a room shown rather than cut away. Pictured.
        /// </summary>
        private static IEnumerator SlopeCutShows(Probe p)
        {
            var places = ClimbingPlaces.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).Where(e => e != null).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", ClimbingPlaces) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            BigStage();
            var sloped = 0;
            var shafts = 0;
            var unlaid = new List<string>();
            var cutAway = new List<string>();
            foreach (var entry in places)
            {
                Select(entry);
                yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0, 60);
                var ways = Stage.ExampleWays;
                if (CopyOf(entry) == null || ways == null)
                {
                    p.Note($"{entry.Name}: no example stood on the stage");
                    continue;
                }
                Stage.SetCutFromSide(false);
                Stage.OpenLevel(Stage.FloorHeights.Count);

                var slope = ways.Rooms.FirstOrDefault(r => Stage.ExampleRoomSlope(r) is RoomCutPlane c && c.Sloping);
                var shaft = ways.Rooms.FirstOrDefault(r => Stage.ExampleRoomSlope(r) is RoomCutPlane c && !c.Sloping);
                if (slope == null && shaft == null) p.Note($"{entry.Name}: no room of this layout climbs with no real floor");
                foreach (var room in new[] { slope, shaft })
                {
                    if (room == null) continue;
                    var plane = Stage.ExampleRoomSlope(room).Value;
                    ScryPanel.ClickRoomForTest(X, room);
                    yield return Until(() => Stage.RoomGoneInto == room && Stage.ExampleRoomsKept, 5);
                    yield return Until(() => !Stage.Gliding, 5);
                    var name = $"{entry.Name}'s {room.Room.Name}";
                    var expected = Stage.RoomCut(room);
                    var laid = Stage.RoomGoneInto == room && Stage.Cutting && Math.Abs(Stage.CutAt - expected) < 0.01f && Stage.SlopeCutLaid == plane.Sloping;
                    if (!laid) unlaid.Add($"{name}: {(plane.Sloping ? "a slope" : "a shaft")}, cut at {Numbers.Fixed(Stage.CutAt, 2)} m, not {Numbers.Fixed(expected, 2)}, sloping laid {Stage.SlopeCutLaid}");
                    var (under, all) = Stage.ExampleRoomPartsUnderCut(room);
                    if (all > 0 && under * 4 < all) cutAway.Add($"{name}: {Numbers.Count(under)} of {Numbers.Count(all)} parts");
                    if (plane.Sloping) sloped++;
                    else shafts++;
                    p.Note($"{name}: {(plane.Sloping ? $"cut along its slope from {Numbers.Fixed(plane.Low.Y, 1)} to {Numbers.Fixed(plane.High.Y, 1)} m" : $"a shaft, cut flat at {Numbers.Fixed(plane.High.Y, 1)} m")}, its ground {Numbers.Fixed(Stage.ExampleRoomGround(room), 1)} m; {Numbers.Count(under)} of {Numbers.Count(all)} of its parts reach under the cut");
                    yield return Picture(p, $"slope {entry.Name} {(plane.Sloping ? "sloping room" : "shaft")}");
                    Stage.ComeOut();
                    yield return Until(() => Stage.RoomGoneInto == null && Stage.ExampleRoomsKept, 5);
                }
            }
            Stage.Inside = wasInside;
            StageAsItWas();
            if (sloped + shafts == 0) p.Skip("no layout shown held a room that climbs with no real floor");
            p.Check(unlaid.Count == 0, "a room that climbs with no real floor is cut along its slope, a shaft flat over its top", string.Join("; ", unlaid));
            p.Check(cutAway.Count == 0, "so cut, at least a quarter of the room's parts reach under the cut", string.Join("; ", cutAway));
        }
    }
}
