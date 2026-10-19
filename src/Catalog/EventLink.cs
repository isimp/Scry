using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Scry's messages between the players' games for the compendium's events (<see cref="WorldEvent"/>):
    /// each Scry says hello on joining, with the version of its messages (<see cref="ScryPeers.Messages"/>),
    /// and answers another's hello, so each knows who plays with Scry; the game that runs an event
    /// sends it to those who did it and to those near (<see cref="EventRecipients"/>), whose Scry
    /// tells it to its character by the one rule (<see cref="CompendiumKeeper.Tell"/>). The
    /// messages are routed, so a server without Scry passes them on all the same
    /// (<c>ZRoutedRpc.RouteRPC</c>). An event comes only from the game that ran it: one sent by
    /// another is passed over. Nothing is sent of what the self-test feeds the hooks.
    /// </summary>
    internal static class EventLink
    {
        static EventLink() => WorldCaches.Register(nameof(EventLink), Forget);

        public const string HelloRpc = "Scry_Hello";
        public const string EventRpc = "Scry_Event";

        /// <summary>How often who is still in the world is looked at, to forget those gone.</summary>
        private const float PruneEvery = 5f;

        private static ZRoutedRpc _registeredOn;
        private static bool _saidHello;
        private static float _prunedAt;

        /// <summary>Who plays with Scry in this world.</summary>
        public static ScryPeers Peers { get; } = new ScryPeers();

        /// <summary>How many events this game sent and took this session, and how many hellos it heard; for the self-test.</summary>
        public static int Sent { get; private set; }

        public static int Taken { get; private set; }

        public static int Hellos { get; private set; }

        /// <summary>Registers the messages on each new connection, says hello once the character is in the world, and forgets players gone; each frame.</summary>
        public static void Update()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc != _registeredOn)
            {
                _registeredOn = rpc;
                _saidHello = false;
                if (rpc == null) return;
                rpc.Register<ZPackage>(HelloRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "hearing another player's Scry say hello", () => Heard(sender, package)));
                rpc.Register<ZPackage>(EventRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "taking an event another player's game ran", () => Take(sender, package)));
            }
            if (rpc == null || ZNet.instance == null) return;
            if (!_saidHello && Player.m_localPlayer != null)
            {
                _saidHello = true;
                rpc.InvokeRoutedRPC(ZRoutedRpc.Everybody, HelloRpc, HelloPackage(asking: true));
            }
            if (Time.unscaledTime - _prunedAt >= PruneEvery)
            {
                _prunedAt = Time.unscaledTime;
                var sessions = new List<long>();
                foreach (var player in ZNet.instance.GetPlayerList()) sessions.Add(player.m_characterID.UserID);
                Peers.KeepOnly(sessions);
            }
        }

        private static ZPackage HelloPackage(bool asking)
        {
            var package = new ZPackage();
            package.Write(ScryPeers.Messages);
            package.Write(asking);
            return package;
        }

        /// <summary>Another player's Scry said hello: kept, and answered where it asks, so it knows this one too.</summary>
        private static void Heard(long sender, ZPackage package)
        {
            if (sender == ZNet.GetUID()) return;
            var messages = package.ReadInt();
            var asking = package.ReadBool();
            Peers.Hello(sender, messages);
            Hellos++;
            if (asking) ZRoutedRpc.instance.InvokeRoutedRPC(sender, HelloRpc, HelloPackage(asking: false));
        }

        /// <summary>
        /// An event this game ran, sent to the other players it concerns whose games have Scry:
        /// those who did it, and those near it by where this game has their characters.
        /// </summary>
        public static void Send(WorldEvent told)
        {
            var rpc = ZRoutedRpc.instance;
            if (told == null || rpc == null || rpc != _registeredOn || CompendiumKeeper.RecordingForTest) return;
            var me = ZNet.GetUID();
            var players = new List<(long, float, float, float)>();
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null || player == Player.m_localPlayer) continue;
                var at = player.transform.position;
                players.Add((player.GetZDOID().UserID, at.x, at.y, at.z));
            }
            var to = EventRecipients.For(told, me, players, Peers.Has);
            if (to.Count == 0) return;
            var bytes = EventWire.Write(told);
            foreach (var session in to)
            {
                var package = new ZPackage();
                package.Write(bytes);
                rpc.InvokeRoutedRPC(session, EventRpc, package);
                Sent++;
            }
        }

        /// <summary>An event another player's game ran, told to the character by the one rule; one sent by any but the game that ran it is passed over.</summary>
        private static void Take(long sender, ZPackage package)
        {
            var told = EventWire.Read(package.ReadByteArray());
            if (told == null || told.Game != sender || sender == ZNet.GetUID()) return;
            Taken++;
            CompendiumKeeper.Tell(told);
        }

        private static void Forget()
        {
            _saidHello = false;
            _prunedAt = 0f;
            Peers.KeepOnly(System.Array.Empty<long>());
        }
    }
}
