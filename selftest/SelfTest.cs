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
    /// Runs only with the SelfTest setting on: it starts with <c>/scry selftest</c>, or by itself a
    /// few seconds after the catalog is read when the file <c>BepInEx/Scry-selftest.run</c> exists;
    /// <c>/scry selftest stop</c> stops it.
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
        public static string MarkerFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.run");

        /// <summary>Scry's share of a frame above which a frame counts as heavy in the notes, in milliseconds.</summary>
        private const double Budget = 16;

        private static ScenarioRunner _runner;
        private static Before _before;

        /// <summary>Scry's own work in every frame of the run (<see cref="Timing.Measuring"/>).</summary>
        private static readonly FrameStats Frames = new FrameStats();

        private static Explorer _autoFor;
        private static float _readyAt = -1f;

        public static bool Running => _runner != null;

        /// <summary>The self-test as Scry reaches it (<see cref="SelfTestHost"/>).</summary>
        internal sealed class Runner : SelfTestHost.IRunner
        {
            public bool Running => SelfTest.Running;
            public void Tick() => SelfTest.Tick();
            public string Start(string why) => SelfTest.Start(why);
            public string Stop() => SelfTest.Stop();
            public string Progress => SelfTest.Progress;
            public float Fraction => SelfTest.Fraction;
            public string LastHeadline => SelfTest.LastHeadline;
            public System.Collections.Generic.IReadOnlyList<string> LastSummary => SelfTest.LastSummary;
            public string LastAdvice => SelfTest.LastAdvice;
            public bool LastFailed => SelfTest.LastFailed;
            public string LastText => SelfTest.LastText;
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
            if (Settings.SelfTestAllowed && File.Exists(MarkerFile)) Start("the marker file is present");
        }

        public static string Start(string why)
        {
            if (_runner != null) return "the self-test is already running.";
            if (Player.m_localPlayer == null) return "the self-test runs in a world.";
            if (Session.Explorer == null) return "the catalog of this world is not read yet; try again in a few seconds.";

            Write("");
            Write($"==== Scry {About.Version} self-test, {DateTime.Now:yyyy-MM-dd HH:mm:ss}, started because {why} ====");
            Write($"World {ZNet.instance.OrNull()?.GetWorldName()}, {Numbers.Count(Session.Explorer.Catalog.Count)} entries, {Numbers.Count(BepInEx.Bootstrap.Chainloader.PluginInfos.Count)} mods loaded, panel {(Session.IsOpen ? "open" : "closed")}" +
                  $"{(ScryPanel.Compact ? " in its compact view" : "")}, locations {Locations.Now}, LogPreviews {(Settings.LogPreviews ? "on" : "off")}.");
            Write("Each part uses Scry as a player would and checks what it did. START and PASS, FAIL or SKIP frame a part; within it, PASS is a check that held,");
            Write("FAIL one that did not (with what was found), and note is something worth knowing. The summary at the end lists every failed check again.");

            _before = Before.Take();
            Frames.Clear();
            Timing.Measuring = Frames;
            _runner = new ScenarioRunner(Scenarios(), Write, Finish);
            ScryPanel.ShowTestResult();
            return SelfTestWords.Started(_runner.Total);
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
            var reports = runner?.Reports ?? new List<ScenarioReport>();
            LastHeadline = stopped == null ? SelfTestWords.Finished(reports, runner?.Seconds) : $"Self-test stopped after {Numbers.Count(reports.Count)} of {Numbers.Count(runner?.Total ?? 0)} parts, as {stopped}.";
            LastSummary = SelfTestWords.Summary(reports);
            LastAdvice = SelfTestWords.Advice(reports, "BepInEx/Scry-selftest.log");
            LastFailed = reports.Any(r => r.Result == Result.Fail);

            var readHere = _before != null && _before.LocationsWere != Locations.State.Read && Locations.Now == Locations.State.Read;
            var failure = Steps.Run(PutBack, null);
            Write(failure != null
                ? $"FAIL putting things back threw {failure.GetType().Name}: {failure.Message}"
                : "Everything the test changed has been put back" + (readHere ? ", except that the locations stay read, as Read all locations leaves them." : "."));
            Timing.Measuring = null;
            _before = null;

            Write("==== Summary ====");
            Write(LastHeadline);
            foreach (var line in LastSummary) Write(line);
            Write(LastAdvice);
            ScryPanel.ShowTestResult();
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
            public bool Open, InWorld, Repeat, OnPerson, PlanFolded, RunesFolded, ReadmeFolded;
            public Locations.State LocationsWere;

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
                LocationsWere = Locations.Now,
            };
        }

        /// <summary>Remembers the view the panel is in, once, the first time the test has it open.</summary>
        private static void KeepView()
        {
            if (_before != null && !_before.Compact.HasValue) _before.Compact = ScryPanel.Compact;
        }

        private static void PutBack()
        {
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
            ScryPanel.HideModReport();
            if (before.Compact.HasValue) ScryPanel.Compact = before.Compact.Value;
            if (Session.Explorer != null && Session.Explorer == before.Explorer) before.Explorer.Restore(before.Kept);
            if (!before.Open) Session.Hide();
            else Previews.Rebuild();
        }

        // ----- What the scenarios share -----

        /// <summary>
        /// A scenario that also fails when any part of Scry failed while it ran or a feature went
        /// off for a game change, and notes how long Scry's own work took in its frames. A part of
        /// a single prefab left out fails it too, unless it reads what mods add as well
        /// (<paramref name="bearsSkips"/>), where Scry is made to bear that and it is only noted.
        /// Its body may yield another's steps, which run in place.
        /// </summary>
        private static Scenario S(string name, Func<Probe, IEnumerator> body, double timeout = 20, Action cleanup = null, bool bearsSkips = false) =>
            new Scenario(name, p => Watched(p, body, bearsSkips), cleanup) { Timeout = timeout };

        private static IEnumerator Watched(Probe p, Func<Probe, IEnumerator> body, bool bearsSkips)
        {
            var from = Frames.Frames;
            var faults = Faults.Count;
            var skipped = Faults.Skipped;
            var off = Faults.ChangedFeatures.Count;
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
        }

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
