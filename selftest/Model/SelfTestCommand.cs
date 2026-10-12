using System;

namespace Scry
{
    /// <summary>What a /scry command asks of the self-test.</summary>
    internal enum SelfTestAsk
    {
        /// <summary>Nothing: the words are Scry's, a search or a command of its own.</summary>
        None,
        /// <summary>A quick run: every part but those only a full run takes.</summary>
        Start,

        /// <summary>A full run: every part, reading every place again among them.</summary>
        StartFull,

        Stop,
    }

    /// <summary>
    /// The words of /scry the self-test answers once installed: <c>selftest</c> starts a quick
    /// run, <c>selftest full</c> a full one, and <c>selftest stop</c> stops it, in any case and
    /// spacing. Everything else stays Scry's, so without the self-test the same words are only a
    /// search. The marker file starts a quick run, or a full one where it says <c>full</c>.
    /// </summary>
    internal static class SelfTestCommand
    {
        private const string Word = "selftest";
        private const string StopWord = "stop";
        private const string FullWord = "full";

        /// <summary>What the marker file asks for: a full run where it says so, else a quick one.</summary>
        public static SelfTestAsk OfMarker(string text) =>
            (text ?? "").Trim().Equals(FullWord, StringComparison.OrdinalIgnoreCase) ? SelfTestAsk.StartFull : SelfTestAsk.Start;

        /// <summary>What the words typed after /scry ask of the self-test.</summary>
        public static SelfTestAsk Of(string typed)
        {
            var words = (typed ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !words[0].Equals(Word, StringComparison.OrdinalIgnoreCase)) return SelfTestAsk.None;
            if (words.Length == 1) return SelfTestAsk.Start;
            if (words.Length != 2) return SelfTestAsk.None;
            if (words[1].Equals(StopWord, StringComparison.OrdinalIgnoreCase)) return SelfTestAsk.Stop;
            return words[1].Equals(FullWord, StringComparison.OrdinalIgnoreCase) ? SelfTestAsk.StartFull : SelfTestAsk.None;
        }
    }
}
