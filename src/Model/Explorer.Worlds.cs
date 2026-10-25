namespace Scry
{
    /// <summary>The worlds a character's book spans, whose days run apart: a day of another world is named by it.</summary>
    internal sealed partial class Explorer
    {
        /// <summary>The name of the world played now, which a day of it needs no naming by; none where no world is played.</summary>
        public string WorldNow { get; set; }

        /// <summary>The world an entry's latest news came in; none with the compendium off, no news, or a moment kept before worlds were.</summary>
        public string LearnedWorld(Entry entry) => NewsOf(entry)?.World;

        /// <summary>A world's name as a page or the Learned list names it: only where it is not the world played now.</summary>
        public string WorldShown(string world) => world != null && world != WorldNow ? world : null;

        /// <summary>A moment's world as a page names it after the day (<see cref="WorldShown"/>): another world by name; none for the world played or a moment kept before worlds were, which the Learned list's heading alone sets apart.</summary>
        public string MomentWorldShown(string world) => WorldShown(world);

        /// <summary>The world an entry's Learned group is headed by (<see cref="JournalWords.WorldGroup"/>): a moment kept before worlds were stands apart.</summary>
        public string LearnedWorldShown(Entry entry) => JournalWords.WorldGroup(LearnedWorld(entry), WorldNow);
    }
}
