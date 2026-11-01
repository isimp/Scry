using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test as the strip and the card show it: the run going on or the last one
    /// (<see cref="SelfTestLive"/>), its line, how long is left by the parts' last times, a pause
    /// between parts that sets the frame figures aside while it holds; and between runs, what it
    /// recommends running next (<see cref="SelfTestRecommended"/>), worked out every few seconds,
    /// with a line in chat once a session for each kind of thing that comes near and lets untried
    /// rows be tried, and once the machine runs near its best again for the checks not judged.
    /// </summary>
    internal static partial class SelfTest
    {

        /// <summary>The run going on, else the last one this session; none before any.</summary>
        public static ScenarioRunner LiveRunner => _runner ?? _lastRunner;

        /// <summary>Whether the run is held between parts.</summary>
        public static bool Paused => _runner?.Paused == true;

        /// <summary>Whether a pause is asked for, held once the part running is done.</summary>
        public static bool PauseAsked => _runner?.PauseAsked == true;

        /// <summary>Holds the run after the part running, or lets it go on.</summary>
        public static void TogglePause()
        {
            if (_runner == null) return;
            if (_runner.PauseAsked) _runner.GoOn();
            else _runner.Pause();
        }

        /// <summary>The run's full sentence of progress, with what to do meanwhile, for the strip's tip.</summary>
        public static string ProgressTip => _runner == null ? null : SelfTestWords.Progress(_runner.Done, _runner.Total, _runner.Current, _runner.FailedSoFar);

        private static double _leftAt = -10;
        private static double _left;

        /// <summary>About how long the parts not done take by their last times, worked out at most once a second.</summary>
        private static double SecondsLeft()
        {
            if (_runner == null || _runTimes == null) return 0;
            if (Time.unscaledTime - _leftAt < 1f) return _left;
            _leftAt = Time.unscaledTime;
            var done = new HashSet<string>(_runner.Reports.Select(r => r.Name), StringComparer.Ordinal);
            _left = _runTimes.Estimate(_runner.Parts.Where(s => !done.Contains(s.Name)).Select(s => s.Name), _sampled);
            return _left;
        }

        /// <summary>The strip's line while a run goes on (<see cref="SelfTestWords.Running"/>).</summary>
        private static string LiveLine()
        {
            var marked = _runner.Marked;
            var newest = marked.Count == 0 ? null : SelfTestLive.EntryOf(marked[marked.Count - 1].Part, marked[marked.Count - 1].Line, marked[marked.Count - 1].At);
            return SelfTestWords.Running(_runner.Done, _runner.Total, SecondsLeft(), _runner.Current, SelfTestLive.Latest(newest, Time.unscaledTime), _partSlowdown, _runner.Paused);
        }

        /// <summary>While the run is held the frame figures are set aside, the player's own moving no work of Scry's to judge.</summary>
        private static void HoldFigures()
        {
            if (_runner == null) return;
            if (_runner.Paused) Timing.Measuring = null;
            else if (Timing.Measuring == null) Timing.Measuring = Frames;
        }

        // ----- Recommended, between runs -----

        private static SelfTestRecommended _recommended;
        private static float _recommendedAt = -10f;
        private static float _pacedIdleAt = -100f;
        private static double _idleSlowdown = 1.0;
        private static readonly HashSet<string> NearSaid = new HashSet<string>(StringComparer.Ordinal);
        private static bool _quietSaid;

        /// <summary>The keys the rows untried last name, looked for near the character one a second.</summary>
        private static List<string> _wantedNear = new List<string>();

        /// <summary>What the self-test recommends running next; none during a run or outside a world.</summary>
        public static SelfTestRecommended Recommended => _runner == null ? _recommended : null;

        /// <summary>How many times slower than its best the machine ran when last sampled between runs.</summary>
        public static double IdleSlowdown => _idleSlowdown;

        /// <summary>Works out what to recommend every five seconds between runs, and says in chat what newly lets something be run.</summary>
        private static void Recommend()
        {
            LookNear(_wantedNear);
            if (Time.unscaledTime - _recommendedAt < 5f) return;
            _recommendedAt = Time.unscaledTime;
            var results = SelfTestResults.Load(Kept(ResultsFile));
            // The reference work, a few milliseconds, only where checks wait for a quiet machine, twice a minute.
            if (results.BusyChecks > 0 && Time.unscaledTime - _pacedIdleAt >= 30f)
            {
                _pacedIdleAt = Time.unscaledTime;
                _idleSlowdown = IdlePace();
            }
            var untried = results.Untried();
            _wantedNear = untried.Where(u => u.Need != null).Select(u => u.Need).Distinct().ToList();
            _recommended = SelfTestRecommended.Of(SelfTestPlan.Load(Kept(PlanFile)), PartNames(), results, NearNow, _idleSlowdown);
            SayWhatIsNew(_recommended);
        }

        private static void SayWhatIsNew(SelfTestRecommended recommended)
        {
            foreach (var need in recommended.Near.Select(n => n.Need).Distinct())
            {
                if (!NearSaid.Add(need)) continue;
                var rows = recommended.Near.Count(n => n.Need == need);
                Chat.instance.OrNull()?.AddString(SelfTestWords.NearChat(NeedWords.TryGetValue(need, out var thing) ? thing : need, rows));
            }
            if (!_quietSaid && recommended.BusyChecks > 0 && recommended.BusyReady)
            {
                _quietSaid = true;
                Chat.instance.OrNull()?.AddString(SelfTestWords.QuietChat(recommended.BusyChecks));
            }
        }

        /// <summary>Starts a recommended run from the card, as the words after /scry would.</summary>
        public static string StartRecommended(RunScope scope) =>
            Start("it was asked for from the recommended runs", new SelfTestRequest { Ask = SelfTestAsk.Start, Scope = scope });

        /// <summary>Starts a run of the parts named, as the card's Run again and Try ask.</summary>
        public static string StartParts(IEnumerable<string> parts) =>
            Start("it was asked for from the card", new SelfTestRequest { Ask = SelfTestAsk.Start, Scope = RunScope.Parts, Words = parts.ToArray() });

        /// <summary>Asks the run to hold once the part running is done, as a count on the strip asks.</summary>
        public static void PauseSoon()
        {
            if (_runner != null && !_runner.PauseAsked) _runner.Pause();
        }

        /// <summary>Whether something a key names was found near the character between runs (<see cref="LookNear"/>).</summary>
        public static bool IsNear(string need) => need != null && NearNow.Contains(need);

        /// <summary>How long a part took last; none where it never ran.</summary>
        public static double? LastTimeOf(string part) => _runTimes?.Of(part, _sampled);

        /// <summary>How long the part running has taken so far.</summary>
        public static double PartSeconds => _runner == null ? 0 : Time.unscaledTime - _runner.StartedAt;

        /// <summary>Which run the card tells of, how long it took and the hour and minute it ended; while one runs, which run it is.</summary>
        public static string RunTold =>
            _runner != null ? _runScope.ToString().ToLowerInvariant() + " run" :
            _lastRunner == null ? "" : $"{_runScope.ToString().ToLowerInvariant()} run · {SelfTestWords.Duration(_lastRunner.Seconds)} · {_runEnded:HH:mm}";
    }
}
