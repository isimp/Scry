using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Scry's own messages between a game and its world's server, for the map catch-up
    /// (<see cref="CompendiumMap"/>): a client of a dedicated server sends its explored map packed
    /// (<see cref="ExploredGrid"/>), and the server's Scry answers with the kinds of place lying in
    /// it (<see cref="MapCatchUp.Kinds"/>), never where they are; asked again with the ground new
    /// since as the map grows (<see cref="AskMore"/>). The messages are registered on each new
    /// connection, as the game makes its routed messages anew for each; whoever is the world's
    /// server answers, a dedicated one or a game hosting. Only a server whose Scry said hello is
    /// asked (<see cref="EventLink.Server"/>): one that did not within the wait runs none, and the
    /// client gives up on the world for the session at once, its places counting as the character
    /// meets them; an answer lost is given up on after <see cref="AnswerWait"/> seconds. The ground
    /// from shared maps goes by messages of its own, so a server whose Scry knows only the first
    /// still answers it. Each answer tells how many of each kind lie in the ground after the kinds,
    /// which an older Scry leaves unread; the census, how many of each kind the world holds, never
    /// where, goes by messages of its own, asked once a session while it is shown.
    /// </summary>
    internal static class CompendiumLink
    {
        static CompendiumLink() => WorldCaches.Register(nameof(CompendiumLink), Forget);

        public const string AskRpc = "Scry_MapAsk";
        public const string AnswerRpc = "Scry_MapAnswer";
        public const string SharedAskRpc = "Scry_SharedMapAsk";
        public const string SharedAnswerRpc = "Scry_SharedMapAnswer";
        public const string CensusAskRpc = "Scry_CensusAsk";
        public const string CensusAnswerRpc = "Scry_CensusAnswer";

        /// <summary>The world the census was asked for this connection; none before.</summary>
        private static string _censusAskedFor;
        private static Task<byte[]> _packingShared;
        private static string _sharedAskedFor;

        /// <summary>How long a client waits for the server's answer.</summary>
        private const float AnswerWait = 30f;

        private static ZRoutedRpc _registeredOn;
        private static string _askedFor;
        private static float _askedAt;
        private static Task<(ExploredGrid Grid, byte[] Packed)> _packing;

        /// <summary>The world the ground new since was last asked about for, and when; none while no answer is waited for.</summary>
        private static string _askedMoreFor;
        private static float _askedMoreAt;

        /// <summary>Whether an answer on the ground new since is waited for.</summary>
        public static bool AskingMore => _askedMoreFor != null && Time.unscaledTime - _askedMoreAt < AnswerWait;

        /// <summary>Whether the server's Scry was found missing this session, said once.</summary>
        private static bool _toldNoScry;

        /// <summary>How many maps this game has answered, as a server; for the self-test.</summary>
        public static int Answered { get; private set; }

        /// <summary>How many answers this game took from its world's server, as a client; for the self-test.</summary>
        public static int Taken { get; private set; }

        /// <summary>How many kinds of place the last answer taken named; for the self-test.</summary>
        public static int LastTold { get; private set; } = -1;

        /// <summary>Registers the messages on each new connection; called each frame, on a dedicated server too.</summary>
        public static void Update()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == _registeredOn) return;
            _registeredOn = rpc;
            _askedFor = null;
            _askedMoreFor = null;
            _packing = null;
            _packingShared = null;
            _sharedAskedFor = null;
            _censusAskedFor = null;
            _toldNoScry = false;
            if (rpc == null) return;
            rpc.Register<ZPackage>(CensusAskRpc, (sender, package) => Guard.Run(Feature.CompendiumMap, "answering a player's ask for the world's census", () => AnswerCensus(sender, package)));
            rpc.Register<ZPackage>(CensusAnswerRpc, (sender, package) => Guard.Run(Feature.CompendiumMap, "taking the server's census of the world", () => TakeCensus(sender, package)));
            rpc.Register<ZPackage>(SharedAskRpc, (sender, package) => Guard.Run(Feature.CompendiumMap, "answering a player's shared ground", () => Answer(sender, package, SharedAnswerRpc)));
            rpc.Register<ZPackage>(SharedAnswerRpc, (sender, package) => Guard.Run(Feature.CompendiumMap, "taking the server's answer on the shared ground", () => TakeShared(package)));
            rpc.Register<ZPackage>(AskRpc, (sender, package) => Guard.Run(Feature.CompendiumMap, "answering a player's explored map", () => Answer(sender, package, AnswerRpc)));
            rpc.Register<ZPackage>(AnswerRpc, (sender, package) => Guard.Run(Feature.CompendiumMap, "taking the server's answer on the explored map", () => Take(package)));
        }

        /// <summary>Whether the server's Scry is missing, the world given up on for the session and said once; false while its hello is waited for or heard.</summary>
        private static bool WithoutServerScry(string world)
        {
            if (!ServerScries.GivesUp(EventLink.Server)) return false;
            CompendiumMap.GiveUp(world);
            if (!_toldNoScry) Log.Report(CompendiumWords.MapNoServerScry);
            _toldNoScry = true;
            return true;
        }

        /// <summary>
        /// A client asks the server once which kinds of place lie in its explored map, packed on a
        /// worker thread first, once its Scry said hello; gives up at once where it runs none, and
        /// after a while without an answer.
        /// </summary>
        public static void Ask(string world)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn || EventLink.Server == ServerScry.Waiting || WithoutServerScry(world)) return;
            if (_askedFor == world)
            {
                if (Time.unscaledTime - _askedAt < AnswerWait) return;
                _askedFor = null;
                CompendiumMap.GiveUp(world);
                Log.Report(CompendiumWords.MapNoServerScry);
                return;
            }
            if (_packing == null) _packing = CompendiumMap.GridOnWorker();
            if (_packing == null || !_packing.IsCompleted) return;
            var packing = _packing;
            _packing = null;
            if (packing.Status != TaskStatus.RanToCompletion)
            {
                CompendiumMap.GiveUp(world);
                Faults.Tell(Feature.CompendiumMap, "packing the explored map", packing.Exception?.GetBaseException());
                return;
            }
            var package = new ZPackage();
            package.Write(world);
            package.Write(packing.Result.Packed);
            _askedFor = world;
            _askedAt = Time.unscaledTime;
            CompendiumMap.Asked(world, packing.Result.Grid);
            rpc.InvokeRoutedRPC(AskRpc, package);
        }

        /// <summary>A client asks the server which kinds of place lie in the ground explored since it last asked, packed already; the answer comes as the first's, taken for this ask.</summary>
        public static void AskMore(string world, byte[] packed)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn || !ServerScries.Answers(EventLink.Server)) return;
            var package = new ZPackage();
            package.Write(world);
            package.Write(packed);
            _askedMoreFor = world;
            _askedMoreAt = Time.unscaledTime;
            rpc.InvokeRoutedRPC(AskRpc, package);
        }

        /// <summary>
        /// A client asks the server once which kinds of place lie in its ground from shared maps,
        /// packed on a worker thread first; true once asked, or where there is no such ground.
        /// </summary>
        public static bool AskShared(string world)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn || EventLink.Server == ServerScry.Waiting) return false;
            if (ServerScries.GivesUp(EventLink.Server)) return true;
            if (_packingShared == null)
            {
                _packingShared = CompendiumMap.PackSharedOnWorker();
                if (_packingShared == null) return true;
            }
            if (!_packingShared.IsCompleted) return false;
            var packing = _packingShared;
            _packingShared = null;
            if (packing.Status != TaskStatus.RanToCompletion)
            {
                Faults.Tell(Feature.CompendiumMap, "packing the shared ground", packing.Exception?.GetBaseException());
                return true;
            }
            var package = new ZPackage();
            package.Write(world);
            package.Write(Utils.Compress(packing.Result));
            _sharedAskedFor = world;
            rpc.InvokeRoutedRPC(SharedAskRpc, package);
            return true;
        }

        /// <summary>The server answers a player's explored ground, its own or from shared maps, with the kinds of place lying in it.</summary>
        private static void Answer(long sender, ZPackage package, string answerRpc)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            var world = package.ReadString();
            var grid = ExploredGrid.Unpack(Utils.Decompress(package.ReadByteArray()));
            if (grid == null) return;
            var counted = MapCatchUp.Counted(grid, CompendiumMap.Places());
            var reply = new ZPackage();
            reply.Write(world);
            reply.Write(counted.Count);
            foreach (var (kind, _) in counted) reply.Write(kind);
            // How many of each, after the kinds, so a client's older Scry reads the kinds as before.
            foreach (var (_, count) in counted) reply.Write(count);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, answerRpc, reply);
            Answered++;
            Log.Report(CompendiumWords.MapAnswered(counted.Count));
        }

        /// <summary>The kinds an answer names, with how many of each where the server's Scry tells it, else one each.</summary>
        private static List<(string Kind, int Count)> ReadCounted(ZPackage package)
        {
            var count = package.ReadInt();
            if (count < 0 || count > MostKinds) return new List<(string, int)>();
            var counted = new List<(string Kind, int Count)>(count);
            for (var i = 0; i < count; i++) counted.Add((package.ReadString(), 1));
            if (package.GetPos() >= package.Size()) return counted;
            for (var i = 0; i < count; i++) counted[i] = (counted[i].Kind, package.ReadInt());
            return counted;
        }

        /// <summary>The most kinds of place an answer may name.</summary>
        private const int MostKinds = 100000;

        /// <summary>A client asks the server once a connection for the world's census, how many of each kind of place it holds, once its Scry said hello.</summary>
        public static void AskCensus(string world)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn || _censusAskedFor == world || !ServerScries.Answers(EventLink.Server)) return;
            _censusAskedFor = world;
            var package = new ZPackage();
            package.Write(world);
            rpc.InvokeRoutedRPC(CensusAskRpc, package);
        }

        /// <summary>The server answers with how many of each kind of place the world holds, never where.</summary>
        private static void AnswerCensus(long sender, ZPackage package)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            var world = package.ReadString();
            var census = MapCatchUp.Census(CompendiumMap.Places());
            var reply = new ZPackage();
            reply.Write(world);
            reply.Write(census.Count);
            foreach (var pair in census)
            {
                reply.Write(pair.Key);
                reply.Write(pair.Value);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, CensusAnswerRpc, reply);
            CensusAnswered++;
        }

        /// <summary>The client takes the server's census, from the world's server alone.</summary>
        private static void TakeCensus(long sender, ZPackage package)
        {
            if (sender != EventLink.WorldSession) return;
            var world = package.ReadString();
            var count = package.ReadInt();
            if (world != _censusAskedFor || count < 0 || count > MostKinds) return;
            var census = new Dictionary<string, int>(count, System.StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                var kind = package.ReadString();
                census[kind] = package.ReadInt();
            }
            CompendiumMap.ToldCensus(CompendiumKeeper.Book, world, census);
        }

        /// <summary>How many censuses this game answered, as a server; for the self-test.</summary>
        [Diagnostic]
        public static int CensusAnswered { get; private set; }

        /// <summary>The client takes the server's answer into the book of the character played.</summary>
        private static void Take(ZPackage package)
        {
            var world = package.ReadString();
            var counted = ReadCounted(package);
            if (world == _askedMoreFor && world != _askedFor)
            {
                _askedMoreFor = null;
                TakenMore++;
                CompendiumMap.ToldMore(CompendiumKeeper.Book, world, counted);
                return;
            }
            if (world != _askedFor) return;
            _askedFor = null;
            LastTold = counted.Count;
            Taken++;
            CompendiumMap.Told(CompendiumKeeper.Book, world, counted);
        }

        /// <summary>How many answers on the ground new since this game took, as a client; for the self-test.</summary>
        [Diagnostic]
        public static int TakenMore { get; private set; }

        /// <summary>The client keeps what the server told lies in its ground from shared maps.</summary>
        private static void TakeShared(ZPackage package)
        {
            var world = package.ReadString();
            var counted = ReadCounted(package);
            if (world != _sharedAskedFor) return;
            _sharedAskedFor = null;
            CompendiumMap.ToldShared(CompendiumKeeper.Book, world, counted);
        }

        private static void Forget()
        {
            _askedFor = null;
            _askedMoreFor = null;
            _censusAskedFor = null;
            _packing = null;
            _packingShared = null;
            _sharedAskedFor = null;
        }
    }
}
