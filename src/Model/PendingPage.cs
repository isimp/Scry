namespace Scry
{
    /// <summary>
    /// What the panel is asked to show before the catalog is read, as a click on a dungeon's mark
    /// on Valheim's map asks while Scry still reads it: the search and the entry, the latest asked
    /// for, kept until the catalog is read and handed over once. The panel closed meanwhile lets
    /// go of the entry, the click taken back; the search stays.
    /// </summary>
    internal sealed class PendingPage
    {
        public string Search { get; private set; }

        public string Entry { get; private set; }

        /// <summary>Keeps a search or an entry asked for; none leaves what was asked before.</summary>
        public void Ask(string search, string entry)
        {
            if (search != null) Search = search;
            if (entry != null) Entry = entry;
        }

        /// <summary>The panel closed before the catalog was read: the entry is let go of.</summary>
        public void Closed() => Entry = null;

        /// <summary>The search and the entry asked for, handed over once.</summary>
        public (string Search, string Entry) Take()
        {
            var taken = (Search, Entry);
            Search = null;
            Entry = null;
            return taken;
        }
    }
}
