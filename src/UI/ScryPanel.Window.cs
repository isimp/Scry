using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The panel's window: its place and size in each view, dragging them, and keeping them between games.</summary>
    internal static partial class ScryPanel
    {
        // ----- Window -----

        private static Rect _full;
        private static Rect _compactRect;
        private static bool _compact;
        private static bool _placed;

        // The list's share of the room it shares with the details (the full view's width, the
        // compact view's height), set by dragging the gap between them, and whether it is folded
        // away; each view keeps its own.
        private static float _listShareFull = 0.40f;
        private static float _listShareCompact = 0.46f;
        private static bool _listHiddenFull;
        private static bool _listHiddenCompact;

        private static float ListShare
        {
            get => _compact ? _listShareCompact : _listShareFull;
            set
            {
                if (_compact) _listShareCompact = Mathf.Clamp(value, ListNarrowest, ListWidest);
                else _listShareFull = Mathf.Clamp(value, ListNarrowest, ListWidest);
            }
        }

        private static bool ListHidden
        {
            get => _compact ? _listHiddenCompact : _listHiddenFull;
            set
            {
                if (_compact) _listHiddenCompact = value;
                else _listHiddenFull = value;
            }
        }

        /// <summary>Where a drag of the gap has got to, below the list's smallest when it would fold away.</summary>
        private static float _listDragged;

        private const float ListNarrowest = 0.20f;
        private const float ListWidest = 0.72f;
        private const float ListFolds = 0.12f;

        /// <summary>The stage's height against its usual one, set by dragging its bottom edge.</summary>
        private static float _stageScale = 1f;

        private enum Drag { None, Move, Resize, Orbit, StageSize, Pan, ListSize, Ruler }

        private static Drag _drag;

        /// <summary>How far the mouse has moved in the drag going on, which tells a click on the stage from turning it.</summary>
        private static float _dragMoved;

        /// <summary>Starts a drag where the mouse went down: of the window, its corner, the stage, its picture, the ruler or the gap.</summary>
        private static void StartDrag(Drag drag)
        {
            _drag = drag;
            _dragMoved = 0f;
        }

        /// <summary>Puts the stage back to its usual height, as a double-click on its bottom edge does.</summary>
        private static void ResetStageSize()
        {
            _stageScale = 1f;
            SaveRects();
        }

        /// <summary>Whether the panel is in its compact view; setting it switches views, as the header's button does.</summary>
        public static bool Compact
        {
            get => _compact;
            set
            {
                if (value != _compact) ToggleCompact();
            }
        }

        private static Rect Win
        {
            get => _compact ? _compactRect : _full;
            set
            {
                if (_compact) _compactRect = value;
                else _full = value;
            }
        }

        private static void Place()
        {
            if (!_placed)
            {
                _placed = true;
                var w = Mathf.Min(U(1180f), Screen.width - U(40f));
                var h = Mathf.Min(U(760f), Screen.height - U(40f));
                _full = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
                var cw = Mathf.Min(U(500f), Screen.width - U(40f));
                _compactRect = new Rect(Screen.width - cw - U(20f), U(40f), cw, Screen.height - U(80f));
                LoadRects();
            }

            var win = Win;
            var minW = Mathf.Min(U(_compact ? 420f : 820f), Screen.width);
            var minH = Mathf.Min(U(_compact ? 440f : 540f), Screen.height);
            win.width = Mathf.Clamp(win.width, minW, Screen.width);
            win.height = Mathf.Clamp(win.height, minH, Screen.height);
            win.x = Mathf.Clamp(win.x, 0f, Screen.width - win.width);
            win.y = Mathf.Clamp(win.y, 0f, Screen.height - win.height);
            Win = win;
        }

        private static void ToggleCompact()
        {
            _compact = !_compact;
            DetailsFromTop();
            RevealSelected();
            SaveRects();
        }

        /// <summary>Folds the list away or brings it back, in the view in use.</summary>
        private static void ToggleList()
        {
            ListHidden = !ListHidden;
            RevealSelected();
            SaveRects();
        }

        /// <summary>
        /// The gap between the list and the details: a grip to drag it (past the smallest the list
        /// folds away), a double-click to fold it. While folded, the gap is a button along its
        /// whole length that brings the list back. Across the full view it runs top to bottom;
        /// in the compact view, where the list is above the details, left to right.
        /// </summary>
        private static void ListDivider(Rect gap, bool upright)
        {
            var e = Event.current;
            var inside = gap.Contains(e.mousePosition);

            if (ListHidden)
            {
                if (GUI.Button(gap, ListWords.ShowList(upright), Skin.Button)) ToggleList();
                if (inside) AskTip("list-gap", ListWords.BringBack);
                return;
            }

            var hover = inside || _drag == Drag.ListSize;
            var grip = upright
                ? new Rect(gap.center.x - U(2f), gap.center.y - U(24f), U(4f), U(48f))
                : new Rect(gap.center.x - U(24f), gap.center.y - U(2f), U(48f), U(4f));
            Skin.Fill(grip, hover ? Skin.Dim : Skin.Outline);
            if (inside) AskTip("list-gap", "Drag to make the list bigger or smaller, past the smallest to fold it away; double-click to fold it");

            if (e.type != EventType.MouseDown || e.button != 0 || !inside) return;
            if (e.clickCount == 2)
            {
                ToggleList();
            }
            else
            {
                _listDragged = ListShare;
                StartDrag(Drag.ListSize);
            }
            e.Use();
        }

        private static void Drags()
        {
            var e = Event.current;
            if (_drag == Drag.None) return;

            if (e.rawType == EventType.MouseUp)
            {
                if (_drag == Drag.Move || _drag == Drag.Resize || _drag == Drag.StageSize || _drag == Drag.ListSize) SaveRects();
                _drag = Drag.None;
                Stage.Dragging = false;
                return;
            }

            if (e.type != EventType.MouseDrag) return;

            var win = Win;
            switch (_drag)
            {
                case Drag.Move:
                    win.position += e.delta;
                    break;
                case Drag.Resize:
                    win.width += e.delta.x;
                    win.height += e.delta.y;
                    break;
                case Drag.Orbit:
                    _dragMoved += e.delta.magnitude;
                    Stage.Orbit(e.delta);
                    break;
                case Drag.Pan:
                    // What was under the pointer goes with it.
                    var now = GUIUtility.GUIToScreenPoint(e.mousePosition);
                    Stage.Pan(OnPicture(now - e.delta), OnPicture(now));
                    break;
                case Drag.Ruler:
                    RulerAt(GUIUtility.GUIToScreenPoint(e.mousePosition).y);
                    break;
                case Drag.StageSize:
                    _stageScale = Mathf.Clamp(_stageScale + e.delta.y / Mathf.Max(1f, _stageBaseH), 0.4f, 2.4f);
                    break;
                case Drag.ListSize:
                    // Past its narrowest the list folds away; dragged back out, it opens again.
                    _listDragged += (_compact ? e.delta.y : e.delta.x) / Mathf.Max(1f, _listSpan);
                    ListHidden = _listDragged < ListFolds;
                    if (!ListHidden) ListShare = _listDragged;
                    break;
            }
            Win = win;
            e.Use();
        }

        private static string RectFile => Path.Combine(Settings.DataFolder, "panel.txt");

        /// <summary>Where the panel was, in both views, and how its stage was seen (<see cref="PanelPlace"/>).</summary>
        private static void LoadRects() => Guard.Run("reading where the panel was", () =>
        {
            if (!File.Exists(RectFile)) return;
            var place = PanelPlace.Read(File.ReadAllLines(RectFile));
            Rect RectOf(PanelPlace.Area area) => new Rect(area.X, area.Y, area.Width, area.Height);

            if (place.Full.HasValue) _full = RectOf(place.Full.Value);
            if (place.Compact.HasValue) _compactRect = RectOf(place.Compact.Value);
            if (place.CompactView.HasValue) _compact = place.CompactView.Value;
            if (place.Lighting.HasValue) Stage.LightingIndex = place.Lighting.Value;
            if (place.Backdrop.HasValue) Stage.BackdropIndex = place.Backdrop.Value;
            if (place.Ground != null) Stage.GroundChoice = place.Ground;
            if (place.Person.HasValue) Stage.ShowPerson = place.Person.Value;
            if (place.Worn.HasValue) Looks.OnPerson = place.Worn.Value;
            if (place.Spin.HasValue) Stage.Spin = place.Spin.Value;
            if (place.Creatures.HasValue) Stage.CreaturesShown = place.Creatures.Value;
            if (place.Folded != null) FoldOnly(place.Folded);
            if (place.StageScale.HasValue) _stageScale = Mathf.Clamp(place.StageScale.Value, 0.4f, 2.4f);
            if (place.ListShareFull.HasValue) _listShareFull = Mathf.Clamp(place.ListShareFull.Value, ListNarrowest, ListWidest);
            if (place.ListHiddenFull.HasValue) _listHiddenFull = place.ListHiddenFull.Value;
            if (place.ListShareCompact.HasValue) _listShareCompact = Mathf.Clamp(place.ListShareCompact.Value, ListNarrowest, ListWidest);
            if (place.ListHiddenCompact.HasValue) _listHiddenCompact = place.ListHiddenCompact.Value;
        });

        private static void SaveRects() => Guard.Run("remembering where the panel is", () =>
        {
            PanelPlace.Area AreaOf(Rect r) => new PanelPlace.Area(r.x, r.y, r.width, r.height);
            var place = new PanelPlace
            {
                Full = AreaOf(_full),
                Compact = AreaOf(_compactRect),
                CompactView = _compact,
                Lighting = Stage.LightingIndex,
                Backdrop = Stage.BackdropIndex,
                Ground = Stage.GroundChoice,
                Person = Stage.ShowPerson,
                Worn = Looks.OnPerson,
                Spin = Stage.Spin,
                Creatures = Stage.CreaturesShown,
                Folded = Folded.ToArray(),
                StageScale = _stageScale,
                ListShareFull = _listShareFull,
                ListHiddenFull = _listHiddenFull,
                ListShareCompact = _listShareCompact,
                ListHiddenCompact = _listHiddenCompact,
            };
            Directory.CreateDirectory(Settings.DataFolder);
            File.WriteAllLines(RectFile, place.Lines());
        });
    }
}
