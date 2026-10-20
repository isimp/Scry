namespace Scry
{
    /// <summary>Where the parts of the panel's header stand that do not follow a chip row.</summary>
    internal static class HeaderLayout
    {
        /// <summary>How far past a row's end something may reach and still be taken to fit: sums of floats end a hair off.</summary>
        private const float Slack = 0.5f;

        /// <summary>
        /// Where the full view's origin switch (all, the game's, mods') stands: at the end of the
        /// first row, where the search leaves room for it, so rounding in the sums of the buttons
        /// before it never pushes it down. The compact view has it as a chip (<see cref="CompactKindRow"/>).
        /// </summary>
        public static float OriginSwitch(float width, float rowRight) => rowRight - width;

        /// <summary>Where what follows the name starts: a gap after the name as drawn, not after the slot it is given.</summary>
        public static float AfterName(float left, float nameWidth, float gap) => left + nameWidth + gap;

        /// <summary>How wide buttons joined as one group are: each a size, a seam between each two.</summary>
        public static float JoinedWidth(int pieces, float size, float seam) => pieces <= 0 ? 0f : pieces * size + (pieces - 1) * seam;

        /// <summary>
        /// Whether the compact view's list button and way back and forward stand in the title row
        /// after the name: where they end by <paramref name="before"/>, the buttons at the row's
        /// right and their gap; else they start the search row, as the full view has them.
        /// </summary>
        public static bool NavInTitleRow(float afterName, float navWidth, float before) => afterName + navWidth <= before + Slack;

        /// <summary>
        /// The compact view's search row: the search from the row's start, or after the list button
        /// and the way back where they start it, up to its help, favourites and recent at the row's end.
        /// </summary>
        public static (float SearchX, float SearchWidth, float ButtonsX) CompactSearchRow(float left, float right, float navWidth, float buttonsWidth, float gap)
        {
            var buttons = right - buttonsWidth;
            var search = navWidth > 0f ? left + navWidth + gap : left;
            return (search, buttons - gap - search, buttons);
        }

        /// <summary>
        /// The compact view's kind row: the pill of the kind picked at its start, Learned after it
        /// while it holds news (none where <paramref name="learnedWidth"/> is 0), and the chip of
        /// where the list is taken from at its end; a long pill gives way to them.
        /// </summary>
        public static (float PillX, float PillWidth, float LearnedX, float OriginX) CompactKindRow(float left, float right, float pillWidth, float learnedWidth, float originWidth, float gap)
        {
            var origin = right - originWidth;
            var room = origin - gap - (learnedWidth > 0f ? learnedWidth + gap : 0f) - left;
            var pill = pillWidth < room ? pillWidth : room;
            return (left, pill, left + pill + gap, origin);
        }
    }
}
