namespace Scry
{
    /// <summary>
    /// How much of a page under the strict rules was found out: each number it keeps back for a
    /// deed, each weak spot, damage type and attack's damage, each counted cell of a table and
    /// each line of where an item comes from, and each name it could leave out until seen
    /// together with it, found or still kept back; one rule for all, a cell or a name a find.
    /// </summary>
    internal sealed class PageFinds
    {
        public int Found { get; private set; }
        public int Of { get; private set; }

        /// <summary>How many of the names counted are still left out, not yet seen together with the page.</summary>
        public int NamesLeft { get; private set; }

        /// <summary>Whether the page keeps anything back at all, found or not.</summary>
        public bool Any => Of > 0;

        /// <summary>Whether everything it kept back was found out: so too where it keeps nothing back.</summary>
        public bool AllFound => Found >= Of;

        /// <summary>One more thing the page keeps back under the rules, still kept back or found out.</summary>
        public void Count(bool kept)
        {
            Of++;
            if (!kept) Found++;
        }

        /// <summary>One more name the page could leave out under the rules, still left out or shown, seen together with it.</summary>
        public void Name(bool kept)
        {
            Count(kept);
            if (kept) NamesLeft++;
        }
    }
}
