using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>Yielded by a scenario to pause for a while. Yielding null waits for the next tick.</summary>
    internal sealed class Wait
    {
        public readonly double Seconds;
        public Wait(double seconds) { Seconds = seconds; }
    }

    internal enum Result { Pass, Fail, Skip }

    /// <summary>What one scenario found.</summary>
    internal sealed class ScenarioReport
    {
        public string Name;
        public Result Result;
        public readonly List<string> Lines = new List<string>();
    }

    /// <summary>A named test that runs over time, and what to put back afterwards.</summary>
    internal sealed class Scenario
    {
        public readonly string Name;
        public readonly Func<Probe, IEnumerator> Body;
        public readonly Action Cleanup;

        /// <summary>Seconds before a scenario that has not finished counts as stuck.</summary>
        public double Timeout = 30;

        public Scenario(string name, Func<Probe, IEnumerator> body, Action cleanup = null)
        {
            Name = name;
            Body = body;
            Cleanup = cleanup;
        }
    }

    /// <summary>Thrown by <see cref="Probe.Skip"/> to stop a scenario that cannot run here.</summary>
    internal sealed class SkipScenario : Exception
    {
        public SkipScenario(string reason) : base(reason) { }
    }

    /// <summary>What a scenario records its findings with.</summary>
    internal sealed class Probe
    {
        private readonly ScenarioReport _report;
        private readonly Action<string> _write;

        internal int Checks;
        internal bool Failed;

        internal Probe(ScenarioReport report, Action<string> write)
        {
            _report = report;
            _write = write;
        }

        /// <summary>Records one expectation. Returns whether it held, so a scenario can stop early.</summary>
        public bool Check(bool ok, string what, string detail = null)
        {
            Checks++;
            if (!ok) Failed = true;
            Line(ok ? $"  PASS {what}" : $"  FAIL {what}{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}");
            return ok;
        }

        /// <summary>Writes something worth knowing that is not a pass or fail.</summary>
        public void Note(string text) => Line($"  note {text}");

        /// <summary>Stops the scenario as not runnable here, saying why.</summary>
        public void Skip(string reason) => throw new SkipScenario(reason);

        internal void Line(string text)
        {
            _report.Lines.Add(text);
            _write(text);
        }
    }

    /// <summary>
    /// Plays scenarios one after another on a clock it is given, so the same runner serves the
    /// game (real time) and the tests (a made-up clock). A failed check, an exception or a
    /// scenario that never finishes fails that scenario only; its cleanup always runs, and the
    /// run goes on. When all are done, the finishing step runs once and a summary is written.
    /// </summary>
    internal sealed class ScenarioRunner
    {
        private readonly Queue<Scenario> _pending;
        private readonly Action<string> _write;
        private readonly Action _finally;
        private readonly List<ScenarioReport> _reports = new List<ScenarioReport>();

        private Scenario _current;
        private IEnumerator _body;
        private Probe _probe;
        private ScenarioReport _report;
        private double _startedAt;
        private double _firstAt = -1;
        private double _lastEnd = -1;
        private double _waitUntil;
        private bool _finished;

        public IReadOnlyList<ScenarioReport> Reports => _reports;
        public bool Finished => _finished;

        /// <summary>How many scenarios there are, how many are done, which runs now (null between them and at the end), and how many failed so far.</summary>
        public int Total { get; }
        public int Done => _reports.Count;
        public string Current => _current?.Name;
        public int FailedSoFar => _reports.Count(r => r.Result == Result.Fail);

        /// <summary>How long the run has taken, from the first part's start to the last one's end so far.</summary>
        public double Seconds => _firstAt < 0 || _lastEnd < 0 ? 0 : _lastEnd - _firstAt;

        public string Summary =>
            $"{Numbers.Count(_reports.Count(r => r.Result == Result.Pass))} passed, " +
            $"{Numbers.Count(_reports.Count(r => r.Result == Result.Fail))} failed, " +
            $"{Numbers.Count(_reports.Count(r => r.Result == Result.Skip))} skipped";

        public ScenarioRunner(IEnumerable<Scenario> scenarios, Action<string> write, Action finish = null)
        {
            _pending = new Queue<Scenario>(scenarios ?? Enumerable.Empty<Scenario>());
            Total = _pending.Count;
            _write = write ?? (_ => { });
            _finally = finish;
        }

        /// <summary>Advances to the given time. True while there is more to run.</summary>
        public bool Tick(double now)
        {
            if (_finished) return false;

            if (_current == null && !Begin(now))
            {
                Finish();
                return false;
            }

            if (now - _startedAt > _current.Timeout)
            {
                End(now, Result.Fail, $"  FAIL timed out after {Numbers.Amount(_current.Timeout, 1)} s");
                return true;
            }

            if (now < _waitUntil) return true;

            var failure = Steps.Run(_body.MoveNext, out var more, null);
            if (failure is SkipScenario skip)
            {
                End(now, _probe.Failed ? Result.Fail : Result.Skip, $"  SKIP {skip.Message}");
                return true;
            }
            if (failure != null)
            {
                End(now, Result.Fail, $"  FAIL threw {failure.GetType().Name}: {failure.Message}");
                return true;
            }

            if (!more)
            {
                if (_probe.Checks == 0) End(now, Result.Fail, "  FAIL checked nothing");
                else End(now, _probe.Failed ? Result.Fail : Result.Pass, null);
                return true;
            }

            _waitUntil = _body.Current is Wait wait ? now + wait.Seconds : now;
            return true;
        }

        private bool Begin(double now)
        {
            if (_pending.Count == 0) return false;

            _current = _pending.Dequeue();
            _report = new ScenarioReport { Name = _current.Name };
            _probe = new Probe(_report, _write);
            _startedAt = now;
            if (_firstAt < 0) _firstAt = now;
            _waitUntil = now;

            _write($"START {_current.Name}");
            var failure = Steps.Run(() => _current.Body(_probe), out _body, null);
            if (failure != null)
            {
                _body = Empty();
                _probe.Line($"  FAIL could not start: {failure.Message}");
                _probe.Failed = true;
                _probe.Checks++;
            }
            return true;
        }

        private static IEnumerator Empty()
        {
            yield break;
        }

        private void End(double now, Result result, string line)
        {
            if (line != null) _probe.Line(line);

            var failure = Steps.Run(() => _current.Cleanup?.Invoke(), null);
            if (failure != null)
            {
                _probe.Line($"  FAIL cleanup threw {failure.GetType().Name}: {failure.Message}");
                result = Result.Fail;
            }

            _report.Result = result;
            _reports.Add(_report);
            _lastEnd = now;
            _write($"{result.ToString().ToUpperInvariant()} {_current.Name} ({Numbers.Fixed(now - _startedAt, 1)} s)");

            _current = null;
            _body = null;
        }

        private void Finish()
        {
            _finished = true;
            var failure = Steps.Run(() => _finally?.Invoke(), null);
            if (failure != null) _write($"FAIL the finishing step threw {failure.GetType().Name}: {failure.Message}");
            _write($"DONE {Summary}, in {SelfTestWords.Duration(Seconds)}");
        }
    }
}
