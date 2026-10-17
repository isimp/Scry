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

        /// <summary>Whether the cut is held at a height of its own, a room's gone into, whatever the floors.</summary>
        public bool Held => _held.HasValue;

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

        /// <summary>Cuts at a height set by hand, opening the floor it is over (<see cref="PlaceView.LevelAt"/>).</summary>
        public void CutTo(float height)
        {
            if (_floors.Count == 0) return;
            Open(PlaceView.LevelAt(_floors, height));
            _shift = height - _cuts[_level];
        }

        /// <summary>Moves the cut up or down; it shows while a floor is opened, and a floor opened starts it at its height.</summary>
        public void CutBy(float metres) => _shift += metres;

        /// <summary>How far over its lowest floor Shift and the wheel leave the cut at the lowest.</summary>
        public const float LeastOver = 0.5f;

        /// <summary>How far over the top floor's cut Shift and the wheel raise it before the roof goes back on.</summary>
        public const float RoofOver = 2f;

        /// <summary>
        /// Moves the cut as Shift and the wheel do: in the room gone into, its cut up or down,
        /// never under its least, raised past its most its roof on, and down from there cut where
        /// it was; else up or down through the floors, opening the floor it is over
        /// (<see cref="CutTo"/>), never under the lowest floor; down with the roof on, the top
        /// floor opened at its cut; raised well over the top floor's cut, the roof back on.
        /// </summary>
        public void Wheel(float metres)
        {
            if (_held is float held)
            {
                if (_heldRoof)
                {
                    if (metres < 0f) _heldRoof = false;
                    return;
                }
                var next = held + _shift + metres;
                if (next > _most)
                {
                    _heldRoof = true;
                    return;
                }
                _shift = Math.Max(next, _least) - held;
                return;
            }
            if (_floors.Count == 0) return;
            if (_level >= _floors.Count)
            {
                if (metres < 0f) Open(0);
                return;
            }
            var height = At + metres;
            if (height > _cuts[0] + RoofOver)
            {
                Open(_floors.Count);
                return;
            }
            CutTo(Math.Max(height, _floors[_floors.Count - 1] + LeastOver));
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
