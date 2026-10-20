using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The dungeons lying in each ground whose pin is on (<see cref="CompendiumBook.MarksDungeonsNow"/>)
    /// or whose row shows With insides, for their marks on Valheim's big map (<see cref="DungeonMarks"/>)
    /// and what the row shows (<see cref="SourceWalks"/>): worked out by the game running the world
    /// (<see cref="WorldDungeons.InGround"/>), here where it is this game, else asked of a dedicated
    /// server's Scry by messages of its own, the ground packed as for the explored map and the kinds
    /// of dungeon named (<see cref="DungeonKinds"/>). Each comes with its rooms where the world keeps
    /// them and the server's walks on it, anyone's (<see cref="GroupMapsLink.RecordOf"/>), else by
    /// its entrance alone. Asked as a pin goes on and again every ten minutes while it is, as the
    /// ground grows; for the single places a row shows with insides, the server's records of their
    /// kinds of dungeon are asked for. A server without Scry never answers, and only dungeons walked
    /// by you are marked. Held for the session only.
    /// </summary>
    internal static class DungeonsInGround
    {
        static DungeonsInGround() => WorldCaches.Register(nameof(DungeonsInGround), Forget);

        public const string AskRpc = "Scry_DungeonsAsk";
        public const string AnswerRpc = "Scry_DungeonsAnswer";

        /// <summary>How often a ground is asked about again while its pin is on, in seconds.</summary>
        private const float AskEvery = 600f;

        private static readonly Dictionary<WorldSource, DungeonMaps> Held = new Dictionary<WorldSource, DungeonMaps>();
        private static readonly Dictionary<WorldSource, float> AskedAt = new Dictionary<WorldSource, float>();
        private static readonly Dictionary<WorldSource, Task<byte[]>> Packing = new Dictionary<WorldSource, Task<byte[]>>();
        private static ZRoutedRpc _registeredOn;
        private static string _world;

        /// <summary>Whether the self-test holds its own in place of the world's, and what: one ground's made-up dungeons, or none.</summary>
        private static bool _insteadOn;
        private static (WorldSource Source, DungeonMaps Maps)? _instead;

        /// <summary>Counts up whenever what is held changes, for the marks to be listed again.</summary>
        public static int Stamp { get; private set; }

        /// <summary>How many answers came this session, for the self-test.</summary>
        public static int Answers { get; private set; }

        /// <summary>The dungeons held of a ground.</summary>
        public static IEnumerable<(string Id, DungeonMap Map)> Of(WorldSource source)
        {
            if (_insteadOn) return _instead is (WorldSource ground, DungeonMaps made) && ground == source ? made.All : Enumerable.Empty<(string, DungeonMap)>();
            return Held.TryGetValue(source, out var maps) ? maps.All : Enumerable.Empty<(string, DungeonMap)>();
        }

        /// <summary>A dungeon held of any ground, by its id; none.</summary>
        public static DungeonMap MapOf(string id)
        {
            if (_insteadOn) return _instead?.Maps.Of(id);
            foreach (var maps in Held.Values)
            {
                var map = maps.Of(id);
                if (map != null) return map;
            }
            return null;
        }

        /// <summary>Made-up dungeons of one ground in place of every ground's, those of the world kept to come back (<see cref="WorldAgainForTest"/>); for the self-test.</summary>
        internal static void InsteadForTest(WorldSource source, DungeonMaps maps)
        {
            _insteadOn = true;
            _instead = (source, maps);
            Stamp++;
        }

        /// <summary>No dungeons in any ground, those of the world kept to come back; for the self-test, so its own marks stand alone.</summary>
        internal static void NoneForTest()
        {
            _insteadOn = true;
            _instead = null;
            Stamp++;
        }

        /// <summary>The world's dungeons in the grounds again, after the self-test's.</summary>
        internal static void WorldAgainForTest()
        {
            if (!_insteadOn) return;
            _insteadOn = false;
            _instead = null;
            Stamp++;
        }

        /// <summary>Asks about a ground at the next frame, as its pin goes on.</summary>
        public static void AskNow(WorldSource source) => AskedAt.Remove(source);

        /// <summary>Registers the messages on each new connection, on a dedicated server too, and asks about each ground marked as it is due.</summary>
        public static void Update()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc != _registeredOn)
            {
                _registeredOn = rpc;
                if (rpc == null) return;
                rpc.Register<ZPackage>(AskRpc, (sender, package) => Guard.Run(Feature.DungeonMaps, "answering which dungeons lie in a player's ground", () => Asked(sender, package)));
                rpc.Register<ZPackage>(AnswerRpc, (sender, package) => Guard.Run(Feature.DungeonMaps, "taking which dungeons lie in the ground", () => Answered(sender, package)));
            }
            var world = CompendiumMap.World;
            if (world != _world)
            {
                Clear();
                _world = world;
            }
            var book = CompendiumKeeper.Book;
            if (rpc == null || book == null || world == null || ZNet.instance == null) return;
            // A server whose Scry is missing, or not heard yet, is asked nothing.
            if (!ServerScries.Answers(EventLink.Server)) return;
            foreach (var source in WorldSources.All)
            {
                var insides = book.Shows(source) == SourceLevel.WithInsides;
                if (!WorldSources.HasGround(source))
                {
                    // The server's records of the kinds of dungeon a row of single places shows with insides, each asked at most once a minute.
                    if (insides) foreach (var kind in book.Found(world, source)) if (DungeonKinds.Is(kind)) GroupMapsLink.Ask(kind);
                    continue;
                }
                if (!book.MarksDungeonsNow(source) && !insides) continue;
                if (AskedAt.TryGetValue(source, out var at) && Time.unscaledTime - at < AskEvery) continue;
                Ask(rpc, world, source);
            }
        }

        /// <summary>The ground packed on a worker thread, then worked out here where this game runs the world, else sent to the server's Scry.</summary>
        private static void Ask(ZRoutedRpc rpc, string world, WorldSource source)
        {
            if (!Packing.TryGetValue(source, out var packing))
            {
                packing = source == WorldSource.Explored ? CompendiumMap.PackOnWorker() : CompendiumMap.PackSharedOnWorker();
                if (packing == null)
                {
                    AskedAt[source] = Time.unscaledTime;
                    return;
                }
                Packing[source] = packing;
            }
            if (!packing.IsCompleted) return;
            Packing.Remove(source);
            AskedAt[source] = Time.unscaledTime;
            if (packing.Status != TaskStatus.RanToCompletion)
            {
                Faults.Tell(Feature.DungeonMaps, "packing the ground for its dungeons", packing.Exception?.GetBaseException());
                return;
            }
            var kinds = new HashSet<string>(DungeonKinds.All, System.StringComparer.Ordinal);
            if (ZNet.instance.IsServer())
            {
                Take(world, source, InGround(world, packing.Result, kinds));
                return;
            }
            var package = new ZPackage();
            package.Write(world);
            package.Write((int)source);
            package.Write(Utils.Compress(packing.Result));
            // The kinds of dungeon after the ground, which a server's older Scry leaves unread.
            package.Write(kinds.Count);
            foreach (var kind in kinds) package.Write(kind);
            rpc.InvokeRoutedRPC(AskRpc, package);
        }

        /// <summary>
        /// The dungeons lying in a ground packed: those whose rooms the world keeps, with the server's
        /// record of anyone's walks on each where its rooms are the same, and those of the kinds named
        /// holding none yet by their entrance alone.
        /// </summary>
        private static DungeonMaps InGround(string world, byte[] packed, ICollection<string> kinds)
        {
            var maps = new DungeonMaps();
            var entrances = new List<(string Location, Vector3 At)>();
            foreach (var dungeon in WorldDungeons.InGround(world, ExploredGrid.Unpack(packed), kinds, entrances))
            {
                var record = GroupMapsLink.RecordOf(dungeon.Id);
                if (record?.Rooms != null && record.Rooms.Count == dungeon.Saved.Count)
                {
                    // As the server answers a record: the rooms its traces uncover walked, those of traces alone told apart.
                    maps.TakeAnswered(dungeon.Id, record);
                    continue;
                }
                maps.Keep(dungeon.Id, dungeon.Location, dungeon.Saved, dungeon.Entrance, dungeon.Generator, dungeon.Turn);
            }
            foreach (var (location, at) in entrances)
            {
                var id = DungeonMaps.EntranceIdOf(world, at.x, at.z);
                if (id != null) maps.Keep(id, location, new List<(int, Vec3, Vec3)>(), new Vec3(at.x, at.y, at.z), default, Quat.Identity);
            }
            return maps;
        }

        /// <summary>The kinds of dungeon a player's game named after its ground; none from an older Scry, which names none.</summary>
        private static HashSet<string> KindsAsked(ZPackage package)
        {
            if (package.GetPos() >= package.Size()) return null;
            var count = package.ReadInt();
            if (count < 0 || count > MostKinds) return null;
            var kinds = new HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < count; i++) kinds.Add(package.ReadString());
            return kinds;
        }

        /// <summary>The most kinds of dungeon an ask may name.</summary>
        private const int MostKinds = 4096;

        /// <summary>In the game running the world: which dungeons lie in a player's ground, answered to that player's game as the maps' own text.</summary>
        private static void Asked(long sender, ZPackage package)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            var world = package.ReadString();
            var source = package.ReadInt();
            var ground = Utils.Decompress(package.ReadByteArray());
            var maps = InGround(world, ground, KindsAsked(package));
            var answer = new ZPackage();
            answer.Write(world);
            answer.Write(source);
            answer.Write(Utils.Compress(Encoding.UTF8.GetBytes(maps.ToSave()())));
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, answer);
        }

        /// <summary>The server's answer, kept for the ground asked about; from any other game, passed over.</summary>
        private static void Answered(long sender, ZPackage package)
        {
            if (sender != EventLink.WorldSession) return;
            var world = package.ReadString();
            var source = (WorldSource)package.ReadInt();
            if (!System.Enum.IsDefined(typeof(WorldSource), source)) return;
            // A server's older Scry names its dungeons by their generators' numbers: re-keyed by place.
            Take(world, source, DungeonMaps.Load(Encoding.UTF8.GetString(Utils.Decompress(package.ReadByteArray()))).ByPlace());
        }

        private static void Take(string world, WorldSource source, DungeonMaps maps)
        {
            if (world != _world || maps == null) return;
            Held[source] = maps;
            Answers++;
            Stamp++;
        }

        private static void Clear()
        {
            Held.Clear();
            AskedAt.Clear();
            Packing.Clear();
            Stamp++;
        }

        private static void Forget()
        {
            Clear();
            _world = null;
        }
    }
}
