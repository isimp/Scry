using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The runs the self-test keeps (<see cref="SelfTestHistory"/>, <c>BepInEx/Scry-selftest.history</c>):
    /// read the first time asked, or where no history was kept yet, read back from the runs the
    /// logs hold; each run added as it ends. The card steps through them and tells each against
    /// the run before of its kind.
    /// </summary>
    internal static partial class SelfTest
    {
        public static string HistoryFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.history");

        private static SelfTestHistory _history;

        /// <summary>The runs kept, read the first time asked; where none were kept, the runs the logs hold.</summary>
        public static SelfTestHistory History
        {
            get
            {
                if (_history != null) return _history;
                var kept = Kept(HistoryFile);
                _history = kept != null ? SelfTestHistory.Load(kept) : SelfTestHistory.FromLog((Kept(OlderFile) ?? "") + "\n" + (Kept(ResultFile) ?? ""));
                if (kept == null) SaveHistory();
                return _history;
            }
        }

        /// <summary>A run that ended kept with every part's report, the oldest going once more than are kept.</summary>
        private static void KeepRun(ScenarioRunner runner)
        {
            if (runner == null || runner.Reports.Count == 0) return;
            History.Add(new KeptRun(_runStarted, _runScope, runner.Seconds, runner.Reports));
            SaveHistory();
        }

        private static void SaveHistory()
        {
            var text = _history?.Save();
            if (text != null) Steps.Run(() => File.WriteAllText(HistoryFile, text), null);
        }

        private static Dictionary<string, string> _areas;

        /// <summary>The area a part belongs to now; none for a part the list no longer holds.</summary>
        public static string AreaOf(string part)
        {
            if (_areas == null)
            {
                _areas = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var scenario in Scenarios()) _areas[scenario.Name] = scenario.Area;
            }
            return part != null && _areas.TryGetValue(part, out var area) ? area : null;
        }
    }
}
