using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The kinds the character keeps on Valheim's map, standing every time it opens as Show on map
    /// shows them: each by the key its spots are kept by, several kinds of one place shown at once
    /// as one (<see cref="MapShowing.AllKindsKey"/>); every kind of dungeon kept from the start,
    /// as one, until taken off. Each kept kind stands in a mark of its own (<see cref="KeptMarks"/>),
    /// the lowest free in its category's shape as it is kept, kept with it while it is.
    /// </summary>
    internal sealed partial class CompendiumBook
    {
        /// <summary>The key every kind of dungeon is kept by, as one.</summary>
        public const string Dungeons = "dungeons";

        /// <summary>The kinds kept, each with its mark.</summary>
        private readonly Dictionary<string, int> _kept = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Whether every kind of dungeon was taken off the map, kept from the start otherwise.</summary>
        private bool _dungeonsUnkept;

        private List<string> _keptList;

        /// <summary>Counts up whenever what is kept changes, for the map and the list to be worked out again.</summary>
        public int KeptVersion { get; private set; }

        /// <summary>Whether a kind is kept on the map.</summary>
        public bool Keeps(string kind) => kind == Dungeons ? !_dungeonsUnkept : kind != null && _kept.ContainsKey(kind);

        /// <summary>The mark a kept kind stands in, the dungeons' for every kind of dungeon; none for one not kept.</summary>
        public int? MarkOf(string kind) => kind == Dungeons ? KeptMarks.Dungeons : kind != null && _kept.TryGetValue(kind, out var mark) ? mark : (int?)null;

        /// <summary>
        /// The mark a line kept by its own key stands in, for its picker; none for every kind of
        /// dungeon, which keeps its amber discs, nor for a kind kept only with others as one line.
        /// </summary>
        public int? PickableMark(string kind) => kind != null && _kept.TryGetValue(kind, out var mark) ? mark : (int?)null;

        /// <summary>The lowest mark no kept kind stands in, in the shape of a kind's category: for that kind as it is kept, or shown and not kept.</summary>
        public int FreeMark(string kind) => KeptMarks.Dealt(KeptCategories.ShapeOf(kind), _kept.Values);

        /// <summary>The spots crossed off or highlighted, by world and where each lies; plain ones are not kept.</summary>
        private readonly Dictionary<string, Dictionary<(int X, int Z), SpotMode>> _spotModes = new Dictionary<string, Dictionary<(int X, int Z), SpotMode>>(StringComparer.Ordinal);

        /// <summary>Counts up whenever a spot's mode changes.</summary>
        public int SpotModesVersion { get; private set; }

        /// <summary>A spot's mode in a world: its own, or that of one lying within a few metres of it (<see cref="SpotModes.Near"/>).</summary>
        public SpotMode SpotModeAt(string world, int x, int z)
        {
            if (world == null || !_spotModes.TryGetValue(world, out var spots) || spots.Count == 0) return SpotMode.Plain;
            if (spots.TryGetValue((x, z), out var own)) return own;
            for (var dz = -SpotModes.Near; dz <= SpotModes.Near; dz++)
            {
                for (var dx = -SpotModes.Near; dx <= SpotModes.Near; dx++)
                {
                    if (spots.TryGetValue((x + dx, z + dz), out var near)) return near;
                }
            }
            return SpotMode.Plain;
        }

        /// <summary>Gives a spot a mode, in place of any kept within a few metres of it, so one thing never holds two; whether it changed.</summary>
        public bool SetSpotMode(string world, int x, int z, SpotMode mode)
        {
            if (world == null || SpotModeAt(world, x, z) == mode) return false;
            if (!_spotModes.TryGetValue(world, out var spots)) _spotModes[world] = spots = new Dictionary<(int X, int Z), SpotMode>();
            for (var dz = -SpotModes.Near; dz <= SpotModes.Near; dz++)
            {
                for (var dx = -SpotModes.Near; dx <= SpotModes.Near; dx++) spots.Remove((x + dx, z + dz));
            }
            if (mode != SpotMode.Plain) spots[(x, z)] = mode;
            SpotModesVersion++;
            Changed = true;
            return true;
        }

        /// <summary>The spots' modes as they are saved, in an order that does not change.</summary>
        private List<(string World, int X, int Z, SpotMode Mode)> SpotModeLines() =>
            _spotModes.SelectMany(w => w.Value.Select(s => (World: w.Key, s.Key.X, s.Key.Z, Mode: s.Value)))
                .OrderBy(s => s.World, StringComparer.Ordinal).ThenBy(s => s.X).ThenBy(s => s.Z).ToList();

        /// <summary>Whether the marks stand in their categories' shapes, those of a book kept before them moved once as it is read.</summary>
        private bool _keptShapes = true;

        /// <summary>What is kept, the dungeons first, then the rest by their keys.</summary>
        public IReadOnlyList<string> Kept
        {
            get
            {
                if (_keptList != null) return _keptList;
                _keptList = new List<string>();
                if (!_dungeonsUnkept) _keptList.Add(Dungeons);
                _keptList.AddRange(_kept.Keys.OrderBy(kind => kind, StringComparer.Ordinal));
                return _keptList;
            }
        }

        /// <summary>
        /// Keeps a kind on the map, or takes it off: a choice of the player's. Several kinds kept as
        /// one (<see cref="MapShowing.AllKindsKey"/>) take each one's own line off, so nothing stands
        /// twice, and stand in the lowest mark those stood in, so they keep their look.
        /// </summary>
        public void Keep(string kind, bool on)
        {
            if (string.IsNullOrEmpty(kind) || Keeps(kind) == on) return;
            if (on) ShowKept(kind);
            if (kind == Dungeons) _dungeonsUnkept = !on;
            else if (on)
            {
                var replaced = kind.IndexOf('+') >= 0 ? kind.Split('+').Where(_kept.ContainsKey).ToList() : new List<string>();
                var marks = replaced.Select(k => _kept[k]).ToList();
                foreach (var own in replaced) _kept.Remove(own);
                // A picture is only ever picked, so it is the look the player gave them.
                var pictured = marks.Where(m => m >= KeptMarks.Most).ToList();
                _kept[kind] = pictured.Count > 0 ? pictured.Min() : marks.Count > 0 ? marks.Min() : FreeMark(kind);
            }
            else _kept.Remove(kind);
            KeptChanged();
        }

        /// <summary>
        /// Gives a kept kind the mark the player picked (<see cref="KeptMarks"/>); another kept may
        /// stand in it too. Whether it changed: never for every kind of dungeon, a kind not kept,
        /// a mark this Scry does not offer, or the mark it has. Every kind of dungeon is kept without a
        /// mark of its own, so it is never picked.
        /// </summary>
        public bool Pick(string kind, int mark)
        {
            if (kind == null || !KeptMarks.Offered(mark) || !_kept.TryGetValue(kind, out var had) || had == mark) return false;
            _kept[kind] = mark;
            KeptChanged();
            return true;
        }

        /// <summary>The kinds hidden on the map while kept, by their own keys, and whether every mark is hidden.</summary>
        private readonly HashSet<string> _hidden = new HashSet<string>(StringComparer.Ordinal);
        private bool _hideAll;

        /// <summary>Counts up whenever what is hidden changes, for the map to place its marks again.</summary>
        public int HiddenVersion { get; private set; }

        /// <summary>Whether every one of Scry's marks is hidden on the map.</summary>
        public bool HidesAll => _hideAll;

        /// <summary>Whether anything is hidden on the map.</summary>
        public bool HidesAny => _hideAll || _hidden.Count > 0;

        /// <summary>The kinds hidden on their own, as the self-test notes them to put back.</summary>
        [ForTest]
        public IReadOnlyCollection<string> HiddenKinds => _hidden;

        /// <summary>Whether a kind's marks are hidden on the map: on its own, or with every mark.</summary>
        public bool Hides(string kind) => _hideAll || kind != null && _hidden.Contains(kind);

        /// <summary>
        /// Hides kinds on the map or shows them again, each kept as it was: a choice of the player's.
        /// Showing one while every mark is hidden shows it alone, every other kind standing,
        /// <paramref name="standing"/>, staying hidden.
        /// </summary>
        public void Hide(IEnumerable<string> kinds, bool on, IEnumerable<string> standing = null)
        {
            var changed = false;
            if (!on && _hideAll)
            {
                _hideAll = false;
                if (standing != null) foreach (var kind in standing) _hidden.Add(kind);
                changed = true;
            }
            foreach (var kind in kinds)
            {
                if (string.IsNullOrEmpty(kind)) continue;
                changed |= on ? _hidden.Add(kind) : _hidden.Remove(kind);
            }
            if (changed) HiddenChanged();
        }

        /// <summary>Hides every one of Scry's marks on the map, or lets those not hidden on their own stand again.</summary>
        public void HideAll(bool on)
        {
            if (_hideAll == on) return;
            _hideAll = on;
            HiddenChanged();
        }

        /// <summary>Shows every mark hidden again.</summary>
        public void ShowAll()
        {
            if (!HidesAny) return;
            _hideAll = false;
            _hidden.Clear();
            HiddenChanged();
        }

        /// <summary>A kind kept stands again, as keeping it is asking to see it.</summary>
        private void ShowKept(string kind)
        {
            if (_hidden.Count == 0) return;
            var changed = false;
            foreach (var one in kind.Split('+')) changed |= _hidden.Remove(one);
            if (changed) HiddenChanged();
        }

        private void HiddenChanged()
        {
            HiddenVersion++;
            Changed = true;
        }

        /// <summary>The kinds hidden as they are saved, in an order that does not change.</summary>
        private List<string> HiddenLines() => _hidden.OrderBy(kind => kind, StringComparer.Ordinal).ToList();

        /// <summary>Takes every kind off the map, the dungeons too.</summary>
        public void TakeAllOff()
        {
            if (_kept.Count == 0 && _dungeonsUnkept) return;
            _kept.Clear();
            _dungeonsUnkept = true;
            KeptChanged();
        }

        /// <summary>
        /// Once a book is read: a kind kept without a mark, as an older book keeps it, or in one
        /// this Scry does not offer, given the lowest free, in the order of their keys. Kinds
        /// sharing a mark keep it, as the player may pick one look for several.
        /// </summary>
        private void MarkKept()
        {
            var held = new List<int>();
            var unmarked = new List<string>();
            foreach (var kind in _kept.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (KeptMarks.Offered(_kept[kind])) held.Add(_kept[kind]);
                else unmarked.Add(kind);
            }
            foreach (var kind in unmarked)
            {
                var mark = KeptMarks.Dealt(KeptCategories.ShapeOf(kind), held);
                _kept[kind] = mark;
                held.Add(mark);
            }
            if (!_keptShapes) MoveToCategoryShapes();
        }

        /// <summary>
        /// A book kept before categories: each plain mark moved into its category's shape, in its
        /// colour where that is free there, else the lowest free, and kinds that shared a mark still
        /// sharing one. Nothing tells a dealt mark from a picked one, so a plain shape picked moves too,
        /// this once. A picture was only ever picked, so it stays.
        /// </summary>
        private void MoveToCategoryShapes()
        {
            _keptShapes = true;
            var keys = _kept.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            bool Stays(string kind) => _kept[kind] >= KeptMarks.Most || KeptMarks.ShapeOf(_kept[kind]) == KeptCategories.ShapeOf(kind);
            var held = keys.Where(Stays).Select(k => _kept[k]).ToList();
            var moved = new Dictionary<(int Mark, MarkShape Shape), int>();
            foreach (var kind in keys.Where(k => !Stays(k)).ToList())
            {
                var shape = KeptCategories.ShapeOf(kind);
                var was = _kept[kind];
                if (!moved.TryGetValue((was, shape), out var mark))
                {
                    mark = KeptMarks.Plain(KeptMarks.ColourOf(was), shape);
                    if (held.Contains(mark)) mark = KeptMarks.Dealt(shape, held);
                    moved[(was, shape)] = mark;
                    held.Add(mark);
                }
                _kept[kind] = mark;
                Changed = true;
            }
        }

        private void KeptChanged()
        {
            _keptList = null;
            KeptVersion++;
            Changed = true;
        }
    }
}
