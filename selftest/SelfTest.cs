using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The in-game self-test: scenarios that use Scry as a player would, through the panel's own
    /// paths (searching, selecting, playing, loading locations, laying out dungeons), and check
    /// what it did, written to <c>BepInEx/Scry-selftest.log</c>. Every scenario also fails when any
    /// part of Scry failed while it ran, and tells how long Scry's own work took in its frames.
    ///
    /// Safe on a real character: Scry spawns nothing, and the test only opens the panel, looks at
    /// entries and plays previews, each taken away again. The panel's search, filters, selection,
    /// recent entries and history are put back as they were, it closes again if it was closed,
    /// and a copy the player had standing in the world is left alone.
    ///
    /// Runs wherever this plugin is installed: it starts with <c>/scry selftest</c>, a quick run
    /// whose long parts go through a few of their places, <c>/scry selftest deep</c> through all
    /// of them, <c>/scry selftest full</c> also reading every place again, <c>/scry selftest
    /// failed</c> the parts that failed last, or any other words the parts whose names hold them
    /// (<see cref="SelfTestCommand"/>); or by itself a few seconds after the catalog is read when
    /// the file <c>BepInEx/Scry-selftest.run</c> exists, by the words it holds.
    /// <c>/scry selftest list</c> writes every part's name, <c>/scry selftest stop</c> stops it.
    /// Each part's last time and the parts that failed last are kept beside the log.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>Leaving a world lets go of which world's catalog was seen (<see cref="WorldCaches"/>); a run going on then is stopped by <see cref="Tick"/>.</summary>
        static SelfTest() => WorldCaches.Register(nameof(SelfTest), () =>
        {
            _autoFor = null;
            _readyAt = -1f;
        });

        public static string ResultFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.log");

        /// <summary>Where the runs before the log's last go once it grows large (<see cref="SelfTestLog"/>).</summary>
        private static string OlderFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.old.log");
        public static string MarkerFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.run");

        /// <summary>Each part's last time, and the parts that failed in the last run (<see cref="SelfTestTimes"/>).</summary>
        public static string TimesFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.times");
        public static string FailedFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.failed");

        /// <summary>Whether this run's long parts go through a few of their places: a quick run (<see cref="SelfTestRequest.Sampled"/>).</summary>
        private static bool _sampled;

        /// <summary>The places a long part goes through: the first few in a quick run, which its list names first as telling most, else all.</summary>
        private static string[] Few(string[] all, int few) => _sampled ? SelfTestSample.First(all, few) : all;

        /// <summary>Some of many, spread over them, in a quick run; all of them otherwise.</summary>
        private static List<T> Spread<T>(List<T> all, int few) => _sampled ? Sample(all, few) : all;

        /// <summary>The few of a list a quick run takes, whatever the run: for a part about how many it takes, not about every one, whose time is set by them.</summary>
        private static List<T> Sample<T>(List<T> all, int few) => SelfTestSample.Spread(all.ToArray(), few).ToList();

        /// <summary>Scry's share of a frame above which a frame counts as heavy in the notes, in milliseconds.</summary>
        private const double Budget = 16;

        /// <summary>A frame of Scry's own work this long has all its parts told.</summary>
        private const double SlowFrame = 50;

        private static ScenarioRunner _runner;
        private static Before _before;

        /// <summary>
        /// Scry's own work in every frame of the run (<see cref="Timing.Measuring"/>), room made
        /// for a run of seven minutes at 144 frames a second (3 MB), so noting them allocates
        /// nothing in the frames measured.
        /// </summary>
        private static readonly FrameStats Frames = new FrameStats(1 << 16);

        private static Explorer _autoFor;
        private static float _readyAt = -1f;

        public static bool Running => _runner != null;

        /// <summary>The self-test as Scry reaches it (<see cref="SelfTestHost"/>).</summary>
        internal sealed class Runner : SelfTestHost.IRunner
        {
            public void Tick() => SelfTest.Tick();

            public string Answer(string typed)
            {
                var asked = SelfTestCommand.Of(typed);
                switch (asked.Ask)
                {
                    case SelfTestAsk.Start: return SelfTest.Start("it was asked for with /scry " + string.Join(" ", typed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)), asked);
                    case SelfTestAsk.Stop: return SelfTest.Stop();
                    case SelfTestAsk.List: return SelfTest.List();
                    case SelfTestAsk.Help: return SelfTest.Help();
                    default: return null;
                }
            }

            public bool StripShown => SelfTestPanel.StripShown;
            public void DrawStrip(UnityEngine.Rect rect) => SelfTestPanel.Strip(rect);
            public bool CardShown => SelfTestPanel.CardShown;
            public void DrawCard(UnityEngine.Rect rect) => SelfTestPanel.Card(rect);
        }

        /// <summary>Called every frame: runs the scenarios, and starts them by marker file once per world.</summary>
        public static void Tick()
        {
            if (_runner != null)
            {
                if (Player.m_localPlayer == null || Session.Explorer == null || Session.Explorer != _before?.Explorer)
                {
                    Abort("the world was left");
                    return;
                }
                if (!_runner.Tick(Time.unscaledTime)) _runner = null;
                return;
            }

            // Once a world's catalog is read, and a few seconds more for the world to settle.
            var explorer = Session.Explorer;
            if (explorer == null || Player.m_localPlayer == null)
            {
                _readyAt = -1f;
                return;
            }
            if (explorer == _autoFor) return;
            if (_readyAt < 0f)
            {
                _readyAt = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - _readyAt < 5f) return;
            _autoFor = explorer;
            _readyAt = -1f;
            if (File.Exists(MarkerFile)) Start("the marker file is present", SelfTestCommand.OfMarker(File.ReadAllText(MarkerFile)));
        }

        /// <summary>Starts a run of the parts asked for (<see cref="Scenario.ForRun"/>), and says so, with about how long it takes by the parts' last times.</summary>
        public static string Start(string why, SelfTestRequest asked)
        {
            if (_runner != null) return "the self-test is already running.";
            if (Player.m_localPlayer == null) return "the self-test runs in a world.";
            if (Session.Explorer == null) return "the catalog of this world is not read yet; try again in a few seconds.";
            var parts = Scenario.ForRun(Scenarios(), asked, SelfTestTimes.FailedOf(Kept(FailedFile)));
            if (parts.Count == 0) return SelfTestWords.NothingToRun(asked);
            var times = SelfTestTimes.Load(Kept(TimesFile));

            LogAside();
            Write("");
            Write($"==== Scry {About.Version} {asked.Scope.ToString().ToLowerInvariant()} self-test, {DateTime.Now:yyyy-MM-dd HH:mm:ss}, started because {why} ====");
            Write($"World {ZNet.instance.OrNull()?.GetWorldName()}, {Numbers.Count(Session.Explorer.Catalog.Count)} entries, {Numbers.Count(BepInEx.Bootstrap.Chainloader.PluginInfos.Count)} mods loaded, panel {(Session.IsOpen ? "open" : "closed")}" +
                  $"{(ScryPanel.Compact ? " in its compact view" : "")}, locations {Locations.Now}, LogPreviews {(Settings.LogPreviews ? "on" : "off")}, Compendium {CompendiumKeeper.Book?.Rule ?? CompendiumRule.Off} (shown off while the test runs).");
            Write("Each part uses Scry as a player would and checks what it did. START and PASS, FAIL or SKIP frame a part; within it, PASS is a check that held,");
            Write("FAIL one that did not (with what was found), and note is something worth knowing. The summary at the end lists every failed check again.");

            // The panel's saved place is read on its first draw; read before anything is kept, so a
            // test started with the panel never opened keeps the player's place and sets up over it.
            ScryPanel.PlacedForTest();
            _before = Before.Take();
            // The list is shown while the test runs, as parts read what it draws: the search's
            // help, the mod report and the floor's marks stand in it.
            ScryPanel.ListsHidden = (false, false);
            // The rooms below stand dimmed while the test runs, as its parts look at them so.
            ScryPanel.PlanBelowShown = true;
            // The grown plan names its rooms while the test runs, as its parts read the names.
            ScryPanel.PlanNamesShown = true;
            // Only where the test points the mouse lights a room, wherever the player's own stands.
            ScryPanel.MouseAwayForTest(true);
            _allKnown = null;
            AllKnown();
            // Each place rolls the same while the test runs, so its pictures compare run to run.
            StageDice.Fix(true);
            Frames.Clear();
            Timing.Measuring = Frames;
            _sampled = asked.Sampled;
            if (asked.Scope == RunScope.Quick || asked.Scope == RunScope.Deep || asked.Scope == RunScope.Full) PicturesAside();
            ReadmeFor(asked.Scope);
            _runner = new ScenarioRunner(parts, Write, Finish, PartBegins);
            SelfTestPanel.ShowResult();
            return SelfTestWords.Started(_runner.Total, asked.Scope, times.Estimate(parts.Select(s => s.Name), asked.Sampled));
        }

        /// <summary>
        /// Keeps the log to its last runs once it grew past <see cref="SelfTestLog.KeptBytes"/>, the
        /// runs before them moved to the older log in place of what it held.
        /// </summary>
        private static void LogAside() => Steps.Run(() =>
        {
            var log = new FileInfo(ResultFile);
            if (!log.Exists || log.Length < SelfTestLog.KeptBytes) return;
            var (older, kept) = SelfTestLog.Split(File.ReadAllText(ResultFile));
            if (older.Length == 0) return;
            File.WriteAllText(OlderFile, older);
            File.WriteAllText(ResultFile, kept);
        }, null);

        /// <summary>A few words a part's name holds, as examples of a run by words in the help.</summary>
        private static readonly string[] HelpExamples = { "rune", "plan box", "compendium box", "map" };

        /// <summary>What can be run: each run's parts and about how long they take by their last times, the parts that failed last, and runs by words.</summary>
        public static string Help()
        {
            var times = SelfTestTimes.Load(Kept(TimesFile));
            var failed = SelfTestTimes.FailedOf(Kept(FailedFile));
            List<Scenario> Parts(string words) => Scenario.ForRun(Scenarios(), SelfTestCommand.Of(("selftest " + words).Trim()), failed);
            RunSize Size(string words)
            {
                var asked = SelfTestCommand.Of(("selftest " + words).Trim());
                var parts = Parts(words);
                return new RunSize(parts.Count, times.Estimate(parts.Select(s => s.Name), asked.Sampled));
            }
            var examples = HelpExamples.Select(w => (w, Size(w))).Where(e => e.Item2.Parts > 0).ToList();
            return SelfTestWords.Help(Size(""), Size("deep"), Size("full"), Size("failed"), Parts("failed").Select(s => s.Name).ToList(), examples, "BepInEx/Scry-selftest.log");
        }

        /// <summary>Writes every part's name to the log, in the order a run takes them, with how long each took last.</summary>
        public static string List()
        {
            var times = SelfTestTimes.Load(Kept(TimesFile));
            var all = Scenarios().ToList();
            Write("");
            Write($"==== Scry {About.Version} self-test parts, {DateTime.Now:yyyy-MM-dd HH:mm:ss}: /scry selftest and any words of a part's name runs the parts holding them ====");
            for (var i = 0; i < all.Count; i++)
            {
                var took = times.Of(all[i].Name, quick: false);
                Write($"{Numbers.Count(i + 1)}. {all[i].Name}{(all[i].Full ? " (full runs only)" : "")}: {(took.HasValue ? SelfTestWords.Duration(took.Value) : "not timed yet")}");
            }
            return SelfTestWords.Listed(all.Count, "BepInEx/Scry-selftest.log");
        }

        /// <summary>A file the self-test keeps beside the log; none where it is not there or cannot be read.</summary>
        private static string Kept(string file) => Steps.Run(() => File.Exists(file) ? File.ReadAllText(file) : null, out var text, null) == null ? text : null;

        /// <summary>
        /// Keeps each part's time this run in place of its last, and the parts that failed: those
        /// failed before and not run this time, and those that failed now.
        /// </summary>
        private static void KeepTimes(IReadOnlyList<ScenarioReport> reports)
        {
            var times = SelfTestTimes.Load(Kept(TimesFile));
            foreach (var report in reports) times.Took(report.Name, report.Seconds, _sampled);
            var ran = new HashSet<string>(reports.Select(r => r.Name), StringComparer.Ordinal);
            var failed = SelfTestTimes.FailedOf(Kept(FailedFile)).Where(name => !ran.Contains(name)).ToList();
            failed.AddRange(reports.Where(r => r.Result == Result.Fail).Select(r => r.Name));
            Steps.Run(() => File.WriteAllText(TimesFile, times.Save()), null);
            Steps.Run(() => File.WriteAllText(FailedFile, SelfTestTimes.FailedText(failed)), null);
        }

        public static string Stop()
        {
            if (_runner == null) return "the self-test is not running.";
            Abort("you stopped it");
            return "self-test stopped; what it changed has been put back.";
        }

        private static void Abort(string why)
        {
            Write($"ABORTED: {why}");
            var runner = _runner;
            _runner = null;
            Finish(runner, why);
        }

        private static void Finish() => Finish(_runner, null);

        /// <summary>The self-test's progress while it runs, for the panel's strip; null when none runs.</summary>
        public static string Progress => _runner == null ? null : SelfTestWords.Progress(_runner.Done, _runner.Total, _runner.Current, _runner.FailedSoFar);

        /// <summary>How much of the run is done, from 0 to 1.</summary>
        public static float Fraction => _runner == null || _runner.Total == 0 ? 0f : (float)_runner.Done / _runner.Total;

        /// <summary>The last run's headline, the failed and skipped parts with their checks, and what to do; null before any run this session.</summary>
        public static string LastHeadline { get; private set; }
        public static List<string> LastSummary { get; private set; } = new List<string>();
        public static string LastAdvice { get; private set; }
        public static bool LastFailed { get; private set; }

        /// <summary>The whole of the last run's outcome as text, for copying.</summary>
        public static string LastText => LastHeadline == null ? "" : LastHeadline + Environment.NewLine + string.Join(Environment.NewLine, LastSummary) + Environment.NewLine + LastAdvice;

        private static void Finish(ScenarioRunner runner, string stopped)
        {
            SelfTestPanel.HiddenForPicture = false;
            var reports = runner?.Reports ?? new List<ScenarioReport>();
            LastHeadline = stopped == null ? SelfTestWords.Finished(reports, runner?.Seconds) : $"Self-test stopped after {Numbers.Count(reports.Count)} of {Numbers.Count(runner?.Total ?? 0)} parts, as {stopped}.";
            LastSummary = SelfTestWords.Summary(reports);
            LastAdvice = SelfTestWords.Advice(reports, "BepInEx/Scry-selftest.log");
            LastFailed = reports.Any(r => r.Result == Result.Fail);
            KeepTimes(reports);

            var readHere = _before != null && _before.LocationsWere != Locations.State.Read && Locations.Now == Locations.State.Read;
            StageDice.Fix(false);
            var failure = Steps.Run(PutBack, null);
            Write(failure != null
                ? $"FAIL putting things back threw {failure.GetType().Name}: {failure.Message}"
                : "Everything the test changed has been put back" + (readHere ? ", except that the locations stay read, as Find where things are leaves them." : "."));
            Timing.Measuring = null;
            _before = null;

            Write("==== Summary ====");
            Write(LastHeadline);
            foreach (var line in LastSummary) Write(line);
            Write(LastAdvice);
            SelfTestPanel.ShowResult();
            Chat.instance.OrNull()?.AddString($"Scry: {LastHeadline} Open Scry for the details; BepInEx/Scry-selftest.log has everything.");
        }

        private static void Write(string line)
        {
            Log.Report($"[selftest] {line}");
            // A line the file will not take is still in the log above.
            Steps.Run(() => File.AppendAllText(ResultFile, line + Environment.NewLine), null);
        }

        // ----- What the test changes, and puts back -----

        private sealed class Before
        {
            public Explorer Explorer;
            public Explorer.Kept Kept;
            public bool Open, InWorld, Repeat, OnPerson, PlanFolded, RunesFolded, ReadmeFolded, TooltipFolded;
            public Locations.State LocationsWere;

            /// <summary>The stage's height and whether the list was folded in each view, which the pictures' parts change.</summary>
            public float StageScale;
            public (bool Full, bool Compact) ListsHidden;

            /// <summary>Whether the rooms below stood dimmed, as the plan box's switch had it.</summary>
            public bool BelowShown;
            public bool NamesShown;

            /// <summary>The view the panel was in when the test first opened it, for the full view the stage is drawn in.</summary>
            public bool? Compact;

            public static Before Take() => new Before
            {
                Explorer = Session.Explorer,
                Kept = Session.Explorer.Keep(),
                Open = Session.IsOpen,
                InWorld = Previews.InWorld,
                Repeat = Previews.Repeat,
                OnPerson = Looks.OnPerson,
                PlanFolded = ScryPanel.PlanFolded,
                RunesFolded = ScryPanel.RunesFolded,
                ReadmeFolded = ScryPanel.ReadmeFolded,
                TooltipFolded = ScryPanel.TooltipFolded,
                LocationsWere = Locations.Now,
                StageScale = ScryPanel.StageScale,
                ListsHidden = ScryPanel.ListsHidden,
                BelowShown = ScryPanel.PlanBelowShown,
                NamesShown = ScryPanel.PlanNamesShown,
            };
        }

        /// <summary>Everything known, shown while the test runs; made again only for a new catalog.</summary>
        private static CompendiumView _allKnown;

        /// <summary>As each part begins: the book as every part expects it, and the compendium's box let go, whichever part held it open.</summary>
        private static void PartBegins()
        {
            AllKnown();
            ScryPanel.HoldCompendiumBox(false);
        }

        /// <summary>
        /// Shows everything as known while the test runs, whatever the Compendium setting, so every
        /// part sees the whole catalog; the parts that check the compendium show a made-up
        /// character's knowledge or the played one, and the runner comes back to this before each
        /// part, whatever the one before left. Putting back shows the played one again.
        /// </summary>
        private static void AllKnown()
        {
            var explorer = Session.Explorer;
            if (_allKnown == null || !ReferenceEquals(_allKnown.Catalog, explorer.Entries))
            {
                _allKnown = new CompendiumView(explorer.Entries);
                _allKnown.Tell(null, CompendiumRule.Off);
            }
            Session.ShowKnownInstead(_allKnown);
            if (!ReferenceEquals(explorer.Compendium, _allKnown)) explorer.Know(_allKnown);
        }

        /// <summary>Remembers the view the panel is in, once, the first time the test has it open.</summary>
        private static void KeepView()
        {
            if (_before != null && !_before.Compact.HasValue) _before.Compact = ScryPanel.Compact;
        }

        private static void PutBack()
        {
            // The player's mouse first, so it works again whatever else fails to be put back.
            ScryPanel.MouseAwayForTest(false);
            var before = _before;
            if (before == null) return;
            Previews.StopSound();
            Previews.StopStatus(false);
            if (Previews.InWorld != before.InWorld) Previews.ToggleWorld();
            Previews.Repeat = before.Repeat;
            Looks.OnPerson = before.OnPerson;
            ScryPanel.PlanFolded = before.PlanFolded;
            ScryPanel.RunesFolded = before.RunesFolded;
            ScryPanel.ReadmeFolded = before.ReadmeFolded;
            ScryPanel.TooltipFolded = before.TooltipFolded;
            ScryPanel.HideModReport();
            if (before.Compact.HasValue) ScryPanel.Compact = before.Compact.Value;
            // A part stopped between making the stage big and putting it back leaves both to here.
            StageAsItWas();
            ScryPanel.StageScale = before.StageScale;
            ScryPanel.ListsHidden = before.ListsHidden;
            ScryPanel.PlanBelowShown = before.BelowShown;
            ScryPanel.PlanNamesShown = before.NamesShown;
            ScryPanel.RememberPlaceForTest();
            Session.ShowKnownInstead(null);
            if (Session.Explorer != null) Session.Explorer.Peeking = false;
            if (Session.Explorer != null && Session.Explorer == before.Explorer) before.Explorer.Restore(before.Kept);
            if (!before.Open) Session.Hide();
            else Previews.Rebuild();
        }

        // ----- What the scenarios share -----

        /// <summary>
        /// A scenario that also fails when any part of Scry failed while it ran or a feature went
        /// off for a game change, and notes how long Scry's own work took in its frames, and what
        /// set off each time what the played character knows was counted whole. A part of
        /// a single prefab left out fails it too, unless it reads what mods add as well
        /// (<paramref name="bearsSkips"/>), where Scry is made to bear that and it is only noted.
        /// Its body may yield another's steps, which run in place. Only a full run takes one
        /// marked <paramref name="full"/>.
        /// </summary>
        private static Scenario S(string name, Func<Probe, IEnumerator> body, double timeout = 20, Action cleanup = null, bool bearsSkips = false, bool full = false) =>
            new Scenario(name, p => Watched(p, body, bearsSkips), cleanup) { Timeout = timeout, Full = full };

        private static IEnumerator Watched(Probe p, Func<Probe, IEnumerator> body, bool bearsSkips)
        {
            var from = Frames.Frames;
            var faults = Faults.Count;
            var skipped = Faults.Skipped;
            var off = Faults.ChangedFeatures.Count;
            var whole = WholeCounts();
            var steps = new Stack<IEnumerator>();
            steps.Push(body(p));
            while (steps.Count > 0)
            {
                var top = steps.Peek();
                if (!top.MoveNext())
                {
                    steps.Pop();
                    continue;
                }
                if (top.Current is IEnumerator inner)
                {
                    steps.Push(inner);
                    continue;
                }
                yield return top.Current;
            }

            var failed = Faults.Count - faults;
            p.Check(failed == 0, "no part of Scry failed meanwhile", $"{Numbers.Count(failed)} failures, the latest in {Faults.Latest}");
            var turnedOff = Faults.ChangedFeatures.Count - off;
            p.Check(turnedOff == 0, "no feature went off for a game change meanwhile", string.Join("; ", Faults.ChangedFeatures.Skip(off)));
            var left = Faults.Skipped - skipped;
            if (left > 0 && bearsSkips) p.Note($"{Numbers.Count(left)} parts of single prefabs were left out, the latest {Faults.LatestSkipped}");
            else p.Check(left == 0, "no part of a prefab was left out meanwhile", $"{Numbers.Count(left)} left out, the latest {Faults.LatestSkipped}");
            var mine = Frames.Since(from);
            if (mine.Frames > 0) p.Note(mine.Line(Budget) + Slowest(mine, 1));
            // Where a part's slowest frame was slow, every part of it: the slowest alone may hold others.
            if (mine.Max >= SlowFrame && mine.PartsOfSlowest(1)[0] is string parts) p.Note("its slowest frame: " + parts);
            var wholeNow = WholeCounts();
            var causes = Causes.Where(c => wholeNow[(int)c] > whole[(int)c]).Select(c => $"{c} {Numbers.Count(wholeNow[(int)c] - whole[(int)c])}").ToList();
            if (causes.Count > 0) p.Note("the played character's compendium counted whole, by what set it off: " + string.Join(", ", causes));
        }

        private static readonly WholeCause[] Causes = (WholeCause[])Enum.GetValues(typeof(WholeCause));

        /// <summary>How many times what the played character knows was counted whole so far, by cause.</summary>
        private static int[] WholeCounts() => Causes.Select(Session.KnownWholeCountsOf).ToArray();

        private static string Slowest(FrameStats stats, int count)
        {
            var slowest = stats.Slowest(count).Where(s => s.Ms >= 1).ToList();
            if (slowest.Count == 0) return "";
            return "; slowest " + string.Join(", ", slowest.Select(s => $"{Numbers.Amount(s.Ms, 1)} ms ({s.Part} {Numbers.Amount(s.PartMs, 1)}{(s.Cleanups > 0 ? ", a memory cleanup in it" : "")})"));
        }

        /// <summary>Waits a frame at a time until something holds, for up to so many seconds.</summary>
        private static IEnumerator Until(Func<bool> ready, double seconds)
        {
            var until = Time.unscaledTime + seconds;
            while (!ready() && Time.unscaledTime < until) yield return null;
        }

        private static Explorer X => Session.Explorer;

        /// <summary>An entry of a kind: the first of the names found, else any of that kind with something to show.</summary>
        private static Entry Pick(Kind kind, params string[] names)
        {
            foreach (var name in names)
            {
                var named = X.Catalog.FirstOrDefault(e => e.Kind == kind && e.Name == name);
                if (named != null) return named;
            }
            return X.Catalog.FirstOrDefault(e => e.Kind == kind && !e.Empty);
        }

        /// <summary>Selects an entry as a link to it does, clearing any search that hides it, with the panel open, as only the open panel shows it.</summary>
        private static void Select(Entry entry)
        {
            if (!Session.IsOpen) Session.Show(null);
            X.Jump(entry.Key);
        }

        /// <summary>
        /// The stage's copy of an entry, once made; null until then. Two entries can share a key
        /// (a mod registering its own of a game's prefab by the same name), and selecting by key
        /// may show the other, so the key is what is compared.
        /// </summary>
        private static GameObject CopyOf(Entry entry) =>
            entry != null && Stage.Showing != null && (Stage.Showing == entry || Stage.Showing.Key == entry.Key) ? Stage.Subject : null;

        private static int Renderers(GameObject copy) => copy == null ? 0 : copy.GetComponentsInChildren<Renderer>(true).Length;

        /// <summary>Whether the facts have a row or pair of that title.</summary>
        private static bool Tells(Facts facts, string title) =>
            facts.Pairs.Any(pair => pair.Key == title) || facts.Rows.Any(row => row.Title == title);

        private static string Pairs(Facts facts) => string.Join("; ", facts.Pairs.Select(pair => pair.Key + ": " + pair.Value));

        /// <summary>The value of the facts' pair of that title, or null.</summary>
        private static string Value(Facts facts, string title) => facts.Pairs.FirstOrDefault(pair => pair.Key == title).Value;

        private static PlaceSource PlaceOf(Entry entry) => entry?.Source as PlaceSource;

        private static Entry LocationNamed(params string[] names) => Pick(Kind.Location, names);

        /// <summary>Does something to each item, yielding a frame whenever this frame's share is used up, so a sweep over thousands does not stall the game.</summary>
        private static IEnumerator Budgeted<T>(IReadOnlyList<T> items, Action<T> each, double ms = 8)
        {
            var watch = Stopwatch.StartNew();
            foreach (var item in items)
            {
                each(item);
                if (watch.Elapsed.TotalMilliseconds < ms) continue;
                yield return null;
                watch.Restart();
            }
        }

        /// <summary>Up to so many of a list, spread evenly over it rather than its first few.</summary>
        private static List<T> Spread<T>(IReadOnlyList<T> all, int count)
        {
            if (all.Count <= count) return all.ToList();
            var step = (double)all.Count / count;
            return Enumerable.Range(0, count).Select(i => all[(int)(i * step)]).ToList();
        }
    }
}
