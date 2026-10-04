using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test in the panel: while it runs, a strip under the header says which part runs,
    /// how far it has got and what failed so far, with a button to stop it; once it is done, the
    /// strip gives its headline, and its details, in the list's place, name every failed part
    /// with the checks that failed in it, every skipped part with why, and what to do next.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static bool _testShown;
        private static bool _testDetails;
        private static Vector2 _testScroll;

        /// <summary>Shows the self-test's strip again, as a run starts or ends.</summary>
        public static void ShowTestResult()
        {
            _testShown = true;
            _testDetails = false;
        }

        /// <summary>How tall the self-test's strip is: none while no run goes on and none has ended or it was put away.</summary>
        private static float TestNoticeHeight() => SelfTestHost.Running || (_testShown && SelfTestHost.LastHeadline != null) ? U(32f) : 0f;

        /// <summary>The self-test's strip: what runs and how far it has got, with Stop; once done, its headline, its details and a cross.</summary>
        [Diagnostic]
        private static void TestNotice(Rect rect)
        {
            CountDrawn(PanelPart.TestNotice);
            var running = SelfTestHost.Running;
            var tone = running ? Skin.Accent : SelfTestHost.LastFailed ? Skin.Warn : Skin.KindColor(Kind.StatusEffect);
            if (running)
            {
                if (NoticeStrip(rect, tone, SelfTestHost.Progress, Skin.Label, false, "Stop", false, false, chipTip: "Stops the self-test and puts back what it changed") == StripClick.Chip)
                {
                    Say("Scry: " + SelfTestHost.Stop());
                }
                Skin.Fill(new Rect(rect.x, rect.yMax - U(3f), rect.width * SelfTestHost.Fraction, U(3f)), tone);
                return;
            }
            switch (NoticeStrip(rect, tone, SelfTestHost.LastHeadline, Skin.Label, false, "Details", _testDetails, true))
            {
                case StripClick.Chip:
                    _testDetails = !_testDetails;
                    CloseCards();
                    break;
                case StripClick.Cross:
                    _testShown = false;
                    _testDetails = false;
                    break;
            }
        }

        /// <summary>The last run's outcome in the list's place: its headline, every failed and skipped part, and what to do.</summary>
        private static void TestCard(Rect rect)
        {
            var width = CardWidth(rect);
            var headline = SelfTestHost.LastHeadline ?? "";
            var advice = SelfTestHost.LastAdvice ?? "";
            var headlineH = Skin.Height(Skin.Wrap, headline, width);
            var adviceH = Skin.Height(Skin.DimWrap, advice, width);
            var lines = SelfTestHost.LastSummary;
            var height = U(44f) + headlineH + U(10f) + lines.Count * U(22f) + U(10f) + adviceH + U(12f);
            var card = BeginCard(rect, ref _testScroll, height, "Self-test");
            var x = card.X;
            var y = card.Y;
            GUI.Label(new Rect(x, y, width, headlineH), headline, Skin.Wrap);
            y += headlineH + U(10f);
            var inFailed = false;
            foreach (var line in lines)
            {
                if (!line.StartsWith(" ", System.StringComparison.Ordinal)) inFailed = line.StartsWith("Failed", System.StringComparison.Ordinal);
                GUI.Label(new Rect(x, y, width, U(22f)), line, inFailed ? Skin.WarnLabel : Skin.Label);
                y += U(22f);
            }
            y += U(10f);
            GUI.Label(new Rect(x, y, width, adviceH), advice, Skin.DimWrap);
            if (EndCard(rect, out var close)) _testDetails = false;
            if (CardButton(rect, close, "Copy summary", out _))
            {
                GUIUtility.systemCopyBuffer = SelfTestHost.LastText;
                Say("Copied the self-test's summary.");
            }
        }
    }
}
