using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The panel's life: opening and closing it, the catalog of the world it is in, and letting
    /// everything go when that world is left.
    ///
    /// The catalog is read over many frames, a few milliseconds each, starting a little after
    /// the world is entered, so it is usually ready by the time the panel is first opened.
    /// Opened before then, the panel says how far it has got, and the reading goes faster.
    /// </summary>
    internal static class Session
    {
        private static ZNetScene _scene;
        private static Favourites _favourites;
        private static CatalogJob _job;
        private static float _inWorldSince = -1f;
        private static ZNetScene _failedIn;
        private static string _pendingSearch;

        /// <summary>The search and filters of the last world's explorer, carried over to the next.</summary>
        private static (string Text, Kind? Kind, OriginFilter Origin, bool Favourites)? _carried;

        /// <summary>How long after arriving in a world the catalog starts to be read, so the world's own loading goes first.</summary>
        private const float QuietDelay = 10f;

        /// <summary>Scry's share of a frame for reading the catalog while nobody waits for it, and while the open panel does.</summary>
        private const double QuietBudgetMs = 4;
        private const double WaitedBudgetMs = 14;

        /// <summary>What is being read of the catalog while the open panel waits for it; null once there is nothing to wait for.</summary>
        public static string Reading => IsOpen && Explorer == null ? _job != null ? _job.Progress : "Reading the catalog" : null;
        private static int _closedFrame = -10;
        private static string _note;
        private static float _noteUntil;

        public static bool IsOpen { get; private set; }

        public static Explorer Explorer { get; private set; }

        /// <summary>How the catalog came out, for the panel's header.</summary>
        public static string CatalogSummary { get; private set; } = "";

        /// <summary>
        /// Whether the game should keep its hands off input. Stays true for the frame after
        /// closing, so the Escape that closed the panel does not also open the game's menu.
        /// </summary>
        public static bool BlocksInput => IsOpen || Time.frameCount <= _closedFrame + 1;

        /// <summary>A short line shown at the foot of the panel for a few seconds.</summary>
        public static string Note => Time.unscaledTime < _noteUntil ? _note : null;

        public static void Say(string text)
        {
            _note = text;
            _noteUntil = Time.unscaledTime + 4f;
        }

        public static void Toggle()
        {
            if (IsOpen) Hide();
            else Show(null);
        }

        public static void Show(string search)
        {
            if (ZNetScene.instance == null || ObjectDB.instance == null || Player.m_localPlayer == null)
            {
                Chat.instance?.AddString("Scry opens once you are in a world.");
                return;
            }

            // Opened before the catalog is read, the panel says how far it has got meanwhile.
            if (search != null)
            {
                if (Explorer != null) Explorer.Text = search;
                else _pendingSearch = search;
            }
            if (Explorer == null && ReferenceEquals(_failedIn, ZNetScene.instance)) _failedIn = null;
            IsOpen = true;
            ScryPanel.Opened();
        }

        public static void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Looking = false;
            _closedFrame = Time.frameCount;
            Previews.Suspend();

            if (Previews.AnythingInWorld && !_toldPreviewsStay)
            {
                _toldPreviewsStay = true;
                MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft,
                    "Scry's previews stay in the world until you clear them in the panel or with /scry clear.");
            }
        }

        /// <summary>
        /// Whether the right mouse button is held to look around while the panel is open. Only the
        /// camera is freed; moving, blocking and attacking stay off.
        /// </summary>
        public static bool Looking { get; private set; }

        /// <summary>
        /// Whether the character can walk while the panel is open: whenever none of its text boxes
        /// has the keyboard, so typing a search never moves anyone.
        /// </summary>
        public static bool Walking => IsOpen && !ScryPanel.Typing;

        private static bool _toldPreviewsStay;

        public static void Update()
        {
            // Each step on its own: one that fails (a game update renamed what it reads) is told
            // once and costs only itself, never the key that opens the panel or the steps after it.
            Step("noticing a world left", () =>
            {
                // Leaving a world destroys every prefab the catalog points at. Compared as references:
                // once the scene is destroyed, Unity's own comparison calls it null, the same as the
                // instance the game has then, and the change would never be seen.
                // A world left while its catalog is still being read goes the same way.
                var of = !ReferenceEquals(_scene, null) ? _scene : _job?.Scene;
                if (!ReferenceEquals(of, null) && !ReferenceEquals(ZNetScene.instance, of)) Forget();
                if (IsOpen && Player.m_localPlayer == null) Hide();
            });

            Step("reading the catalog", () => ReadCatalog());
            if (Explorer != null) Step("preparing the panel's lookups", () => ScryPanel.Prepare(Explorer));

            Step("the key that opens the panel", () =>
            {
                if (Input.GetKeyDown(Plugin.OpenKey) && CanToggle() && !TypingIt(Plugin.OpenKey)) Toggle();
            });

            Step("looking around and stepping back", () =>
            {
                // Looking starts only from a press outside the panel, so a right click on it stays a click.
                if (!IsOpen || !Input.GetMouseButton(1)) Looking = false;
                else if (Input.GetMouseButtonDown(1) && !ScryPanel.Covers(Input.mousePosition)) Looking = true;

                // The mouse's own back and forward buttons step through jumps, as in a browser.
                if (IsOpen && Explorer != null && Input.GetKeyDown(KeyCode.Mouse3)) ScryPanel.Step(Explorer, true);
                if (IsOpen && Explorer != null && Input.GetKeyDown(KeyCode.Mouse4)) ScryPanel.Step(Explorer, false);
            });

            Step("the previews", () => Previews.Update(IsOpen ? Explorer : null));

            var reading = Timing.Start();
            try { Locations.Update(); } catch (Exception ex) { Faults.Tell("reading the locations", ex); Locations.Forget(); }
            Timing.Add("locations", reading);
        }

        private static void Step(string part, Action step)
        {
            try { step(); }
            catch (Exception ex) { Faults.Tell(part, ex); }
        }

        public static void LateUpdate()
        {
            if (IsOpen) Stage.Render();
        }

        public static void Shutdown()
        {
            Previews.ClearWorld();
            Stage.Clear();
        }

        /// <summary>A key that types a character does not close the panel while its search box has it.</summary>
        private static bool TypingIt(KeyCode key)
        {
            if (!IsOpen || !ScryPanel.SearchFocused) return false;
            return (key >= KeyCode.Space && key <= KeyCode.Z) || (key >= KeyCode.Keypad0 && key <= KeyCode.KeypadEquals);
        }

        /// <summary>
        /// Not while typing in the chat or console, or with the game's menu up. Should the game no
        /// longer answer one of those, the key opens the panel anyway, rather than never.
        /// </summary>
        private static bool CanToggle()
        {
            if (IsOpen) return true;
            try
            {
                return GameAllowsToggle();
            }
            catch (Exception ex)
            {
                Faults.Tell("telling whether the chat, console or menu is up", ex);
                return true;
            }
        }

        private static bool GameAllowsToggle()
        {
            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            if (global::Console.IsVisible() || Menu.IsVisible()) return false;
            return true;
        }

        /// <summary>
        /// Reads on at the catalog of the world, if it is not read yet: once the world has been
        /// entered a few seconds, or at once when the panel is opened, and faster while it waits.
        /// A reading that failed is tried again only when the panel is next opened.
        /// </summary>
        private static void ReadCatalog()
        {
            var scene = ZNetScene.instance;
            if (scene == null || ObjectDB.instance == null || Player.m_localPlayer == null)
            {
                _inWorldSince = -1f;
                return;
            }
            if (Explorer != null && ReferenceEquals(_scene, scene)) return;

            if (_inWorldSince < 0f) _inWorldSince = Time.unscaledTime;
            if (_job == null)
            {
                if (!IsOpen && (Time.unscaledTime - _inWorldSince < QuietDelay || ReferenceEquals(_failedIn, scene))) return;
                _job = new CatalogJob();
            }

            var started = Timing.Start();
            var done = _job.Advance(IsOpen ? WaitedBudgetMs : QuietBudgetMs);
            Timing.Add("catalog", started);
            if (!done) return;

            var job = _job;
            _job = null;
            if (job.Entries != null) Made(job, scene);
            else if (job.Failure != "the world was left") Failed(job.Failure, scene);
        }

        private static void Made(CatalogJob job, ZNetScene scene)
        {
            var catalog = job.Entries;
            _favourites = _favourites ?? new Favourites(Path.Combine(Plugin.DataFolder, "favourites.txt"));
            if (_favourites.Problem != null) Plugin.Log.LogWarning($"Scry could not read its favourites: {_favourites.Problem}");

            Explorer = new Explorer(catalog, _favourites);
            if (_carried.HasValue)
            {
                var carried = _carried.Value;
                Explorer.Text = carried.Text;
                Explorer.KindFilter = carried.Kind;
                Explorer.Origin = carried.Origin;
                Explorer.FavouritesOnly = carried.Favourites;
            }
            if (_pendingSearch != null) Explorer.Text = _pendingSearch;
            _pendingSearch = null;
            _carried = null;

            _scene = scene;
            _failedIn = null;
            CatalogSummary = $"{catalog.Count:N0} prefabs";
            var kinds = string.Join(", ", catalog.GroupBy(e => e.Kind).OrderBy(g => g.Key).Select(g => $"{g.Count()} {Kinds.Label(g.Key).ToLowerInvariant()}"));
            Plugin.Log.LogInfo($"Scry read {catalog.Count} prefabs and status effects in {job.WorkMs:0} ms over {job.Frames} frames ({job.ElapsedMs / 1000.0:0.0} s in all): {kinds}.");
        }

        private static void Failed(string why, ZNetScene scene)
        {
            _failedIn = scene;
            Plugin.Log.LogError($"Scry could not read the game's prefabs: {why}");
            if (!IsOpen) return;
            Hide();
            Chat.instance?.AddString("Scry could not read the game's prefabs; the log has the details.");
        }

        /// <summary>
        /// Lets go of everything from the world left. The scene is let go first, so a step that
        /// fails is not tried again every frame, and each step goes on its own.
        /// </summary>
        private static void Forget()
        {
            _scene = null;
            _job = null;
            _failedIn = null;
            _inWorldSince = -1f;
            try { Hide(); } catch (Exception ex) { Faults.Tell("closing on leaving a world", ex); }
            try { Previews.ClearWorld(); } catch (Exception ex) { Faults.Tell("clearing the world's previews", ex); }

            // Everything else kept of the world, each class having registered how it forgets.
            WorldCaches.ForgetAll((name, ex) => Faults.Tell("forgetting " + name, ex));

            // The search and filters carry over to the next world; the entries cannot.
            try { if (Explorer != null) Explorer.Select(null); } catch (Exception ex) { Faults.Tell("letting go of the selection", ex); }
            if (Explorer != null) _carried = (Explorer.Text, Explorer.KindFilter, Explorer.Origin, Explorer.FavouritesOnly);
            Explorer = null;
        }
    }
}
