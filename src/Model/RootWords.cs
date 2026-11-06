namespace Scry
{
    /// <summary>
    /// What a root a tap draws from tells on its page (<c>ResourceRoot</c>): how much sap it
    /// holds, how fast it refills, and the amount each line the game shows on it stands for; and
    /// what a tap's slow working means on the tap's page.
    /// </summary>
    internal static class RootWords
    {
        /// <summary>The title of the table pairing each line the game shows on a root with the sap it stands for.</summary>
        public const string LooksTitle = "When you look at it";

        /// <summary>The table's columns: the line the game shows, and the sap left while it shows.</summary>
        public static readonly string[] Columns = { "It reads", "Sap left" };

        /// <summary>The label of the note under a tap's Needs, telling what its slow working means.</summary>
        public const string SlowLabel = "Running low";

        /// <summary>How much a root holds: it starts full, as the game reads a root never drained at its most (<c>ResourceRoot.GetLevel</c>).</summary>
        public static string Holds(float most) => $"up to {Numbers.Amount(most)}, full when first found";

        /// <summary>How fast a root refills, one at a time, and how long from empty to full; "never" for one that does not.</summary>
        public static string Refills(float perSecond, float most) =>
            perSecond <= 0f ? "never" : $"one every {Numbers.Duration(1.0 / perSecond)}, {Numbers.Duration(most / perSecond)} from empty to full";

        /// <summary>
        /// The sap each line stands for, in the order the game picks them (<c>ResourceRoot.GetHoverText</c>):
        /// above the high mark the full line, above the empty mark the fading one, else the dried-up one.
        /// </summary>
        public static string[] Bands(float high, float empty) =>
            new[] { $"above {Numbers.Amount(high)}", $"above {Numbers.Amount(empty)}, up to {Numbers.Amount(high)}", $"{Numbers.Amount(empty)} or less" };

        /// <summary>
        /// What a tap's slow line means (<c>SapCollector.GetStatusText</c>): the root under it is
        /// below its empty mark, and the tap, no slower itself, waits for the root to gain one back
        /// for each sap; none for a root that never refills. The word "root" is not said: it is
        /// the name of an item, which the strict rules hide wherever it stands until met.
        /// </summary>
        public static string Slows(string slowLine, float empty, float perSecond) =>
            perSecond <= 0f ? null : $"{slowLine} shows once what it stands on has {Numbers.Amount(empty)} or less left, each sap then waiting the {Numbers.Duration(1.0 / perSecond)} that takes to gain one back";
    }
}
