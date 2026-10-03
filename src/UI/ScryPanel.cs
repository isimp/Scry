using System;
using System.Collections.Generic;
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

        private enum Drag { None, Move, Resize, Orbit, StageSize, Pan, ListSize, Ruler }

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

        /// <summary>The length the list's share is of, in the view drawn last, for a drag of the gap.</summary>
        private static float _listSpan = 1f;

        private const float ListNarrowest = 0.20f;
        private const float ListWidest = 0.72f;
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

        /// <summary>A rect of the panel's, where it is on the screen: kept so a later event or a box drawn over everything finds it.</summary>
        private static Rect OnScreen(Rect here) => new Rect(GUIUtility.GUIToScreenPoint(here.position), here.size);

        /// <summary>A rect kept on the screen, back in the coordinates of what is being drawn now.</summary>
        private static Rect Here(Rect onScreen) => new Rect(GUIUtility.ScreenToGUIPoint(onScreen.position), onScreen.size);

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
            RaidCardCache.Clear();
            VariantCache.Clear();
            PrefabIcons.Clear();
            KindByKey.Clear();
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
            _listRows.Clear();
            _rowOfEntry.Clear();
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
            MemberKeys.Clear();
            Assist.Forget();

            // What the panel last drew for, which would keep the world left alive until drawn again.
            _firstClip = null;
            _firstEffect = default;
            _foundInFor = null;
            _foundInOf = null;
            _foundInIn = null;
            FoundInItems.Clear();
            _reportFor = null;
            _report = null;
            _modCountFor = null;
            _planOf = null;
            PlanRooms.Clear();
            // A box left open would still match the new world's empty selection and draw on.
            CloseBoxes();
            _slideFor = null;
            _viewFor = null;
        }

        /// <summary>How many times the open panel has been drawn this session.</summary>
        public static int Repaints { get; private set; }

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
                if (!Guard.Run("the panel", DrawPanel, scale))
                {
                    // Told once for each way it fails; the groups and scroll views left open are
                    // closed by leaving this event the way Unity provides for it.
                    GUI.enabled = true;
                    GUI.color = Color.white;
                    GUI.skin = skin;
                    GUIUtility.ExitGUI();
                }
            }
            finally
            {
                GUI.skin = skin;
            }
        }

        /// <summary>The open panel, drawn for one event: its place, its keys, its drags, its parts and its boxes over everything.</summary>
        private static void DrawPanel(float scale)
        {
            Skin.Ensure(scale);
            _s = scale;
            GUI.skin = Skin.Gui;
            if (Event.current.type == EventType.Repaint) _askedTipKey = null;

            Place();
            var explorer = Session.Explorer;

            // A header slider's box, or the stage's View box, takes the mouse over it before anything under it does.
            if (SlideInput(explorer) || ViewInput(explorer)) return;

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
            SlideDraw(explorer);
            ViewDraw();
            Tooltip();

            // The panel is solid: clicks and the wheel over it stop here.
            var e = Event.current;
            if (Win.Contains(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();

            if (e.type == EventType.Repaint) Repaints++;

            var focused = GUI.GetNameOfFocusedControl();
            SearchFocused = focused == SearchControl;
            Typing = focused == SearchControl || focused == ClipControl || focused == EffectControl;
            FocusedFilter = focused == ClipControl || focused == EffectControl ? focused : null;
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

                // The card is the top of the panel, and dragging it moves the panel, as the
                // header does once the catalog is read.
                Drags();

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

                // Anywhere but the close button, which has taken its own click by now.
                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    _drag = Drag.Move;
                    e.Use();
                }

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
            var locText = reading ? $"Stop reading  {Numbers.Count(Locations.Done)} of {Numbers.Count(Locations.Total)}" : LocationsButtonText;
            var locW = Skin.Width(Skin.Button, reading ? "Stop reading  000 of 000" : LocationsButtonText) + U(10f);
            var locRect = new Rect(clearRect.x - U(8f) - locW, clearRect.y, locW, clearRect.height);
            var showLoc = Locations.Now != Locations.State.Read && locRect.x > pad + U(98f);

            var header = new Rect(0f, 0f, (showLoc ? locRect.x : clearRect.x) - U(8f), U(52f));
            GUI.Label(new Rect(pad, U(10f), U(90f), U(34f)), "Scry", Skin.Title);

            if (showLoc)
            {
                // While reading, the same button stops it.
                if (GUI.Button(locRect, locText, Skin.Button))
                {
                    if (reading) StopReadingLocations();
                    else StartReadingLocations();
                }
                if (locRect.Contains(e.mousePosition)) AskTip("locations", reading ? "Stop reading the locations and dungeons; nothing read so far is kept" : LocationsButtonTip);
            }

            if (outLines > 0 && GUI.Button(clearRect, clearText, Skin.Primary))
            {
                Previews.ClearWorld();
                _outOpen = false;
                Session.Say("Cleared. Nothing from Scry is left in the world.");
            }
            OutHover(clearRect, outLines, e, new Rect(pad, 0f, w - pad * 2f, h - pad));
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

            // The self-test and what is off, in strips under the header, the rest moved down to make room.
            var testH = TestNoticeHeight();
            if (testH > 0f) TestNotice(new Rect(pad, U(54f), w - pad * 2f, testH - U(4f)));
            var noticeH = OffNoticeHeight();
            if (noticeH > 0f) OffNotice(new Rect(pad, U(54f) + testH, w - pad * 2f, noticeH - U(4f)));
            noticeH += testH;

            var controls = Timing.Start();
            var y = Controls(explorer, new Rect(pad, U(56f) + noticeH, w - pad * 2f, U(36f)));
            Timing.Add("controls search", controls);
            var tabs = Timing.Start();
            y = Tabs(explorer, new Rect(pad, y + U(10f), w - pad * 2f, U(30f)));
            Timing.Add("controls tabs", tabs);
            Timing.Add("panel controls", controls);

            var bodyTop = y + U(12f);
            var footerH = U(28f);
            var bodyBottom = h - pad - footerH;
            var bodyH = bodyBottom - bodyTop;

            // The list and the details share the room at the gap between them, which can be
            // dragged, or folded away to leave the details all of it.
            if (_compact)
            {
                var gapH = ListHidden ? U(28f) : U(12f);
                _listSpan = bodyH - U(12f);
                var listH = ListHidden ? 0f : Mathf.Round(_listSpan * ListShare);
                if (!ListHidden)
                {
                    var listed = Timing.Start();
                    List(explorer, new Rect(pad, bodyTop, w - pad * 2f, listH));
                    Timing.Add("panel list", listed);
                }
                var gap = new Rect(pad, bodyTop + listH, w - pad * 2f, gapH);
                ListDivider(gap, upright: false);
                var sideTop = gap.yMax + (ListHidden ? U(8f) : 0f);
                Side(explorer, new Rect(pad, sideTop, w - pad * 2f, bodyBottom - sideTop), withStage: false);
            }
            else
            {
                var gapW = ListHidden ? U(26f) : pad;
                _listSpan = w - pad * 3f;
                var leftW = ListHidden ? 0f : Mathf.Round(_listSpan * ListShare);
                if (!ListHidden)
                {
                    var listed = Timing.Start();
                    List(explorer, new Rect(pad, bodyTop, leftW, bodyH));
                    Timing.Add("panel list", listed);
                }
                var gap = new Rect(pad + leftW, bodyTop, gapW, bodyH);
                ListDivider(gap, upright: true);
                var rightX = gap.xMax + (ListHidden ? U(8f) : 0f);
                Side(explorer, new Rect(rightX, bodyTop, w - rightX - pad, bodyH), withStage: true);
            }

            Footer(new Rect(pad, h - pad - footerH + U(6f), w - pad * 2f, footerH));

            // Over everything below the search box; its clicks were taken before any of it drew.
            Assist.Draw();
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
    }
}
