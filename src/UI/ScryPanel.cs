using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The panel. In full view: search, favourites and origin across the top, the kind tabs under
    /// them, the list on the left and the preview with everything that can be done with the
    /// selection on the right. In compact view it is a slim column at the side of the screen with
    /// no turntable, which leaves the middle free for watching a preview in the world.
    ///
    /// Drawn with IMGUI in plain rectangles rather than automatic layout, which keeps a list of
    /// thousands of rows cheap: only the rows in view are drawn. Sizes are in design units scaled
    /// by the screen height and the PanelScale setting.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static ScryPanel() => WorldCaches.Register(nameof(ScryPanel), Forget);

        private const string SearchControl = "scry-search";
        private const string ClipControl = "scry-clip-filter";
        private const string EffectControl = "scry-effect-filter";
        private static float TipDelay => Plugin.TooltipDelay;

        private static Rect _full;
        private static Rect _compactRect;
        private static bool _compact;
        private static bool _placed;
        private static float _s = 1f;

        private static Vector2 _listScroll;
        private static Vector2 _sideScroll;
        private static float _sideHeight;
        private static int _rowsInView = 10;

        private static bool _focusSearch;
        private static bool _reveal;
        private static string _clipFilter = "";
        private static bool _help;
        private static float _badgeWidth;
        private static string _effectFilter = "";

        private enum Drag { None, Move, Resize, Orbit, StageSize, Pan, ListSize }

        /// <summary>The list's share of the full view's width, set by dragging the gap beside it.</summary>
        private static float _listShare = 0.40f;

        /// <summary>Whether the list is folded away in the full view, leaving the width to the details.</summary>
        private static bool _listHidden;

        /// <summary>Where a drag of the gap has got to, below the list's narrowest when it would fold away.</summary>
        private static float _listDragged;

        private const float ListNarrowest = 0.20f;
        private const float ListWidest = 0.70f;
        private const float ListFolds = 0.12f;

        /// <summary>The stage's height against its usual one, set by dragging its bottom edge.</summary>
        private static float _stageScale = 1f;
        private static float _stageBaseH = 300f;
        private static Drag _drag;

        // A tooltip asked for during this repaint, and the one showing.
        private static string _askedTipKey;
        private static string _askedTipText;
        private static Vector2 _askedTipAt;
        private static string _tipKey;
        private static float _tipSince;

        /// <summary>Whether the search box has the keyboard, so a letter key does not close the panel.</summary>
        public static bool SearchFocused { get; private set; }

        /// <summary>Whether any of the panel's text boxes has the keyboard.</summary>
        public static bool Typing { get; private set; }

        /// <summary>The filter box that had the keyboard at the end of the last pass, or null.</summary>
        private static string FocusedFilter;

        /// <summary>Whether the panel is in its compact view.</summary>
        public static bool Compact => _compact;

        private static Rect Win
        {
            get => _compact ? _compactRect : _full;
            set
            {
                if (_compact) _compactRect = value;
                else _full = value;
            }
        }

        public static void Opened()
        {
            Skin.LookForFontsAgain();

            // In the compact view the keys walk until the search is clicked, so it is not focused on
            // opening; in the full view as the player sets it.
            _focusSearch = !_compact && Plugin.FocusSearchOnOpen;
            _reveal = true;
        }

        /// <summary>
        /// What the panel last drew: the whole window, or only the card shown while the catalog is
        /// read, so the space around that card is the world's, for looking around.
        /// </summary>
        private static Rect _drawn;

        /// <summary>Whether a mouse position (in Unity's bottom-up screen coordinates) is over the panel.</summary>
        public static bool Covers(Vector3 mouse)
        {
            return Session.IsOpen && _drawn.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }

        private static float U(float v) => Mathf.Round(v * _s);

        private static float Scale() => Mathf.Clamp(Screen.height / 1080f, 0.75f, 3f) * Plugin.UiScale;

        /// <summary>Lets go of the lists kept for the entries of the world left, which point at its prefabs.</summary>
        public static void Forget()
        {
            EffectCache.Clear();
            _effectsEntry = null;
            _effectsCarried = null;
            _effects = null;
            ComponentLists.Clear();
            SoundFactCache.Clear();
            StatusListCache.Clear();
            VariantCache.Clear();
            PrefabIcons.Clear();
            KindByName.Clear();
            StatusNames.Clear();
            ShownNames.Clear();
            _catalogNames = null;
            _rowsFor = null;
            _foldChecked = null;
            _preparing = null;
            _prepared = null;
            _playsInRows = new List<PlaysInRow>();
            _playsInFor = null;
            _linksFor = null;
            _linksIn = null;
            _usersFor = null;
            _users = new List<(string, string, string, Action)>();
            LinkRows.Clear();
            Unpacked.Clear();
            _listRows.Clear();
            _rowOfEntry.Clear();
            _statusFor = null;
            _shownFor = null;
            _kindsFor = null;
            _namesFor = null;
            _clipRows = new List<ClipRow>();
            _rowsClips = null;
            _rowsTags = null;

            _groundsFor = null;
            _commandFor = null;
            _sideFor = null;

            // The search help's index holds the whole catalog.
            _terms = null;
            _termsFor = null;
            _suggestFor = null;
            _suggested = new List<Suggestion>();
            _dropShown = false;
            _dropList = new List<Suggestion>();
            _caretTo = -1;
            Cycle.Reset();
        }

        public static void OnGUI()
        {
            var scale = Scale();
            if (!Session.IsOpen || Session.Explorer == null)
            {
                SearchFocused = false;
                Typing = false;
                if (Session.Reading != null) DrawReading(scale, Session.Reading);

                // Warmed in a world only: at the main menu the game's fonts are not loaded yet, so
                // warming there would measure a font the panel never uses, for most of a second.
                else if (Player.m_localPlayer != null) Skin.Warm(scale);
                return;
            }

            var skin = GUI.skin;
            try
            {
                Skin.Ensure(scale);
                _s = scale;
                GUI.skin = Skin.Gui;
                if (Event.current.type == EventType.Repaint) _askedTipKey = null;

                Place();
                var explorer = Session.Explorer;

                // A click anywhere lets go of the keyboard; a click on a text box takes it straight
                // back. So clicking the list or a button after typing hands the keys back to walking.
                if (Event.current.type == EventType.MouseDown) GUIUtility.keyboardControl = 0;

                Keys(explorer);
                if (!Session.IsOpen) return;

                // A key no text box of the panel has, such as one held to walk and repeated, has
                // nothing to draw: only the panel's own keys above answer it.
                var key = Event.current;
                if ((key.type == EventType.KeyDown || key.type == EventType.KeyUp) && GUIUtility.keyboardControl == 0) return;

                // Dragging the panel, its corner or the stage needs nothing but the drag.
                if (_drag != Drag.None && Event.current.type == EventType.MouseDrag)
                {
                    Drags();
                    Event.current.Use();
                    return;
                }

                Draw(explorer);
                Drags();
                Tooltip();

                // The panel is solid: clicks and the wheel over it stop here.
                var e = Event.current;
                if (Win.Contains(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();

                var focused = GUI.GetNameOfFocusedControl();
                SearchFocused = focused == SearchControl;
                Typing = focused == SearchControl || focused == ClipControl || focused == EffectControl;
                FocusedFilter = focused == ClipControl || focused == EffectControl ? focused : null;
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Told once for each way it fails; the groups and scroll views left open are
                // closed by leaving this event the way Unity provides for it.
                Faults.Tell("the panel", ex);
                GUI.enabled = true;
                GUI.color = Color.white;
                GUI.skin = skin;
                GUIUtility.ExitGUI();
            }
            finally
            {
                GUI.skin = skin;
            }
        }

        // ----- Keyboard -----

        private static void Keys(Explorer explorer)
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            // A Tab also comes as a typed character; in a filter box neither means anything.
            if (e.character == '	' && Typing && !SearchFocused)
            {
                e.Use();
                return;
            }

            switch (e.keyCode)
            {
                case KeyCode.Escape:
                    // Out of a text box first, handing the keys back to the list and to walking;
                    // from anywhere else the panel closes.
                    if (Typing)
                    {
                        GUIUtility.keyboardControl = 0;
                        Cycle.Reset();
                    }
                    else Session.Hide();
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    Step(explorer, -1);
                    e.Use();
                    break;
                case KeyCode.DownArrow:
                    Step(explorer, 1);
                    e.Use();
                    break;
                case KeyCode.PageUp:
                    Step(explorer, -Math.Max(1, _rowsInView - 1));
                    e.Use();
                    break;
                case KeyCode.PageDown:
                    Step(explorer, Math.Max(1, _rowsInView - 1));
                    e.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    // In a filter box Enter plays what the filter shows first; in the search, while
                    // it offers a suggestion for the word being typed, it takes it; else it plays
                    // or shows the selection.
                    if (FocusedFilter == EffectControl) PlayFirstEffect();
                    else if (FocusedFilter == ClipControl) PlayFirstClip();
                    else if (!TakeSuggestion(explorer)) Primary(explorer.Selected);
                    e.Use();
                    break;
                case KeyCode.F:
                    if (e.control)
                    {
                        _focusSearch = true;
                        e.Use();
                    }
                    break;
                case KeyCode.Tab:
                    // Tab completes the search; in a filter box it means nothing, rather than Unity
                    // moving the keyboard on to whichever text box comes next.
                    if (Typing && !SearchFocused) e.Use();
                    break;
            }
        }

        private static void Step(Explorer explorer, int rows)
        {
            explorer.Move(rows);

            // Past a folded group rather than into it, one entry at a time the same way.
            var step = rows < 0 ? -1 : 1;
            for (var guard = 0; guard < explorer.Results.Count && InFoldedGroup(explorer); guard++)
            {
                var before = explorer.SelectedIndex;
                explorer.Move(step);
                if (explorer.SelectedIndex == before) break;
            }
            _reveal = true;
        }

        private static bool InFoldedGroup(Explorer explorer)
        {
            var entry = explorer.Selected;
            return entry != null && Grouped(explorer) && FoldedGroups.Contains(FoldKey(explorer, entry.Group));
        }

        /// <summary>
        /// What Enter and a double click do: the thing most wanted of each kind. A sound or an
        /// effect plays, a projectile is fired, a status effect is shown; a creature makes its
        /// first attack; an item is worn or taken off, if it can be; a tree, log, rock or piece
        /// does what the game does when it is felled or broken, or a log or rock falls where it
        /// has nothing else. Anything else is left alone; showing it in the world is a button.
        /// </summary>
        private static void Primary(Entry entry)
        {
            if (entry == null) return;

            switch (entry.Kind)
            {
                case Kind.Sound:
                    Previews.PlaySound(entry);
                    break;
                case Kind.Effect:
                    Previews.PlayEffect(entry, onYou: false);
                    break;
                case Kind.Projectile:
                    Previews.Fire(entry);
                    break;
                case Kind.StatusEffect:
                    if (Previews.StatusShowing) Previews.StopStatus(true);
                    else Previews.ShowStatus(entry);
                    break;
                case Kind.Creature:
                    FirstAttack();
                    break;
                case Kind.Item:
                    if (entry.Source is GameObject item && Gear.IsWearable(item))
                    {
                        Looks.OnPerson = !Looks.OnPerson;
                        Previews.Rebuild();
                        SaveRects();
                    }
                    break;
                default:
                    OwnAction(entry);
                    break;
            }
        }

        /// <summary>
        /// Plays a creature's first attack, as its chip under Animations does, once its clips are
        /// known to be attacks; until then it says so rather than playing something else.
        /// </summary>
        private static void FirstAttack()
        {
            var clips = Previews.Clips();
            var names = Previews.ClipNames();
            var tags = Previews.ClipTags();
            for (var i = 0; i < clips.Count; i++)
            {
                var name = i < names.Count ? names[i] : clips[i].name;
                if (!tags.TryGetValue(name, out var tag) || !tag.StartsWith("attack", StringComparison.Ordinal)) continue;
                Previews.PlayClip(clips[i]);
                Previews.LastClip = clips[i];
                return;
            }
            if (clips.Count > 0 && tags.Count == 0) Session.Say("Its animations are still being worked out; Enter plays its first attack once they are.");
        }

        /// <summary>
        /// What a tree, log, rock or piece does when the game fells or breaks it, as the chip of
        /// that effect list does it; a log or rock with nothing of the kind falls instead.
        /// </summary>
        private static void OwnAction(Entry entry)
        {
            if (!(entry.Source is GameObject prefab) || !Previews.IsModel(entry)) return;
            foreach (var pair in Previews.PrefabLists(prefab))
            {
                if (pair.Value == null || !Falling.IsDestroyedList(prefab, pair.Value)) continue;
                Previews.PlayEffectList(pair.Key, pair.Value);
                return;
            }
            if (Previews.CanLetFall(entry)) Previews.LetFall();
        }

        // ----- Window -----

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
            _sideScroll = Vector2.zero;
            _reveal = true;
            SaveRects();
        }

        /// <summary>
        /// The gap between the list and the details: a grip to drag it wider or narrower (past the
        /// narrowest it folds the list away), a double-click or its arrow to fold or open the list.
        /// </summary>
        private static void ListDivider(Rect gap)
        {
            var e = Event.current;
            var hover = gap.Contains(e.mousePosition) || _drag == Drag.ListSize;
            var grip = new Rect(gap.center.x - U(2f), gap.center.y - U(24f), U(4f), U(48f));
            Skin.Fill(grip, hover ? Skin.Dim : Skin.Outline);

            var arrow = new Rect(gap.x, gap.y + U(4f), gap.width, U(22f));
            GUI.Label(arrow, _listHidden ? "›" : "‹", hover ? Skin.Center : Skin.CenterDim);
            if (gap.Contains(e.mousePosition))
            {
                AskTip("list-gap", _listHidden ? "Click to bring the list back" : "Drag to widen or narrow the list; double-click or ‹ to fold it away");
            }

            if (e.type != EventType.MouseDown || e.button != 0 || !gap.Contains(e.mousePosition)) return;
            if (_listHidden || e.clickCount == 2 || arrow.Contains(e.mousePosition))
            {
                _listHidden = !_listHidden;
                _reveal = true;
                SaveRects();
            }
            else
            {
                _listDragged = _listShare;
                _drag = Drag.ListSize;
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
                    Stage.Orbit(e.delta);
                    break;
                case Drag.Pan:
                    Stage.Pan(e.delta);
                    break;
                case Drag.StageSize:
                    _stageScale = Mathf.Clamp(_stageScale + e.delta.y / Mathf.Max(1f, _stageBaseH), 0.4f, 2.4f);
                    break;
                case Drag.ListSize:
                    // Past its narrowest the list folds away; dragged back out, it opens again.
                    _listDragged += e.delta.x / Mathf.Max(1f, win.width - U(16f) * 3f);
                    _listHidden = _listDragged < ListFolds;
                    if (!_listHidden) _listShare = Mathf.Clamp(_listDragged, ListNarrowest, ListWidest);
                    break;
            }
            Win = win;
            e.Use();
        }

        private static string RectFile => Path.Combine(Plugin.DataFolder, "panel.txt");

        /// <summary>
        /// Where the panel was, in both views, and which view was in use. One line each:
        /// "full x y w h", "compact x y w h" and "view full|compact".
        /// </summary>
        private static void LoadRects()
        {
            try
            {
                if (!File.Exists(RectFile)) return;
                foreach (var line in File.ReadAllLines(RectFile))
                {
                    var parts = line.Split(' ');
                    if (parts.Length == 2 && parts[0] == "view") _compact = parts[1] == "compact";
                    if (parts.Length == 2 && parts[0] == "light" && int.TryParse(parts[1], out var light)) Stage.LightingIndex = light;
                    if (parts.Length == 2 && parts[0] == "backdrop" && int.TryParse(parts[1], out var backdrop)) Stage.BackdropIndex = backdrop;
                    if (parts.Length == 2 && parts[0] == "person") Stage.ShowPerson = parts[1] == "1";
                    if (parts.Length == 2 && parts[0] == "worn") Looks.OnPerson = parts[1] == "1";
                    if (parts.Length == 2 && parts[0] == "spin") Stage.Spin = parts[1] == "1";
                    if (parts.Length == 2 && parts[0] == "stage" && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var stage)) _stageScale = Mathf.Clamp(stage, 0.4f, 2.4f);
                    if (parts.Length == 2 && parts[0] == "list" && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var share)) _listShare = Mathf.Clamp(share, ListNarrowest, ListWidest);
                    if (parts.Length == 2 && parts[0] == "listhidden") _listHidden = parts[1] == "1";
                    if (parts.Length >= 1 && parts[0] == "folded")
                    {
                        Folded.Clear();
                        foreach (var key in parts.Skip(1)) if (key.Length > 0) Folded.Add(key);
                    }
                    if (parts.Length != 5) continue;

                    var v = parts.Skip(1).Select(p => float.Parse(p, CultureInfo.InvariantCulture)).ToArray();
                    var rect = new Rect(v[0], v[1], v[2], v[3]);
                    if (parts[0] == "full") _full = rect;
                    else if (parts[0] == "compact") _compactRect = rect;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not read where the panel was: {ex.Message}");
            }
        }

        private static void SaveRects()
        {
            try
            {
                string Line(string name, Rect r) => name + " " + string.Join(" ", new[] { r.x, r.y, r.width, r.height }
                    .Select(v => Mathf.Round(v).ToString(CultureInfo.InvariantCulture)));

                Directory.CreateDirectory(Plugin.DataFolder);
                File.WriteAllLines(RectFile, new[] { Line("full", _full), Line("compact", _compactRect), "view " + (_compact ? "compact" : "full"),
                    "light " + Stage.LightingIndex, "backdrop " + Stage.BackdropIndex, "person " + (Stage.ShowPerson ? "1" : "0"), "worn " + (Looks.OnPerson ? "1" : "0"),
                    "spin " + (Stage.Spin ? "1" : "0"), "folded " + string.Join(" ", Folded),
                    "stage " + _stageScale.ToString("0.00", CultureInfo.InvariantCulture),
                    "list " + _listShare.ToString("0.00", CultureInfo.InvariantCulture), "listhidden " + (_listHidden ? "1" : "0") });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not remember the panel's place: {ex.Message}");
            }
        }

        // ----- Drawing -----

        /// <summary>The panel while the catalog is still being read: how far it has got, and a way to close it.</summary>
        private static void DrawReading(float scale, string progress)
        {
            var skin = GUI.skin;
            try
            {
                Skin.Ensure(scale);
                _s = scale;
                GUI.skin = Skin.Gui;
                Place();

                var e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    Session.Hide();
                    e.Use();
                    return;
                }

                var pad = U(16f);
                var rect = new Rect(Win.x, Win.y, Win.width, Mathf.Min(Win.height, U(124f)));
                _drawn = rect;
                Skin.Box(rect, Skin.Backdrop, Skin.Outline);
                GUI.BeginGroup(rect);
                GUI.Label(new Rect(pad, U(10f), U(90f), U(34f)), "Scry", Skin.Title);
                if (GUI.Button(new Rect(rect.width - pad - U(32f), U(12f), U(32f), U(32f)), "×", Skin.Close)) Session.Hide();
                GUI.Label(new Rect(pad, U(58f), rect.width - 2f * pad, U(24f)), "Reading the catalog, once for this world", Skin.Label);
                GUI.Label(new Rect(pad, U(84f), rect.width - 2f * pad, U(22f)), progress, Skin.DimLabel);
                GUI.EndGroup();

                // The panel is solid: clicks and the wheel over it stop here.
                if (rect.Contains(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();
            }
            finally
            {
                GUI.skin = skin;
            }
        }

        private static void Draw(Explorer explorer)
        {
            var win = Win;
            _drawn = win;
            Skin.Box(win, Skin.Backdrop, Skin.Outline);

            GUI.BeginGroup(win);
            var w = win.width;
            var h = win.height;
            var pad = U(16f);
            var e = Event.current;

            // Header: the name, the catalog size, the view switch and a close button. It drags the panel.
            var viewText = _compact ? "Full view" : "Compact";
            var viewW = Skin.Width(Skin.Button, viewText) + U(10f);
            // Clear world sits in the header so it is in the same place whatever is selected, and
            // only while something of Scry's is in the world.
            var outLines = Previews.OutLines;
            var clearText = ClearText(outLines);
            var clearW = outLines > 0 ? Skin.Width(Skin.Button, clearText) + U(10f) : 0f;
            var viewRect = new Rect(w - pad - U(40f) - viewW, U(15f), viewW, U(28f));
            var clearRect = new Rect(viewRect.x - (outLines > 0 ? U(8f) + clearW : 0f), viewRect.y, clearW, viewRect.height);

            // Reading the locations completes what the search and the details know, so it is
            // offered here too until it is done; while reading it shows how far it has got. Its
            // width is fixed while reading, as the text changes every frame and is not measured.
            var reading = Locations.Now == Locations.State.Reading;
            var locText = reading ? $"Locations {Locations.Done} of {Locations.Total}" : LocationsButtonText;
            var locW = Skin.Width(Skin.Button, reading ? "Locations 000 of 000" : LocationsButtonText) + U(10f);
            var locRect = new Rect(clearRect.x - U(8f) - locW, clearRect.y, locW, clearRect.height);
            var showLoc = Locations.Now != Locations.State.Read && locRect.x > pad + U(98f);

            var header = new Rect(0f, 0f, (showLoc ? locRect.x : clearRect.x) - U(8f), U(52f));
            GUI.Label(new Rect(pad, U(10f), U(90f), U(34f)), "Scry", Skin.Title);
            GameChangedChip(pad + U(84f), header.xMax - U(4f), e);

            if (showLoc)
            {
                var wasEnabled = GUI.enabled;
                GUI.enabled = !reading;
                if (GUI.Button(locRect, locText, Skin.Button)) StartReadingLocations();
                GUI.enabled = wasEnabled;
                if (locRect.Contains(e.mousePosition)) AskTip("locations", reading ? "Reading where things are found in this world's locations and dungeons" : LocationsButtonTip);
            }

            if (outLines > 0 && GUI.Button(clearRect, clearText, Skin.Primary))
            {
                Previews.ClearWorld();
                _outOpen = false;
                Session.Say("Cleared. Nothing from Scry is left in the world.");
            }
            OutHover(clearRect, outLines, e);
            OutClicks(explorer, e);

            if (GUI.Button(viewRect, viewText, Skin.Button)) ToggleCompact();
            if (GUI.Button(new Rect(w - pad - U(32f), U(12f), U(32f), U(32f)), "×", Skin.Close)) Session.Hide();
            if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition))
            {
                _drag = Drag.Move;
                e.Use();
            }

            // Search, favourites and origin, then the kind tabs, across the whole width.
            // The mouse's side buttons, as the panel's own events see them.
            if (Event.current.type == EventType.MouseDown && (Event.current.button == 3 || Event.current.button == 4))
            {
                Step(explorer, Event.current.button == 3);
                Event.current.Use();
            }

            var controls = Timing.Start();
            var y = Controls(explorer, new Rect(pad, U(56f), w - pad * 2f, U(36f)));
            Timing.Add("controls search", controls);
            var tabs = Timing.Start();
            y = Tabs(explorer, new Rect(pad, y + U(10f), w - pad * 2f, U(30f)));
            Timing.Add("controls tabs", tabs);
            Timing.Add("panel controls", controls);

            var bodyTop = y + U(12f);
            var footerH = U(28f);
            var bodyBottom = h - pad - footerH;
            var bodyH = bodyBottom - bodyTop;

            if (_compact)
            {
                var listH = Mathf.Round(bodyH * 0.46f);
                var listed = Timing.Start();
                List(explorer, new Rect(pad, bodyTop, w - pad * 2f, listH));
                Timing.Add("panel list", listed);
                Side(explorer, new Rect(pad, bodyTop + listH + U(12f), w - pad * 2f, bodyH - listH - U(12f)), withStage: false);
            }
            else
            {
                // The list and the details share the width at the gap between them, which can be
                // dragged, or folded away to leave the details the whole width.
                var leftW = _listHidden ? 0f : Mathf.Round((w - pad * 3f) * _listShare);
                var rightX = pad * 2f + leftW;
                if (!_listHidden)
                {
                    var listed = Timing.Start();
                    List(explorer, new Rect(pad, bodyTop, leftW, bodyH));
                    Timing.Add("panel list", listed);
                }
                ListDivider(new Rect(pad + leftW, bodyTop, pad, bodyH));
                Side(explorer, new Rect(rightX, bodyTop, w - rightX - pad, bodyH), withStage: true);
            }

            Footer(new Rect(pad, h - pad - footerH + U(6f), w - pad * 2f, footerH));

            // Over everything below the search box; its clicks were taken before any of it drew.
            DrawSuggestions();
            DrawOutList(explorer);

            // Resize grip in the corner.
            var grip = new Rect(w - U(22f), h - U(22f), U(20f), U(20f));
            for (var i = 0; i < 3; i++)
            {
                var d = U(4f) * i;
                Skin.Fill(new Rect(grip.xMax - U(6f) - d, grip.yMax - U(6f), U(2f), U(2f)), Skin.Faint);
                Skin.Fill(new Rect(grip.xMax - U(6f), grip.yMax - U(6f) - d, U(2f), U(2f)), Skin.Faint);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
            {
                _drag = Drag.Resize;
                e.Use();
            }

            GUI.EndGroup();
        }

        private static readonly string[] OriginNames = { "All", "Game", "Mods" };

        /// <summary>
        /// The search box, then which list is shown (help, favourites, recent), then apart from
        /// those the origin switch, labelled so its "All" is not taken for the kind tabs' own.
        /// </summary>
        private static float Controls(Explorer explorer, Rect rect)
        {
            var names = OriginNames;
            var widths = names.Select(n => Skin.Width(Skin.Segment, n) + U(6f)).ToArray();
            const string fromText = "From";
            var fromW = Skin.Width(Skin.FaintLabel, fromText) + U(8f);
            var originW = fromW + widths.Sum() + U(4f) * (names.Length - 1);
            var starW = rect.height;
            var gap = U(8f);
            var recentW = starW;

            // In the compact view the search has the whole first row and the buttons go below it.
            var row = rect.y;
            var navW = starW * 2f + U(4f) + gap;
            Rect search;
            if (_compact)
            {
                search = new Rect(rect.x + navW, rect.y, rect.width - navW, rect.height);
                row = rect.yMax + U(8f);
            }
            else
            {
                search = new Rect(rect.x + navW, rect.y, rect.width - navW - originW - starW * 2f - recentW - gap * 5f, rect.height);
            }
            BackAndForward(explorer, new Rect(rect.x, rect.y, starW, rect.height), new Rect(rect.x + starW + U(4f), rect.y, starW, rect.height));
            Search(explorer, search);
            var startX = _compact ? rect.x - gap : search.xMax;

            // Help for the search terms.
            var help = new Rect(startX + gap, row, starW, rect.height);
            if (GUI.Button(help, "?", _help ? Skin.On : Skin.IconButton)) _help = !_help;
            if (help.Contains(Event.current.mousePosition)) AskTip("help", "How to search");

            var star = new Rect(help.xMax + gap, row, starW, rect.height);
            if (GUI.Button(star, GUIContent.none, explorer.FavouritesOnly ? Skin.On : Skin.IconButton))
            {
                explorer.FavouritesOnly = !explorer.FavouritesOnly;
                _listScroll = Vector2.zero;
                _help = false;
            }
            var icon = new Rect(star.x + star.width * 0.22f, star.y + star.height * 0.22f, star.width * 0.56f, star.height * 0.56f);
            Skin.Icon(icon, explorer.FavouritesOnly ? Skin.Star : Skin.StarHollow, explorer.FavouritesOnly ? Skin.Accent : Skin.Dim);
            if (star.Contains(Event.current.mousePosition)) AskTip("fav", explorer.FavouritesOnly ? "Showing only favourites" : "Show only favourites");

            var recent = new Rect(star.xMax + gap, row, recentW, rect.height);
            if (GUI.Button(recent, GUIContent.none, explorer.RecentOnly ? Skin.On : Skin.IconButton))
            {
                explorer.RecentOnly = !explorer.RecentOnly;
                _listScroll = Vector2.zero;
                _help = false;
            }
            var clock = new Rect(recent.x + recent.width * 0.22f, recent.y + recent.height * 0.22f, recent.width * 0.56f, recent.height * 0.56f);
            Skin.Icon(clock, Skin.Clock, explorer.RecentOnly ? Skin.Accent : Skin.Dim);
            if (recent.Contains(Event.current.mousePosition)) AskTip("recent", explorer.RecentOnly ? "Showing what you looked at last, newest first" : "Show what you looked at last, newest first");

            var originX = recent.xMax + gap * 2f;
            var originRow = row;
            if (originX + originW > rect.xMax)
            {
                originX = rect.x;
                originRow = row + rect.height + U(6f);
            }

            GUI.Label(new Rect(originX, originRow, fromW, rect.height), fromText, Skin.FaintLabel);
            var x = originX + fromW;
            for (var i = 0; i < names.Length; i++)
            {
                var on = (int)explorer.Origin == i;
                if (GUI.Button(new Rect(x, originRow, widths[i], rect.height), names[i], on ? Skin.SegmentOn : Skin.Segment))
                {
                    explorer.Origin = (OriginFilter)i;
                    _listScroll = Vector2.zero;
                    _help = false;
                }
                x += widths[i] + U(4f);
            }
            var originRect = new Rect(originX, originRow, originW, rect.height);
            if (originRect.Contains(Event.current.mousePosition)) AskTip("origin", "Everything, only the game's own, or only what mods added");

            return originRow + rect.height;
        }

        private static int _steppedFrame = -1;

        /// <summary>Back to the entry shown before, and forward again, as in a browser.</summary>
        private static void BackAndForward(Explorer explorer, Rect back, Rect forward)
        {
            var was = GUI.enabled;
            GUI.enabled = was && explorer.CanGoBack;
            if (GUI.Button(back, "\u2039", Skin.IconButton)) Step(explorer, true);
            GUI.enabled = was && explorer.CanGoForward;
            if (GUI.Button(forward, "\u203A", Skin.IconButton)) Step(explorer, false);
            GUI.enabled = was;
            if (back.Contains(Event.current.mousePosition)) AskTip("back", "Back to the entry shown before (mouse back button)");
            if (forward.Contains(Event.current.mousePosition)) AskTip("forward", "Forward again (mouse forward button)");
        }

        /// <summary>Goes back or forward, and shows where it lands.</summary>
        public static void Step(Explorer explorer, bool back)
        {
            // The same press can arrive both as a key and as a panel event; it counts once.
            if (explorer == null || _steppedFrame == Time.frameCount) return;
            _steppedFrame = Time.frameCount;
            if (!(back ? explorer.Back() : explorer.Forward())) return;
            _reveal = true;
            _sideScroll = Vector2.zero;
            _help = false;
        }

        /// <summary>
        /// A small filter box with its placeholder and, while it holds anything, a button to clear
        /// it, as the search has. Returns what it holds now.
        /// </summary>
        private static string FilterField(string control, string value, Rect rect)
        {
            var e = Event.current;
            var hasText = !string.IsNullOrEmpty(value);
            var clear = new Rect(rect.xMax - U(28f), rect.y + (rect.height - U(22f)) / 2f, U(22f), U(22f));

            // The text field takes every click inside it, so the clear button is handled before it.
            if (hasText && e.type == EventType.MouseDown && e.button == 0 && clear.Contains(e.mousePosition))
            {
                GUIUtility.keyboardControl = 0;
                e.Use();
                return "";
            }

            GUI.SetNextControlName(control);
            value = GUI.TextField(rect, value, 40, Skin.Field);
            if (string.IsNullOrEmpty(value))
            {
                GUI.Label(rect, "Filter", Skin.Placeholder);
                return value;
            }

            var hover = clear.Contains(e.mousePosition);
            if (hover) Skin.Icon(clear, Skin.Circle, new Color(1f, 1f, 1f, 0.12f));
            var style = Skin.Cross;
            var was = style.normal.textColor;
            style.normal.textColor = hover ? Skin.Text : Skin.Dim;
            GUI.Label(new Rect(clear.x, clear.y - U(1f), clear.width, clear.height), "×", style);
            style.normal.textColor = was;
            if (hover) AskTip("clear-filter:" + control, "Clear the filter");
            return value;
        }

        private static void Search(Explorer explorer, Rect rect)
        {
            SearchPicks(explorer);
            var e = Event.current;
            var hasText = !string.IsNullOrEmpty(explorer.Text);
            var clear = new Rect(rect.xMax - U(32f), rect.y + (rect.height - U(24f)) / 2f, U(24f), U(24f));

            // The text field takes every click inside it, so the clear button is handled before it.
            if (hasText && e.type == EventType.MouseDown && e.button == 0 && clear.Contains(e.mousePosition))
            {
                explorer.Text = "";
                _listScroll = Vector2.zero;
                _reveal = true;

                // A focused field keeps showing its own copy of the text until it lets go of the keyboard.
                GUIUtility.keyboardControl = 0;
                _focusSearch = true;
                hasText = false;
                e.Use();
            }

            GUI.SetNextControlName(SearchControl);
            var text = GUI.TextField(rect, explorer.Text, 80, Skin.Field);
            if (text != explorer.Text)
            {
                explorer.Text = text;
                _listScroll = Vector2.zero;
                _reveal = true;
                _help = false;
            }

            // After the typed text is taken: what Tab puts in must not be undone by it.
            SearchTab(explorer);
            hasText = !string.IsNullOrEmpty(explorer.Text);

            if (!hasText)
            {
                GUI.Label(rect, "Search by name", Skin.Placeholder);
            }
            else
            {
                var hover = clear.Contains(e.mousePosition);
                if (hover) Skin.Icon(clear, Skin.Circle, new Color(1f, 1f, 1f, 0.12f));
                var style = Skin.Cross;
                var was = style.normal.textColor;
                style.normal.textColor = hover ? Skin.Text : Skin.Dim;
                GUI.Label(new Rect(clear.x, clear.y - U(1f), clear.width, clear.height), "×", style);
                style.normal.textColor = was;
                if (hover) AskTip("clear-search", "Clear the search");
            }

            SearchSuggestions(explorer, rect);

            if (_focusSearch && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(SearchControl);
                _focusSearch = false;
            }
        }

        private static readonly Kind[] EveryKind = (Kind[])Enum.GetValues(typeof(Kind));

        /// <summary>
        /// Each tab's text by its name, count and whether it is picked, so drawing the tabs, which
        /// happens several times a frame, makes no new text while the counts stay the same.
        /// </summary>
        private static readonly Dictionary<(string Label, int Count, bool On), string> TabTexts =
            new Dictionary<(string, int, bool), string>();

        private static string TabText(string label, int count, bool on)
        {
            if (TabTexts.TryGetValue((label, count, on), out var text)) return text;
            if (TabTexts.Count > 2000) TabTexts.Clear();
            text = $"{label}  <color=#{(on ? "5a4526" : "8f929c")}>{count:N0}</color>";
            TabTexts[(label, count, on)] = text;
            return text;
        }

        /// <summary>One tab per kind with what the search holds of it, wrapping when the row is full.</summary>
        private static float Tabs(Explorer explorer, Rect rect)
        {
            var x = rect.x;
            var y = rect.y;
            var tabH = rect.height;
            var gap = U(4f);

            bool Tab(string label, int count, Color dot, bool on)
            {
                var style = on ? Skin.TabOn : Skin.Tab;
                var text = TabText(label, count, on);
                var width = Skin.Width(style, text) + U(2f);
                if (x + width > rect.xMax && x > rect.x)
                {
                    x = rect.x;
                    y += tabH + gap;
                }
                var tab = new Rect(x, y, width, tabH);
                var clicked = GUI.Button(tab, text, style);
                var size = U(8f);
                Skin.Icon(new Rect(tab.x + U(10f), tab.y + (tabH - size) / 2f, size, size), Skin.Circle, on ? Skin.OnAccent : dot);
                x += width + gap;
                return clicked;
            }

            if (Tab("All", explorer.CountAll, Skin.Text, explorer.KindFilter == null)) Filter(explorer, null);
            foreach (var kind in EveryKind)
            {
                var count = explorer.CountOf(kind);
                if (count == 0 && explorer.KindFilter != kind) continue;
                if (Tab(Kinds.Label(kind), count, Skin.KindColor(kind), explorer.KindFilter == kind)) Filter(explorer, kind);
            }

            return y + tabH;
        }

        private static void Filter(Explorer explorer, Kind? kind)
        {
            explorer.KindFilter = kind;
            _listScroll = Vector2.zero;
            _help = false;
            _reveal = true;
        }

        private static void Footer(Rect rect)
        {
            var note = Session.Note;
            var text = note ?? FootHint();
            Ticker(new Rect(rect.x, rect.y, rect.width - U(40f), rect.height), text, note != null ? Skin.DimLabel : Skin.FaintLabel);
        }

        /// <summary>
        /// One line of text that does not wrap. When it is longer than its room it slides slowly
        /// to its end and back, resting a moment at each end, so all of it can be read.
        /// </summary>
        private static void Ticker(Rect rect, string text, GUIStyle style)
        {
            var width = Skin.Width(style, text);
            var overflow = width - rect.width;
            if (overflow <= 0f)
            {
                GUI.Label(rect, text, style);
                return;
            }

            const float rest = 1.6f;
            var slide = overflow / Mathf.Max(1f, U(40f));
            var cycle = 2f * (rest + slide);
            var phase = Time.unscaledTime % cycle;
            float along;
            if (phase < rest) along = 0f;
            else if (phase < rest + slide) along = (phase - rest) / slide;
            else if (phase < rest * 2f + slide) along = 1f;
            else along = 1f - (phase - rest * 2f - slide) / slide;

            GUI.BeginGroup(rect);
            GUI.Label(new Rect(-overflow * along, 0f, width + U(4f), rect.height), text, style);
            GUI.EndGroup();
        }

        // ----- Sections and rows -----

        /// <summary>The sections folded shut, by key. Details start shut; the rest start open.</summary>
        private static readonly HashSet<string> Folded = new HashSet<string> { "details" };

        private static bool IsFolded(string key) => key != null && Folded.Contains(key);

        /// <summary>Whether this pass over the side has drawn the fold-all link yet.</summary>

        /// <summary>Every section that folds, by key.</summary>
        private static readonly string[] Foldable = { "kept", "variants", "adjust", "animations", "effects", "playsin", "links", "facts", "command", "details" };

        private static void FoldAll(bool fold)
        {
            if (fold) foreach (var key in Foldable) Folded.Add(key);
            else Folded.Clear();
            SaveRects();
        }

        /// <summary>
        /// A section's heading and rule. With a key, the heading folds the section shut or opens
        /// it when clicked, and the choice is remembered; the caller skips its body while folded.
        /// </summary>
        private static float SectionHeading(string text, float width, float y, Action reset, string key = null)
        {
            var folded = IsFolded(key);
            var shown = key == null ? text : text + (folded ? "  \u25B8" : "  \u25BE");
            var textW = Skin.Width(Skin.Heading, shown);
            var head = new Rect(0f, y, textW + U(4f), U(20f));
            if (key == null)
            {
                GUI.Label(head, shown, Skin.Heading);
            }
            else
            {
                LinkLabel(head, shown, Skin.Heading, Skin.Heading.normal.textColor);
                if (head.Contains(Event.current.mousePosition)) AskTip("fold:" + key, (folded ? "Open this section" : "Fold this section away") + "\nShift-click: every section");
                if (GUI.Button(head, GUIContent.none, GUIStyle.none))
                {
                    if (Event.current.shift) FoldAll(!folded);
                    else if (folded) Folded.Remove(key);
                    else Folded.Add(key);
                    SaveRects();
                }
            }
            var lineEnd = reset != null ? width - U(74f) : width;

            Skin.Fill(new Rect(textW + U(12f), y + U(10f), Mathf.Max(0f, lineEnd - textW - U(12f)), U(1f)), Skin.Outline);
            if (reset != null && GUI.Button(new Rect(width - U(64f), y - U(2f), U(64f), U(24f)), "Reset", Skin.Chip)) reset();
            return y + U(folded ? 36f : 30f);
        }

        private static float SliderRow(string label, string value, float current, float min, float max, float width, float labelW, ref float y)
        {
            var rowH = U(26f);
            FitLabel(new Rect(0f, y, labelW - U(6f), rowH), label, Skin.DimLabel, 10f);
            var valueW = U(64f);
            var slider = new Rect(labelW, y + (rowH - U(14f)) / 2f, width - labelW - valueW - U(10f), U(14f));
            var result = GUI.HorizontalSlider(slider, current, min, max);
            GUI.Label(new Rect(width - valueW, y, valueW, rowH), value, Skin.Label);
            y += rowH + U(8f);
            return result;
        }

        private static int Segments(string label, List<string> names, int selected, float width, float labelW, ref float y)
        {
            var rowH = U(28f);
            FitLabel(new Rect(0f, y, labelW - U(6f), rowH), label, Skin.DimLabel, 10f);
            var x = labelW;
            var chosen = -1;
            for (var i = 0; i < names.Count; i++)
            {
                var style = i == selected ? Skin.SegmentOn : Skin.Segment;
                var w = Skin.Width(style, names[i]) + U(10f);
                if (x + w > width && x > labelW)
                {
                    x = labelW;
                    y += rowH + U(4f);
                }
                if (GUI.Button(new Rect(x, y, w, rowH), names[i], style)) chosen = i;
                x += w + U(4f);
            }
            y += rowH + U(8f);
            return chosen;
        }

        // ----- Tooltips -----

        /// <summary>Asks for a tooltip at the mouse, shown once the mouse has rested on the same thing for a moment.</summary>
        /// <summary>The panel's foot line: its keys, and walking and looking as the settings allow them.</summary>
        private static readonly string[] FootHints = new string[8];

        /// <summary>
        /// The keys that are not obvious, short, and only those the settings allow; the rest is
        /// under "?". Made once for each view and setting.
        /// </summary>
        private static string FootHint()
        {
            var walk = Plugin.WalkWhileOpen;
            var look = Plugin.LookWithRightMouse;
            var index = (_compact ? 4 : 0) + (walk ? 2 : 0) + (look ? 1 : 0);
            if (FootHints[index] != null) return FootHints[index];

            var parts = new List<string>();
            if (!_compact) parts.AddRange(new[] { "Enter plays", "Ctrl+F searches", "Esc leaves a box" });
            if (walk) parts.Add("keys walk when not typing");
            if (look) parts.Add("right-drag outside the panel to look");
            if (parts.Count == 0) parts.Add("Enter plays");
            parts[0] = char.ToUpperInvariant(parts[0][0]) + parts[0].Substring(1);
            return FootHints[index] = string.Join("  ·  ", parts);
        }

        private static int _offCount = -1;
        private static string _offTip = "";

        /// <summary>
        /// When a game update has turned features off, a chip beside the title says so, and its
        /// tip names them: nothing fails quietly. It stays until Scry is updated for the game.
        /// </summary>
        private static void GameChangedChip(float x, float limit, Event e)
        {
            var count = Compatibility.OffCount;
            if (count == 0) return;
            if (count != _offCount)
            {
                _offCount = count;
                var off = Compatibility.FeaturesOff();
                _offTip = "This version of Scry does not fully know this version of the game, so these are off until Scry is updated:\n"
                          + string.Join("\n", off.Select(f => "  " + f)) + "\nEverything else works. The log has the details.";
            }
            const string text = "Game changed";
            var w = Skin.Width(Skin.Chip, text) + U(12f);
            if (x + w > limit) return;
            var rect = new Rect(x, U(18f), w, U(24f));
            GUI.Label(rect, text, Skin.Chip);
            if (rect.Contains(e.mousePosition)) AskTip("game-changed", _offTip);
        }

        private static void AskTip(string key, string text)
        {
            if (Event.current.type != EventType.Repaint || _drag != Drag.None || string.IsNullOrEmpty(text)) return;
            _askedTipKey = key;
            _askedTipText = text;
            _askedTipAt = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        }

        private static void Tooltip()
        {
            if (Event.current.type != EventType.Repaint) return;

            if (_askedTipKey == null)
            {
                _tipKey = null;
                return;
            }

            if (_askedTipKey != _tipKey)
            {
                _tipKey = _askedTipKey;
                _tipSince = Time.unscaledTime;
            }
            if (Time.unscaledTime - _tipSince < TipDelay) return;

            var content = new GUIContent(_askedTipText);
            var maxW = U(420f);
            var width = Mathf.Min(maxW, Skin.Width(Skin.Tip, _askedTipText) + U(2f));
            var height = Skin.Height(Skin.Tip, _askedTipText, width);
            var x = Mathf.Min(_askedTipAt.x + U(16f), Screen.width - width - U(4f));
            var y = _askedTipAt.y + U(20f);
            if (y + height > Screen.height - U(4f)) y = _askedTipAt.y - height - U(8f);
            Skin.Tip.Draw(new Rect(x, y, width, height), content, false, false, false, false);
        }
    }
}
