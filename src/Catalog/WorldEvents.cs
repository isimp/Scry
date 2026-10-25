using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The events this game runs, made as it runs them (<see cref="WorldEvent"/>): numbered in
    /// turn, with who did them as the game itself knows it, told to the character's game, which is
    /// this one (<see cref="CompendiumKeeper.Tell(WorldEvent)"/>), and sent to the other players' games it
    /// concerns (<see cref="EventLink.Send"/>). Who did it goes by the player's
    /// game's session, as the game addresses a player's game: a hit's attacker among the world's
    /// players by their character, a creature's attackers by the names the game flags it with as
    /// they hit it (<c>Character.RPC_Damage</c>) and tells each of them its kill by
    /// (<c>Character.OnDeath</c>), a pick by the game that asked for it. What another game ran
    /// and this one saw the trace of, a creature's remains or a thing picked, is told here
    /// alone, never sent (<see cref="Seen"/>), marked so the game that ran it may tell the same.
    /// </summary>
    internal static class WorldEvents
    {
        static WorldEvents() => WorldCaches.Register(nameof(WorldEvents), Forget);

        private static readonly long[] NoOne = Array.Empty<long>();

        /// <summary>The number of the last event this game ran in this world.</summary>
        private static int _number;

        /// <summary>An event this game ran, by the prefab of the thing it happened to, numbered next.</summary>
        public static WorldEvent Make(Happened what, string thing, Vector3 at, IReadOnlyList<(string Item, int Amount)> came = null, int level = 1,
            IReadOnlyList<long> doers = null, string with = null, string mark = null) =>
            new WorldEvent(ZNet.GetUID(), ++_number, what, thing, at.x, at.y, at.z, came, level, doers, with, mark);

        /// <summary>An event this game ran, told to the character.</summary>
        public static void Tell(Happened what, string thing, Vector3 at, IReadOnlyList<(string Item, int Amount)> came = null, int level = 1,
            IReadOnlyList<long> doers = null, string with = null, string mark = null, Vector3? stood = null, bool? seen = null)
        {
            if (string.IsNullOrEmpty(thing)) return;
            var told = Make(what, thing, at, came, level, doers, with, mark);
            CompendiumKeeper.Tell(told, stood, seen);
            EventLink.Send(told);
        }

        /// <summary>An event another game ran, seen from this one by what it left: told to the character, never sent.</summary>
        public static WorldEvent Seen(Happened what, string thing, Vector3 at, IReadOnlyList<(string Item, int Amount)> came, int level,
            IReadOnlyList<long> doers, string mark)
        {
            if (string.IsNullOrEmpty(thing)) return null;
            var told = Make(what, thing, at, came, level, doers, null, mark);
            CompendiumKeeper.Tell(told);
            return told;
        }

        /// <summary>The id of a networked thing, as the mark of an event about it; none for a thing not in the network.</summary>
        public static string MarkOf(Component thing)
        {
            var view = thing != null ? thing.GetComponent<ZNetView>().OrNull() : null;
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo != null ? zdo.m_uid.ToString() : null;
        }

        /// <summary>Something seen done to a thing where it stands, which no one does.</summary>
        public static void Tell(Happened what, Component thing, string with = null)
        {
            if (thing == null) return;
            Tell(what, Utils.GetPrefabName(thing.gameObject), thing.transform.position, with: with);
        }

        /// <summary>The player whose hit it is: the one among the world's players whose character dealt it; no one for a hit no player dealt.</summary>
        public static IReadOnlyList<long> HitBy(HitData hit)
        {
            if (hit == null || hit.m_attacker.IsNone() || ZNet.instance == null) return NoOne;
            foreach (var player in ZNet.instance.GetPlayerList())
            {
                if (player.m_characterID == hit.m_attacker) return new[] { player.m_characterID.UserID };
            }
            return NoOne;
        }

        /// <summary>The players a creature is flagged as hit by, the game's way: by name, among the world's players.</summary>
        public static IReadOnlyList<long> Attackers(Character creature)
        {
            var zdo = creature != null ? creature.GetComponent<ZNetView>().OrNull()?.GetZDO() : null;
            if (zdo == null || ZNet.instance == null || zdo.GetInt(ZDOVars.s_attackers) == 0) return NoOne;
            List<long> attackers = null;
            foreach (var player in ZNet.instance.GetPlayerList())
            {
                if (!zdo.GetBool(AttackerKey(player.m_name))) continue;
                (attackers ?? (attackers = new List<long>())).Add(player.m_characterID.UserID);
            }
            return attackers ?? (IReadOnlyList<long>)NoOne;
        }

        /// <summary>
        /// The key a creature is flagged under as hit by a player of a name, as the game makes it: the
        /// attackers' key's number, then the name. That number is positive, so its digits read the
        /// same whatever the PC's language.
        /// </summary>
        private static string AttackerKey(string name) => Stored.Count(ZDOVars.s_attackers) + name;

        /// <summary>The player whose game asked for a pick, by the session it was sent from.</summary>
        public static IReadOnlyList<long> AskedBy(long sender) => sender == 0L ? NoOne : new[] { sender };

        /// <summary>On leaving a world: numbering begins again with the next world's session.</summary>
        private static void Forget() => _number = 0;
    }
}
