using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What a server keeps of the walks a player's game sends it: only a walk of a location the
    /// world places where the walk's id says, and of that location, with the rooms its map holds
    /// and no day later than the world's today. A made-up walk of a place the world holds nothing
    /// at, or of another location, is left out, so a game cannot light rooms nobody walked or grow
    /// the server's records without end.
    /// </summary>
    public class GroupWalksHeldTests
    {
        private static List<(int, Vec3, Vec3)> Rooms(int count) => Enumerable.Range(0, count).Select(i => (i, new Vec3(i, 0f, 0f), new Vec3(0f, 0f, 0f))).ToList();

        private static string Sent(DungeonMaps sent, string world, float x, float z, string location, int first, int last, params int[] walked)
        {
            var id = DungeonMaps.EntranceIdOf(world, x, z);
            var map = sent.Keep(id, location, Rooms(3), new Vec3(x, 30f, z), default, Quat.Identity);
            foreach (var room in walked) sent.Walk(id, room);
            map.FirstDay = first;
            map.LastDay = last;
            return id;
        }

        [Fact]
        public void TheServerKeepsAWalkOnlyOfALocationTheWorldPlacesThere()
        {
            var sent = new DungeonMaps();
            var real = Sent(sent, "w", 100f, 200f, "Crypt2", 3, 9, 1, 2);
            var nowhere = Sent(sent, "w", 5000f, 5000f, "Crypt2", 3, 9, 0);
            var another = Sent(sent, "w", 300f, 300f, "Crypt2", 3, 9, 0);
            var placed = new Dictionary<string, string> { [real] = "Crypt2", [another] = "TrollCave02" };

            var held = sent.HeldBy(placed, today: 50);

            Assert.Equal(new[] { real }, held.All.Select(m => m.Id));
            Assert.Equal(new[] { 1, 2 }, held.Of(real).Walked);
            Assert.Equal((3, 9), (held.Of(real).FirstDay, held.Of(real).LastDay));
            Assert.Null(held.Of(nowhere));
        }

        [Fact]
        public void AWalksDaysAreNoLaterThanTheWorldsToday()
        {
            var sent = new DungeonMaps();
            var id = Sent(sent, "w", 100f, 200f, "Crypt2", 80, 999, 0);
            var held = sent.HeldBy(new Dictionary<string, string> { [id] = "Crypt2" }, today: 50);
            Assert.Equal((50, 50), (held.Of(id).FirstDay, held.Of(id).LastDay));

            // Before the world's clock runs, the days are taken as sent.
            var early = sent.HeldBy(new Dictionary<string, string> { [id] = "Crypt2" }, today: -1);
            Assert.Equal((80, 999), (early.Of(id).FirstDay, early.Of(id).LastDay));
        }
    }
}
