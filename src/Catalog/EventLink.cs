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
    /// another is passed over. Nothing is sent of what the self-test feeds the hooks; the
    /// self-test's own events go to the game running the world and come back from it
    /// (<see cref="AskEcho"/>), over the network where that is a server.
    /// </summary>
    internal static class EventLink
    {
        static EventLink() => WorldCaches.Register(nameof(EventLink), Forget);

        public const string HelloRpc = "Scry_Hello";
        public const string EventRpc = "Scry_Event";
        public const string EchoRpc = "Scry_EventEcho";

        /// <summary>How often who is in the world is looked at: those new noted, those gone forgotten, whom the character plays with told anew.</summary>
        private const float LookEvery = 1f;

        /// <summary>At most so many copies of a made-up event are echoed back, and none longer than so many bytes (<see cref="AskEcho"/>).</summary>
        private const int MostEchoes = 4;

        private const int LongestEcho = 4096;

        private static ZRoutedRpc _registeredOn;
        private static bool _saidHello;
        private static float _lookedAt;
        private static List<(string Name, PeerScry Scry)> _playingWith = new List<(string, PeerScry)>();
        private static List<(string Name, PeerScry Scry)> _playingWithForTest;

        /// <summary>The players played with and whether each one's game has Scry (<see cref="ScryPeers.PlayingWith"/>), looked at each second; made-up ones while the self-test sets them.</summary>
        public static IReadOnlyList<(string Name, PeerScry Scry)> PlayingWith => _playingWithForTest ?? _playingWith;

        /// <summary>Counts up whenever <see cref="PlayingWith"/> changes, for what is told from it to tell again.</summary>
        public static int PlayingWithVersion { get; private set; }

        /// <summary>Made-up players played with in place of the world's, or the world's again (null); for the self-test.</summary>
        internal static void PlayingWithForTest(List<(string Name, PeerScry Scry)> players)
        {
            _playingWithForTest = players;
            PlayingWithVersion++;
        }

        /// <summary>Whether an event from this game itself is taken as from another: in a world this game runs, where the self-test's echo goes through it alone.</summary>
        internal static bool TakeOwnForTest { get; set; }

        /// <summary>The session of the game running the world: the server's, or this game's where it runs the world; none while not connected.</summary>
        public static long WorldSession
        {
            get
            {
                if (ZNet.instance == null) return 0L;
                if (ZNet.instance.IsServer()) return ZNet.GetUID();
                var server = ZNet.instance.GetServerPeer();
                return server != null ? server.m_uid : 0L;
            }
        }

        /// <summary>Whether a game said hello this session, of any messages: the server's among them, where it runs Scry.</summary>
        public static bool HeardFrom(long session) => Peers.Has(session) || Peers.Outdated(session);

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
                rpc.Register<ZPackage>(EchoRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "echoing the self-test's event", () => Echo(sender, package)));
            }
            if (rpc == null || ZNet.instance == null) return;
            if (!_saidHello && Player.m_localPlayer != null)
            {
                _saidHello = true;
                rpc.InvokeRoutedRPC(ZRoutedRpc.Everybody, HelloRpc, HelloPackage(asking: true));
            }
            if (Time.unscaledTime - _lookedAt >= LookEvery) Look();
        }

        /// <summary>
        /// The players in the world, noted and told as played with (<see cref="PlayingWith"/>):
        /// each by its character's session, those the game lists without a character, as one
        /// rising again after a death, counted.
        /// </summary>
        private static void Look()
        {
            _lookedAt = Time.unscaledTime;
            var players = new List<(long Session, string Name)>();
            var withoutCharacter = 0;
            foreach (var player in ZNet.instance.GetPlayerList())
            {
                if (player.m_characterID.IsNone()) withoutCharacter++;
                else players.Add((player.m_characterID.UserID, player.m_name));
            }
            var now = Time.unscaledTimeAsDouble;
            Peers.InWorld(players, withoutCharacter, now);
            var with = Peers.PlayingWith(ZNet.GetUID(), now);
            if (Same(with, _playingWith)) return;
            _playingWith = with;
            PlayingWithVersion++;
        }

        private static bool Same(List<(string Name, PeerScry Scry)> a, List<(string Name, PeerScry Scry)> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
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
            _lookedAt = 0f;
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
            if (told == null || told.Game != sender || (sender == ZNet.GetUID() && !TakeOwnForTest)) return;
            Taken++;
            CompendiumKeeper.Tell(told);
        }

        /// <summary>
        /// Asks the game running the world to send a made-up event back so many times, as from
        /// itself (<see cref="Echo"/>); for the self-test, which makes the event the world's game's own.
        /// </summary>
        internal static void AskEcho(WorldEvent told, int times)
        {
            var rpc = ZRoutedRpc.instance;
            if (told == null || rpc == null || rpc != _registeredOn) return;
            var package = new ZPackage();
            package.Write(EventWire.Write(told));
            package.Write(times);
            rpc.InvokeRoutedRPC(EchoRpc, package);
        }

        /// <summary>
        /// The self-test's event, in the game running the world: sent back to the game that asked,
        /// a few times at most, as the events' own message, over the network where that is
        /// another game. Only the world's game echoes, and only back to the one asking.
        /// </summary>
        private static void Echo(long sender, ZPackage package)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            var bytes = package.ReadByteArray();
            var times = Mathf.Clamp(package.ReadInt(), 1, MostEchoes);
            if (bytes == null || bytes.Length > LongestEcho) return;
            for (var i = 0; i < times; i++)
            {
                var echo = new ZPackage();
                echo.Write(bytes);
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, EventRpc, echo);
            }
        }

        private static void Forget()
        {
            _saidHello = false;
            _lookedAt = 0f;
            Peers.Forget();
            _playingWith = new List<(string, PeerScry)>();
            PlayingWithVersion++;
        }
    }
}
