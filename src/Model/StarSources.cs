using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>How a source gives a creature its level, as the game's code for each has it.</summary>
    internal enum LevelWay
    {
        /// <summary>A world spawn or a raid (<c>SpawnSystem.Spawn</c>): from its least level, rising toward its most, the least kept whatever the most.</summary>
        World,

        /// <summary>A spawner, a nest or a trigger (<c>CreatureSpawner</c>, <c>SpawnArea</c>, <c>TriggerSpawner</c>): levelled only where its most is over one.</summary>
        Spawner,

        /// <summary>A summon (<c>SpawnAbility</c>): set to the level its caster's skill reaches.</summary>
        Set,
    }

    /// <summary>
    /// The levels a place sets for the spawn points within it, its dungeon's rooms among them, in
    /// place of theirs (<c>Location.m_enemyMinLevelOverride</c>, <c>m_enemyMaxLevelOverride</c>),
    /// but for the spawn groups it leaves out (<c>m_excludeEnemyLevelOverrideGroups</c>); -1 for none.
    /// </summary>
    internal sealed class PlaceLevels
    {
        public int Min = -1, Max = -1;
        public HashSet<int> Excluded = new HashSet<int>();

        /// <summary>A place that sets no levels.</summary>
        public static readonly PlaceLevels None = new PlaceLevels();

        /// <summary>Whether it sets any level at all.</summary>
        public bool Sets => Min >= 0 || Max >= 0;

        /// <summary>A spawn point's least and most level within it, as <c>CreatureSpawner.Spawn</c> takes them.</summary>
        public (int Min, int Max) Over(int min, int max, int group)
        {
            if (Excluded != null && Excluded.Contains(group)) return (min, max);
            return (Min >= 0 ? Min : min, Max >= 0 ? Max : max);
        }
    }

    /// <summary>A spawn point of a place: the creature it places, its spawn group, and its own least and most level (<c>CreatureSpawner</c>).</summary>
    internal sealed class PlaceSpawnLevel
    {
        public string Creature = "";
        public int Group, Min = 1, Max = 1;
    }

    /// <summary>
    /// Every source in the world that can give a creature a level, and the most it can reach:
    /// each source by its own way (<see cref="LevelWay"/>), and a young one, an egg or one grown
    /// up at the level of what it came of (<c>Procreation</c>, <c>EggGrow</c>, <c>Growup</c>).
    /// Where the world lets no level rise, only a source's least counts.
    /// </summary>
    internal sealed class StarSources
    {
        private readonly List<(string Creature, LevelWay Way, int Min, int Max)> _own = new List<(string, LevelWay, int, int)>();
        private readonly List<(string From, string To, int Least)> _passes = new List<(string, string, int)>();
        private Dictionary<string, int> _settled;

        /// <summary>Whether a level can rise at all, as the world's settings have it (<c>SpawnSystem.GetLevelUpChance</c> over none).</summary>
        public bool LevelsRise
        {
            get => _rise;
            set
            {
                _rise = value;
                _settled = null;
            }
        }

        private bool _rise = true;

        /// <summary>A source of a creature: how it levels it, from its least to its most.</summary>
        public void Add(string creature, LevelWay way, int min, int max)
        {
            if (string.IsNullOrEmpty(creature)) return;
            _own.Add((creature, way, min, max));
            _settled = null;
        }

        /// <summary>One thing coming of another at its level, at the least given: a young one of its parent, a creature of its egg, one grown up of its young.</summary>
        public void Passes(string from, string to, int least = 0)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            _passes.Add((from, to, least));
            _settled = null;
        }

        /// <summary>
        /// A place's spawn points, each by every place it stands in: a location's own, or each
        /// location whose dungeon a room is built into, its points' own levels where it stands in none.
        /// </summary>
        public void Place(IEnumerable<PlaceSpawnLevel> spawns, IReadOnlyList<PlaceLevels> standsIn)
        {
            foreach (var spawn in spawns ?? Array.Empty<PlaceSpawnLevel>())
            {
                if (spawn == null) continue;
                if (standsIn == null || standsIn.Count == 0)
                {
                    Add(spawn.Creature, LevelWay.Spawner, spawn.Min, spawn.Max);
                    continue;
                }
                foreach (var place in standsIn)
                {
                    var (min, max) = (place ?? PlaceLevels.None).Over(spawn.Min, spawn.Max, spawn.Group);
                    Add(spawn.Creature, LevelWay.Spawner, min, max);
                }
            }
        }

        /// <summary>Another holding every source of this one, to take more apart.</summary>
        public StarSources Copy()
        {
            var copy = new StarSources { LevelsRise = LevelsRise };
            copy._own.AddRange(_own);
            copy._passes.AddRange(_passes);
            return copy;
        }

        /// <summary>The most level a source can give, by its way.</summary>
        public static int Reach(LevelWay way, int min, int max, bool rise)
        {
            switch (way)
            {
                case LevelWay.World: return Math.Max(1, rise ? Math.Max(min, max) : min);
                case LevelWay.Spawner: return max <= 1 ? 1 : Math.Max(1, rise ? Math.Max(min, max) : min);
                default: return Math.Max(1, max);
            }
        }

        /// <summary>The most level anything in the world can give a creature; none where nothing gives it any.</summary>
        public int? MostLevel(string creature)
        {
            if (string.IsNullOrEmpty(creature)) return null;
            if (_settled == null) Settle();
            return _settled.TryGetValue(creature, out var most) ? most : (int?)null;
        }

        /// <summary>Each source's level carried on along what comes of what, until nothing rises.</summary>
        private void Settle()
        {
            var settled = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (creature, way, min, max) in _own)
            {
                var reach = Reach(way, min, max, LevelsRise);
                if (!settled.TryGetValue(creature, out var had) || reach > had) settled[creature] = reach;
            }
            var rose = true;
            while (rose)
            {
                rose = false;
                foreach (var (from, to, least) in _passes)
                {
                    if (!settled.TryGetValue(from, out var level)) continue;
                    var passed = Math.Max(level, least);
                    if (settled.TryGetValue(to, out var had) && had >= passed) continue;
                    settled[to] = passed;
                    rose = true;
                }
            }
            _settled = settled;
        }

        /// <summary>
        /// The stars a page shows of a creature, from the most level its sources reach: that less
        /// one; none where nothing gives the game's own creature a level; and for a mod's creature
        /// no source names, two as the game's are, unsure, as its mod may give them in its own way.
        /// </summary>
        public static (int Stars, bool Unsure) Shown(int? most, bool mod)
        {
            if (most is int level) return (Math.Max(0, level - 1), false);
            return mod ? (2, true) : (0, false);
        }
    }
}
