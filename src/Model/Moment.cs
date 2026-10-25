namespace Scry
{
    /// <summary>The time of day a moment fell in, as the game's turn of the day runs; none where it was not kept.</summary>
    internal enum DayPart
    {
        None,
        Dawn,
        Day,
        Dusk,
        Night,
    }

    /// <summary>
    /// A moment of play, kept with what the character saw or came to know then: the game's day,
    /// the biome it stood in, the time of day, the weather (the game's name for it), what it
    /// had in hand (an item's prefab, none bare-handed) and the world's name, as a character's
    /// book spans worlds whose days run apart. Each part is open where it was not known.
    /// </summary>
    internal readonly struct Moment
    {
        public readonly int Day;
        public readonly string Biome;
        public readonly DayPart Time;
        public readonly string Weather;
        public readonly string InHand;
        public readonly string World;

        public Moment(int day, string biome, DayPart time = DayPart.None, string weather = null, string inHand = null, string world = null)
        {
            Day = day;
            Biome = string.IsNullOrEmpty(biome) ? null : biome;
            Time = time;
            Weather = string.IsNullOrEmpty(weather) ? null : weather;
            InHand = string.IsNullOrEmpty(inHand) ? null : inHand;
            World = string.IsNullOrEmpty(world) ? null : world;
        }

        /// <summary>A moment nothing is known of.</summary>
        public static readonly Moment None = new Moment(-1, null);

        /// <summary>
        /// The part of the day a fraction of the game's turn of it falls in: its day runs from a
        /// quarter to three quarters of the turn, morning and evening coming within a twentieth of
        /// either end, so dawn from 0.2 to 0.3, dusk from 0.7 to 0.8, night outside them.
        /// </summary>
        public static DayPart PartOf(float fraction)
        {
            if (fraction < 0.2f) return DayPart.Night;
            if (fraction < 0.3f) return DayPart.Dawn;
            if (fraction < 0.7f) return DayPart.Day;
            return fraction < 0.8f ? DayPart.Dusk : DayPart.Night;
        }
    }

    /// <summary>What an entry's latest news was, for the Learned list and the notice.</summary>
    internal enum NewsHow
    {
        Seen,
        Known,
        FoundOut,
        Map,
    }

    /// <summary>
    /// An entry's latest news in its character's book: how it came, what was found out of it
    /// (<see cref="EntryKeys"/>' proof, or the explored map's mark), the way it came to be known
    /// where one was kept, the game's day, -1 where none was (caught up from the game's
    /// records, or kept before days were), and the world that day was in, none where it was not kept.
    /// </summary>
    internal readonly struct BookNews
    {
        public readonly NewsHow How;
        public readonly string Proof;
        public readonly CameBy? Way;
        public readonly int Day;
        public readonly string World;

        public BookNews(NewsHow how, string proof, CameBy? way, int day, string world = null)
        {
            How = how;
            Proof = proof;
            Way = way;
            Day = day;
            World = world;
        }
    }

    /// <summary>A line of the notice's news: the entry's name as shown, how it came, what was found out, the day (-1 for none), and its world where that is not the one played.</summary>
    internal readonly struct NewsLine
    {
        public readonly string Name;
        public readonly NewsHow How;
        public readonly string Proof;
        public readonly int Day;
        public readonly string World;

        public NewsLine(string name, NewsHow how, string proof, int day, string world = null)
        {
            Name = name;
            How = how;
            Proof = proof;
            Day = day;
            World = world;
        }
    }
}
