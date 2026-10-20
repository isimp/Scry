using System;
using System.Linq;

namespace Scry
{
    /// <summary>What a /scry command asks of the self-test.</summary>
    internal enum SelfTestAsk
    {
        /// <summary>Nothing: the words are Scry's, a search or a command of its own.</summary>
        None,

        /// <summary>A run, of the parts its scope takes (<see cref="RunScope"/>).</summary>
        Start,

        Stop,

        /// <summary>Every part's name written to the log, with how long it took last.</summary>
        List,
    }

    /// <summary>Which parts a run takes, and how far each goes.</summary>
    internal enum RunScope
    {
        /// <summary>Every part but those only a full run takes, a part that goes through many places going through a few (<see cref="SelfTestSample"/>).</summary>
        Quick,

        /// <summary>Every part but those only a full run takes, each through every place it names.</summary>
        Deep,

        /// <summary>Every part, reading every place again among them.</summary>
        Full,

        /// <summary>The parts that failed in the last run.</summary>
        Failed,

        /// <summary>The parts whose names hold every word given.</summary>
        Named,
    }

    /// <summary>What a /scry command or the marker file asks of the self-test: start, stop or list, which parts and how far.</summary>
    internal sealed class SelfTestRequest
    {
        public SelfTestAsk Ask;
        public RunScope Scope;

        /// <summary>The words a part's name must hold, in lower case, for a run of the parts named so.</summary>
        public string[] Words = Array.Empty<string>();

        /// <summary>Whether the parts going through many places go through a few: a quick run's.</summary>
        public bool Sampled => Scope == RunScope.Quick;
    }

    /// <summary>
    /// The words of /scry the self-test answers once installed: <c>selftest</c> starts a quick
    /// run, <c>selftest deep</c> one going through every place, <c>selftest full</c> one also
    /// reading every location again, <c>selftest failed</c> the parts that failed last,
    /// <c>selftest list</c> writes every part's name, <c>selftest stop</c> stops it, and any
    /// other words after <c>selftest</c> run the parts whose names hold them all; in any case and
    /// spacing. Everything else stays Scry's, so without the self-test the same words are only a
    /// search. The marker file starts a run by the words after <c>selftest</c>.
    /// </summary>
    internal static class SelfTestCommand
    {
        private const string Word = "selftest";

        /// <summary>What the marker file asks for: a run by its words, as typed after <c>selftest</c>; a quick one where it says nothing.</summary>
        public static SelfTestRequest OfMarker(string text)
        {
            var asked = Of(Word + " " + (text ?? ""));
            return asked.Ask == SelfTestAsk.Start ? asked : new SelfTestRequest { Ask = SelfTestAsk.Start, Scope = RunScope.Quick };
        }

        /// <summary>What the words typed after /scry ask of the self-test.</summary>
        public static SelfTestRequest Of(string typed)
        {
            var words = (typed ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(w => w.ToLowerInvariant()).ToArray();
            if (words.Length == 0 || words[0] != Word) return new SelfTestRequest { Ask = SelfTestAsk.None };
            if (words.Length == 1) return new SelfTestRequest { Ask = SelfTestAsk.Start, Scope = RunScope.Quick };
            if (words.Length == 2)
            {
                switch (words[1])
                {
                    case "stop": return new SelfTestRequest { Ask = SelfTestAsk.Stop };
                    case "list": return new SelfTestRequest { Ask = SelfTestAsk.List };
                    case "deep": return new SelfTestRequest { Ask = SelfTestAsk.Start, Scope = RunScope.Deep };
                    case "full": return new SelfTestRequest { Ask = SelfTestAsk.Start, Scope = RunScope.Full };
                    case "failed": return new SelfTestRequest { Ask = SelfTestAsk.Start, Scope = RunScope.Failed };
                }
            }
            return new SelfTestRequest { Ask = SelfTestAsk.Start, Scope = RunScope.Named, Words = words.Skip(1).ToArray() };
        }
    }

    /// <summary>A few of the places a long part goes through, for a quick run.</summary>
    internal static class SelfTestSample
    {
        /// <summary>Some of many, spread evenly over them from the first; all where they are no more.</summary>
        public static T[] Spread<T>(T[] all, int few)
        {
            if (all.Length <= few) return all;
            var picked = new T[few];
            for (var i = 0; i < few; i++) picked[i] = all[i * all.Length / few];
            return picked;
        }

        /// <summary>The first few, for a list whose order puts the places that tell most first.</summary>
        public static T[] First<T>(T[] all, int few) => all.Length <= few ? all : all.Take(few).ToArray();
    }
}
