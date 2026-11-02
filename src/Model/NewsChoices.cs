namespace Scry
{
    /// <summary>What the notice under the panel's header tells of what the character learned.</summary>
    internal enum NewsShown
    {
        All,
        NewPages,
        Off,
    }

    /// <summary>
    /// The settings' choice of the news of what the character learned: all of it, pages newly met
    /// only, leaving out what was found out about one met before, or none, the Learned tab alone
    /// telling it.
    /// </summary>
    internal static class NewsChoices
    {
        public static bool Shows(NewsShown shown) => shown != NewsShown.Off;

        public static bool PagesOnly(NewsShown shown) => shown == NewsShown.NewPages;

        /// <summary>The choice from the single switch it was before: off stays off, on tells all.</summary>
        public static NewsShown From(bool notice) => notice ? NewsShown.All : NewsShown.Off;
    }
}
