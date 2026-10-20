using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// What a player's game lists of its walks of a world to the server on joining, a few bytes a
    /// dungeon: which, how many of its rooms were walked, and the game's day it was last walked
    /// (-1 where not known). The server answers with those it lacks, has fewer rooms of, or an
    /// older last day of (<see cref="Wanted"/>), none last walked before its records were
    /// forgotten, and the game sends just those, a batch to a message (<see cref="Batches"/>).
    /// </summary>
    internal static class WalkList
    {
        /// <summary>One walk as listed.</summary>
        internal readonly struct Walk
        {
            public Walk(string id, int walked, int lastDay)
            {
                Id = id;
                Walked = walked;
                LastDay = lastDay;
            }

            /// <summary>The dungeon, by its id (<see cref="DungeonMaps.PlaceIdOf"/>).</summary>
            public string Id { get; }

            /// <summary>How many of its rooms were walked.</summary>
            public int Walked { get; }

            /// <summary>The game's day it was last walked; -1 where not known.</summary>
            public int LastDay { get; }
        }

        private static readonly byte[] Mark = { (byte)'S', (byte)'C', (byte)'W', (byte)'L' };

        /// <summary>The most walks a list may hold, past which it is none of Scry's.</summary>
        private const int MostListed = 100000;

        /// <summary>How many walks go in one message.</summary>
        public const int BatchSize = 20;

        /// <summary>The walks of a world a game holds, each with a room walked at least, in the order kept; none outside a world.</summary>
        public static List<Walk> Of(DungeonMaps own, string world) =>
            own.OfWorld(world).Where(m => m.Map.Walked.Count > 0).Select(m => new Walk(m.Id, m.Map.Walked.Count, m.Map.LastDay)).ToList();

        /// <summary>The list as bytes: a mark, how many, then each walk.</summary>
        public static byte[] Write(IReadOnlyList<Walk> walks)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Mark);
                writer.Write(walks.Count);
                foreach (var walk in walks)
                {
                    writer.Write(walk.Id ?? "");
                    writer.Write(walk.Walked);
                    writer.Write(walk.LastDay);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>A list read back from <see cref="Write"/>; none from bytes that are not one whole, such as bytes cut short.</summary>
        public static List<Walk> Read(byte[] data)
        {
            if (data == null || data.Length < Mark.Length + 4) return null;
            return Steps.Run(() => ReadWhole(data), out var walks, null) == null ? walks : null;
        }

        private static List<Walk> ReadWhole(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
            {
                if (!reader.ReadBytes(Mark.Length).SequenceEqual(Mark)) return null;
                var count = reader.ReadInt32();
                if (count < 0 || count > MostListed) return null;
                var walks = new List<Walk>(Math.Min(count, 1024));
                for (var i = 0; i < count; i++) walks.Add(new Walk(reader.ReadString(), reader.ReadInt32(), reader.ReadInt32()));
                return reader.BaseStream.Position == data.Length ? walks : null;
            }
        }

        /// <summary>
        /// The walks listed that the server wants, in their order: those its records lack, have fewer
        /// rooms walked of, or an older last day of; none last walked before the day the world's
        /// records were forgotten on, a walk of no known day among them, so a forget sticks.
        /// </summary>
        public static List<string> Wanted(DungeonMaps records, IEnumerable<Walk> listed, int forgottenDay)
        {
            var wanted = new List<string>();
            foreach (var walk in listed)
            {
                if (string.IsNullOrEmpty(walk.Id) || forgottenDay >= 0 && walk.LastDay < forgottenDay) continue;
                var record = records.Of(walk.Id);
                if (record == null || record.Walked.Count < walk.Walked || walk.LastDay > record.LastDay) wanted.Add(walk.Id);
            }
            return wanted;
        }

        /// <summary>The maps of the walks wanted that the game holds, in batches of <see cref="BatchSize"/>, each a message's.</summary>
        public static IEnumerable<DungeonMaps> Batches(DungeonMaps own, IEnumerable<string> wanted)
        {
            var batch = new DungeonMaps();
            var count = 0;
            foreach (var id in wanted)
            {
                var map = own.Of(id);
                if (map == null) continue;
                var kept = batch.Keep(id, map.Location, map.Rooms, map.Entrance, map.Generator, map.Turn);
                foreach (var room in map.Walked) batch.Walk(id, room);
                kept.FirstDay = map.FirstDay;
                kept.LastDay = map.LastDay;
                if (++count < BatchSize) continue;
                yield return batch;
                batch = new DungeonMaps();
                count = 0;
            }
            if (count > 0) yield return batch;
        }
    }
}
