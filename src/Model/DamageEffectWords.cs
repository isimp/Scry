using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What the status effects a damage type puts on whatever it hits really do, from their own
    /// settings, as the game deals them (<c>Character.RPC_Damage</c>): fire and spirit dealt over
    /// a few seconds in even hits rather than at once (<c>SE_Burning</c>), poison over a time that
    /// grows with it (<c>SE_Poison.AddDamage</c>), frost dealt at once and slowing for a time that
    /// grows with the share of health it took (<c>SE_Frost</c>), and lightning dealt at once, its
    /// effect only marking the hit.
    /// </summary>
    internal static class DamageEffectWords
    {
        /// <summary>The least each hit of fire or spirit may deal for a hit to set it going at all (<c>SE_Burning.AddFireDamage</c>).</summary>
        public const float LeastHit = 0.2f;

        /// <summary>How much faster burning ends while wet (<c>SE_Burning.UpdateStatusEffect</c> adds five times its time to the time it runs).</summary>
        public const int BurnsOutWet = 6;

        /// <summary>How much faster Wet wears off while burning (<c>SE_Wet.UpdateStatusEffect</c> adds fifty times its time).</summary>
        public const int DriesBurning = 51;

        /// <summary>How many even hits a time is dealt in, one each so often, as the game counts them.</summary>
        public static int Hits(float lasts, float every) => every > 0f ? Math.Max(1, (int)(lasts / every)) : 1;

        /// <summary>One hit so often, in words: "one a second", "one every 0.5 s".</summary>
        private static string Each(float every) => Math.Abs(every - 1f) < 1e-4f ? "one a second" : "one every " + Numbers.Duration(every);

        /// <summary>
        /// Fire or spirit dealt over the effect's time in even hits rather than at once: what a hit
        /// leaves after resistances and armour, more of it adding to what is left and starting the
        /// time again, and a hit too small to deal <see cref="LeastHit"/> each time dealing none.
        /// </summary>
        public static List<FactPair> OverTime(string damage, float lasts, float every)
        {
            var hits = Hits(lasts, every);
            return new List<FactPair>
            {
                new FactPair(Naming.Capital(damage) + " damage", $"Dealt over {Numbers.Duration(lasts)} in {Numbers.Count(hits)} even hits, {Each(every)}, not at once: what a hit leaves after resistances and armour"),
                new FactPair("More " + damage, $"Adds to what is left, over {Numbers.Duration(lasts)} again"),
                new FactPair("Too little", $"A hit leaving under {Numbers.Amount(LeastHit * hits, 1)} {damage} damage deals none of it"),
            };
        }

        /// <summary>What an effect dealing fire, or spirit, over its time does: spirit's over its time alone, fire's with what Wet and resting have to do with it.</summary>
        public static List<FactPair> Burns(bool spirit, float lasts, float every)
        {
            var pairs = OverTime(spirit ? "spirit" : "fire", lasts, every);
            if (!spirit) pairs.AddRange(Fire());
            return pairs;
        }

        /// <summary>What burning does besides: Wet puts it out sooner, and it keeps one from resting and dries one off (<c>Player.UpdateEnvStatusEffects</c>).</summary>
        public static List<FactPair> Fire() => new List<FactPair>
        {
            new FactPair("Wet", $"Puts it out {Numbers.Count(BurnsOutWet)} times as fast, the rest of its damage lost"),
            new FactPair("While it burns", $"No resting, and Wet wears off {Numbers.Count(DriesBurning)} times as fast"),
        };

        /// <summary>What a poison's own time is: none, each hit sets it.</summary>
        public const string PoisonLasts = "By the poison a hit leaves";

        /// <summary>What frost's own time is: none, each hit sets it.</summary>
        public const string FrostLasts = "By the share of health a hit takes";

        /// <summary>How long poison lasts in words: a base time and the damage, times so much, to a power ("the square root of" for a half).</summary>
        private static string PoisonTime(float baseLasts, float perDamage, float power)
        {
            var times = Math.Abs(perDamage - 1f) < 1e-4f ? "the damage" : $"{Numbers.Amount(perDamage)} times the damage";
            var raised = Math.Abs(power - 0.5f) < 1e-4f ? "the square root of " + times : $"{times} to the power {Numbers.Amount(power)}";
            return $"{Numbers.Duration(baseLasts)} and {raised}";
        }

        /// <summary>Poison dealt over a time that grows with it, in even hits; only a stronger one starts it over (<c>SE_Poison.AddDamage</c>).</summary>
        public static List<FactPair> Poison(float baseLasts, float perDamagePlayer, float perDamage, float power, float every) => new List<FactPair>
        {
            new FactPair("Poison damage", $"Dealt over time in even hits, {Each(every)}, not at once: what a hit leaves after resistances and armour"),
            new FactPair("How long", $"On you {PoisonTime(baseLasts, perDamagePlayer, power)}; on a creature {PoisonTime(baseLasts, perDamage, power)}"),
            new FactPair("More poison", "Only a stronger hit starts it over; a weaker one adds nothing while it lasts"),
        };

        /// <summary>The poison table's title and columns: how much, then how long and how much each hit on you and on a creature.</summary>
        public const string PoisonTitle = "How long poison lasts";

        public static readonly string[] PoisonColumns = { "Poison", "On you", "Each second", "On a creature", "Each second" };

        /// <summary>The poison a hit may leave that the table tells.</summary>
        private static readonly float[] PoisonShown = { 10f, 25f, 50f, 100f };

        /// <summary>How long so much poison lasts on you and on a creature, and how much each hit of it deals.</summary>
        public static List<string[]> PoisonLines(float baseLasts, float perDamagePlayer, float perDamage, float power, float every, IEnumerable<float> damages = null)
        {
            string[] Line(float damage)
            {
                var onYou = baseLasts + (float)Math.Pow(damage * perDamagePlayer, power);
                var onCreature = baseLasts + (float)Math.Pow(damage * perDamage, power);
                return new[]
                {
                    Numbers.Amount(damage, 1),
                    Numbers.Duration(onYou), Numbers.Amount(damage / Hits(onYou, every), 1),
                    Numbers.Duration(onCreature), Numbers.Amount(damage / Hits(onCreature, every), 1),
                };
            }
            return (damages ?? PoisonShown).Select(Line).ToList();
        }

        /// <summary>Frost dealt at once, then slowing from <paramref name="leastSpeed"/> back to full by its end, for a time that grows with the share of health the hit took (<c>SE_Frost.AddDamage</c>, <c>ModifySpeed</c>).</summary>
        public static List<FactPair> Frost(float freezePlayer, float freezeCreature, float leastSpeed) => new List<FactPair>
        {
            new FactPair("Frost damage", "Dealt at once, then it slows"),
            new FactPair("Slows", $"To {Numbers.Percent(leastSpeed, 1)} of the speed at first, coming back by its end"),
            new FactPair("How long", $"{Numbers.Duration(freezePlayer)} on you, {Numbers.Duration(freezeCreature)} on a creature, times the share of health the hit took"),
            new FactPair("A stronger hit", "Starts it over where it would last longer than is left"),
        };

        /// <summary>The frost slowing table's title and columns: how frost is taken, and the speed it slows to at first.</summary>
        public const string FrostSlowTitle = "How far frost slows";

        public static readonly string[] FrostSlowColumns = { "Its frost", "Slowed at first to" };

        /// <summary>
        /// The speed frost slows to at first by how it is taken: <paramref name="leastSpeed"/> as
        /// most take it, less for what is weak to it (over its multiplier); what resists it at all
        /// or is immune is not slowed (<c>SE_Frost.ModifySpeed</c>).
        /// </summary>
        public static List<string[]> FrostSlowLines(float leastSpeed, IEnumerable<(Degree Degree, float Multiplier)> multipliers)
        {
            var lines = new List<string[]> { new[] { "Normal", Numbers.Percent(leastSpeed, 1) } };
            foreach (var (degree, multiplier) in multipliers.Where(m => m.Multiplier > 0f && Weak(m.Degree)).OrderBy(m => m.Multiplier))
            {
                lines.Add(new[] { WeakWords(degree), Numbers.Percent(leastSpeed / multiplier, 1) });
            }
            lines.Add(NotSlowed);
            return lines;
        }

        private static readonly string[] NotSlowed = { "Resisting it at all", "not slowed" };

        private static bool Weak(Degree degree) => degree == Degree.SlightlyWeak || degree == Degree.Weak || degree == Degree.VeryWeak;

        private static string WeakWords(Degree degree)
        {
            switch (degree)
            {
                case Degree.SlightlyWeak: return "Slightly weak";
                case Degree.VeryWeak: return "Very weak";
                default: return "Weak";
            }
        }

        /// <summary>The frost time table's title and columns: the share of health a hit takes, and how long it slows you and a creature.</summary>
        public const string FrostTimeTitle = "How long frost slows";

        public static readonly string[] FrostTimeColumns = { "The hit takes of its health", "On you", "On a creature" };

        /// <summary>The shares of health a hit may take that the table tells.</summary>
        private static readonly float[] SharesShown = { 0.1f, 0.25f, 0.5f, 1f };

        /// <summary>How long a frost hit taking so much of its target's health slows you and a creature.</summary>
        public static List<string[]> FrostTimeLines(float freezePlayer, float freezeCreature) =>
            SharesShown.Select(share => new[] { Numbers.Percent(share), Numbers.Duration(share * freezePlayer), Numbers.Duration(share * freezeCreature) }).ToList();

        /// <summary>Lightning dealt at once; its effect only marks the hit (<c>Character.AddLightningDamage</c>).</summary>
        public static List<FactPair> Lightning(float lasts) => new List<FactPair>
        {
            new FactPair("Lightning damage", "Dealt at once"),
            new FactPair("This effect", $"Changes nothing itself: it marks the hit for {Numbers.Duration(lasts)}"),
        };
    }
}
