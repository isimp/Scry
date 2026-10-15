using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a hook of the compendium records in play (<see cref="PlayCounts"/>).</summary>
    internal enum PlayEvent
    {
        /// <summary>A hit of the character's on a creature, the game telling how its damage landed.</summary>
        HitTold,

        /// <summary>A hit of the character's on a creature's weak spot.</summary>
        WeakSpotHit,

        /// <summary>A creature's attack hitting the character.</summary>
        HitTaken,

        /// <summary>A creature's loot coming out as it died, counted.</summary>
        LootCounted,

        /// <summary>A tree, log, rock or other thing breaking and giving something, counted.</summary>
        BreakCounted,

        /// <summary>A tamed creature giving birth within sight.</summary>
        Born,

        /// <summary>A young creature growing up within sight.</summary>
        GrewUp,

        /// <summary>A pickable growing back within sight.</summary>
        GrewBack,

        /// <summary>A creature tamed near the character.</summary>
        Tamed,

        /// <summary>The explored map read for the compendium.</summary>
        MapRead,

        /// <summary>A pickable picked and what it gave counted.</summary>
        PickCounted,

        /// <summary>A chest opened for the first time and what it held counted.</summary>
        ChestCounted,

        /// <summary>A starred creature seen to fall.</summary>
        StarredFell,

        /// <summary>A spawner seen spawning.</summary>
        Spawned,

        /// <summary>A producer collected from.</summary>
        Collected,

        /// <summary>A weather seen coming in a biome.</summary>
        WeatherSeen,
    }

    /// <summary>
    /// How often each hook of the compendium fired this session, so ordinary play shows which
    /// really ran on this machine (some run only for an object's owner): a zero where it surely
    /// happened is a fault to chase. Told by the self-test.
    /// </summary>
    internal sealed class PlayCounts
    {
        private readonly int[] _counts = new int[Enum.GetValues(typeof(PlayEvent)).Length];

        /// <summary>One more of a kind.</summary>
        public void Note(PlayEvent what) => _counts[(int)what]++;

        /// <summary>How many of a kind this session.</summary>
        public int Of(PlayEvent what) => _counts[(int)what];

        /// <summary>Every kind with its count, the zeros among them.</summary>
        public string Told()
        {
            var told = new List<string>();
            foreach (PlayEvent what in Enum.GetValues(typeof(PlayEvent))) told.Add(Said(what, Of(what)));
            return "this session: " + string.Join(", ", told);
        }

        private static string Said(PlayEvent what, int n)
        {
            switch (what)
            {
                case PlayEvent.HitTold: return Naming.Count(n, "hit", "hits") + " told";
                case PlayEvent.WeakSpotHit: return Naming.Count(n, "weak spot hit", "weak spot hits");
                case PlayEvent.HitTaken: return Naming.Count(n, "hit", "hits") + " taken";
                case PlayEvent.LootCounted: return Naming.Count(n, "kill's", "kills'") + " loot counted";
                case PlayEvent.BreakCounted: return Naming.Count(n, "break", "breaks") + " counted";
                case PlayEvent.Born: return Naming.Count(n, "birth", "births") + " seen";
                case PlayEvent.GrewUp: return Numbers.Count(n) + " seen grow up";
                case PlayEvent.GrewBack: return Numbers.Count(n) + " seen grow back";
                case PlayEvent.Tamed: return Numbers.Count(n) + " tamed";
                case PlayEvent.MapRead: return Naming.Count(n, "map read", "map reads");
                case PlayEvent.PickCounted: return Naming.Count(n, "pick", "picks") + " counted";
                case PlayEvent.ChestCounted: return Naming.Count(n, "chest", "chests") + " counted";
                case PlayEvent.StarredFell: return Naming.Count(n, "starred fall", "starred falls") + " seen";
                case PlayEvent.Spawned: return Naming.Count(n, "spawn", "spawns") + " seen";
                case PlayEvent.Collected: return Numbers.Count(n) + " collected from";
                default: return Naming.Count(n, "weather", "weathers") + " seen";
            }
        }
    }
}
