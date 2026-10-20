using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The server's records of a world's dungeons, anyone's walks on them, gathered by Scry's copy on
    /// the world's server (a dedicated one or a game hosting): each client sends the record of the
    /// dungeon it walks in, at most every ten seconds, on each visit, each new day in it and each
    /// room new to it, and the server keeps one record a dungeon, its rooms as the first player sent
    /// them, every room any player stood in and the earliest first and latest last day walked, not
    /// who stood there (<see cref="DungeonMaps.Merge(DungeonMaps, string)"/>), in a file of the world's own under the
    /// cache. A client asks for the records of the locations its compendium floor opens, at most once
    /// a minute each, and gets them as they stand from their generators, no place of the world in
    /// them (<see cref="DungeonMaps.Only"/>); a server without Scry never answers. An admin forgets
    /// the world's records, the day noted so walks last walked before it are not taken back. On
    /// joining, a client lists its walks of the world to the server, a few bytes a dungeon
    /// (<see cref="WalkList"/>); the server answers with those it lacks, has fewer rooms of or an
    /// older day of, and the client sends just those, so walks made before the server kept them, or
    /// lost from it, come in. Every map that comes in is re-keyed by place (<see cref="DungeonMaps.ByPlace"/>).
    /// The messages are registered on each new connection, as the game makes its routed messages
    /// anew for each. What only an admin may ask, forgetting the records and reading the traces,
    /// comes by the player's own connection to the server (<see cref="AdminAsksOn"/>): a routed
    /// message names its sender as the sending game wrote it, which the server passes on as it is
    /// (<c>ZRoutedRpc.RouteRPC</c>), so any game could name an admin's.
    /// </summary>
    internal static class GroupMapsLink
    {
        static GroupMapsLink() => WorldCaches.Register(nameof(GroupMapsLink), Forget);

        public const string WalkedRpc = "Scry_MapWalked";
        public const string AskRpc = "Scry_GroupAsk";
        public const string AnswerRpc = "Scry_GroupAnswer";
        public const string ForgetRpc = "Scry_GroupForget";
        public const string ForgotRpc = "Scry_GroupForgot";
        public const string WalksListRpc = "Scry_WalksList";
        public const string WalksWantedRpc = "Scry_WalksWanted";
        public const string ReadTracesRpc = "Scry_ReadTraces";
        public const string TracesReadRpc = "Scry_TracesRead";

        /// <summary>The world whose dungeons' traces were set reading once its records were read, this session; none before.</summary>
        private static string _tracedFor;

        /// <summary>What the server last answered on reading traces, for the self-test: how many dungeons, rooms and rooms on the way, or -1 where it refused; none before an answer.</summary>
        [Diagnostic]
        public static (int Dungeons, int Rooms, int OnTheWay)? TracesReadForTest { get; private set; }

        /// <summary>Asks the server to read the traces in the world's dungeons and keeps its answer, nothing said in the chat; for the self-test.</summary>
        [Diagnostic]
        public static void ReadTracesForTest()
        {
            TracesReadForTest = null;
            _readQuietly = true;
            ReadTraces();
        }

        /// <summary>Whether the next answer on reading traces is only kept for the self-test.</summary>
        private static bool _readQuietly;

        /// <summary>The world the walks were listed to the server for this connection; none before.</summary>
        private static string _listedFor;

        /// <summary>Whether the next answer on a list is only told to the self-test, nothing sent; and the ids the last answer wanted.</summary>
        private static bool _listOnlyForTest;

        /// <summary>The ids the server's last answer on a list wanted, for the self-test; none before one.</summary>
        [Diagnostic]
        public static List<string> WantedForTest { get; private set; }

        /// <summary>How many walks this game sent the server as its answers on its lists wanted, this session; for the self-test.</summary>
        [Diagnostic]
        public static int SentWanted { get; private set; }

        /// <summary>Lists made-up walks to the server and keeps what it answers, nothing sent after; for the self-test.</summary>
        [Diagnostic]
        public static void ListForTest(List<WalkList.Walk> walks)
        {
            var rpc = ZRoutedRpc.instance;
            var world = CompendiumMap.World;
            if (rpc == null || world == null) return;
            WantedForTest = null;
            _listOnlyForTest = true;
            var package = new ZPackage();
            package.Write(world);
            package.Write(Utils.Compress(WalkList.Write(walks)));
            rpc.InvokeRoutedRPC(WalksListRpc, package);
        }

        /// <summary>How often a client sends its walk, and asks again for a location's records, in seconds.</summary>
        private const float ReportEvery = 10f, AskEvery = 60f;

        /// <summary>The most a message of maps or of walks listed may unpack to (<see cref="Packed"/>).</summary>
        private const int MostText = 1 << 20;

        private const int QuitWaitMs = 2000;

        private static readonly FileQueue Files = new FileQueue();
        private static ZRoutedRpc _registeredOn;

        // ----- A client -----

        private static DungeonMaps _group = new DungeonMaps();

        /// <summary>The records the server sent of the locations the floor opens; none from a server without Scry.</summary>
        public static DungeonMaps Group => _instead ?? _group;

        /// <summary>Records the self-test keeps in place of the server's.</summary>
        private static DungeonMaps _instead;

        /// <summary>The dungeon walked since last sent, and when it was last sent.</summary>
        private static string _toReport;
        private static float _reportedAt = float.NegativeInfinity;

        /// <summary>When the records of each location were last asked for.</summary>
        private static readonly Dictionary<string, float> AskedAt = new Dictionary<string, float>(System.StringComparer.Ordinal);

        // ----- The server -----

        private static DungeonMaps _records;
        private static string _recordsWorld;
        private static Task<DungeonMaps> _reading;
        private static bool _unreadable;
        private static float _savedAt;

        /// <summary>How many walks this game took in and asks it answered, as a server; for the self-test.</summary>
        [Diagnostic]
        public static int Merged { get; private set; }

        [Diagnostic]
        public static int Answered { get; private set; }

        /// <summary>Keeps made-up records in place of the server's, for the self-test; none puts them back.</summary>
        [Diagnostic]
        public static void GroupInsteadForTest(DungeonMaps group) => _instead = group;

        private static void Forget()
        {
            Save(now: true);
            _group = new DungeonMaps();
            _instead = null;
            _toReport = null;
            _reportedAt = float.NegativeInfinity;
            AskedAt.Clear();
            _records = null;
            _recordsWorld = null;
            _reading = null;
            _unreadable = false;
            _listedFor = null;
            _listOnlyForTest = false;
            _tracedFor = null;
            _readQuietly = false;
            AdminAsksOn.Clear();
            _peersLookedAt = float.NegativeInfinity;
        }

        /// <summary>Registers the messages on each new connection, sends a walk due, and on a server keeps its records; called each frame, on a dedicated server too.</summary>
        public static void Update()
        {
            Faults.TellEach(Feature.GroupDungeonMaps, Files.Failures());
            var rpc = ZRoutedRpc.instance;
            if (rpc != _registeredOn)
            {
                _registeredOn = rpc;
                if (rpc != null)
                {
                    rpc.Register<ZPackage>(WalkedRpc, (sender, package) => Guard.Run(Feature.GroupDungeonMaps, "taking in a player's dungeon walk", () => TakeWalk(package)));
                    rpc.Register<ZPackage>(AskRpc, (sender, package) => Guard.Run(Feature.GroupDungeonMaps, "answering a player's ask for dungeon records", () => Answer(sender, package)));
                    rpc.Register<ZPackage>(AnswerRpc, (sender, package) => Guard.Run(Feature.GroupDungeonMaps, "taking the server's dungeon records", () => Take(sender, package)));
                    rpc.Register<int>(ForgotRpc, (sender, count) => Guard.Run(Feature.GroupDungeonMaps, "taking the server's answer to forgetting", () => Forgot(sender, count)));
                    rpc.Register<ZPackage>(WalksListRpc, (sender, package) => Guard.Run(Feature.GroupDungeonMaps, "answering a player's list of walks", () => ListAsked(sender, package)));
                    rpc.Register<ZPackage>(WalksWantedRpc, (sender, package) => Guard.Run(Feature.GroupDungeonMaps, "sending the walks the server wants", () => WantedTaken(sender, package)));
                    rpc.Register<ZPackage>(TracesReadRpc, (sender, package) => Guard.Run(Feature.GroupDungeonMaps, "taking the server's answer on traces read", () => TracesRead(sender, package)));
                }
                _listedFor = null;
                AdminAsksOn.Clear();
            }
            if (IsServer)
            {
                KeepRecords();
                RegisterAdminAsks();
            }
            if (_toReport != null && Time.unscaledTime - _reportedAt >= ReportEvery) SendWalk();
            ListWalks();
        }

        /// <summary>
        /// Once a connection, the character in the world and its maps read: its walks of the world
        /// listed to the server once its Scry said hello, or taken into the records here where this
        /// game runs the world, those last walked before a forget left out.
        /// </summary>
        private static void ListWalks()
        {
            var world = CompendiumMap.World;
            if (world == null || _listedFor == world || Player.m_localPlayer == null || !DungeonWalks.Ready) return;
            if (!IsServer && !ServerScries.Answers(EventLink.Server)) return;
            if (IsServer && _records == null) return;
            _listedFor = world;
            var walks = WalkList.Of(DungeonWalks.Maps, world);
            if (walks.Count == 0) return;
            if (IsServer)
            {
                foreach (var batch in WalkList.Batches(DungeonWalks.Maps, WalkList.Wanted(_records, walks, _records.ForgottenDay(world)))) MergeIn(batch, world);
                return;
            }
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn) return;
            var package = new ZPackage();
            package.Write(world);
            package.Write(Utils.Compress(WalkList.Write(walks)));
            rpc.InvokeRoutedRPC(WalksListRpc, package);
        }

        /// <summary>The server answers a player's list of walks with the ids of those it wants.</summary>
        private static void ListAsked(long sender, ZPackage package)
        {
            if (!IsServer || _records == null) return;
            var world = package.ReadString();
            var packed = package.ReadByteArray();
            var listed = WalkList.Read(Packed.Unpack(packed, MostText));
            if (world != CompendiumMap.World || listed == null) return;
            var wanted = WalkList.Wanted(_records, listed, _records.ForgottenDay(world));
            var reply = new ZPackage();
            reply.Write(world);
            reply.Write(wanted.Count);
            foreach (var id in wanted) reply.Write(id);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, WalksWantedRpc, reply);
        }

        /// <summary>The client sends the walks the server wants, a batch to a message, by the message a walk goes by; from any game but the world's server, passed over.</summary>
        private static void WantedTaken(long sender, ZPackage package)
        {
            if (sender != EventLink.WorldSession) return;
            var world = package.ReadString();
            var count = package.ReadInt();
            if (count < 0 || count > 100000) return;
            var wanted = new List<string>(Math.Min(count, 1024));
            for (var i = 0; i < count; i++) wanted.Add(package.ReadString());
            if (world != CompendiumMap.World) return;
            if (_listOnlyForTest)
            {
                _listOnlyForTest = false;
                WantedForTest = wanted;
                return;
            }
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn) return;
            foreach (var batch in WalkList.Batches(DungeonWalks.Maps, wanted))
            {
                var walk = new ZPackage();
                walk.Write(world);
                walk.Write(Utils.Compress(Encoding.UTF8.GetBytes(batch.ToSave()())));
                rpc.InvokeRoutedRPC(WalkedRpc, walk);
                SentWanted += batch.All.Count();
            }
        }

        /// <summary>The game's day here, for when a world's records were forgotten; -1 before the world's clock runs.</summary>
        private static int Today() => EnvMan.instance != null ? EnvMan.instance.GetDay() : -1;

        private static bool IsServer => ZNet.instance != null && ZNet.instance.IsServer();

        /// <summary>The server's record of a dungeon, anyone's walks on it, where this game runs the world and has read its records; none otherwise.</summary>
        public static DungeonMap RecordOf(string id) => IsServer ? _records?.Of(id) : null;

        /// <summary>A dungeon been in, a room newly walked or a new day in it, to be sent with the next walk.</summary>
        public static void Walked(string id) => _toReport = id;

        /// <summary>Sends the record of the dungeon walked to the server; a game that is the server takes it in itself.</summary>
        private static void SendWalk()
        {
            var id = _toReport;
            _toReport = null;
            _reportedAt = Time.unscaledTime;
            var map = DungeonWalks.Maps.Of(id);
            var world = CompendiumMap.World;
            if (map == null || world == null) return;
            var one = new DungeonMaps();
            var sent = one.Keep(id, map.Location, map.Rooms, map.Entrance, map.Generator, map.Turn);
            foreach (var room in map.Walked) one.Walk(id, room);
            sent.FirstDay = map.FirstDay;
            sent.LastDay = map.LastDay;
            if (IsServer)
            {
                MergeIn(one, world);
                return;
            }
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn) return;
            var package = new ZPackage();
            package.Write(world);
            package.Write(Utils.Compress(Encoding.UTF8.GetBytes(one.ToSave()())));
            rpc.InvokeRoutedRPC(WalkedRpc, package);
        }

        /// <summary>Asks the server for the records of a location's dungeons, at most once a minute; a game that is the server answers itself.</summary>
        public static void Ask(string location)
        {
            var world = CompendiumMap.World;
            if (world == null || location == null) return;
            if (AskedAt.TryGetValue(location, out var at) && Time.unscaledTime - at < AskEvery) return;
            AskedAt[location] = Time.unscaledTime;
            if (IsServer)
            {
                if (_records != null) _group.Replace(_records.Only(world, new[] { location }));
                return;
            }
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn) return;
            var package = new ZPackage();
            package.Write(world);
            package.Write(location);
            rpc.InvokeRoutedRPC(AskRpc, package);
        }

        /// <summary>Forgets the world's records: where this game is the server at once, else by asking the server, which does it for an admin alone.</summary>
        public static string ForgetWorld()
        {
            var world = CompendiumMap.World;
            if (world == null) return DungeonMapWords.GroupForgotten(0);
            if (IsServer)
            {
                var forgotten = _records?.ForgetWorld(world) ?? 0;
                _records?.NoteForgotten(world, Today());
                _group.ForgetWorld(world);
                Save(now: true);
                return DungeonMapWords.GroupForgotten(forgotten);
            }
            var server = ZNet.instance != null ? ZNet.instance.GetServerRPC() : null;
            if (server == null || ZRoutedRpc.instance != _registeredOn) return DungeonMapWords.GroupForgotten(0);
            var package = new ZPackage();
            package.Write(world);
            server.Invoke(ForgetRpc, package);
            return DungeonMapWords.GroupAsked;
        }

        // ----- The server's side -----

        private static void TakeWalk(ZPackage package)
        {
            if (!IsServer || _records == null) return;
            var world = package.ReadString();
            var text = Unpacked(package.ReadByteArray());
            if (world != CompendiumMap.World || text == null) return;
            // A client's older Scry names its dungeons by their generators' numbers: re-keyed by place.
            MergeIn(DungeonMaps.Load(text).ByPlace(), world);
        }

        private static void MergeIn(DungeonMaps walked, string world)
        {
            if (_records == null) return;
            _records.Merge(walked, world);
            // The way to a trace goes by the rooms walked: worked out again with them.
            foreach (var (id, _) in walked.OfWorld(world)) DungeonTraceReader.Again(_records, id);
            Merged++;
        }

        /// <summary>
        /// Reads the traces in the world's dungeons: where this game is the server at once, told in
        /// the chat once done, else by asking the server, which reads them for an admin alone and
        /// answers once done.
        /// </summary>
        public static string ReadTraces()
        {
            var world = CompendiumMap.World;
            if (world == null) return DungeonMapWords.TracesRead(0, 0, 0);
            if (IsServer)
            {
                DungeonTraceReader.Start(world, (dungeons, rooms, onTheWay) => Said(dungeons, rooms, onTheWay));
                return DungeonMapWords.TracesReading;
            }
            var server = ZNet.instance != null ? ZNet.instance.GetServerRPC() : null;
            if (server == null || ZRoutedRpc.instance != _registeredOn) return DungeonMapWords.TracesRead(0, 0, 0);
            var package = new ZPackage();
            package.Write(world);
            server.Invoke(ReadTracesRpc, package);
            return DungeonMapWords.TracesAsked;
        }

        /// <summary>The server reads the traces as an admin asks by its own connection, and answers once done; anyone else is refused.</summary>
        private static void ReadTracesAsked(ZNetPeer peer, ZPackage package)
        {
            if (!IsServer || _records == null) return;
            var world = package.ReadString();
            var sender = peer.m_uid;
            if (!IsAdmin(peer) || world != CompendiumMap.World)
            {
                Reply(sender, -1, 0, 0);
                return;
            }
            DungeonTraceReader.Start(world, (dungeons, rooms, onTheWay) => Reply(sender, dungeons, rooms, onTheWay));
        }

        private static void Reply(long sender, int dungeons, int rooms, int onTheWay)
        {
            var reply = new ZPackage();
            reply.Write(dungeons);
            reply.Write(rooms);
            reply.Write(onTheWay);
            ZRoutedRpc.instance?.InvokeRoutedRPC(sender, TracesReadRpc, reply);
        }

        /// <summary>The server's answer on reading traces, said in the chat; from any game but the world's server, passed over.</summary>
        private static void TracesRead(long sender, ZPackage package)
        {
            if (sender != EventLink.WorldSession) return;
            Said(package.ReadInt(), package.ReadInt(), package.ReadInt());
        }

        private static void Said(int dungeons, int rooms, int onTheWay)
        {
            if (_readQuietly)
            {
                _readQuietly = false;
                TracesReadForTest = (dungeons, rooms, onTheWay);
                return;
            }
            Chat.instance.OrNull()?.AddString(ConsoleWords.Reply(dungeons < 0 ? DungeonMapWords.TracesRefused : DungeonMapWords.TracesRead(dungeons, rooms, onTheWay)));
        }

        private static void Answer(long sender, ZPackage package)
        {
            if (!IsServer || _records == null) return;
            var world = package.ReadString();
            var location = package.ReadString();
            if (world != CompendiumMap.World) return;
            var reply = new ZPackage();
            reply.Write(world);
            reply.Write(Utils.Compress(Encoding.UTF8.GetBytes(_records.Only(world, new[] { location }).ToSave()())));
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, reply);
            Answered++;
        }

        /// <summary>The server forgets the world's records as an admin asks by its own connection, and answers how many it forgot; anyone else is refused.</summary>
        private static void ForgetAsked(ZNetPeer peer, ZPackage package)
        {
            if (!IsServer || _records == null) return;
            var world = package.ReadString();
            var sender = peer.m_uid;
            var forgotten = IsAdmin(peer) && world == CompendiumMap.World ? _records.ForgetWorld(world) : -1;
            // The day noted, so the walks players list later are not taken back from before it.
            if (forgotten >= 0) _records.NoteForgotten(world, Today());
            if (forgotten >= 0) Save(now: true);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, ForgotRpc, forgotten);
        }

        /// <summary>Whether a player's connection is an admin's, by the server's admin list.</summary>
        private static bool IsAdmin(ZNetPeer peer) => peer.m_socket != null && ZNet.instance.IsAdmin(peer.m_socket.GetHostName());

        /// <summary>The players' connections the admin asks are registered on, those gone let go of each time the peers are looked at.</summary>
        private static readonly HashSet<ZRpc> AdminAsksOn = new HashSet<ZRpc>();

        private static float _peersLookedAt = float.NegativeInfinity;

        /// <summary>Registers the admin asks on each player's connection to this server, looked at each second: a request then comes with the connection it came by.</summary>
        private static void RegisterAdminAsks()
        {
            if (ZNet.instance == null || Time.unscaledTime - _peersLookedAt < 1f) return;
            _peersLookedAt = Time.unscaledTime;
            var peers = ZNet.instance.GetPeers();
            AdminAsksOn.RemoveWhere(rpc => !peers.Exists(peer => peer.m_rpc == rpc));
            foreach (var peer in peers)
            {
                if (peer?.m_rpc == null || !AdminAsksOn.Add(peer.m_rpc)) continue;
                var from = peer;
                peer.m_rpc.Register<ZPackage>(ForgetRpc, (rpc, package) => Guard.Run(Feature.GroupDungeonMaps, "forgetting the world's dungeon records", () => ForgetAsked(from, package)));
                peer.m_rpc.Register<ZPackage>(ReadTracesRpc, (rpc, package) => Guard.Run(Feature.GroupDungeonMaps, "reading the traces in the world's dungeons as asked", () => ReadTracesAsked(from, package)));
            }
        }

        /// <summary>Reads the world's records once it runs, and writes them as they change, at most every half minute.</summary>
        private static void KeepRecords()
        {
            var world = CompendiumMap.World;
            if (world == null) return;
            if (world != _recordsWorld)
            {
                Save(now: true);
                _recordsWorld = world;
                _records = null;
                _unreadable = false;
                var path = PathOf(world);
                _reading = Files.Then(() => Read(path));
            }
            if (_reading != null)
            {
                if (!_reading.IsCompleted) return;
                if (_reading.Status == TaskStatus.RanToCompletion) _records = _reading.Result;
                else
                {
                    Faults.Tell(Feature.GroupDungeonMaps, "reading the world's dungeon records", _reading.Exception?.GetBaseException());
                    _records = new DungeonMaps();
                    _unreadable = true;
                }
                _reading = null;
            }
            // Once its records are read, a dedicated server reads the traces in the world's dungeons, a
            // few milliseconds a frame; a game hosting the world reads them only on the command, its
            // player playing in it.
            if (_records != null && _tracedFor != world && ZNet.instance.IsDedicated() && ZoneSystem.instance != null && DungeonDB.instance != null)
            {
                _tracedFor = world;
                DungeonTraceReader.Start(world, null);
            }
            DungeonTraceReader.Step(_records, world);
            Save();
        }

        /// <summary>Writes the records once they changed, at most every half minute unless asked to now, on a worker thread; a file that could not be read is never written over.</summary>
        private static void Save(bool now = false)
        {
            if (_records == null || _recordsWorld == null || _unreadable || !_records.Changed || (!now && Time.unscaledTime - _savedAt < 30f)) return;
            _savedAt = Time.unscaledTime;
            var text = _records.ToSave();
            var path = PathOf(_recordsWorld);
            Files.Then("keeping the world's dungeon records", () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                FileSwap.Write(path, aside => File.WriteAllText(aside, text()), lasting: true);
            });
        }

        /// <summary>Writes the records and waits a moment for them to be written, as the game or the server quits.</summary>
        public static void Finish()
        {
            Save(now: true);
            Files.Then(() => true).Wait(QuitWaitMs);
        }

        private static DungeonMaps Read(string path)
        {
            var file = FileSwap.ToRead(path);
            return DungeonMaps.Load(file != null ? File.ReadAllText(file) : null).ByPlace();
        }

        private static string PathOf(string world) => Path.Combine(Settings.CacheFolder, DungeonMaps.ServerFile(world));

        // ----- A client's side -----

        /// <summary>The server's records of a location's dungeons; from any game but the world's server, passed over.</summary>
        private static void Take(long sender, ZPackage package)
        {
            if (sender != EventLink.WorldSession) return;
            var world = package.ReadString();
            var text = Unpacked(package.ReadByteArray());
            if (world != CompendiumMap.World || text == null) return;
            // The answer stands for each dungeon it holds: the server takes a guess round a loop back as a walk shows the other way.
            _group.Replace(DungeonMaps.Load(text).ByPlace());
        }

        /// <summary>The server's answer to forgetting the world's records, said in the chat; from any game but the world's server, passed over.</summary>
        private static void Forgot(long sender, int count)
        {
            if (sender != EventLink.WorldSession) return;
            Chat.instance.OrNull()?.AddString(ConsoleWords.Reply(count < 0 ? DungeonMapWords.GroupForgetRefused : DungeonMapWords.GroupForgotten(count)));
            if (count > 0) _group.ForgetWorld(CompendiumMap.World);
        }

        /// <summary>The text a message carries, none where it is too much or no text.</summary>
        private static string Unpacked(byte[] packed)
        {
            var bytes = Packed.Unpack(packed, MostText);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
        }
    }
}
