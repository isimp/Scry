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
        public const string RunRpc = "Scry_EventRun";
        public const string RanRpc = "Scry_EventRan";
        public const string TakenAskRpc = "Scry_EventTakenAsk";
        public const string TakenAnswerRpc = "Scry_EventTakenAnswer";

        /// <summary>How often who is in the world is looked at: those new noted, those gone forgotten, whom the character plays with told anew.</summary>
        private const float LookEvery = 1f;

        /// <summary>At most so many copies of a made-up event are echoed back, and none longer than so many bytes (<see cref="AskEcho"/>).</summary>
        private const int MostEchoes = 4;

        private const int LongestEcho = 4096;

        private static ZRoutedRpc _registeredOn;
        private static bool _saidHello;
        private static float _lookedAt;
        private static List<(string Name, PeerScry Scry)> _playingWith = new List<(string, PeerScry)>();

        /// <summary>When this game said hello this connection, for how long the server's has been waited for.</summary>
        private static float _saidAt;

        private static ServerScry _server = ServerScry.Waiting;
        private static ServerScry? _serverForTest;

        /// <summary>Whether the world's server runs Scry, by its hello (<see cref="ServerScries.Of"/>), looked at each second; as the self-test sets it while it does.</summary>
        public static ServerScry Server => _serverForTest ?? _server;

        /// <summary>A made-up state of the server's Scry in place of the world's, or the world's again (null); for the self-test.</summary>
        internal static void ServerForTest(ServerScry? server)
        {
            _serverForTest = server;
            PlayingWithVersion++;
        }
        private static List<(string Name, PeerScry Scry)> _playingWithForTest;
        private static List<(long Session, string Name)> _alsoListedForTest;
        private static int _alsoWithoutCharacterForTest;

        /// <summary>The events this game took lately, for the world's game to answer whether one came (<see cref="AskTaken"/>).</summary>
        private static TakenLately _taken = new TakenLately();

        /// <summary>
        /// What the world's game said of the self-test's events it ran (<see cref="AskRun"/>): each
        /// thing, its number there, whether it was sent to this game, and how many games the rules
        /// chose in all; none until it answers.
        /// </summary>
        internal static List<(string Thing, int Number, bool ToYou, int Chosen)> RanForTest { get; private set; }

        /// <summary>Where the world's game had this game's character as it ran them; none where it had none.</summary>
        internal static Vector3? RanSawYouAt { get; private set; }

        /// <summary>The world's game's answer whether it took an event (<see cref="AskTaken"/>); none until it answers.</summary>
        internal static (long Game, int Number, bool Taken)? TakenForTest { get; private set; }

        /// <summary>
        /// Made-up players listed beside the game's own as who is in the world is looked at, so the
        /// real ones are kept as they are: each by session and name, and how many more are listed
        /// without a character; none again with none (null). For the self-test.
        /// </summary>
        internal static void AlsoListedForTest(List<(long Session, string Name)> players, int withoutCharacter)
        {
            _alsoListedForTest = players;
            _alsoWithoutCharacterForTest = withoutCharacter;
            _lookedAt = 0f;
        }

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
                _server = ServerScry.Waiting;
                if (rpc == null) return;
                rpc.Register<ZPackage>(HelloRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "hearing another player's Scry say hello", () => Heard(sender, package)));
                rpc.Register<ZPackage>(EventRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "taking an event another player's game ran", () => Take(sender, package)));
                rpc.Register<ZPackage>(EchoRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "echoing the self-test's event", () => Echo(sender, package)));
                rpc.Register<ZPackage>(RunRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "running the self-test's events", () => Run(sender, package)));
                rpc.Register<ZPackage>(RanRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "hearing what the self-test's events came to", () => Ran(sender, package)));
                rpc.Register<ZPackage>(TakenAskRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "answering whether an event came", () => TakenAsked(sender, package)));
                rpc.Register<ZPackage>(TakenAnswerRpc, (sender, package) => Guard.Run(Feature.CompendiumTogether, "hearing whether an event came", () => TakenAnswered(sender, package)));
            }
            if (rpc == null || ZNet.instance == null) return;
            if (!_saidHello && Player.m_localPlayer != null)
            {
                _saidHello = true;
                _saidAt = Time.unscaledTime;
                rpc.InvokeRoutedRPC(ZRoutedRpc.Everybody, HelloRpc, HelloPackage(asking: true));
            }
            if (Time.unscaledTime - _lookedAt >= LookEvery) Look();
        }

        /// <summary>
        /// The players in the world, noted and told as played with (<see cref="PlayingWith"/>):
        /// each by its character's session, those the game lists without a character, as one
        /// rising again after a death, counted; and the self-test's made-up ones beside them.
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
            if (_alsoListedForTest != null)
            {
                players.AddRange(_alsoListedForTest);
                withoutCharacter += _alsoWithoutCharacterForTest;
            }
            var now = Time.unscaledTimeAsDouble;
            Peers.InWorld(players, withoutCharacter, now);
            var with = Peers.PlayingWith(ZNet.GetUID(), now);
            // The server's own hello, its wait counted from this game's, as a hello asks for one back.
            var world = WorldSession;
            var server = ServerScries.Of(ZNet.instance.IsServer(), Peers.Has(world), Peers.Outdated(world), _saidHello ? Time.unscaledTime - _saidAt : 0.0);
            if (Same(with, _playingWith) && server == _server) return;
            _playingWith = with;
            _server = server;
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
        /// The players in the world by session and where their characters stand as this game has
        /// them: from the game's list of players and their characters' synced places, the same in a
        /// player's game as on a dedicated server, which makes no characters away from the world's
        /// middle. A character this game holds nothing of is left out: it is far from here.
        /// </summary>
        private static List<(long Session, float X, float Y, float Z)> Players()
        {
            var players = new List<(long Session, float X, float Y, float Z)>();
            var zdos = ZDOMan.instance;
            if (ZNet.instance == null || zdos == null) return players;
            foreach (var player in ZNet.instance.GetPlayerList())
            {
                if (player.m_characterID.IsNone()) continue;
                var zdo = zdos.GetZDO(player.m_characterID);
                if (zdo == null) continue;
                var at = zdo.GetPosition();
                players.Add((player.m_characterID.UserID, at.x, at.y, at.z));
            }
            return players;
        }

        /// <summary>The games an event this game ran goes to (<see cref="EventRecipients"/>): those who did it, and those near it by where this game has their characters, whose games have Scry.</summary>
        private static List<long> Recipients(WorldEvent told) => EventRecipients.For(told, ZNet.GetUID(), Players(), Peers.Has);

        /// <summary>An event this game ran, sent to the other players it concerns whose games have Scry (<see cref="Recipients"/>); the games it went to.</summary>
        public static IReadOnlyList<long> Send(WorldEvent told)
        {
            var rpc = ZRoutedRpc.instance;
            if (told == null || rpc == null || rpc != _registeredOn || CompendiumKeeper.RecordingForTest) return System.Array.Empty<long>();
            var to = Recipients(told);
            SendTo(rpc, told, to);
            return to;
        }

        private static void SendTo(ZRoutedRpc rpc, WorldEvent told, IReadOnlyList<long> to)
        {
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

        /// <summary>
        /// An event another player's game ran, told to the character by the one rule; one sent by
        /// any but the game that ran it is passed over, and a made-up one of the self-test reaches
        /// no book but the self-test's own (<see cref="TestEvents.Reaches"/>).
        /// </summary>
        private static void Take(long sender, ZPackage package)
        {
            var told = EventWire.Read(package.ReadByteArray());
            if (told == null || told.Game != sender || (sender == ZNet.GetUID() && !TakeOwnForTest)) return;
            Taken++;
            _taken.Took(told.Game, told.Number);
            if (!TestEvents.Reaches(told.Thing, CompendiumKeeper.RecordingForTest)) return;
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

        /// <summary>
        /// Asks the game running the world to run made-up events as its own (<see cref="Run"/>):
        /// each of a thing at a place, done by this game's character or by no one. For the self-test.
        /// </summary>
        internal static void AskRun(IReadOnlyList<(string Thing, Vector3 At, bool Done)> events)
        {
            var rpc = ZRoutedRpc.instance;
            if (events == null || rpc == null || rpc != _registeredOn) return;
            RanForTest = null;
            RanSawYouAt = null;
            var package = new ZPackage();
            package.Write(events.Count);
            foreach (var (thing, at, done) in events)
            {
                package.Write(thing);
                package.Write(at);
                package.Write(done);
            }
            rpc.InvokeRoutedRPC(RunRpc, package);
        }

        /// <summary>
        /// The self-test's made-up events, in the game running the world: each of a thing the
        /// self-test made up (<see cref="TestEvents"/>) run as this game's own, done by the game
        /// asking or by no one; who gets it is chosen by the rules for every event, by where this
        /// game has the players' characters, and it is sent only to the game asking, where the rules
        /// chose it. Then that game is told what was run and chosen, after the events, so they come
        /// first, and where this game had its character.
        /// </summary>
        private static void Run(long sender, ZPackage package)
        {
            var rpc = ZRoutedRpc.instance;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || rpc == null) return;
            var count = TestEvents.HowMany(package.ReadInt());
            var ran = new List<(string Thing, int Number, bool ToYou, int Chosen)>();
            for (var i = 0; i < count; i++)
            {
                var thing = package.ReadString();
                var at = package.ReadVector3();
                var done = package.ReadBool();
                if (!TestEvents.Runs(thing)) continue;
                var told = WorldEvents.Make(Happened.Died, thing, at, doers: done ? new[] { sender } : null);
                var chosen = Recipients(told);
                var toYou = TestEvents.ToAsker(chosen, sender);
                if (toYou) SendTo(rpc, told, new[] { sender });
                ran.Add((thing, told.Number, toYou, chosen.Count));
            }
            var answer = new ZPackage();
            var players = Players();
            var you = players.FindIndex(p => p.Session == sender);
            answer.Write(you >= 0);
            answer.Write(you >= 0 ? new Vector3(players[you].X, players[you].Y, players[you].Z) : Vector3.zero);
            answer.Write(ran.Count);
            foreach (var (thing, number, toYou, chosen) in ran)
            {
                answer.Write(thing);
                answer.Write(number);
                answer.Write(toYou);
                answer.Write(chosen);
            }
            rpc.InvokeRoutedRPC(sender, RanRpc, answer);
        }

        /// <summary>What the world's game ran of the self-test's events, kept for it; from any other game, passed over.</summary>
        private static void Ran(long sender, ZPackage package)
        {
            if (sender != WorldSession) return;
            var seen = package.ReadBool();
            var at = package.ReadVector3();
            var count = TestEvents.HowMany(package.ReadInt());
            var ran = new List<(string Thing, int Number, bool ToYou, int Chosen)>();
            for (var i = 0; i < count; i++) ran.Add((package.ReadString(), package.ReadInt(), package.ReadBool(), package.ReadInt()));
            RanSawYouAt = seen ? at : (Vector3?)null;
            RanForTest = ran;
        }

        /// <summary>Asks the game running the world whether it took an event (<see cref="TakenAsked"/>); for the self-test.</summary>
        internal static void AskTaken(long game, int number)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn) return;
            TakenForTest = null;
            var package = new ZPackage();
            package.Write(game);
            package.Write(number);
            rpc.InvokeRoutedRPC(TakenAskRpc, package);
        }

        /// <summary>In the game running the world: whether it took an event lately, answered to the game asking.</summary>
        private static void TakenAsked(long sender, ZPackage package)
        {
            var rpc = ZRoutedRpc.instance;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || rpc == null) return;
            var game = package.ReadLong();
            var number = package.ReadInt();
            var answer = new ZPackage();
            answer.Write(game);
            answer.Write(number);
            answer.Write(_taken.Has(game, number));
            rpc.InvokeRoutedRPC(sender, TakenAnswerRpc, answer);
        }

        /// <summary>The world's game's answer whether it took an event, kept for the self-test; from any other game, passed over.</summary>
        private static void TakenAnswered(long sender, ZPackage package)
        {
            if (sender != WorldSession) return;
            TakenForTest = (package.ReadLong(), package.ReadInt(), package.ReadBool());
        }

        private static void Forget()
        {
            _saidHello = false;
            _lookedAt = 0f;
            Peers.Forget();
            _playingWith = new List<(string, PeerScry)>();
            PlayingWithVersion++;
            _taken = new TakenLately();
            _alsoListedForTest = null;
            RanForTest = null;
            RanSawYouAt = null;
            TakenForTest = null;
        }
    }
}
