namespace Scry
{
    /// <summary>
    /// An older config's Dungeon marks switched off, as the dungeons came to be kept on the map in
    /// each character's book (<see cref="CompendiumBook.Kept"/>): the first character's book read
    /// keeps them off too, and only then is the switch taken out of the config file, so quitting
    /// before a character was read loses nothing.
    /// </summary>
    internal static class KeptMigration
    {
        /// <summary>Whether the book now read takes the dungeons off: the old switch is off and the book was read, not started anew for one that could not be.</summary>
        public static bool TakeDungeonsOff(bool wasOff, bool bookRead) => wasOff && bookRead;
    }
}
