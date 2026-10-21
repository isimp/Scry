using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A player's asks about a ground of their own (<see cref="GroundAsk"/>), which go to the world's
    /// server by the player's own connection (<c>ZRpc</c>), not by routed messages: a routed message
    /// names its sender as the sending game wrote it, so a pace kept by sender could be dodged, or
    /// spent on another player's behalf. The server registers them on each player's connection,
    /// looked at each second, answers each player at the pace of <see cref="GroundAskPace"/>, by
    /// their connection's platform id, and says once in its log of a player whose asks it passed
    /// over. The answers go back by routed message to that connection's game.
    /// </summary>
    internal static class GroundAsks
    {
        static GroundAsks() => WorldCaches.Register(nameof(GroundAsks), Forget);

        private static readonly GroundAskPace Pace = new GroundAskPace();

        /// <summary>The players' connections the asks are registered on, those gone let go of each time the peers are looked at.</summary>
        private static readonly HashSet<ZRpc> RegisteredOn = new HashSet<ZRpc>();

        /// <summary>The players whose asks passed over were said in the log.</summary>
        private static readonly HashSet<string> Told = new HashSet<string>(System.StringComparer.Ordinal);

        private static float _peersLookedAt = float.NegativeInfinity;

        /// <summary>How many asks the server passed over; for the self-test.</summary>
        [Diagnostic]
        public static int PassedOver => Pace.PassedOver;

        /// <summary>Sends an ask to the world's server by this game's own connection; false where it has none, being the server.</summary>
        public static bool Send(string ask, ZPackage package)
        {
            var server = ZNet.instance != null ? ZNet.instance.GetServerRPC() : null;
            if (server == null) return false;
            server.Invoke(ask, package);
            return true;
        }

        /// <summary>Where this game runs the world, registers the asks on each player's connection, once a second, and lets go of players quiet long enough; called each frame, on a dedicated server too.</summary>
        public static void Update()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || Time.unscaledTime - _peersLookedAt < 1f) return;
            _peersLookedAt = Time.unscaledTime;
            var peers = ZNet.instance.GetPeers();
            RegisteredOn.RemoveWhere(rpc => !peers.Exists(peer => peer.m_rpc == rpc));
            Pace.Forget(Time.unscaledTime);
            foreach (var peer in peers)
            {
                if (peer?.m_rpc == null || !RegisteredOn.Add(peer.m_rpc)) continue;
                var from = peer;
                peer.m_rpc.Register<ZPackage>(CompendiumLink.AskRpc, (rpc, package) => Guard.Run(Feature.CompendiumMap, "answering a player's explored map", () =>
                {
                    if (Answers(from, GroundAsk.Explored)) CompendiumLink.Answer(from.m_uid, package, CompendiumLink.AnswerRpc);
                }));
                peer.m_rpc.Register<ZPackage>(CompendiumLink.SharedAskRpc, (rpc, package) => Guard.Run(Feature.CompendiumMap, "answering a player's shared ground", () =>
                {
                    if (Answers(from, GroundAsk.Shared)) CompendiumLink.Answer(from.m_uid, package, CompendiumLink.SharedAnswerRpc);
                }));
                peer.m_rpc.Register<ZPackage>(DungeonsInGround.AskRpc, (rpc, package) => Guard.Run(Feature.DungeonMaps, "answering which dungeons lie in a player's ground", () =>
                {
                    if (Answers(from, GroundAsk.Dungeons)) DungeonsInGround.Asked(from.m_uid, package);
                }));
            }
        }

        /// <summary>Whether a player's ask is answered now, by their connection's platform id; one passed over is said in the log once a player.</summary>
        private static bool Answers(ZNetPeer peer, GroundAsk ask)
        {
            var player = peer.m_socket?.GetHostName() ?? Stored.Count(peer.m_uid);
            if (Pace.Answers(player, ask, Time.unscaledTime)) return true;
            if (Told.Add(player)) Log.Report(CompendiumWords.GroundAsksPassedOver(peer.m_playerName));
            return false;
        }

        private static void Forget()
        {
            RegisteredOn.Clear();
            Told.Clear();
            _peersLookedAt = float.NegativeInfinity;
        }
    }
}
