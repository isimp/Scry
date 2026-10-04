using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What is felled, mined, broken, picked or grown, in words: how much it takes and with what
    /// tool, what a shell breaks into, what is picked a day, the ground and weather a plant takes,
    /// and how each row of what it gives begins.
    /// </summary>
    internal static class GatherWords
    {
        /// <summary>How each row of drops begins, by when the game drops them (<see cref="DropWords.Led"/>).</summary>
        public const string WhenFelled = "When felled, ";
        public const string WhenBroken = "When broken, ";
        public const string EachPiece = "Each piece ";
        public const string AlsoDrops = "Also ";

        public const string NothingWhenBroken = "nothing when broken";
        public const string CultivatedGround = "cultivated ground";

        /// <summary>Its health, of the whole or of each piece a rock or vein is mined in.</summary>
        public static string Health(float health, bool perPiece) => Numbers.Amount(health) + (perPiece ? " a piece" : "");

        /// <summary>The tool tier it needs; any for none.</summary>
        public static string ToolTier(int tier) => tier > 0 ? Numbers.Count(tier) : "any";

        /// <summary>What a shell turns into when struck once, as a silver vein's does.</summary>
        public static string BreaksInto(string inside) => inside + ", mined a piece at a time";

        /// <summary>What is picked a day, with how long the world's day is.</summary>
        public static string PerDay(string yields, float daySeconds) => $"{yields} (a day is {Numbers.Duration(daySeconds)})";

        /// <summary>The weather a plant tolerates, or null for none.</summary>
        public static string Tolerates(bool heat, bool cold)
        {
            var tolerates = new List<string>();
            if (heat) tolerates.Add("heat");
            if (cold) tolerates.Add("cold");
            return tolerates.Count > 0 ? string.Join(", ", tolerates) : null;
        }

        public static string Picked(bool oneOf) => oneOf ? "Picked, one of" : "Picked";

        public static string GrowsInto(bool oneOf) => oneOf ? "Grows into one of" : "Grows into";
    }
}
