using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The dungeons lying in each ground whose pin is on while its row shows With insides
    /// (<see cref="CompendiumBook.MarksDungeonsNow"/>) and whose rooms the world keeps, for their
    /// marks on Valheim's big map (<see cref="DungeonMarks"/>): worked out by the game running the
    /// world (<see cref="WorldDungeons.InGround"/>), here where it is this game, else asked of a
    /// dedicated server's Scry by messages of its own, the ground packed as for the explored map.
    /// Asked as a pin goes on and again every ten minutes while it is, as the ground grows. A server
    /// without Scry never answers, and only walked dungeons are marked. Held for the session only.
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

        /// <summary>Counts up whenever what is held changes, for the marks to be listed again.</summary>
        public static int Stamp { get; private set; }

        /// <summary>How many answers came this session, for the self-test.</summary>
        public static int Answers { get; private set; }

        /// <summary>The dungeons held of a ground.</summary>
        public static IEnumerable<(string Id, DungeonMap Map)> Of(WorldSource source) =>
            Held.TryGetValue(source, out var maps) ? maps.All : Enumerable.Empty<(string, DungeonMap)>();

        /// <summary>A dungeon held of any ground, by its id; none.</summary>
        public static DungeonMap MapOf(string id)
        {
            foreach (var maps in Held.Values)
            {
                var map = maps.Of(id);
                if (map != null) return map;
            }
            return null;
        }

        /// <summary>Made-up dungeons held of a ground in place of the world's, or none again (null); for the self-test.</summary>
        internal static void ForTest(WorldSource source, DungeonMaps maps)
        {
            if (maps == null) Held.Remove(source);
            else Held[source] = maps;
            AskedAt[source] = Time.unscaledTime;
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
            foreach (var source in WorldSources.All)
            {
                if (!WorldSources.HasGround(source) || !book.MarksDungeonsNow(source)) continue;
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
            if (ZNet.instance.IsServer())
            {
                Take(world, source, InGround(world, packing.Result));
                return;
            }
            var package = new ZPackage();
            package.Write(world);
            package.Write((int)source);
            package.Write(Utils.Compress(packing.Result));
            rpc.InvokeRoutedRPC(AskRpc, package);
        }

        /// <summary>The dungeons the world keeps in a ground packed, as maps none of whose rooms is walked.</summary>
        private static DungeonMaps InGround(string world, byte[] packed)
        {
            var maps = new DungeonMaps();
            foreach (var dungeon in WorldDungeons.InGround(world, ExploredGrid.Unpack(packed)))
            {
                maps.Keep(dungeon.Id, dungeon.Location, dungeon.Saved, dungeon.Entrance, dungeon.Generator, dungeon.Turn);
            }
            return maps;
        }

        /// <summary>In the game running the world: which dungeons lie in a player's ground, answered to that player's game as the maps' own text.</summary>
        private static void Asked(long sender, ZPackage package)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            var world = package.ReadString();
            var source = package.ReadInt();
            var maps = InGround(world, Utils.Decompress(package.ReadByteArray()));
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
            Take(world, source, DungeonMaps.Load(Encoding.UTF8.GetString(Utils.Decompress(package.ReadByteArray()))));
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
