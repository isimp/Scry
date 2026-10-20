using System.Collections.Generic;
using System.IO;
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
    /// the world's records. The messages are registered on each new connection, as the game makes its
    /// routed messages anew for each.
    /// </summary>
    internal static class GroupMapsLink
    {
        static GroupMapsLink() => WorldCaches.Register(nameof(GroupMapsLink), Forget);

        public const string WalkedRpc = "Scry_MapWalked";
        public const string AskRpc = "Scry_GroupAsk";
        public const string AnswerRpc = "Scry_GroupAnswer";
        public const string ForgetRpc = "Scry_GroupForget";
        public const string ForgotRpc = "Scry_GroupForgot";

        /// <summary>How often a client sends its walk, and asks again for a location's records, in seconds.</summary>
        private const float ReportEvery = 10f, AskEvery = 60f;

        /// <summary>The most text of maps a message may carry, unpacked.</summary>
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
                    rpc.Register<ZPackage>(AnswerRpc, (sender, package) => Guard.Run(Feature.GroupDungeonMaps, "taking the server's dungeon records", () => Take(package)));
                    rpc.Register<ZPackage>(ForgetRpc, (sender, package) => Guard.Run(Feature.GroupDungeonMaps, "forgetting the world's dungeon records", () => ForgetAsked(sender, package)));
                    rpc.Register<int>(ForgotRpc, (sender, count) => Guard.Run(Feature.GroupDungeonMaps, "taking the server's answer to forgetting", () => Forgot(count)));
                }
            }
            if (IsServer) KeepRecords();
            if (_toReport != null && Time.unscaledTime - _reportedAt >= ReportEvery) SendWalk();
        }

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
                if (_records != null) _group.Merge(_records.Only(world, new[] { location }));
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
                _group.ForgetWorld(world);
                Save(now: true);
                return DungeonMapWords.GroupForgotten(forgotten);
            }
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc != _registeredOn) return DungeonMapWords.GroupForgotten(0);
            var package = new ZPackage();
            package.Write(world);
            rpc.InvokeRoutedRPC(ForgetRpc, package);
            return DungeonMapWords.GroupAsked;
        }

        // ----- The server's side -----

        private static void TakeWalk(ZPackage package)
        {
            if (!IsServer || _records == null) return;
            var world = package.ReadString();
            var text = Unpacked(package.ReadByteArray());
            if (world != CompendiumMap.World || text == null) return;
            MergeIn(DungeonMaps.Load(text), world);
        }

        private static void MergeIn(DungeonMaps walked, string world)
        {
            if (_records == null) return;
            _records.Merge(walked, world);
            Merged++;
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

        private static void ForgetAsked(long sender, ZPackage package)
        {
            if (!IsServer || _records == null) return;
            var world = package.ReadString();
            var peer = ZNet.instance.GetPeer(sender);
            var admin = peer?.m_socket != null && ZNet.instance.IsAdmin(peer.m_socket.GetHostName());
            var forgotten = admin && world == CompendiumMap.World ? _records.ForgetWorld(world) : -1;
            if (forgotten > 0) Save(now: true);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, ForgotRpc, forgotten);
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
                FileSwap.Write(path, aside => File.WriteAllText(aside, text()));
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
            return DungeonMaps.Load(file != null ? File.ReadAllText(file) : null);
        }

        private static string PathOf(string world) => Path.Combine(Settings.CacheFolder, DungeonMaps.ServerFile(world));

        // ----- A client's side -----

        private static void Take(ZPackage package)
        {
            var world = package.ReadString();
            var text = Unpacked(package.ReadByteArray());
            if (world != CompendiumMap.World || text == null) return;
            _group.Merge(DungeonMaps.Load(text), world);
        }

        private static void Forgot(int count)
        {
            Chat.instance.OrNull()?.AddString(ConsoleWords.Reply(count < 0 ? DungeonMapWords.GroupForgetRefused : DungeonMapWords.GroupForgotten(count)));
            if (count > 0) _group.ForgetWorld(CompendiumMap.World);
        }

        /// <summary>The text a message carries, none where it is too much or no text.</summary>
        private static string Unpacked(byte[] packed)
        {
            if (packed == null) return null;
            var bytes = Utils.Decompress(packed);
            return bytes != null && bytes.Length <= MostText ? Encoding.UTF8.GetString(bytes) : null;
        }
    }
}
