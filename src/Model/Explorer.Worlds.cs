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
    }
}
