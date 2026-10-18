using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The floors of the place on the stage and the cut opening one of them (<see cref="PlaceView"/>):
    /// its floors from the top down, above its root as if at size one, where each is cut, which
    /// is opened (one past the last for the roof on), the one last opened, which taking the roof
    /// off again opens, and how far the cut has been moved from its floor. A new copy of the same
    /// entry keeps its cut; another entry starts afresh.
    /// </summary>
    internal sealed class FloorCut
    {
        private readonly List<float> _floors = new List<float>();
        private List<float> _cuts = new List<float>();
        private int _lastOpen;
        private int _level;
        private float _shift;
        private Entry _for;
        private Func<float, float?> _ceilingOver;

        public IReadOnlyList<float> Floors => _floors;
        public IReadOnlyList<float> Cuts => _cuts;
        public bool HasFloors => _floors.Count > 0;

        /// <summary>The ceiling over a floor, from the top, as found; null with none found over it or past the floors.</summary>
        public float? CeilingOver(int level) => level >= 0 && level < _floors.Count ? _ceilingOver?.Invoke(_floors[level]) : null;

        /// <summary>The height the cut is held at, a room's gone into; none while the floors decide.</summary>
        private float? _held;

        /// <summary>How low and how high the cut held at a room may be moved; past the highest its roof goes on.</summary>
        private float _least = float.NegativeInfinity, _most = float.PositiveInfinity;

        /// <summary>Whether the room gone into has its roof on, the cut off it and the rooms above it, as where it was cut is kept.</summary>
        private bool _heldRoof;

        /// <summary>The lowest floor any room of the place has, which the cut goes down to over the lowest of the floors; none known, the lowest of the floors.</summary>
        private float _lowest = float.PositiveInfinity;

        /// <summary>How near two reaches must come to meet, the cut passing from one floor's into the next's.</summary>
        private const float Meet = 0.01f;

        /// <summary>Takes the lowest floor any room of the place has (a pit, a room's own lower floor), which the cut may go down to.</summary>
        public void Reach(float lowest) => _lowest = lowest;

        /// <summary>Whether the cut is held at a height of its own, a room's gone into, whatever the floors.</summary>
        public bool Held => _held.HasValue;

        /// <summary>Where the room gone into was first cut, however the cut has moved since; NaN outside a room.</summary>
        public float HeldFrom => _held ?? float.NaN;

        /// <summary>Whether the room gone into has its roof on: the cut off, the room still gone into.</summary>
        public bool HeldRoofOn => _held.HasValue && _heldRoof;

        /// <summary>
        /// Holds the cut at a height of its own, a room's gone into, the floor opened let go of,
        /// moved by hand no lower than <paramref name="least"/> and no higher than
        /// <paramref name="most"/>, past which the room's roof goes on; none puts the roof back on.
        /// </summary>
        public void Hold(float? height, float least = float.NegativeInfinity, float most = float.PositiveInfinity)
        {
            _held = height;
            _least = least;
            _most = most;
            _heldRoof = false;
            _shift = 0f;
            _level = _floors.Count;
        }

        /// <summary>Sets anew how low and how high the room gone into may be cut, as more of it is read, its cut kept within them.</summary>
        public void Bound(float least, float most)
        {
            _least = least;
            _most = most;
            if (_held is float held) _shift = Math.Min(Math.Max(held + _shift, least), most) - held;
        }

        /// <summary>Cuts the room gone into at a height set by hand: no lower than its least, and over its most, its roof on.</summary>
        public void HoldAt(float height)
        {
            if (!(_held is float held)) return;
            if (height > _most)
            {
                _heldRoof = true;
                return;
            }
            _heldRoof = false;
            _shift = Math.Max(height, _least) - held;
        }

        /// <summary>Whether a floor is opened or the cut is held at a room with its roof off.</summary>
        public bool Cutting => _held.HasValue ? !_heldRoof : _level < _floors.Count;

        /// <summary>Which floor is opened, from the top; the floors' count for the roof on.</summary>
        public int Level => _level;

        /// <summary>What the cut's chip says.</summary>
        public string Label => PlaceView.CutLabel(_level, _floors.Count);

        /// <summary>Where it is cut, above the root as if at size one; out of reach with the roof on.</summary>
        public float At => _held is float held ? (_heldRoof ? float.PositiveInfinity : held + _shift) : _level < _floors.Count ? _cuts[_level] + _shift : float.PositiveInfinity;

        /// <summary>Opens the floor below, or the top floor from the roof; goes up a floor, or puts the roof back from the top floor.</summary>
        public void Step(bool down) => Open(PlaceView.StepCut(_level, _floors.Count, down));

        /// <summary>Takes the roof off, opening the floor last opened, or puts it back; in a room gone into, its own roof, the room still gone into and cut where it was after.</summary>
        public void ToggleRoof()
        {
            if (_held.HasValue)
            {
                _heldRoof = !_heldRoof;
                return;
            }
            if (_floors.Count == 0) return;
            Open(Cutting ? _floors.Count : Math.Max(0, Math.Min(_lastOpen, _floors.Count - 1)));
        }

        /// <summary>Opens a floor (from the top), or puts the roof on past the last; the cut back at its floor, out of a room gone into.</summary>
        public void Open(int level)
        {
            if (_floors.Count == 0) return;
            _held = null;
            _level = Math.Max(0, Math.Min(level, _floors.Count));
            _shift = 0f;
            if (Cutting) _lastOpen = _level;
        }

        /// <summary>Cuts at a height set by hand, exactly there, opening the floor it is over (<see cref="PlaceView.LevelAt"/>); only Shift and the wheel keep to a floor's reach.</summary>
        public void CutTo(float height)
        {
            if (_floors.Count == 0) return;
            Open(PlaceView.LevelAt(_floors, height));
            _shift = height - _cuts[_level];
        }

        /// <summary>
        /// How low the cut goes over a floor opened: half a metre over it, the lowest floor's over
        /// the lowest floor any room has where that is lower.
        /// </summary>
        private float Low(int level) => (level == _floors.Count - 1 ? Math.Min(_floors[level], _lowest) : _floors[level]) + LeastOver;

        /// <summary>
        /// How high the cut goes over a floor opened: <see cref="RoofOver"/> over its own cut,
        /// past which the top floor has its roof put on; another floor's no higher than the floor
        /// above's lowest, where they meet, but never under its own cut.
        /// </summary>
        private float High(int level) => level == 0 ? _cuts[0] + RoofOver : Math.Max(_cuts[level], Math.Min(_cuts[level] + RoofOver, Low(level - 1)));

        /// <summary>Whether a floor's reach and the one's under it meet, so the cut passes from one into the other.</summary>
        private bool Meets(int upper) => Low(upper) <= High(upper + 1) + Meet;

        private void CutWithin(float height) => _shift = Math.Min(Math.Max(height, Low(_level)), High(_level)) - _cuts[_level];

        /// <summary>Moves the cut up or down; it shows while a floor is opened, and a floor opened starts it at its height.</summary>
        public void CutBy(float metres) => _shift += metres;

        /// <summary>How far over its lowest floor Shift and the wheel leave the cut at the lowest.</summary>
        public const float LeastOver = 0.5f;

        /// <summary>How far over the top floor's cut Shift and the wheel raise it before the roof goes back on.</summary>
        public const float RoofOver = 2f;

        /// <summary>
        /// Moves the cut as Shift and the wheel do. In the room gone into: its cut up or down, never
        /// under its least, raised past its most its roof on, and down from there cut where it was;
        /// pushed on down from its least or up from its roof on, it tells the stage to go on to the
        /// level below (1) or above (-1), as Page Up and Down do. Else through the floors, each
        /// opened within its reach (<see cref="Low"/>, <see cref="High"/>): where two floors' reaches
        /// meet the cut passes from one into the other; where they lie far apart it stops at its
        /// floor's end, and the next turn opens the floor below at its own cut, or the floor above
        /// just over it, so it never stops where neither is open. Down with the roof on, the top
        /// floor opened at its cut; raised well over the top floor's cut, the roof back on. 0 but
        /// for a room's ends.
        /// </summary>
        public int Wheel(float metres)
        {
            if (_held is float held)
            {
                if (_heldRoof)
                {
                    if (metres < 0f) _heldRoof = false;
                    return metres > 0f ? -1 : 0;
                }
                var next = held + _shift + metres;
                if (next > _most)
                {
                    _heldRoof = true;
                    return 0;
                }
                if (next < _least && held + _shift <= _least + Meet) return 1;
                _shift = Math.Max(next, _least) - held;
                return 0;
            }
            if (_floors.Count == 0) return 0;
            if (_level >= _floors.Count)
            {
                if (metres < 0f) Open(0);
                return 0;
            }
            var at = At;
            var height = at + metres;
            if (metres < 0f && height < Low(_level) && _level + 1 < _floors.Count && !Meets(_level))
            {
                if (at > Low(_level) + Meet) CutWithin(height);
                else Open(_level + 1);
                return 0;
            }
            if (metres > 0f && height > High(_level) && _level > 0 && !Meets(_level - 1))
            {
                if (at < High(_level) - Meet) CutWithin(height);
                else
                {
                    Open(_level - 1);
                    CutWithin(Low(_level));
                }
                return 0;
            }
            // Through every floor whose reach meets the next, to where the cut comes to rest or to a gap.
            while (height < Low(_level) && _level + 1 < _floors.Count && Meets(_level)) Open(_level + 1);
            while (height > High(_level) && _level > 0 && Meets(_level - 1)) Open(_level - 1);
            if (_level == 0 && height > High(0))
            {
                Open(_floors.Count);
                return 0;
            }
            CutWithin(height);
            return 0;
        }

        /// <summary>
        /// Takes the floors of the entry shown. The first time for an entry, a room is opened on
        /// its top floor and a location keeps its roof; a new copy of the same keeps its cut: the
        /// roof on, or the floor opened where it still has it. True when the floors are another entry's.
        /// </summary>
        public bool Take(Entry entry, IEnumerable<float> floors, bool open, Func<float, float?> ceilingOver = null)
        {
            var roofOn = !Cutting;
            _held = null;
            _floors.Clear();
            if (floors != null) _floors.AddRange(floors);
            _ceilingOver = ceilingOver;
            _cuts = PlaceView.CutHeights(_floors, _ceilingOver);
            if (ReferenceEquals(_for, entry))
            {
                _level = roofOn ? _floors.Count : Math.Min(_level, _floors.Count);
                return false;
            }
            _for = entry;
            _shift = 0f;
            _lastOpen = 0;
            _lowest = float.PositiveInfinity;
            _level = open ? 0 : _floors.Count;
            return true;
        }

        /// <summary>Takes floors found anew for the place shown (an example's, as its rooms come in), keeping the floor opened where it can, the roof on where it was and a room gone into; each cut under the ceiling over its floor where one is known.</summary>
        public void Refresh(IEnumerable<float> floors, Func<float, float?> ceilingOver = null)
        {
            var roofOn = !Cutting;
            _floors.Clear();
            _floors.AddRange(floors);
            _ceilingOver = ceilingOver;
            _cuts = PlaceView.CutHeights(_floors, _ceilingOver);
            _level = roofOn || _held.HasValue ? _floors.Count : Math.Min(_level, Math.Max(0, _floors.Count - 1));
        }

        /// <summary>Lets go of the entry the floors are of, so the next taken starts afresh.</summary>
        public void ForgetEntry() => _for = null;

        /// <summary>Lets go of everything.</summary>
        public void Clear()
        {
            _floors.Clear();
            _cuts.Clear();
            _ceilingOver = null;
            _for = null;
            _held = null;
            _level = 0;
        }
    }
}
