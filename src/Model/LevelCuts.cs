using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>How the whole of a dungeon's example opens from above: by its floors, or, tried beside them, by the plan's levels a group of rooms one cut opens at a time, or a whole level filmed in as many cuts as it needs.</summary>
    internal enum InsideLevels
    {
        Floors,
        Steps,
        Films,
    }

    /// <summary>
    /// The groups of a dungeon's rooms that one cut opens, level by level as the plan box lays
    /// the levels out (<see cref="PlanLevels"/>). The stage cuts with one height at a time, and a
    /// cut opens a room only over its floor, by what stands on it (<see cref="PlaceView.LeastHeadroom"/>),
    /// and under its ceiling (<see cref="PlaceView.UnderCeiling"/>); a level's rooms stepping down
    /// a few metres, a low corridor under higher rooms cannot be opened with them. Going down
    /// each level from its highest room, a room joins the group above it while one cut still
    /// opens them all, else begins a group of its own; a room too low to stand in stands alone
    /// and keeps its roof. The groups come highest first, level by level. A tall room of a level
    /// below, a shaft or a high cave, reaches into the groups over it where one could stand in it
    /// over their lowest floor (<see cref="Reaches"/>), and opens with them.
    /// </summary>
    internal sealed class LevelCuts
    {
        /// <summary>A group of a level's rooms one cut opens: their places among the rooms given, highest ground first.</summary>
        internal sealed class Group
        {
            /// <summary>The plan's level it is on, 0 the highest.</summary>
            public int Level;

            public readonly List<int> Rooms = new List<int>();

            /// <summary>The highest ground among its rooms, the lowest, and the lowest ceiling.</summary>
            public float Top, Low, Ceiling;

            /// <summary>Where the stage cuts to open it: head height over its highest ground, under its lowest ceiling, never into what stands on its floors.</summary>
            public float Cut => PlaceView.CutHeight(Top, null, Ceiling);

            /// <summary>Whether its cut opens every room in it: over each floor and under each ceiling.</summary>
            public bool OpensAll => Opens(Top, Ceiling);
        }

        private readonly List<Group> _groups = new List<Group>();
        private int[] _groupOf = Array.Empty<int>();
        private float[] _ceilingOf = Array.Empty<float>();

        /// <summary>The groups, highest first, level by level.</summary>
        public IReadOnlyList<Group> Groups => _groups;

        /// <summary>How many levels the groups are on.</summary>
        public int Levels { get; private set; }

        /// <summary>The group a room is in, by its place among the rooms given; -1 for one on no level.</summary>
        public int GroupOf(int room) => room >= 0 && room < _groupOf.Length ? _groupOf[room] : -1;

        /// <summary>The level a room is on, by its group; -1 for one on none.</summary>
        public int LevelOf(int room) => GroupOf(room) is var group && group >= 0 ? _groups[group].Level : -1;

        /// <summary>The first group of a level, the one its mark on the ruler opens; -1 for a level with none.</summary>
        public int FirstOf(int level) => _groups.FindIndex(g => g.Level == level);

        /// <summary>How many groups a level holds.</summary>
        public int GroupsOf(int level) => _groups.FindAll(g => g.Level == level).Count;

        /// <summary>A level's highest ground, its first group's, where every step of it is framed; NaN for a level with none.</summary>
        public float TopOf(int level) => FirstOf(level) is var first && first >= 0 ? _groups[first].Top : float.NaN;

        /// <summary>
        /// Whether a room of a level below a group's rises into it: its ceiling a head height over
        /// the group's lowest floor, as a shaft or a high cave does; never a room of the group's
        /// own level, a level above it, or none.
        /// </summary>
        public bool Reaches(int room, int group)
        {
            if (group < 0 || group >= _groups.Count || LevelOf(room) <= _groups[group].Level) return false;
            return Opens(_groups[group].Low, _ceilingOf[room]);
        }

        /// <summary>Whether a room of a level below rises into a level, into any of its groups (<see cref="Reaches"/>).</summary>
        public bool ReachesLevel(int room, int level)
        {
            for (var i = 0; i < _groups.Count; i++) if (_groups[i].Level == level && Reaches(room, i)) return true;
            return false;
        }

        /// <summary>Whether a room stands whole with a level opened: on it, or rising into it from below (<see cref="ReachesLevel"/>).</summary>
        public bool StandsWith(int room, int level) => LevelOf(room) == level || ReachesLevel(room, level);

        /// <summary>Whether one cut opens rooms whose highest ground and lowest ceiling these are.</summary>
        private static bool Opens(float top, float ceiling) => ceiling - PlaceView.UnderCeiling >= top + PlaceView.LeastHeadroom;

        /// <summary>The groups of rooms by each one's ground, ceiling over it and level (-1 for none), each at the same place in all three.</summary>
        public static LevelCuts Of(IReadOnlyList<float> grounds, IReadOnlyList<float> ceilings, IReadOnlyList<int> levels)
        {
            var cuts = new LevelCuts { _groupOf = new int[grounds.Count], _ceilingOf = new float[grounds.Count] };
            for (var i = 0; i < cuts._ceilingOf.Length; i++) cuts._ceilingOf[i] = ceilings[i];
            for (var i = 0; i < cuts._groupOf.Length; i++) cuts._groupOf[i] = -1;
            var top = -1;
            for (var i = 0; i < levels.Count; i++) top = Math.Max(top, levels[i]);
            cuts.Levels = top + 1;

            var order = new List<int>();
            for (var level = 0; level <= top; level++)
            {
                order.Clear();
                for (var i = 0; i < grounds.Count; i++) if (levels[i] == level) order.Add(i);
                order.Sort((a, b) => grounds[b] != grounds[a] ? grounds[b].CompareTo(grounds[a]) : a.CompareTo(b));
                Group group = null;
                foreach (var room in order)
                {
                    var ceiling = group == null ? ceilings[room] : Math.Min(group.Ceiling, ceilings[room]);
                    if (group == null || !Opens(group.Top, ceiling))
                    {
                        group = new Group { Level = level, Top = grounds[room], Low = grounds[room], Ceiling = ceilings[room] };
                        cuts._groups.Add(group);
                    }
                    else
                    {
                        group.Ceiling = ceiling;
                        group.Low = Math.Min(group.Low, grounds[room]);
                    }
                    group.Rooms.Add(room);
                    cuts._groupOf[room] = cuts._groups.Count - 1;
                }
            }
            return cuts;
        }
    }
}
