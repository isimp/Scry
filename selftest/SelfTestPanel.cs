using UnityEngine;
using static Scry.ScryPanel;

namespace Scry
{
    /// <summary>
    /// The self-test in the panel, drawn by the self-test where the panel leaves it room
    /// (<see cref="SelfTestHost"/>), with the panel's own strips and cards: while it runs, a strip
    /// under the header says which part runs, how far it has got and what failed so far, with a
    /// button to stop it; once it is done, the strip gives its headline, and its details, in the
    /// list's place, name every failed part with the checks that failed in it, every skipped part
    /// with why, and what to do next.
    /// </summary>
    internal static class SelfTestPanel
    {
        private static bool _shown;
        private static bool _details;
        private static Vector2 _scroll;

        /// <summary>Shows the strip again, as a run starts or ends.</summary>
        public static void ShowResult()
        {
            _shown = true;
            _details = false;
        }

        /// <summary>Whether the strip shows: while a run goes on, and once one has ended until it is put away.</summary>
        public static bool StripShown => SelfTest.Running || (_shown && SelfTest.LastHeadline != null);

        /// <summary>Whether the last run's details stand in the list's place.</summary>
        public static bool CardShown => _details;

        /// <summary>The strip: what runs and how far it has got, with Stop; once done, its headline, its details and a cross.</summary>
        [Diagnostic]
        public static void Strip(Rect rect)
        {
            var running = SelfTest.Running;
            var tone = running ? Skin.Accent : SelfTest.LastFailed ? Skin.Warn : Skin.KindColor(Kind.StatusEffect);
            if (running)
            {
                if (NoticeStrip(rect, tone, SelfTest.Progress, Skin.Label, false, "Stop", false, false, chipTip: "Stops the self-test and puts back what it changed") == StripClick.Chip)
                {
                    Say("Scry: " + SelfTest.Stop());
                }
                Skin.Fill(new Rect(rect.x, rect.yMax - U(3f), rect.width * SelfTest.Fraction, U(3f)), tone);
                return;
            }
            switch (NoticeStrip(rect, tone, SelfTest.LastHeadline, Skin.Label, false, "Details", _details, true))
            {
                case StripClick.Chip:
                    _details = !_details;
                    CloseCards();
                    break;
                case StripClick.Cross:
                    _shown = false;
                    _details = false;
                    break;
            }
        }

        /// <summary>The last run's outcome in the list's place: its headline, every failed and skipped part, and what to do.</summary>
        public static void Card(Rect rect)
        {
            var width = CardWidth(rect);
            var headline = SelfTest.LastHeadline ?? "";
            var advice = SelfTest.LastAdvice ?? "";
            var headlineH = Skin.Height(Skin.Wrap, headline, width);
            var adviceH = Skin.Height(Skin.DimWrap, advice, width);
            var lines = SelfTest.LastSummary;
            var height = U(44f) + headlineH + U(10f) + lines.Count * U(22f) + U(10f) + adviceH + U(12f);
            var card = BeginCard(rect, ref _scroll, height, "Self-test");
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
            if (EndCard(rect, out var close)) _details = false;
            if (CardButton(rect, close, "Copy summary", out _))
            {
                GUIUtility.systemCopyBuffer = SelfTest.LastText;
                Say("Copied the self-test's summary.");
            }
        }
    }
}
