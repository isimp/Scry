using System;
using System.Collections.Generic;

namespace Scry
{
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
    /// over their lowest floor (<see cref="Reaches"/>), and opens with them. A room with a floor of
    /// its own on another level, a hall with a pit or a gallery, stands whole on that level too
    /// (<see cref="OnLevelByFloor"/>), keeping to its own level's group.
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

            /// <summary>
            /// Its cut moved by hand by so much, as its level's is with Shift and the wheel: no higher
            /// than under its lowest ceiling, where its own cut does not already stand over it, nor
            /// lower than <see cref="FloorCut.LeastOver"/> over its highest floor.
            /// </summary>
            public float CutBy(float shift) =>
                Math.Min(Math.Max(Cut + shift, Top + FloorCut.LeastOver), Math.Max(Cut, Ceiling - PlaceView.UnderCeiling));
        }

        private readonly List<Group> _groups = new List<Group>();
        private int[] _groupOf = Array.Empty<int>();
        private float[] _ceilingOf = Array.Empty<float>();

        /// <summary>Each room's floors of its own, its ground among them, and the lowest of them.</summary>
        private IReadOnlyList<float>[] _floorsOf = Array.Empty<IReadOnlyList<float>>();
        private float[] _lowestOf = Array.Empty<float>();

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

        /// <summary>
        /// The level of the plan a room gone into is come out to: the one it is on (step 0), or,
        /// stepping past the ends of its ruler, the one below (1) or above (-1), no lower than the
        /// lowest; null past the top, or for a room on no level, for the roof on.
        /// </summary>
        public static int? ComeOutTo(int level, int step, int levels)
        {
            if (level < 0 || levels <= 0 || level + step < 0) return null;
            return Math.Min(level + step, levels - 1);
        }

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

        /// <summary>Whether a room stands whole with a level opened: on it, rising into it from below (<see cref="ReachesLevel"/>), or with a floor of its own on it (<see cref="OnLevelByFloor"/>).</summary>
        public bool StandsWith(int room, int level) => LevelOf(room) == level || ReachesLevel(room, level) || OnLevelByFloor(room, level);

        /// <summary>
        /// Whether a floor of a room's own lies on a level: within <see cref="RoomFocus.SameLevel"/>
        /// of the grounds of one of its groups, as the plan holds rooms of one level together.
        /// </summary>
        public bool OnLevelByFloor(int room, int level)
        {
            if (room < 0 || room >= _floorsOf.Length || _floorsOf[room] == null) return false;
            foreach (var group in _groups)
            {
                if (group.Level != level) continue;
                foreach (var floor in _floorsOf[room])
                {
                    if (floor >= group.Low - RoomFocus.SameLevel && floor <= group.Top + RoomFocus.SameLevel) return true;
                }
            }
            return false;
        }

        /// <summary>The lowest floor of a room's own, else its ground: what the cut must stay over for the room to stand.</summary>
        public float LowestFloor(int room) => room >= 0 && room < _lowestOf.Length ? _lowestOf[room] : float.NaN;

        /// <summary>Whether one cut opens rooms whose highest ground and lowest ceiling these are.</summary>
        private static bool Opens(float top, float ceiling) => ceiling - PlaceView.UnderCeiling >= top + PlaceView.LeastHeadroom;

        /// <summary>The groups of rooms by each one's ground, ceiling over it, level (-1 for none) and floors of its own (none given, its ground alone), each at the same place in all.</summary>
        public static LevelCuts Of(IReadOnlyList<float> grounds, IReadOnlyList<float> ceilings, IReadOnlyList<int> levels, IReadOnlyList<IReadOnlyList<float>> floors = null)
        {
            var cuts = new LevelCuts { _groupOf = new int[grounds.Count], _ceilingOf = new float[grounds.Count], _floorsOf = new IReadOnlyList<float>[grounds.Count], _lowestOf = new float[grounds.Count] };
            for (var i = 0; i < cuts._ceilingOf.Length; i++) cuts._ceilingOf[i] = ceilings[i];
            for (var i = 0; i < grounds.Count; i++)
            {
                var own = floors != null && i < floors.Count ? floors[i] : null;
                cuts._floorsOf[i] = own;
                var lowest = grounds[i];
                if (own != null) foreach (var floor in own) lowest = Math.Min(lowest, floor);
                cuts._lowestOf[i] = lowest;
            }
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
