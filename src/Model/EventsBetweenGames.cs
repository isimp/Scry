using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Scry
{
    /// <summary>
    /// The players whose games run Scry, as each said hello on joining with the version of its
    /// messages (<see cref="Messages"/>): only those of the same messages count as having Scry;
    /// one of other messages is told apart, as it needs updating.
    /// </summary>
    internal sealed class ScryPeers
    {
        /// <summary>The version of the events' messages and the hello; another counts as no Scry, to be updated.</summary>
        public const int Messages = 2;

        /// <summary>The messages each player's game said hello with, by its session.</summary>
        private readonly Dictionary<long, int> _hellos = new Dictionary<long, int>();

        /// <summary>Each player in the world by its session: their name, and when first seen there.</summary>
        private readonly Dictionary<long, (string Name, double Since)> _inWorld = new Dictionary<long, (string, double)>();

        /// <summary>Counts up whenever who has Scry changes, for what is told from it to tell again.</summary>
        public int Version { get; private set; }

        /// <summary>A player's game said hello with its messages.</summary>
        public void Hello(long session, int messages)
        {
            if (_hellos.TryGetValue(session, out var had) && had == messages) return;
            _hellos[session] = messages;
            Version++;
        }

        /// <summary>Whether a player's game runs Scry of the same messages.</summary>
        public bool Has(long session) => _hellos.TryGetValue(session, out var messages) && messages == Messages;

        /// <summary>Whether a player's game runs Scry of other messages.</summary>
        public bool Outdated(long session) => _hellos.TryGetValue(session, out var messages) && messages != Messages;

        /// <summary>How many seconds a player is in the world before a missing hello says their game has no Scry: it may still be on its way.</summary>
        public const double HelloWait = 10.0;

        /// <summary>
        /// The players in the world now, by session and name, at a time in seconds, and how many
        /// more the game lists without a character, as one rising again after a death or still
        /// coming in: those new are noted from then; those missing are forgotten, hello and all,
        /// once every player listed is accounted for, as one without a character may be one of
        /// them. A hello heard before its player shows in the world is kept, as the two travel apart.
        /// </summary>
        public void InWorld(IEnumerable<(long Session, string Name)> players, int withoutCharacter, double now)
        {
            var kept = new HashSet<long>();
            foreach (var (session, name) in players)
            {
                kept.Add(session);
                _inWorld[session] = (name, _inWorld.TryGetValue(session, out var had) ? had.Since : now);
            }
            if (withoutCharacter > 0) return;
            var gone = new List<long>();
            foreach (var session in _inWorld.Keys) if (!kept.Contains(session)) gone.Add(session);
            foreach (var session in gone)
            {
                _inWorld.Remove(session);
                if (_hellos.Remove(session)) Version++;
            }
        }

        /// <summary>On leaving a world: no one is known to play with Scry.</summary>
        public void Forget()
        {
            if (_hellos.Count > 0) Version++;
            _hellos.Clear();
            _inWorld.Clear();
        }

        /// <summary>
        /// The players played with (<see cref="InWorld"/>), by name, and whether each one's game
        /// has Scry: never the character itself, nor one without a hello that came into the world
        /// under <see cref="HelloWait"/> ago.
        /// </summary>
        public List<(string Name, PeerScry Scry)> PlayingWith(long me, double now)
        {
            var with = new List<(string Name, PeerScry Scry)>();
            foreach (var pair in _inWorld)
            {
                if (pair.Key == me) continue;
                var (name, since) = pair.Value;
                if (_hellos.TryGetValue(pair.Key, out var messages)) with.Add((name, messages == Messages ? PeerScry.Scry : PeerScry.Outdated));
                else if (now - since >= HelloWait) with.Add((name, PeerScry.None));
            }
            with.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
            return with;
        }
    }

    /// <summary>
    /// An event as its message, to and from bytes (<see cref="WorldEvent"/>): a mark, the messages'
    /// version (<see cref="ScryPeers.Messages"/>), then the event. A message of other messages, or
    /// cut short, reads as none.
    /// </summary>
    internal static class EventWire
    {
        private static readonly byte[] Mark = { (byte)'S', (byte)'C', (byte)'E', (byte)'V' };

        /// <summary>The most items or doers a message may hold, past which it is no event of Scry's.</summary>
        private const int MostListed = 1000;

        public static byte[] Write(WorldEvent told)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Mark);
                writer.Write(ScryPeers.Messages);
                writer.Write(told.Game);
                writer.Write(told.Number);
                writer.Write((byte)told.What);
                writer.Write(told.Thing ?? "");
                writer.Write(told.X);
                writer.Write(told.Y);
                writer.Write(told.Z);
                writer.Write(told.Level);
                writer.Write(told.With != null);
                writer.Write(told.With ?? "");
                writer.Write(told.Mark != null);
                writer.Write(told.Mark ?? "");
                writer.Write(told.Came.Count);
                foreach (var (item, amount) in told.Came)
                {
                    writer.Write(item ?? "");
                    writer.Write(amount);
                }
                writer.Write(told.Doers.Count);
                foreach (var doer in told.Doers) writer.Write(doer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>The event a message tells; none for one of other messages, cut short or no event's at all.</summary>
        public static WorldEvent Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < Mark.Length) return null;
            // A message cut short fails as it is read: read as none.
            return Steps.Run(() => ReadWhole(bytes), out var told, null) == null ? told : null;
        }

        private static WorldEvent ReadWhole(byte[] bytes)
        {
            using (var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8))
            {
                var mark = reader.ReadBytes(Mark.Length);
                for (var i = 0; i < Mark.Length; i++) if (mark[i] != Mark[i]) return null;
                if (reader.ReadInt32() != ScryPeers.Messages) return null;
                var game = reader.ReadInt64();
                var number = reader.ReadInt32();
                var what = (Happened)reader.ReadByte();
                if (!Enum.IsDefined(typeof(Happened), what)) return null;
                var thing = reader.ReadString();
                float x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle();
                var level = reader.ReadInt32();
                var hasWith = reader.ReadBoolean();
                var with = reader.ReadString();
                var hasMark = reader.ReadBoolean();
                var eventMark = reader.ReadString();
                var cameCount = reader.ReadInt32();
                if (cameCount < 0 || cameCount > MostListed) return null;
                var came = new List<(string, int)>(cameCount);
                for (var i = 0; i < cameCount; i++) came.Add((reader.ReadString(), reader.ReadInt32()));
                var doerCount = reader.ReadInt32();
                if (doerCount < 0 || doerCount > MostListed) return null;
                var doers = new List<long>(doerCount);
                for (var i = 0; i < doerCount; i++) doers.Add(reader.ReadInt64());
                return new WorldEvent(game, number, what, thing, x, y, z, came, level, doers, hasWith ? with : null, hasMark ? eventMark : null);
            }
        }
    }

    /// <summary>
    /// Who an event this game ran goes to: the players who did it, wherever they are, and those
    /// near enough to have seen it, within sight (<see cref="Sighting.PersonReach"/>) and a margin
    /// for where this game has them by now, the rule being theirs to apply as it arrives; only
    /// those whose games have Scry, never this game itself, each once.
    /// </summary>
    internal static class EventRecipients
    {
        /// <summary>How much farther than sight a player may stand to be sent an event, as this game may have them a little behind.</summary>
        public const float Margin = 10f;

        public static List<long> For(WorldEvent told, long me, IReadOnlyList<(long Session, float X, float Y, float Z)> players, Func<long, bool> hasScry)
        {
            var to = new List<long>();
            foreach (var doer in told.Doers)
            {
                if (doer != me && hasScry(doer) && !to.Contains(doer)) to.Add(doer);
            }
            var reach = Sighting.PersonReach + Margin;
            foreach (var (session, x, y, z) in players)
            {
                if (session == me || to.Contains(session) || !hasScry(session)) continue;
                float dx = x - told.X, dy = y - told.Y, dz = z - told.Z;
                if (dx * dx + dy * dy + dz * dz <= reach * reach) to.Add(session);
            }
            return to;
        }
    }
}
