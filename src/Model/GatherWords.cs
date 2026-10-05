using System.Collections.Generic;
using System.Linq;

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

        /// <summary>The title of the row of what breaks a rock, vein or tree.</summary>
        public const string BrokenWithTitle = "Broken with, the weakest of each tier";

        /// <summary>A tool's chip beside its name: its tier, which it stands for with every tier up.</summary>
        public static string TierChip(int tier) => "Tier " + Numbers.Count(tier);

        /// <summary>
        /// What breaks a rock, vein or tree, the lowest tier first, one tool a tier: a hit breaks
        /// it when its tool tier is at least the thing's (<c>HitData.CheckToolTier</c>, the tier the
        /// weapon's own) and some of its damage is of a type the thing takes, the rest coming to
        /// nothing against its resistances. Of each tier the weakest is told, dealing least of
        /// what the thing takes (ties by name): a modded game's 32 axes say no more than one of
        /// each tier.
        /// </summary>
        /// <param name="minTier">The tool tier it needs.</param>
        /// <param name="taken">The damage types it takes any of.</param>
        /// <param name="tools">The items players hit with: each with its tool tier and the damage it deals by type.</param>
        public static List<(string Name, int Tier)> BreaksIt(int minTier, IEnumerable<string> taken, IEnumerable<(string Name, int Tier, (string Type, float Amount)[] Deals)> tools)
        {
            var takes = new HashSet<string>(taken);
            return tools
                .Select(t => (t.Name, t.Tier, Dealt: t.Deals.Where(d => d.Amount > 0f && takes.Contains(d.Type)).Sum(d => d.Amount)))
                .Where(t => t.Tier >= minTier && t.Dealt > 0f)
                .GroupBy(t => t.Tier)
                .OrderBy(g => g.Key)
                .Select(g => g.OrderBy(t => t.Dealt).ThenBy(t => t.Name, System.StringComparer.Ordinal).First())
                .Select(t => (t.Name, t.Tier))
                .ToList();
        }

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
