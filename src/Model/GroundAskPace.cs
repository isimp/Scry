using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a player's game asks the world's server about a ground of its own, each kind paced apart (<see cref="GroundAskPace"/>).</summary>
    internal enum GroundAsk
    {
        /// <summary>Which kinds of place lie in its explored map, or in the ground explored since.</summary>
        Explored,

        /// <summary>Which kinds of place lie in its ground from shared maps.</summary>
        Shared,

        /// <summary>Which dungeons lie in a ground, explored or from shared maps.</summary>
        Dungeons,
    }

    /// <summary>
    /// How often the world's server answers each player's asks about a ground (<see cref="GroundAsk"/>).
    /// Each answer tells something of where things lie, so a game asking with made-up ground, cell
    /// by cell, could learn where every kind of place and every dungeon of the world lies. Each
    /// player is answered a few asks of a kind at once (<see cref="Burst"/>), then one in so many
    /// seconds (<see cref="Every"/>): an explored map or shared ground one a minute, as a game asks
    /// of its own at most (<see cref="MapGrowth.Every"/>), dungeons one in five minutes, as a game
    /// asks of each of its two grounds every ten; what comes sooner is passed over. A player goes
    /// by their connection's platform id, so joining again gives no new asks; one quiet long enough
    /// to be answered as a newcomer is let go of (<see cref="Forget"/>).
    /// </summary>
    internal sealed class GroundAskPace
    {
        /// <summary>How many asks of a kind a player is answered at once.</summary>
        public static int Burst(GroundAsk ask) => ask == GroundAsk.Dungeons ? 4 : 3;

        /// <summary>How many seconds give a player one more ask of a kind.</summary>
        public static double Every(GroundAsk ask) => ask == GroundAsk.Dungeons ? 300.0 : 60.0;

        /// <summary>Each player's asks of each kind: how many are left, and when that was told.</summary>
        private readonly Dictionary<(string Player, GroundAsk Ask), (double Left, double At)> _asks = new Dictionary<(string, GroundAsk), (double, double)>();

        /// <summary>How many asks were passed over.</summary>
        public int PassedOver { get; private set; }

        /// <summary>How many players' asks of a kind are held.</summary>
        public int Held => _asks.Count;

        /// <summary>Whether a player's ask of a kind is answered at a time in seconds, taking one of their asks; else passed over.</summary>
        public bool Answers(string player, GroundAsk ask, double now)
        {
            var key = (player ?? "", ask);
            var left = LeftAt(key, now);
            var answered = left >= 1.0;
            if (!answered) PassedOver++;
            _asks[key] = (answered ? left - 1.0 : left, now);
            return answered;
        }

        /// <summary>How many asks a player has left of a kind at a time, each <see cref="Every"/> seconds giving one more up to <see cref="Burst"/>.</summary>
        private double LeftAt((string Player, GroundAsk Ask) key, double now) =>
            _asks.TryGetValue(key, out var had) ? Math.Min(Burst(key.Ask), had.Left + Math.Max(0.0, now - had.At) / Every(key.Ask)) : Burst(key.Ask);

        /// <summary>Lets go of every player's asks of a kind that would be answered as a newcomer's by a time.</summary>
        public void Forget(double now)
        {
            var full = new List<(string, GroundAsk)>();
            foreach (var key in _asks.Keys) if (LeftAt(key, now) >= Burst(key.Ask)) full.Add(key);
            foreach (var key in full) _asks.Remove(key);
        }
    }
}
