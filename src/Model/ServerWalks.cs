using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What anyone on this server walked inside the dungeons each source of the world holds, for
    /// what a source shows with insides (<see cref="SourceLevel.WithInsides"/>): the kinds of room
    /// stood in, by the hash the game saves a room under, and the kinds of place one of whose
    /// dungeons was walked through whole, its end caps and dividers left out, as no one stands in
    /// them, and a room of a kind not read counted as one. A walk counts only for the sources
    /// whose dungeons it was in.
    /// </summary>
    internal sealed class ServerWalks
    {
        /// <summary>No walks at all, as before any are known.</summary>
        public static readonly ServerWalks None = new ServerWalks(null);

        private readonly Dictionary<WorldSource, HashSet<int>> _rooms = new Dictionary<WorldSource, HashSet<int>>();
        private readonly Dictionary<WorldSource, HashSet<string>> _whole = new Dictionary<WorldSource, HashSet<string>>();
        private readonly Func<string, int> _hashOf;

        private ServerWalks(Func<string, int> hashOf) => _hashOf = hashOf;

        /// <summary>The walks in the dungeons each source holds, a room's kind told by its shape where read (<see cref="DungeonMaps.Walked"/>) and named by its hash.</summary>
        public static ServerWalks Of(Func<WorldSource, IEnumerable<DungeonMap>> dungeons, Func<int, RoomShape> roomOf, Func<string, int> hashOf)
        {
            var walks = new ServerWalks(hashOf);
            foreach (var source in WorldSources.All)
            {
                foreach (var map in dungeons(source))
                {
                    if (map?.Rooms == null) continue;
                    foreach (var at in map.Walked)
                    {
                        if (at < 0 || at >= map.Rooms.Count) continue;
                        if (!walks._rooms.TryGetValue(source, out var rooms)) walks._rooms[source] = rooms = new HashSet<int>();
                        rooms.Add(map.Rooms[at].Hash);
                    }
                    var (walked, count) = DungeonMaps.Walked(map, roomOf);
                    if (count == 0 || walked < count || map.Location == null) continue;
                    if (!walks._whole.TryGetValue(source, out var whole)) walks._whole[source] = whole = new HashSet<string>(StringComparer.Ordinal);
                    whole.Add(map.Location);
                }
            }
            return walks;
        }

        /// <summary>Whether a kind of room, by its prefab's name, was walked in a dungeon the source holds.</summary>
        public bool Walked(WorldSource source, string room) =>
            _hashOf != null && room != null && _rooms.TryGetValue(source, out var rooms) && rooms.Contains(_hashOf(room));

        /// <summary>Whether a dungeon of a kind of place, by its prefab's name, the source holds was walked through whole.</summary>
        public bool Whole(WorldSource source, string location) =>
            location != null && _whole.TryGetValue(source, out var whole) && whole.Contains(location);

        /// <summary>Whether other walks hold the same kinds of room walked and places walked whole for every source, so what they show is the same.</summary>
        public bool SameAs(ServerWalks other) => other != null && Same(_rooms, other._rooms) && Same(_whole, other._whole);

        private static bool Same<T>(Dictionary<WorldSource, HashSet<T>> a, Dictionary<WorldSource, HashSet<T>> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var pair in a) if (!b.TryGetValue(pair.Key, out var other) || !pair.Value.SetEquals(other)) return false;
            return true;
        }
    }
}
