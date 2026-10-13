using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Scry's own messages between a game and its world's server, for the map catch-up
    /// (<see cref="CompendiumMap"/>): a client of a dedicated server sends its explored map packed
    /// (<see cref="ExploredGrid"/>), and the server's Scry answers with the kinds of place lying in
    /// it (<see cref="MapCatchUp.Kinds"/>), never where they are. The messages are registered on
    /// each new connection, as the game makes its routed messages anew for each; whoever is the
    /// world's server answers, a dedicated one or a game hosting. A server without Scry never
    /// answers, so after <see cref="AnswerWait"/> seconds the client gives up on the world for
    /// the session, its places counting as the character meets them.
    /// </summary>
    internal static class CompendiumLink
    {
        static CompendiumLink() => WorldCaches.Register(nameof(CompendiumLink), Forget);

        public const string AskRpc = "Scry_MapAsk";
        public const string AnswerRpc = "Scry_MapAnswer";

        /// <summary>How long a client waits for the server's answer.</summary>
        private const float AnswerWait = 30f;

        private static ZRoutedRpc _registeredOn;
        private static string _askedFor;
        private static float _askedAt;
        private static Task<byte[]> _packing;

        /// <summary>How many maps this game has answered, as a server; for the self-test.</summary>
        public static int Answered { get; private set; }

        /// <summary>How many kinds of place the last answer taken named; for the self-test.</summary>
        public static int LastTold { get; private set; } = -1;

        /// <summary>Registers the messages on each new connection; called each frame, on a dedicated server too.</summary>
        public static void Update()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == _registeredOn) return;
            _registeredOn = rpc;
            _askedFor = null;
            _packing = null;
            if (rpc == null) return;
            rpc.Register<ZPackage>(AskRpc, (sender, package) => Guard.Run(Feature.CompendiumMap, "answering a player's explored map", () => Answer(sender, package)));
            rpc.Register<ZPackage>(AnswerRpc, (sender, package) => Guard.Run(Feature.CompendiumMap, "taking the server's answer on the explored map", () => Take(package)));
        }

        /// <summary>A client asks the server once which kinds of place lie in its explored map, packed on a worker thread first, and gives up after a while without an answer.</summary>
        public static void Ask(string world)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn) return;
            if (_askedFor == world)
            {
                if (Time.unscaledTime - _askedAt < AnswerWait) return;
                _askedFor = null;
                CompendiumMap.GiveUp(world);
                Log.Report(CompendiumWords.MapNoServerScry);
                return;
            }
            if (_packing == null) _packing = CompendiumMap.PackOnWorker();
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
            package.Write(Utils.Compress(packing.Result));
            _askedFor = world;
            _askedAt = Time.unscaledTime;
            rpc.InvokeRoutedRPC(AskRpc, package);
        }

        /// <summary>The server answers a player's explored map with the kinds of place lying in it.</summary>
        private static void Answer(long sender, ZPackage package)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            var world = package.ReadString();
            var grid = ExploredGrid.Unpack(Utils.Decompress(package.ReadByteArray()));
            if (grid == null) return;
            var kinds = MapCatchUp.Kinds(grid, CompendiumMap.Places());
            var reply = new ZPackage();
            reply.Write(world);
            reply.Write(kinds.Count);
            foreach (var kind in kinds) reply.Write(kind);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, reply);
            Answered++;
            Log.Report(CompendiumWords.MapAnswered(kinds.Count));
        }

        /// <summary>The client takes the server's answer into the book of the character played.</summary>
        private static void Take(ZPackage package)
        {
            var world = package.ReadString();
            var count = package.ReadInt();
            var kinds = new List<string>(count);
            for (var i = 0; i < count; i++) kinds.Add(package.ReadString());
            if (world != _askedFor) return;
            _askedFor = null;
            LastTold = kinds.Count;
            CompendiumMap.Told(CompendiumKeeper.Book, world, kinds);
        }

        private static void Forget()
        {
            _askedFor = null;
            _packing = null;
        }
    }
}
