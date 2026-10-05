namespace Scry
{
    /// <summary>Where the parts of the panel's header stand that do not follow a chip row.</summary>
    internal static class HeaderLayout
    {
        /// <summary>How far past a row's end something may reach and still be taken to fit: sums of floats end a hair off.</summary>
        private const float Slack = 0.5f;

        /// <summary>
        /// Where the origin switch (all, the game's, mods') stands: in the full view at the end of
        /// the first row, where the search leaves room for it, so rounding in the sums before it
        /// never pushes it down; in the compact view after the buttons under the search, or at
        /// the row's start a row lower where it does not fit beside them.
        /// </summary>
        /// <param name="compact">Whether the panel is in its compact view.</param>
        /// <param name="afterButtons">Where it would start, after the buttons before it and their gap.</param>
        /// <param name="width">How wide it is.</param>
        /// <param name="rowLeft">Where its row starts.</param>
        /// <param name="rowRight">Where its row ends.</param>
        public static (float X, bool NextRow) OriginSwitch(bool compact, float afterButtons, float width, float rowLeft, float rowRight)
        {
            if (!compact) return (rowRight - width, false);
            return afterButtons + width <= rowRight + Slack ? (afterButtons, false) : (rowLeft, true);
        }
    }
}
