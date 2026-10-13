namespace Scry
{
    /// <summary>What the panel says of the compendium: a silhouette's name, a tab's count of what was found, a page not yet open.</summary>
    internal static class CompendiumWords
    {
        /// <summary>The name a silhouette shows in the list and on its closed page: what kind of thing it is, nothing more.</summary>
        public static string Unknown(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return "Unknown creature";
                case Kind.Item: return "Unknown item";
                case Kind.Piece: return "Unknown piece";
                case Kind.Resource: return "Unknown resource";
                case Kind.Spawner: return "Unknown spawner";
                case Kind.StatusEffect: return "Unknown status effect";
                case Kind.Biome: return "Unknown biome";
                case Kind.Location: return "Unknown place";
                case Kind.Raid: return "Unknown raid";
                default: return "Unknown";
            }
        }

        /// <summary>A kind's tab with the compendium on: how many of it were found, of all there are.</summary>
        public static string Tab(string label, int found, int total, string colour) =>
            $"{label}  <color=#{colour}>{Numbers.Count(found)}/{Numbers.Count(total)}</color>";

        /// <summary>The first line of a page not yet open, by how much the character knows of it.</summary>
        public static string Closed(KnownLevel level)
        {
            switch (level)
            {
                case KnownLevel.HeardOf: return "Heard of, not met yet";
                case KnownLevel.Seen: return "Seen, not yet come to know";
                default: return "Not met yet";
            }
        }

        /// <summary>What opens a page of this kind under the loose rules.</summary>
        public static string HowToKnow(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return "Fight it to learn all about it.";
                case Kind.Item: return "Pick it up, make it or eat it to learn all about it.";
                case Kind.Piece: return "Build it, use it or unlock it to learn all about it.";
                case Kind.Resource: return "Hit it or pick it to learn all about it.";
                case Kind.Spawner: return "Hit it to learn all about it.";
                case Kind.StatusEffect: return "Have it on you to learn all about it.";
                case Kind.Biome: return "Set foot in it to learn all about it.";
                case Kind.Location: return "Step inside to learn all about it.";
                case Kind.Raid: return "Live through it to learn all about it.";
                default: return "Meet it in the world to learn all about it.";
            }
        }
    }
}
