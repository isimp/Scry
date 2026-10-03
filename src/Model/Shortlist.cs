namespace Scry
{
    /// <summary>
    /// How much of a long list the panel shows: all of a short one; of a long one its first part
    /// and a chip for the rest, until that is opened. A list is never shortened to hide only one
    /// or two, since the chip that says so takes their room.
    /// </summary>
    internal static class Shortlist
    {
        /// <summary>How many more than the first part a list may have and still be shown whole.</summary>
        public const int Slack = 2;

        /// <summary>Whether a list is long enough to be shortened, and so to be opened or folded.</summary>
        public static bool Long(int total, int first) => total > first + Slack;

        /// <summary>How many of a list are shown.</summary>
        public static int Shown(int total, int first, bool open) => open || !Long(total, first) ? total : first;

        /// <summary>How many of a list are left out.</summary>
        public static int Hidden(int total, int first, bool open) => total - Shown(total, first, open);
    }
}
