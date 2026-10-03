namespace Scry
{
    /// <summary>
    /// The catalog of the world Scry is in, once it is read: given by the session as the catalog
    /// is made, let go of when the world is left (<see cref="WorldCaches"/>). What reads the
    /// world and what makes the previews find entries through it; the panel through the explorer
    /// it is handed, which shows this same catalog.
    /// </summary>
    internal static class WorldCatalog
    {
        static WorldCatalog() => WorldCaches.Register(nameof(WorldCatalog), Forget);

        /// <summary>The world's catalog, or null before it is read.</summary>
        public static EntryCatalog Current { get; private set; }

        public static void Set(EntryCatalog catalog) => Current = catalog;

        public static void Forget() => Current = null;

        /// <summary>The entry kept under a key in the world's catalog, or null.</summary>
        public static Entry Find(string key) => Current?.Find(key);
    }
}
