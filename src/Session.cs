using System;
using System.Collections.Generic;
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
        /// <summary>The search and the page asked for before the catalog is read, shown once it is.</summary>
        private static readonly PendingPage Pending = new PendingPage();

        /// <summary>The search and filters of the last world's explorer, carried over to the next.</summary>
        private static (string Text, Kind? Kind, OriginFilter Origin, bool Favourites)? _carried;

        /// <summary>How long after entering a world the catalog waits before it is read, unless the panel is opened.</summary>
        private static float QuietDelay => Settings.CatalogDelay;

        /// <summary>Scry's share of a frame for reading the catalog while nobody waits for it, and while the open panel does.</summary>
        private const double QuietBudgetMs = 4;
        private const double WaitedBudgetMs = 14;

        /// <summary>What is being read of the catalog while there is no explorer yet; null once there is.</summary>
        private static string Reading => Explorer == null ? _job != null ? _job.Progress : "Reading the catalog" : null;

        public static bool IsOpen => ScryPanel.IsOpen;

        public static Explorer Explorer { get; private set; }

        public static void Toggle()
        {
            if (IsOpen) Hide();
            else Show(null);
        }

        public static void Show(string search)
        {
            if (ZNetScene.instance == null || ObjectDB.instance == null || Player.m_localPlayer == null)
            {
                Chat.instance.OrNull()?.AddString("Scry opens once you are in a world.");
                return;
            }

            // Opened before the catalog is read, the panel says how far it has got meanwhile.
            if (search != null)
            {
                if (Explorer != null) Explorer.Text = search;
                else Pending.Ask(search, null);
            }
            if (Explorer == null && ReferenceEquals(_failedIn, ZNetScene.instance)) _failedIn = null;
            if (Explorer != null) Explorer.RecentLimit = Settings.RecentCount;
            ScryPanel.Opened(Explorer, Reading);
        }

        /// <summary>
        /// Opens the panel on an entry's page, as a click on a dungeon's mark on Valheim's map does:
        /// at once where the catalog is read, else once it is, unless the panel is closed meanwhile.
        /// </summary>
        public static void ShowEntry(string key)
        {
            Show(null);
            if (!IsOpen) return;
            if (Explorer != null) Explorer.Jump(key);
            else Pending.Ask(null, key);
        }

        public static void Hide()
        {
            // Closed before the catalog is read, the page asked for is let go of.
            Pending.Closed();
            if (!IsOpen) return;
            ScryPanel.Closed();
            Previews.Suspend();
            // A peek lasts while the panel is open.
            if (Explorer != null) Explorer.Peeking = false;

            if (Previews.AnythingInWorld && !_toldPreviewsStay)
            {
                _toldPreviewsStay = true;
                MessageHud.instance.OrNull()?.ShowMessage(MessageHud.MessageType.TopLeft,
                    "Scry's previews stay in the world until you clear them in the panel or with /scry clear.");
            }
        }

        private static bool _toldPreviewsStay;

        public static void Update()
        {
            // Each step on its own: one that fails (a game update renamed what it reads) is told
            // once and costs only itself, never the key that opens the panel or the steps after it.
            Step(Feature.Leaving, "noticing a world left", "update leaving", () =>
            {
                // Leaving a world destroys every prefab the catalog points at. Compared as references:
                // once the scene is destroyed, Unity's own comparison calls it null, the same as the
                // instance the game has then, and the change would never be seen.
                // A world left while its catalog is still being read goes the same way.
                var of = !ReferenceEquals(_scene, null) ? _scene : _job?.Scene;
                if (!ReferenceEquals(of, null) && !ReferenceEquals(ZNetScene.instance, of)) Forget();
                if (IsOpen && Player.m_localPlayer == null) Hide();
            });

            // Timed by itself, as "update catalog", where it reads.
            Step(Feature.Catalog, "reading the catalog", null, () => ReadCatalog());

            Step(Feature.OpenKey, "the key that opens the panel", "update key", () =>
            {
                if (Input.GetKeyDown(Settings.OpenKey) && CanToggle() && !TypingIt(Settings.OpenKey)) Toggle();
            });

            // After the key, so the panel opened by it is handed what it shows in the same frame.
            var panel = Timing.Start();
            ScryPanel.Update(Explorer, Reading);
            Timing.Add("update panel", panel);

            Step(Feature.Previews, "the previews", "update previews", () => Previews.Update(IsOpen ? Explorer : null));
            Step(Feature.World, "compiling Scry's code ahead", "update warmup", Warm);
            Step(Feature.SelfTest, "the self-test", null, () =>
            {
                var started = Timing.Start();
                var inner = Timing.InnerMs();
                SelfTestHost.Tick();
                Timing.Own(Timing.SelfTestPart, started, inner);
            });

            if (!Guard.Run(Feature.Locations, "reading the locations", Locations.Update, "update locations")) Locations.Forget();
            Step(Feature.FloorsKept, "the floors kept", "update floors kept", FloorRecords.Update);
            Step(Feature.CompendiumCatchUp, "what the character knows", "update compendium", CompendiumKeeper.Update);
            Step(Feature.DungeonMaps, "the rooms walked in a dungeon", "update dungeon walks", DungeonWalks.Update);
            Step(Feature.CompendiumShown, "showing what the character knows", "update compendium list", ShowKnown);
        }

        /// <summary>Whether a creature, by its prefab's name, is left off the stage: as the page shown would leave it out (<see cref="Explorer.NotMet"/>, <see cref="Explorer.LeavesOut(string)"/>).</summary>
        private static readonly Func<string, bool> CreatureLeftOff = key => Explorer != null && (Explorer.NotMet(key) || Explorer.LeavesOut(key));

        /// <summary>What the character knows of the explorer's catalog, for the compendium; made with the explorer.</summary>
        private static CompendiumView _known;

        /// <summary>A made-up character's knowledge shown in place of the one played, by the self-test; none otherwise.</summary>
        private static CompendiumView _knownInstead;

        /// <summary>Shows a made-up character's knowledge in place of the one played, for the self-test; none puts the played one back.</summary>
        [ForTest]
        public static void ShowKnownInstead(CompendiumView view) => _knownInstead = view;

        /// <summary>
        /// Hands the explorer what the character knows under the rules chosen for it, which lists
        /// again only when either changed. Not while the panel is closed: nothing shows it then,
        /// and telling it goes through the whole catalog, so what is learned in play waits for the
        /// panel to open. A view the self-test shows in its place is handed over at once. What is
        /// learned while the panel is open is told at most once a second, so things seen one frame
        /// after another do not each cost a listing again; a rule or floor picked shows at once.
        /// </summary>
        private static void ShowKnown()
        {
            if (Explorer == null || (!IsOpen && _knownInstead == null)) return;
            if (_known == null || !ReferenceEquals(_known.Catalog, Explorer.Entries))
            {
                _known = new CompendiumView(Explorer.Entries);
                _known.RoomsHold(RoomContents.Of);
            }
            // The floor as far as the character's bosses fell; the tiers worked out only once one is set.
            var book = CompendiumKeeper.Book;
            var rule = book?.Rule ?? CompendiumRule.Off;
            var floor = System.Math.Min(book?.Floor ?? 0, CompendiumKeeper.Ceiling);
            // Each step timed apart, as a regrouped catalog costs all three at once.
            var step = Timing.Start();
            _known.Milestones(floor > 0 ? CompendiumTiers.Of(Explorer.Entries) : null, floor);
            Timing.Add("compendium tiers", step);
            // Only the skills the character has learned show; read only while the compendium is on.
            if (rule != CompendiumRule.Off) _known.SkillsLearned(YourSkills.Learned, YourSkills.LearnedVersion);
            // Under the strict rules a dungeon's rooms show by their kinds walked.
            if (rule == CompendiumRule.Strict) _known.RoomsWalked(WalkedRooms.Walked, WalkedRooms.Version);
            // What anyone on this server walked in the dungeons the sources hold, for those shown with insides.
            if (rule != CompendiumRule.Off)
            {
                var walks = SourceWalks.Of(book);
                _known.WalkedOnServer(walks, SourceWalks.Version);
            }
            // The places the world's sources found are this world's.
            _known.InWorld(CompendiumMap.World);
            if (!_known.OnlyLearnedSince(book, rule) || Time.unscaledTime - _knownToldAt >= 1f)
            {
                step = Timing.Start();
                var version = _known.Version;
                _known.Tell(book, rule);
                if (_known.Version != version) _knownToldAt = Time.unscaledTime;
                Timing.Add("compendium told", step);
            }
            step = Timing.Start();
            Explorer.Know(_knownInstead ?? _known);
            var (held, heldVersion) = CompendiumMap.HeldPlaces(book, Explorer.Entries);
            Explorer.WorldHolds(held, heldVersion);
            // A world's server allowing no peeking ends one.
            if (book != null && !book.World.Peeking && Explorer.Peeking) Explorer.Peeking = false;
            Timing.Add("compendium listed", step);
        }

        /// <summary>How many times what the played character knows was counted whole for the list, by what set it off; for the self-test.</summary>
        [Diagnostic, ForTest]
        public static int KnownWholeCountsOf(WholeCause cause) => _known != null ? _known.WholeCountsOf(cause) : 0;

        /// <summary>When what the character knows was last told to the list, by the unscaled clock.</summary>
        private static float _knownToldAt = float.NegativeInfinity;

        /// <summary>Scry's code compiled ahead (<see cref="CodeWarmup"/>), once a session, made once a world's catalog is read; for the self-test to see it done.</summary>
        public static CodeWarmup Warmup { get; private set; }

        /// <summary>How much of a frame compiling ahead takes, beyond the one method each frame always compiles.</summary>
        private const double WarmupShareMs = 2.0;

        /// <summary>Compiles on within the frame's share, the panel's code first, until all of Scry's is compiled.</summary>
        private static void Warm()
        {
            if (Explorer == null || Warmup?.Done == true) return;
            if (Warmup == null) Warmup = new CodeWarmup(CodeWarmup.MethodsOf(typeof(Session).Assembly.GetTypes(), typeof(ScryPanel), typeof(Skin), typeof(Facts)));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Warmup.Step(() => watch.Elapsed.TotalMilliseconds, WarmupShareMs, method => Steps.Run(() => method.MethodHandle.GetFunctionPointer(), null) == null);
            if (Warmup.Done) Log.Note($"Scry compiled its code ahead: {Numbers.Count(Warmup.Compiled)} methods, {Numbers.Count(Warmup.Failed)} could not be.");
        }

        /// <summary>One step of the frame, timed as a part of its own inside the update (<paramref name="timed"/>), so what it costs is told apart.</summary>
        private static void Step(Feature feature, string part, string timed, Action step) => Guard.Run(feature, part, step, timed);

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
            return !Guard.Run(Feature.OpenKey, "telling whether the chat, console or menu is up", GameAllowsToggle, out var allows) || allows;
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
            Timing.Add("update catalog", started);
            if (!done) return;

            var job = _job;
            _job = null;
            if (job.Entries != null) Made(job, scene);
            else if (job.Failure != "the world was left") Failed(job.Failure, scene);
        }

        /// <summary>How long reading the catalog took and what it holds, kind by kind, for the log.</summary>
        [Diagnostic]
        private static void TellRead(List<Entry> catalog, CatalogJob job)
        {
            var kinds = string.Join(", ", catalog.GroupBy(e => e.Kind).OrderBy(g => g.Key).Select(g => $"{Numbers.Count(g.Count())} {Kinds.Label(g.Key).ToLowerInvariant()}"));
            Log.Report($"Scry read {Numbers.Count(catalog.Count)} prefabs and status effects in {Numbers.Amount(job.WorkMs, 0)} ms over {Numbers.Count(job.Frames)} frames ({Numbers.Fixed(job.ElapsedMs / 1000.0, 1)} s in all): {kinds}.");
        }

        private static void Made(CatalogJob job, ZNetScene scene)
        {
            var catalog = job.Entries;
            _favourites = _favourites ?? new Favourites(Path.Combine(Settings.DataFolder, "favourites.txt"));
            if (_favourites.Problem != null) Faults.Tell(Feature.Favourites, "reading the favourites", _favourites.Problem);

            Explorer = new Explorer(catalog, _favourites) { RecentLimit = Settings.RecentCount };
            WorldCatalog.Set(Explorer.Entries);
            // A creature the page shown would leave out stands on no place's copy and in no raid's crowd:
            // one the character has not met, and under the strict rules one never seen together with the page.
            Stage.CreatureLeftOff = CreatureLeftOff;
            RaidCrowd.LeftOff = CreatureLeftOff;
            if (_carried.HasValue)
            {
                var carried = _carried.Value;
                Explorer.Text = carried.Text;
                Explorer.KindFilter = carried.Kind;
                Explorer.Origin = carried.Origin;
                Explorer.FavouritesOnly = carried.Favourites;
            }
            var (search, page) = Pending.Take();
            if (search != null) Explorer.Text = search;
            if (page != null) Explorer.Jump(page);
            _carried = null;

            _scene = scene;
            _failedIn = null;
            TellRead(catalog, job);

            // Where things are found: what was kept of the world's places from before, shown once
            // found, and what may have changed read again; with nothing kept, read by itself for
            // those who want it, the same background reading the panel's button starts.
            Guard.Run(Feature.Locations, "finding what the locations hold", () => Locations.Begin(Settings.ReadLocationsAutomatically));
            Guard.Run(Feature.FloorsKept, "finding the floors kept", FloorRecords.Begin);
        }

        private static void Failed(string why, ZNetScene scene)
        {
            _failedIn = scene;
            Faults.Tell(Feature.Catalog, "reading the game's prefabs", why);
            if (!IsOpen) return;
            Hide();
            Chat.instance.OrNull()?.AddString("Scry could not read the game's prefabs; the log has the details.");
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
            Guard.Run(Feature.Leaving, "closing on leaving a world", Hide);
            Guard.Run(Feature.Leaving, "clearing the world's previews", Previews.ClearWorld);

            // Everything else kept of the world, each class having registered how it forgets.
            WorldCaches.ForgetAll((name, ex) => Faults.Tell(Feature.Leaving, "forgetting " + name, ex));

            // The search and filters carry over to the next world; the entries cannot.
            Guard.Run(Feature.Leaving, "letting go of the selection", () => Explorer?.Select(null));
            if (Explorer != null) _carried = (Explorer.Text, Explorer.KindFilter, Explorer.Origin, Explorer.FavouritesOnly);
            Explorer = null;
            _known = null;
            _knownInstead = null;
        }
    }
}
