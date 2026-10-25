namespace Scry
{
    /// <summary>The ways two things are seen together (<see cref="Tie"/>), as the book's lines keep them.</summary>
    internal static class TieWords
    {
        /// <summary>A way as a line keeps it, one word.</summary>
        public static string Kept(Tie tie)
        {
            switch (tie)
            {
                case Tie.Drops: return "drops";
                case Tie.Gives: return "gives";
                case Tie.Eats: return "eats";
                case Tie.Holds: return "holds";
                case Tie.Where: return "where";
                case Tie.BrokenWith: return "brokenwith";
                default: return tie.ToString().ToLowerInvariant();
            }
        }

        /// <summary>A way read back from a line's word; none for a word this Scry does not know.</summary>
        public static Tie? Read(string word)
        {
            switch (word)
            {
                case "drops": return Tie.Drops;
                case "gives": return Tie.Gives;
                case "eats": return Tie.Eats;
                case "holds": return Tie.Holds;
                case "where": return Tie.Where;
                case "brokenwith": return Tie.BrokenWith;
                default: return null;
            }
        }
    }
}
