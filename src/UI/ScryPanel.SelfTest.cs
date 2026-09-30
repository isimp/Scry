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
        private static GUIStyle _testWarn;
        private static GUIStyle _testWarnFrom;

        /// <summary>Shows the self-test's strip again, as a run starts or ends.</summary>
        public static void ShowTestResult()
        {
            _testShown = true;
            _testDetails = false;
        }

        /// <summary>How tall the self-test's strip is: none while no run goes on and none has ended or it was put away.</summary>
        private static float TestNoticeHeight() => SelfTest.Running || (_testShown && SelfTest.LastHeadline != null) ? U(32f) : 0f;

        /// <summary>How often the strip has been drawn, for the self-test.</summary>
        public static int TestNoticesDrawn { get; private set; }

        private static void TestNotice(Rect rect)
        {
            if (Event.current.type == EventType.Repaint) TestNoticesDrawn++;
            var running = SelfTest.Running;
            var tone = running ? Skin.Accent : SelfTest.LastFailed ? Skin.Warn : Skin.KindColor(Kind.StatusEffect);
            Skin.Fill(rect, new Color(tone.r, tone.g, tone.b, 0.13f));
            Skin.Fill(new Rect(rect.x, rect.y, U(3f), rect.height), tone);
            if (running) Skin.Fill(new Rect(rect.x, rect.yMax - U(3f), rect.width * SelfTest.Fraction, U(3f)), tone);

            var buttonH = U(24f);
            var closeW = U(24f);
            var close = new Rect(rect.xMax - closeW - U(4f), rect.y + (rect.height - closeW) / 2f, closeW, closeW);
            var label = running ? "Stop" : "Details";
            var buttonW = Skin.Width(Skin.Chip, label) + U(12f);
            var button = new Rect((running ? rect.xMax - U(4f) : close.x - U(6f)) - buttonW, rect.y + (rect.height - buttonH) / 2f, buttonW, buttonH);
            var text = new Rect(rect.x + U(14f), rect.y, button.x - rect.x - U(22f), rect.height);
            Ticker(text, running ? SelfTest.Progress : SelfTest.LastHeadline, Skin.Label);

            if (running)
            {
                if (GUI.Button(button, label, Skin.Chip)) Session.Say("Scry: " + SelfTest.Stop());
                if (button.Contains(Event.current.mousePosition)) AskTip("test-stop", "Stops the self-test and puts back what it changed");
                return;
            }
            if (GUI.Button(button, label, _testDetails ? Skin.ChipOn : Skin.Chip))
            {
                _testDetails = !_testDetails;
                _help = false;
                _modReport = false;
                _offDetails = false;
            }
            if (GUI.Button(close, "×", Skin.Close))
            {
                _testShown = false;
                _testDetails = false;
            }
        }

        /// <summary>The last run's outcome in the list's place: its headline, every failed and skipped part, and what to do.</summary>
        private static void TestCard(Rect rect)
        {
            if (_testWarn == null || !ReferenceEquals(_testWarnFrom, Skin.Label))
            {
                _testWarnFrom = Skin.Label;
                _testWarn = new GUIStyle(Skin.Label) { normal = { textColor = Skin.Warn } };
            }
            Skin.Box(rect, Skin.Panel);
            var closeH = U(30f);
            var area = new Rect(rect.x + U(4f), rect.y + U(6f), rect.width - U(8f), rect.height - closeH - U(20f));
            var width = area.width - U(38f);
            var headline = SelfTest.LastHeadline ?? "";
            var advice = SelfTest.LastAdvice ?? "";
            var headlineH = Skin.Height(Skin.Wrap, headline, width);
            var adviceH = Skin.Height(Skin.DimWrap, advice, width);
            var lines = SelfTest.LastSummary;
            var height = U(44f) + headlineH + U(10f) + lines.Count * U(22f) + U(10f) + adviceH + U(12f);
            var view = new Rect(0f, 0f, area.width - U(14f), Mathf.Max(height, area.height));
            _testScroll = GUI.BeginScrollView(area, _testScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            var x = U(14f);
            var y = U(10f);
            GUI.Label(new Rect(x, y, width, U(26f)), "Self-test", Skin.Big);
            y += U(34f);
            GUI.Label(new Rect(x, y, width, headlineH), headline, Skin.Wrap);
            y += headlineH + U(10f);
            var inFailed = false;
            foreach (var line in lines)
            {
                if (!line.StartsWith(" ", System.StringComparison.Ordinal)) inFailed = line.StartsWith("Failed", System.StringComparison.Ordinal);
                GUI.Label(new Rect(x, y, width, U(22f)), line, inFailed ? _testWarn : Skin.Label);
                y += U(22f);
            }
            y += U(10f);
            GUI.Label(new Rect(x, y, width, adviceH), advice, Skin.DimWrap);
            GUI.EndScrollView();

            var closeRect = new Rect(rect.xMax - U(96f), rect.yMax - closeH - U(10f), U(80f), closeH);
            if (GUI.Button(closeRect, "Close", Skin.Button)) _testDetails = false;
            const string copy = "Copy summary";
            var copyW = Skin.Width(Skin.Button, copy) + U(10f);
            var copyRect = new Rect(closeRect.x - U(8f) - copyW, closeRect.y, copyW, closeH);
            if (copyRect.x > rect.x + U(8f) && GUI.Button(copyRect, copy, Skin.Button))
            {
                GUIUtility.systemCopyBuffer = SelfTest.LastText;
                Session.Say("Copied the self-test's summary.");
            }
        }
    }
}
