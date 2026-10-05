using System;

namespace Scry
{
    /// <summary>What a /scry command asks of the self-test.</summary>
    internal enum SelfTestAsk
    {
        /// <summary>Nothing: the words are Scry's, a search or a command of its own.</summary>
        None,
        Start,
        Stop,
    }

    /// <summary>
    /// The words of /scry the self-test answers once installed: <c>selftest</c> starts it and
    /// <c>selftest stop</c> stops it, in any case and spacing. Everything else stays Scry's, so
    /// without the self-test the same words are only a search.
    /// </summary>
    internal static class SelfTestCommand
    {
        private const string Word = "selftest";
        private const string StopWord = "stop";

        /// <summary>What the words typed after /scry ask of the self-test.</summary>
        public static SelfTestAsk Of(string typed)
        {
            var words = (typed ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !words[0].Equals(Word, StringComparison.OrdinalIgnoreCase)) return SelfTestAsk.None;
            if (words.Length == 1) return SelfTestAsk.Start;
            return words.Length == 2 && words[1].Equals(StopWord, StringComparison.OrdinalIgnoreCase) ? SelfTestAsk.Stop : SelfTestAsk.None;
        }
    }
}
