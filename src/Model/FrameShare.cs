namespace Scry
{
    /// <summary>
    /// How work spread over frames keeps to its share of each: done a piece at a time, a piece
    /// begun only while one like those before still fits in what is left of the share, and always
    /// at least one a frame, so the work goes on however long a piece takes. Checking only whether
    /// the share is spent before each piece lets the last one run past it by a whole piece.
    /// </summary>
    internal sealed class FrameShare
    {
        /// <summary>How quickly the estimate follows what pieces take now, rather than before.</summary>
        private const double Follow = 0.25;

        private bool _known;

        public FrameShare(double budgetMs)
        {
            BudgetMs = budgetMs;
        }

        /// <summary>How long the work may take each frame, in milliseconds.</summary>
        public double BudgetMs { get; }

        /// <summary>What a piece usually takes, in milliseconds: a running average of those done.</summary>
        public double PieceMs { get; private set; }

        /// <summary>Whether another piece may begin, this far into the frame with this many done in it.</summary>
        public bool MayBegin(double elapsedMs, int doneThisFrame) => MayBegin(elapsedMs, doneThisFrame, BudgetMs);

        /// <summary>The same, in a frame given another share than usual.</summary>
        public bool MayBegin(double elapsedMs, int doneThisFrame, double budgetMs) => doneThisFrame == 0 || elapsedMs + PieceMs <= budgetMs;

        /// <summary>A piece done, and how long it took.</summary>
        public void Took(double ms)
        {
            PieceMs = _known ? PieceMs + (ms - PieceMs) * Follow : ms;
            _known = true;
        }
    }

    /// <summary>Work that can fail, tried again until it has failed a set number of times, then given up.</summary>
    internal sealed class Attempts
    {
        private readonly int _allowed;
        private readonly System.Collections.Generic.Dictionary<string, int> _failed = new System.Collections.Generic.Dictionary<string, int>();

        public Attempts(int allowed)
        {
            _allowed = allowed;
        }

        /// <summary>A failure of the work known by this key; true while it may still be tried again.</summary>
        public bool Failed(string key)
        {
            _failed.TryGetValue(key, out var times);
            _failed[key] = ++times;
            return times < _allowed;
        }

        /// <summary>Whether the work known by this key has failed too often to be tried again.</summary>
        public bool GivenUp(string key) => _failed.TryGetValue(key, out var times) && times >= _allowed;
    }
}
