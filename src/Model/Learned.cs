using System;

namespace Scry
{
    /// <summary>
    /// Word that something read changes what an entry's details tell: a place's contents read,
    /// a dungeon's kinds of room read, where things are found once every location is read. What
    /// reads the world tells it here, and whatever keeps details already told listens, so the
    /// readers need not know who that is.
    /// </summary>
    public static class Learned
    {
        /// <summary>Told with the entry whose details changed, or null when any entry's may have.</summary>
        public static event Action<Entry> Changed;

        /// <summary>Tells that an entry's details changed; nothing for no entry.</summary>
        public static void About(Entry entry)
        {
            if (entry != null) Changed?.Invoke(entry);
        }

        /// <summary>Tells that any entry's details may have changed.</summary>
        public static void AboutAll() => Changed?.Invoke(null);
    }
}
