using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The creatures standing on the stage with a place (<see cref="Stage"/>), and those rolled
    /// and waiting to be made: which stand in which of an example's rooms, how high each flies,
    /// whether they are shown, and which stand above a floor's cut. It keeps them and lets go of
    /// them itself; the stage makes each copy (<see cref="Waiting"/>).
    /// </summary>
    internal sealed class StageCreatures
    {
        /// <summary>A creature rolled and waiting to be made: its point, how far above it it stands and flies, its level and the example's room it stands in, if any.</summary>
        public struct Waiting
        {
            public Transform At;
            public float Rise;
            public float Lift;
            public GameObject Creature;
            public int Level;
            public GameObject Room;
        }

        private readonly List<Waiting> _toMake = new List<Waiting>();

        /// <summary>How high over its ground each creature standing flies, 0 for one that walks.</summary>
        private readonly Dictionary<GameObject, float> _lift = new Dictionary<GameObject, float>();

        /// <summary>The creatures standing, each with the example's room it stands in, if any.</summary>
        private readonly List<KeyValuePair<GameObject, GameObject>> _copies = new List<KeyValuePair<GameObject, GameObject>>();

        private bool _shown = true;
        private float _cutAt = float.NaN;
        private int _cutCount = -1;

        /// <summary>Whether the creatures stand on the stage or are put away, as the stage's chip has it.</summary>
        public bool Shown
        {
            get => _shown;
            set
            {
                if (value == _shown) return;
                _shown = value;
                foreach (var pair in _copies) if (pair.Key != null) pair.Key.SetActive(value);
            }
        }

        /// <summary>Whether any creature stands or waits.</summary>
        public bool Any => _copies.Count > 0 || _toMake.Count > 0;

        /// <summary>Whether any creature copy stands, made or not yet gone.</summary>
        public bool AnyMade => _copies.Count > 0;

        public int Made => _copies.Count(pair => pair.Key != null);
        public int WaitingCount => _toMake.Count;
        public int Standing => _copies.Count(pair => pair.Key != null && pair.Key.activeSelf);

        /// <summary>How many creatures rolled stand below their spawn point, dropped to the ground under it, and how many fly over it.</summary>
        public int Dropped { get; private set; }
        public int Flying { get; private set; }

        /// <summary>The creature copies standing, made and not gone.</summary>
        public IEnumerable<GameObject> Copies => _copies.Where(pair => pair.Key != null).Select(pair => pair.Key);

        /// <summary>Rolls which of a copy's spawn points put their creature there, to be made over the next frames.</summary>
        public void Populate(List<Stage.SpawnHere> points, GameObject room, Func<double> dice)
        {
            if (points == null || points.Count == 0) return;
            var facts = points.Select(p => p.Point).ToList();
            foreach (var (point, level) in SpawnPoints.Roll(facts, dice))
            {
                var here = points[point];
                _toMake.Add(new Waiting { At = here.At, Rise = here.Rise, Lift = here.Grounded ? here.Lift : 0f, Creature = here.Creature, Level = level, Room = room });
                if (here.Lift > 0f) Flying++;
                else if (here.Grounded && here.Drop > 0.05f) Dropped++;
            }
        }

        /// <summary>Makes a few of the creatures waiting, for a share of the frame and at least one, each as the stage makes it; one put away stays so.</summary>
        public void Step(double budgetMs, Func<Waiting, GameObject> make)
        {
            var watch = Stopwatch.StartNew();
            var any = false;
            while (_toMake.Count > 0 && (!any || watch.Elapsed.TotalMilliseconds < budgetMs))
            {
                var waiting = _toMake[0];
                _toMake.RemoveAt(0);
                if (waiting.At == null || waiting.Creature == null) continue;
                any = true;
                var copy = make(waiting);
                if (copy == null) continue;
                if (!_shown) copy.SetActive(false);
                _copies.Add(new KeyValuePair<GameObject, GameObject>(copy, waiting.Room));
                _lift[copy] = waiting.Lift;
            }
        }

        /// <summary>
        /// With a floor's cut laid at a height, a creature standing above it, on a floor above, is
        /// cut away with that floor rather than drawn again over the picture: it stands on the
        /// stage's own layer while the cut is above its feet. Put right as the cut moves or
        /// creatures come.
        /// </summary>
        public void KeepToCut(float cut, int stageLayer, int creatureLayer)
        {
            if (cut.Equals(_cutAt) && _copies.Count == _cutCount) return;
            _cutAt = cut;
            _cutCount = _copies.Count;
            foreach (var pair in _copies)
            {
                var creature = pair.Key;
                if (creature == null) continue;
                _lift.TryGetValue(creature, out var lift);
                var layer = SpawnPoints.AboveCut(creature.transform.position.y, lift, cut) ? stageLayer : creatureLayer;
                if (creature.layer != layer) Ghost.SetLayer(creature.transform, layer);
            }
        }

        /// <summary>How many creatures stand on a layer, for those cut away above a floor's cut.</summary>
        public int OnLayer(int layer) => _copies.Count(pair => pair.Key != null && pair.Key.layer == layer);

        /// <summary>The creatures that fly and how high over their ground, by kind.</summary>
        public string FlyersTold() => string.Join(", ", _lift.Where(pair => pair.Key != null && pair.Value > 0f)
            .GroupBy(pair => (pair.Key.name, pair.Value)).Select(g => $"{g.Key.name} {Numbers.Amount(g.Key.Value, 1)} m up x{Numbers.Count(g.Count())}"));

        /// <summary>Lets go of the example's rooms' creatures, as its rooms go; the location's own stay.</summary>
        public void ForgetRooms()
        {
            _toMake.RemoveAll(c => c.Room != null);
            foreach (var pair in _copies) if (pair.Value != null) _lift.Remove(pair.Key);
            _copies.RemoveAll(pair => pair.Value != null);
            Dropped = 0;
            Flying = 0;
        }

        /// <summary>Lets go of every creature, as the copy they stand on goes.</summary>
        public void Forget()
        {
            _toMake.Clear();
            _copies.Clear();
            _lift.Clear();
            Dropped = 0;
            Flying = 0;
        }
    }
}
