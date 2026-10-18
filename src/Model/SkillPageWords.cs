using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A skill's page in words: what it changes at no skill, at the character's level and at the
    /// top, each line by the game's rule for it (<see cref="SkillMath"/>) and only where something
    /// the game reads uses it (<see cref="SkillInputs"/>); what raises it, how far the next level
    /// is, and what a death takes.
    /// </summary>
    internal static class SkillPageWords
    {
        /// <summary>The page's topic of what the skill changes, and the table there.</summary>
        public const string ChangesHeading = "What it changes";
        public const string ChangesTitle = "By level";

        /// <summary>What a skill a mod adds changes, which only the mod knows.</summary>
        public const string OnlyItsMod = "only the mod adding it knows";

        public const string RisesLabel = "Raised by";
        public const string OnDeathLabel = "On death";
        public const string TrainedWithTitle = "Trained with";
        public const string ToolsTitle = "Tools it eases";
        public const string StationsTitle = "Speeds making at";
        public const string CookingTitle = "Gives bonus food at";
        public const string PickedTitle = "Gives a bonus picking";
        public const string AddsLevelsTitle = "Effects adding levels";
        public const string FasterTitle = "Rises faster with";

        /// <summary>The You box's line of the character's level, and of the way to the next.</summary>
        public const string LevelLabel = "Level";

        public static string ToNextLabel(float level) => $"To level {Numbers.Count((long)Math.Floor(level) + 1)}";

        /// <summary>The character's level on the skill's card on the stage.</summary>
        public static string Card(float level, float fromEffects) => $"Your level {Level(level, fromEffects)}";

        /// <summary>How many levels an effect adds: "+20".</summary>
        public static string AddsLevels(float levels) => Numbers.Amount(levels, 0, signed: true);

        /// <summary>How much faster an effect makes a skill rise: "+50%".</summary>
        public static string RisesFaster(float share) => Numbers.Percent(share, 0, signed: true);

        /// <summary>The table's columns: what changes, then at no skill, at the character's level and at the top.</summary>
        public static string[] ChangesColumns(float level) =>
            new[] { "", "At 0", $"You, {Numbers.Count((long)Math.Floor(level))}", $"At {Numbers.Count((long)SkillMath.Top)}" };

        /// <summary>The weapon skills, whose rises come from hitting creatures; the first ones hit in melee, which rises half as much again.</summary>
        private static readonly HashSet<string> Melee = new HashSet<string>(StringComparer.Ordinal) { "Swords", "Knives", "Clubs", "Polearms", "Spears", "Axes", "Unarmed" };
        private static readonly HashSet<string> Ranged = new HashSet<string>(StringComparer.Ordinal) { "Bows", "Crossbows", "ElementalMagic", "BloodMagic" };

        /// <summary>The table's lines: what changes, at no skill, at the level and at the top.</summary>
        public static List<string[]> Changes(SkillInputs inputs, float level)
        {
            var lines = new List<string[]>();
            void Line(string what, Func<float, string> at) => lines.Add(new[] { what, at(0f), at(level), at(SkillMath.Top) });
            string Share(float share) => Numbers.Percent(share);
            string Chance(float chance) => Numbers.Percent(chance, 1);
            string PerSecond(float amount) => $"{Numbers.Amount(amount, 1)} a second";
            string Seconds(float seconds) => $"{Numbers.Amount(seconds, 1)} s";
            string Speed(float metres) => $"{Numbers.Amount(metres, 1)} m/s";

            if (inputs.Hits) Line("Damage of a hit", l => HitShares(l));
            if (inputs.OnTrees) Line("An axe's hit on a tree", l => HitShares(l));
            if (inputs.Costs) Line("Cost of an attack", l => Share(SkillMath.CostShare(l)));
            var draws = inputs.DrawTimes.Distinct().ToList();
            if (draws.Count == 1) Line("Full draw", l => Seconds(SkillMath.DrawTime(draws[0], l)));
            else if (draws.Count > 1) Line("Full draw, of the bow's time", l => Share(SkillMath.DrawTime(1f, l)));
            if (inputs.DrawDrains) Line("Drain while drawing", l => Share(SkillMath.CostShare(l)));
            var reloads = inputs.ReloadTimes.Distinct().ToList();
            if (reloads.Count == 1) Line("Reloading", l => Seconds(SkillMath.ReloadTime(reloads[0], l)));
            else if (reloads.Count > 1) Line("Reloading, of the weapon's time", l => Share(SkillMath.ReloadTime(1f, l)));

            switch (inputs.Name)
            {
                case "Blocking":
                    Line("Block", l => Share(SkillMath.Block(1f, l)));
                    break;
                case "Run":
                    Line("Running speed", l => Speed(inputs.RunSpeed * SkillMath.RunSpeedShare(l)));
                    Line("Stamina running", l => PerSecond(inputs.RunDrain * SkillMath.RunDrainShare(l)));
                    break;
                case "Swim":
                    Line("Stamina swimming", l => PerSecond(SkillMath.Between(inputs.SwimAtNone, inputs.SwimAtTop, l)));
                    break;
                case "Jump":
                    Line("Jump strength", l => Share(SkillMath.JumpShare(l)));
                    break;
                case "Dodge":
                    Line("Stamina a dodge", l => Numbers.Amount(inputs.Dodge * SkillMath.DodgeShare(l), 1));
                    break;
                case "Sneak":
                    Line("Stamina sneaking", l => PerSecond(inputs.Sneak * SkillMath.SneakDrainShare(l)));
                    Line("Seen in the dark", l => Share(SkillMath.Stealth(l, 0f)));
                    Line("Seen in full light", l => Share(SkillMath.Stealth(l, 1f)));
                    break;
                case "Ride":
                    Line("Mount's running speed", l => Share(SkillMath.MountSpeedShare(l)));
                    Line("Mount's stamina", l => Share(SkillMath.MountDrainShare(l)));
                    break;
                case "Fishing":
                    Line("Stamina, a fish on", l => PerSecond(SkillMath.Between(inputs.HookedAtNone, inputs.HookedAtTop, l)));
                    Line("Stamina reeling in", l => Share(SkillMath.Between(1f, inputs.PullAtTop, l)));
                    Line("Reeling in", l => Speed(SkillMath.Between(inputs.LineAtNone, inputs.LineAtTop, l)));
                    break;
            }

            if (inputs.Crafts)
            {
                Line("Making time", l => Share(SkillMath.CraftTimeShare(l, inputs.CraftMostSaved)));
                if (inputs.CraftBonus > 0f) Line("Bonus, stackables made", l => Chance(SkillMath.BonusChance(l, inputs.CraftBonus)));
            }
            if (inputs.CookBonus > 0f) Line("Bonus, food taken off", l => Chance(SkillMath.BonusChance(l, inputs.CookBonus)));
            if (inputs.PickBonus > 0f) Line("Bonus when picking", l => Chance(SkillMath.BonusChance(l, inputs.PickBonus)));
            if (inputs.BuildStamina) Line("Building stamina", l => Share(SkillMath.BuildStaminaShare(l)));
            if (inputs.WearSaved > 0f) Line("Tool wear placing", l => Share(SkillMath.WearShare(l, inputs.WearSaved)));
            if (inputs.ScytheAtTop > 0f) Line("Scythe reach", l => Numbers.Metres(SkillMath.Between(inputs.ScytheAtNone, inputs.ScytheAtTop, l), 1));
            if (inputs.ShieldListed > 0f) Line("Shield absorbs", l => Numbers.Amount(SkillMath.ShieldAbsorb(inputs.ShieldListed, inputs.ShieldPerLevel, l), 0));
            foreach (var (staff, levels, counts) in inputs.Summons)
            {
                Line($"{staff}, at once", l =>
                {
                    var step = SkillMath.SummonStep(levels, l);
                    return step >= 0 && step < counts.Length ? Numbers.Count(counts[step]) : "1";
                });
            }
            if (inputs.Summons.Count > 0 && inputs.SummonPerLevel > 0f) Line("Summons' hits", l => Numbers.Times(SkillMath.SummonStrength(l, inputs.SummonPerLevel), 1));
            return lines;
        }

        /// <summary>The share of its damage a hit deals at a level, rolled between two ends: "45–75%".</summary>
        private static string HitShares(float level)
        {
            var (least, most) = SkillMath.HitShare(level);
            return Numbers.CountRange((long)Math.Round(least * 100f), (long)Math.Round(most * 100f)) + "%";
        }

        /// <summary>The character's level, with how much of it effects add: "54, 20 of it from effects"; "100, the top".</summary>
        public static string Level(float level, float fromEffects)
        {
            var whole = Numbers.Count((long)Math.Floor(level));
            if (fromEffects >= 1f) return $"{whole}, {Numbers.Count((long)Math.Floor(fromEffects))} of it from effects";
            return level >= SkillMath.Top ? $"{whole}, the top" : whole;
        }

        /// <summary>
        /// How far the next level is: the share of the way there, and about how many of what
        /// raises it are left, and with being rested (<paramref name="rested"/> times as much);
        /// a skill a mod adds only its share. None at the top.
        /// </summary>
        public static string ToNext(string skill, float level, float progress, float step, float rested)
        {
            if (level >= SkillMath.Top) return null;
            var there = $"{Numbers.Percent(Math.Min(1f, progress / SkillMath.NextLevelNeeds(level)))} there";
            var unit = RiseUnit(skill);
            if (unit.Words == null || step <= 0f) return there;
            var each = step * unit.Amount;
            var left = SkillMath.RisesToNext(level, progress, each);
            var leftRested = SkillMath.RisesToNext(level, progress, each * rested);
            return unit.Seconds
                ? $"{there}, about {Numbers.Duration(left)} of {unit.Words}, {Numbers.Duration(leftRested)} rested"
                : $"{there}, about {Numbers.Count(left)} {unit.Words}, {Numbers.Count(leftRested)} rested";
        }

        /// <summary>What a rise of a skill comes from, how much one gives before its step, and whether it comes each second.</summary>
        private static (string Words, float Amount, bool Seconds) RiseUnit(string skill)
        {
            if (Melee.Contains(skill)) return ("hits on creatures", 1.5f, false);
            if (Ranged.Contains(skill)) return ("hits on creatures", 1f, false);
            switch (skill)
            {
                case "Pickaxes": return ("hits on rocks and ore", 1f, false);
                case "WoodCutting": return ("hits on trees", 1f, false);
                case "Blocking": return ("hits blocked", 1f, false);
                case "Jump": return ("jumps", 1f, false);
                case "Dodge": return ("dodges", 0.1f, false);
                case "Run": return ("running", 1f, true);
                case "Swim": return ("swimming", 1f, true);
                case "Ride": return ("riding at a run", 1f, true);
                case "Sneak": return ("sneaking near creatures", 1f, true);
                case "Fishing": return ("reeling in", 1f, true);
                case "Cooking": return ("dishes cooked", 1f, false);
                case "Farming": return ("plants picked", 1f, false);
                case "Crafting": return ("things made", 1f, false);
                default: return (null, 0f, false);
            }
        }

        /// <summary>What raises a skill, as the game raises each; none for a skill a mod adds.</summary>
        public static string RisesBy(string skill)
        {
            if (skill == "BloodMagic") return "Hitting creatures with what trains it, and your summons' hits, half as much";
            if (Melee.Contains(skill) || Ranged.Contains(skill)) return "Hitting creatures with what trains it";
            switch (skill)
            {
                case "Pickaxes": return "Hitting rocks and ore with a pickaxe";
                case "WoodCutting": return "Hitting trees with an axe";
                case "Blocking": return "Each hit blocked, twice as much for a parry";
                case "Jump": return "Each jump";
                case "Dodge": return "Each dodge, a tenth as much unless a hit passes through it";
                case "Run": return "Each second running with stamina left";
                case "Swim": return "Each second swimming";
                case "Ride": return "Each second riding at a run";
                case "Sneak": return "Each second sneaking, a tenth as much with no creature near enough to notice you";
                case "Fishing": return "Each second reeling in, more with a fish on";
                case "Cooking": return "Putting food on a cooking station and taking it off cooked, and making things where it speeds the making";
                case "Farming": return "Picking crops and wild plants, and planting with the cultivator";
                case "Crafting": return "Making things where it speeds the making, building, and repairing";
                default: return null;
            }
        }

        /// <summary>What a death takes of a skill (<c>Skills.OnDeath</c>), and when it takes nothing (<c>Player.OnDeath</c>: not within the wait after a death).</summary>
        public static string OnDeath(float share, float wait) =>
            $"Loses {Numbers.Percent(share, 1)} of its level and the way to the next, unless your last death was less than {Numbers.Duration(wait)} before";

        /// <summary>What a skill leaves alone where players expect it to change something: a pickaxe's digging.</summary>
        public static (string Label, string Text)? NotChanged(string skill) =>
            skill == "Pickaxes" ? ("Digging the ground", "the same at every level; the skill is mining rocks and ore") : ((string, string)?)null;
    }
}
