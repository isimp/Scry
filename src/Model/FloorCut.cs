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

        public IReadOnlyList<float> Floors => _floors;
        public IReadOnlyList<float> Cuts => _cuts;
        public bool HasFloors => _floors.Count > 0;

        /// <summary>Whether a floor is opened.</summary>
        public bool Cutting => _level < _floors.Count;

        /// <summary>Which floor is opened, from the top; the floors' count for the roof on.</summary>
        public int Level => _level;

        /// <summary>What the cut's chip says.</summary>
        public string Label => PlaceView.CutLabel(_level, _floors.Count);

        /// <summary>Where it is cut, above the root as if at size one; out of reach with the roof on.</summary>
        public float At => Cutting ? _cuts[_level] + _shift : float.PositiveInfinity;

        /// <summary>Opens the floor below, or the top floor from the roof; goes up a floor, or puts the roof back from the top floor.</summary>
        public void Step(bool down) => Open(PlaceView.StepCut(_level, _floors.Count, down));

        /// <summary>Takes the roof off, opening the floor last opened, or puts it back.</summary>
        public void ToggleRoof()
        {
            if (_floors.Count == 0) return;
            Open(Cutting ? _floors.Count : Math.Max(0, Math.Min(_lastOpen, _floors.Count - 1)));
        }

        /// <summary>Opens a floor (from the top), or puts the roof on past the last; the cut back at its floor.</summary>
        public void Open(int level)
        {
            if (_floors.Count == 0) return;
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

        /// <summary>
        /// Takes the floors of the entry shown. The first time for an entry, a room is opened on
        /// its top floor and a location keeps its roof; a new copy of the same keeps its cut: the
        /// roof on, or the floor opened where it still has it. True when the floors are another entry's.
        /// </summary>
        public bool Take(Entry entry, IEnumerable<float> floors, bool open)
        {
            var roofOn = !Cutting;
            _floors.Clear();
            if (floors != null) _floors.AddRange(floors);
            _cuts = PlaceView.CutHeights(_floors);
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

        /// <summary>Takes floors found anew for the place shown (an example's, as its rooms come in), keeping the floor opened where it can and the roof on where it was.</summary>
        public void Refresh(IEnumerable<float> floors)
        {
            var roofOn = !Cutting;
            _floors.Clear();
            _floors.AddRange(floors);
            _cuts = PlaceView.CutHeights(_floors);
            _level = roofOn ? _floors.Count : Math.Min(_level, Math.Max(0, _floors.Count - 1));
        }

        /// <summary>Lets go of the entry the floors are of, so the next taken starts afresh.</summary>
        public void ForgetEntry() => _for = null;

        /// <summary>Lets go of everything.</summary>
        public void Clear()
        {
            _floors.Clear();
            _cuts.Clear();
            _for = null;
            _level = 0;
        }
    }
}
