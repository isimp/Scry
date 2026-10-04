using System;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A tame creature's breeding in words, as <c>Procreation.Procreate</c> has it: what it
    /// needs, how its love points come, when too many near stop it, and its young's stars; and
    /// what its young grows into (<c>Growup</c>). The game skips each check at the chance it
    /// calls the pregnancy chance, so a love point comes at the rest of it.
    /// </summary>
    internal static class BreedWords
    {
        /// <summary>What it needs to breed: tame, fed and calm, and a partner near unless it breeds alone too.</summary>
        public static string Needs(float partnerRange, string partner, bool alone)
        {
            const string state = "tame, fed and calm";
            var range = Numbers.Amount(partnerRange);
            if (alone) return $"{state}; a partner within {range} m is not needed";
            return string.IsNullOrEmpty(partner) ? $"{state}, with another of its kind within {range} m" : $"{state}, with a {partner} within {range} m";
        }

        /// <summary>How its love points come, at the chance the game does not skip, and how many make it pregnant.</summary>
        public static string Love(float interval, float skipChance, int points)
        {
            var chance = (int)Math.Round((1f - skipChance) * 100f, MidpointRounding.AwayFromZero);
            if (chance <= 0) return "never: the game skips every check";
            var every = Numbers.Duration(interval);
            return points <= 1 ? $"a {Numbers.Count(chance)}% chance every {every} to get pregnant" : $"a love point every {every} at a {Numbers.Count(chance)}% chance; {Numbers.Count(points)} make it pregnant";
        }

        /// <summary>When too many near stop it.</summary>
        public static string Crowd(int max, float range) => $"once {Numbers.Count(max)} of its kind and its young are within {Numbers.Amount(range)} m";

        /// <summary>Its young's stars: its parent's, and at least so many (the young's level less one).</summary>
        public static string Stars(int minLevel) => minLevel > 1 ? $"with its parent's stars, at least {Numbers.Count(minLevel - 1)}" : "with its parent's stars";

        /// <summary>What an egg hatches into, and that it hatches tame where it does.</summary>
        public static string HatchesInto(string creature, bool tame) => tame ? creature + ", tame" : creature;

        /// <summary>Where an egg hatches (<c>EggGrow.CanGrow</c>): on its own, and by a fire and under a roof with enough cover as it asks.</summary>
        public static string Hatches(bool fire, bool roof, float cover)
        {
            var line = "lying on its own";
            if (fire) line += ", by a fire";
            if (roof) line += cover > 0f ? $", under a roof with at least {Numbers.Count((int)Math.Round(cover * 100f, MidpointRounding.AwayFromZero))}% cover" : ", under a roof";
            return line;
        }

        /// <summary>Each of several weights as its share of them all, in the words every share is told in (<see cref="DropWords.Share"/>).</summary>
        public static string[] Shares(float[] weights)
        {
            var total = weights.Sum();
            return weights.Select(w => DropWords.Share(total > 0f ? w / total : 0f)).ToArray();
        }
    }

    /// <summary>A ridden creature's stamina in words, as its saddle (<c>Sadle</c>) has it.</summary>
    internal static class RideWords
    {
        /// <summary>How much stamina it has and regains, slower when hungry.</summary>
        public static string Stamina(float max, float regen, float hungry)
        {
            var line = $"{Numbers.Amount(max)}, regaining {Numbers.Amount(regen)} a second";
            return Math.Abs(hungry - regen) > 0.001f ? $"{line}, {Numbers.Amount(hungry)} when hungry" : line;
        }

        /// <summary>What running and swimming drain, or null when neither does.</summary>
        public static string Drains(float run, float swim)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (run > 0f) parts.Add($"running {Numbers.Amount(run)} a second");
            if (swim > 0f) parts.Add($"swimming {Numbers.Amount(swim)} a second");
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
    }
}
