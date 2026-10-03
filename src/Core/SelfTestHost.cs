using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Where the in-game self-test plugs in. The self-test is a plugin of its own
    /// (<c>Scry.SelfTest.dll</c>), built and deployed with a development build of Scry and never
    /// packaged with a release; as it loads it registers here, and the frame, the command and
    /// the panel's strip reach it through here. Without it, nothing of it runs or shows, and
    /// <c>/scry selftest</c> says it is not installed.
    /// </summary>
    internal static class SelfTestHost
    {
        /// <summary>What the self-test offers Scry.</summary>
        public interface IRunner
        {
            bool Running { get; }

            /// <summary>Called every frame: runs the scenarios, and starts them by marker file once per world.</summary>
            void Tick();

            string Start(string why);
            string Stop();

            /// <summary>The run's progress while it runs, for the panel's strip; null when none runs.</summary>
            string Progress { get; }

            /// <summary>How much of the run is done, from 0 to 1.</summary>
            float Fraction { get; }

            /// <summary>The last run's headline, failed and skipped parts with their checks, and what to do; null before any run this session.</summary>
            string LastHeadline { get; }
            IReadOnlyList<string> LastSummary { get; }
            string LastAdvice { get; }
            bool LastFailed { get; }

            /// <summary>The whole of the last run's outcome as text, for copying.</summary>
            string LastText { get; }
        }

        private static IRunner _runner;

        /// <summary>The self-test registers itself as it loads.</summary>
        public static void Register(IRunner runner) => _runner = runner;

        public static bool Running => _runner?.Running ?? false;
        public static void Tick() => _runner?.Tick();
        public static string Start(string why) => _runner != null ? _runner.Start(why) : NotInstalled;
        public static string Stop() => _runner != null ? _runner.Stop() : NotInstalled;
        public static string Progress => _runner?.Progress;
        public static float Fraction => _runner?.Fraction ?? 0f;
        public static string LastHeadline => _runner?.LastHeadline;
        public static IReadOnlyList<string> LastSummary => _runner?.LastSummary ?? System.Array.Empty<string>();
        public static string LastAdvice => _runner?.LastAdvice;
        public static bool LastFailed => _runner?.LastFailed ?? false;
        public static string LastText => _runner?.LastText ?? "";

        private const string NotInstalled = "the self-test is not installed with this copy of Scry; it comes with a development build.";
    }
}
