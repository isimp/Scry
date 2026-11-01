namespace Scry
{
    /// <summary>How a measured time or amount stands against its budget.</summary>
    internal enum Verdict { Within, Busy, Over }

    /// <summary>
    /// How fast this machine runs now, against the quickest it was ever seen to: a fixed piece
    /// of reference work timed now, over the quickest time it took, kept between runs. A budget
    /// missed while the machine runs well below its best is not judged, as other programs then
    /// hold the processor; a miss far beyond what the slowdown explains still fails.
    /// </summary>
    internal sealed class MachinePace
    {
        /// <summary>How much slower than its best the machine must run for a missed budget not to be judged.</summary>
        public const double BusyAt = 1.5;

        /// <summary>How far past the budget, times the slowdown, a miss on a busy machine still goes unjudged.</summary>
        public const double Leeway = 2.0;

        /// <summary>The quickest the reference work took, in ms; none before the first sample.</summary>
        public double Best { get; private set; }

        /// <summary>A sample of the reference work, in ms: how many times slower than the quickest seen it ran, the quickest kept.</summary>
        public double Slowdown(double sampleMs)
        {
            if (sampleMs <= 0) return 1.0;
            if (Best <= 0 || sampleMs < Best) Best = sampleMs;
            return sampleMs / Best;
        }

        /// <summary>A value against its budget at a slowdown of the machine.</summary>
        public static Verdict Judge(double value, double limit, double slowdown)
        {
            if (value <= limit) return Verdict.Within;
            return slowdown >= BusyAt && value <= limit * slowdown * Leeway ? Verdict.Busy : Verdict.Over;
        }

        public string Save() => "best\t" + Stored.Number(Best, 4);

        /// <summary>The pace kept, or none for text it cannot read.</summary>
        public static MachinePace Load(string text)
        {
            var pace = new MachinePace();
            var parts = (text ?? "").Trim().Split('\t');
            if (parts.Length == 2 && parts[0] == "best" && Stored.TryNumber(parts[1], out var best) && best > 0) pace.Best = best;
            return pace;
        }
    }
}
