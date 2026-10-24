namespace Scry
{
    /// <summary>
    /// Where a row of the learned list opens its page: at the line showing what was found out,
    /// by the proof it was found by, else at the line of the You box telling how it came to be
    /// known or first seen; where no such line is drawn, at the first of its topics the page has.
    /// The line arrived at is lit a moment, then fades.
    /// </summary>
    internal sealed class NewsArrival
    {
        /// <summary>The You box, as a topic arrived at.</summary>
        public const string YouTopic = "you";

        /// <summary>How long the line arrived at stays lit whole, and when it has faded.</summary>
        private const float LitWhole = 0.6f, LitGone = 2f;

        private static readonly string[] Nowhere = System.Array.Empty<string>();

        /// <summary>What the line arrived at stands for: a proof (<see cref="EntryKeys"/>), or a You box line (<see cref="You"/>); none for a topic alone.</summary>
        public readonly string Spot;

        /// <summary>The topics arrived at where no line stands for <see cref="Spot"/>, the first the page has.</summary>
        public readonly string[] Topics;

        private NewsArrival(string spot, params string[] topics)
        {
            Spot = spot;
            Topics = topics;
        }

        /// <summary>A topic's heading, as a spot lit where a page arrives at the topic alone.</summary>
        public static string Topic(string topic) => "topic:" + topic;

        /// <summary>The You box's line of something still to find out, by its label, arrived at from the title row's chip.</summary>
        public static NewsArrival AtYouLine(string label) => new NewsArrival(You(label), YouTopic);

        /// <summary>A line of the You box, by its label, as a spot arrived at.</summary>
        public static string You(string label) => "you:" + label;

        /// <summary>Where an entry's latest news opens its page.</summary>
        public static NewsArrival Of(BookNews news)
        {
            switch (news.How)
            {
                case NewsHow.Seen: return new NewsArrival(You(CompendiumWords.FirstSeenLabel), YouTopic);
                case NewsHow.Known: return new NewsArrival(You(CompendiumWords.CameLabel), YouTopic);
            }
            var proof = news.Proof;
            if (proof == null || proof == EntryKeys.OnMap) return new NewsArrival(null, "lives", "where", YouTopic);
            if (EntryKeys.IsDeed(proof, out var deed)) return new NewsArrival(proof, DeedTopics(deed));
            if (EntryKeys.IsWith(proof, out var tie)) return new NewsArrival(proof, TieTopics(tie));
            // A weak spot, a damage type's landing or an attack's damage.
            return new NewsArrival(proof, "fight");
        }

        /// <summary>The topics a deed's numbers stand in.</summary>
        private static string[] DeedTopics(Deed deed)
        {
            switch (deed)
            {
                case Deed.Killed: return new[] { "overview", "fight" };
                case Deed.Broke: return new[] { "overview", "breaking" };
                case Deed.StarredFell: return new[] { "fight", "loot" };
                case Deed.Tamed:
                case Deed.Born:
                case Deed.GrewUp: return new[] { "taming" };
                case Deed.Hatched: return new[] { "hatching" };
                case Deed.Ridden: return new[] { "riding" };
                case Deed.Spawned: return new[] { "spawns" };
                case Deed.Grew:
                case Deed.GrewBack: return new[] { "growing", "gathering" };
                case Deed.Collected:
                case Deed.Finished: return new[] { "does", "works", "making" };
                default: return Nowhere;
            }
        }

        /// <summary>The topics what was seen go together with a thing stands in.</summary>
        private static string[] TieTopics(Tie tie)
        {
            switch (tie)
            {
                case Tie.Drops: return new[] { "loot" };
                case Tie.Holds: return new[] { "contents", "loot" };
                case Tie.Gives: return new[] { "given", "from", "loot" };
                case Tie.Eats: return new[] { "taming", "food" };
                case Tie.Where: return new[] { "lives", "where" };
                default: return new[] { "breaking" };
            }
        }

        /// <summary>How lit the line arrived at is, <paramref name="since"/> seconds after: whole a moment, then fading out.</summary>
        public static float Lit(float since) =>
            since <= LitWhole ? 1f : since >= LitGone ? 0f : 1f - (since - LitWhole) / (LitGone - LitWhole);
    }
}
