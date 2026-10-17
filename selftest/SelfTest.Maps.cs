using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for the maps of dungeons a player is in or has been in: the world's
    /// own dungeon read from its saved rooms and shown in place of an example.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// A rolled example, saved as the game saves a dungeon's rooms (each room's name's hash, its
        /// place and its turn as angles) and with a room of a kind not read among them, handed in as
        /// the world's own: it stands as saved, room by room and turned the same, under a tag at the
        /// head of the breadcrumb, the menu switching to an example and back.
        /// </summary>
        private static IEnumerator ThisDungeon(Probe p)
        {
            var entry = Crypt();
            if (entry == null) p.Skip("there is no such location");
            Select(entry);
            Stage.Inside = true;
            yield return Until(() => ExampleLayouts.Of(entry) && ExampleLayouts.Example != null && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleRoomsTotal > 0, 150);
            var rolled = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (!p.Check(rolled != null && !ExampleLayouts.ShowsWorlds, "an example has been laid out")) yield break;

            var saved = rolled.Rooms.Select(r =>
            {
                var angles = new Quaternion(r.Rotation.X, r.Rotation.Y, r.Rotation.Z, r.Rotation.W).eulerAngles;
                return (r.Room.Name.GetStableHashCode(), r.Position, new Vec3(angles.x, angles.y, angles.z));
            }).ToList();
            saved.Add(("not a room Scry has read".GetStableHashCode(), rolled.Rooms[0].Position, new Vec3(0f, 0f, 0f)));
            var world = new WorldDungeon { Saved = SavedRooms.Read(SavedRooms.Write(saved)), Generator = rolled.Site.Generator, Turn = rolled.Site.Turn };
            ExampleLayouts.StandInForTest(world);
            yield return Until(() => ExampleLayouts.ShowsWorlds && ExampleLayouts.Example != rolled && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
            var shown = ExampleLayouts.Example;
            if (!p.Check(ExampleLayouts.ShowsWorlds && shown != rolled, "the world's own dungeon is shown in place of the example"))
            {
                ThisDungeonAsItWas();
                yield break;
            }
            p.Check(ExampleLayouts.Unread == 1 && shown.Rooms.Count == rolled.Rooms.Count, "each room stands by its hash, the one of a kind not read left out and counted",
                $"{Numbers.Count(shown.Rooms.Count)} of {Numbers.Count(rolled.Rooms.Count)} rooms, {Numbers.Count(ExampleLayouts.Unread)} not read");

            var off = new List<string>();
            for (var i = 0; i < Mathf.Min(shown.Rooms.Count, rolled.Rooms.Count); i++)
            {
                var a = rolled.Rooms[i];
                var b = shown.Rooms[i];
                var moved = (b.Position - a.Position).Length;
                var turnedX = (b.Rotation * new Vec3(1f, 0f, 0f) - a.Rotation * new Vec3(1f, 0f, 0f)).Length;
                var turnedZ = (b.Rotation * new Vec3(0f, 0f, 1f) - a.Rotation * new Vec3(0f, 0f, 1f)).Length;
                if (a.Room != b.Room || moved > 0.01f || turnedX > 0.001f || turnedZ > 0.001f)
                    off.Add($"{a.Room.Name} as {b.Room.Name}, {Numbers.Fixed(moved, 3)} m off, turned {Numbers.Fixed(Mathf.Max(turnedX, turnedZ), 4)} off");
                else if (a.Room.EndCap && a.JoinedTo != null && (b.JoinedTo == null || b.JoinedTo.Room != a.JoinedTo.Room || (b.JoinedTo.Position - a.JoinedTo.Position).Length > 0.01f))
                    off.Add($"end cap {a.Room.Name} joined to {b.JoinedTo?.Room.Name ?? "nothing"}, not {a.JoinedTo.Room.Name}");
            }
            p.Check(off.Count == 0, "every room stands where it was saved, turned as it was, each end cap joined to the room it closes", string.Join("; ", off.Take(6)));
            p.Check(DungeonWays.Of(shown.Rooms).Entrance?.Room == rolled.Rooms[0].Room, "its way in is the room saved first", DungeonWays.Of(shown.Rooms).Entrance?.Room.Name ?? "none");

            // The breadcrumb's head says so; the menu switches to an example and back.
            var trails = ScryPanel.Drawn(PanelPart.Trail);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Trail) > trails + 1 && ScryPanel.TrailTaggedForTest, 5);
            if (p.Check(ScryPanel.TrailTaggedForTest, "a tag at the head of the breadcrumb says it is this dungeon")) yield return Picture(p, $"this dungeon {entry.Name} tagged");
            ScryPanel.ChooseTrailMenuForTest(2);
            yield return Until(() => !ExampleLayouts.ShowsWorlds && ExampleLayouts.Example != shown, 5);
            var example = ExampleLayouts.Example;
            trails = ScryPanel.Drawn(PanelPart.Trail);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Trail) > trails + 1, 5);
            p.Check(!ExampleLayouts.ShowsWorlds && example != shown && !ScryPanel.TrailTaggedForTest, "the menu's Show an example lays out an example, the tag gone");
            ScryPanel.ChooseTrailMenuForTest(2);
            yield return Until(() => ExampleLayouts.ShowsWorlds, 5);
            p.Check(ExampleLayouts.ShowsWorlds && ExampleLayouts.Example != example && ExampleLayouts.Example.Rooms.Count == shown.Rooms.Count, "and its Show this dungeon brings the world's own back");
            ThisDungeonAsItWas();
        }

        /// <summary>Ends the stand-in, an example shown again in place of it.</summary>
        private static void ThisDungeonAsItWas()
        {
            ExampleLayouts.ShowExample();
            ExampleLayouts.StandInForTest(null);
        }
    }
}
