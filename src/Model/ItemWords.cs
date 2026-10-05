using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// An item in words: its worth, food and figures with what each quality adds, how its recipe
    /// is titled, and the answers its page gives even when they are "no": whether it goes
    /// through portals, how far gear can be upgraded, that it never wears out, that it has no
    /// armour or cannot block, and where it is repaired (<c>InventoryGui.CanRepair</c>: at the
    /// station its recipe names, or its repair station, from the recipe's level) or that it
    /// cannot be.
    /// </summary>
    internal static class ItemWords
    {
        public const string NoWear = "does not wear out";

        public static string Portals(bool teleportable) => teleportable ? "can go through" : "cannot go through";

        public static string Quality(int maxQuality) => maxQuality > 1 ? $"up to {Numbers.Count(maxQuality)}" : "1 only: it cannot be upgraded";

        public static string Repair(bool canBeRepaired, string station, int level)
        {
            if (!canBeRepaired) return "cannot be repaired";
            if (string.IsNullOrEmpty(station)) return "cannot be repaired: no recipe names a station for it";
            return AtLevel(station, level);
        }

        public static string Coins(int coins) => $"{Numbers.Count(coins)} coins";

        /// <summary>The Forsaken power a trophy gives on its boss stone.</summary>
        public static string Gives(string power) => "gives " + power;

        /// <summary>The title of an item's table by quality level.</summary>
        public const string ByQualityTitle = "By quality";

        /// <summary>That table's columns: a blank over the lines' names, then each quality level.</summary>
        public static string[] QualityColumns(int maxQuality)
        {
            var columns = new List<string> { "" };
            for (var q = 1; q <= maxQuality; q++) columns.Add(Numbers.Count(q));
            return columns.ToArray();
        }

        /// <summary>
        /// A line of that table: its damage at each quality level, each type its base and what
        /// each level adds (<c>ItemData.GetDamage</c>); null where quality adds nothing or it has one level.
        /// </summary>
        public static string[] DamageByQuality(IReadOnlyList<(string Type, float Base, float PerLevel)> damage, int maxQuality)
        {
            if (maxQuality <= 1 || !damage.Any(d => d.PerLevel > 0f)) return null;
            var line = new List<string> { "Damage" };
            for (var q = 1; q <= maxQuality; q++) line.Add(CombatWords.Damage(damage.Select(d => (d.Type, d.Base + d.PerLevel * (q - 1)))) ?? "");
            return line.ToArray();
        }

        /// <summary>
        /// A line of the table by quality: a figure at each quality level, its base and what each
        /// level adds; null where quality adds nothing or it has one level.
        /// </summary>
        public static string[] ByQuality(string label, float value, float perLevel, int maxQuality)
        {
            if (maxQuality <= 1 || perLevel <= 0f) return null;
            var line = new List<string> { label };
            for (var q = 1; q <= maxQuality; q++) line.Add(Numbers.Amount(value + perLevel * (q - 1)));
            return line.ToArray();
        }

        /// <summary>The line of the station level each quality needs, the first its crafting's (<c>Recipe.GetRequiredStationLevel</c>).</summary>
        public static string[] StationLevels(string station, IReadOnlyList<int> levels)
        {
            var line = new List<string> { station + " level" };
            foreach (var level in levels) line.Add(Numbers.Count(level));
            return line.ToArray();
        }

        /// <summary>A figure, and how much each quality adds to it where it adds any (the caller passes none for what cannot be upgraded).</summary>
        public static string PerQuality(double value, double perQuality) =>
            Numbers.Amount(value) + (perQuality > 0.0 ? $", +{Numbers.Amount(perQuality)} per quality" : "");

        public static string Armour(float armour) => armour > 0f ? Numbers.Amount(armour) : "none";

        /// <summary>Its block, told only above 1 as <c>ItemDrop.ItemData.AddBlockTooltip</c> tells it; below that it cannot block.</summary>
        public static string Block(float power) => power > 1f ? Numbers.Amount(power) : "none";

        /// <summary>What a food gives, or null for nothing.</summary>
        public static string Food(float health, float stamina, float eitr)
        {
            var food = new List<string>();
            if (health > 0f) food.Add($"{Numbers.Amount(health)} health");
            if (stamina > 0f) food.Add($"{Numbers.Amount(stamina)} stamina");
            if (eitr > 0f) food.Add($"{Numbers.Amount(eitr)} eitr");
            return food.Count > 0 ? string.Join(", ", food) : null;
        }

        public static string Heals(float perTick) => $"{Numbers.Amount(perTick)} a tick";

        /// <summary>A set's bonus by its effect's name, with how many pieces it takes where the game says.</summary>
        public static string SetBonus(string effect, int pieces) => pieces > 0 ? $"{effect} ({Numbers.Count(pieces)} pieces)" : effect;

        /// <summary>A recipe's title: where it is made, from what station level, how many it makes and whether any one ingredient does.</summary>
        public static string RecipeTitle(string station, int level, int makes, bool anyOne) =>
            (string.IsNullOrEmpty(station) ? "Made by hand" : "Made at " + AtLevel(station, level)) + Makes(makes, anyOne);

        /// <summary>How a title of what is made ends: how many it makes, and that any one of the ingredients does.</summary>
        public static string Makes(int makes, bool anyOne) =>
            (makes > 1 ? $", makes {Numbers.Count(makes)}" : "") + (anyOne ? ", from any one of these" : "");


        /// <summary>The title over what gear resists: armour while worn, a shield or weapon while blocking.</summary>
        public static string DamageTaken(bool worn) => worn ? "Damage it takes while worn" : "Damage it takes while blocking";

        /// <summary>The label for gear that resists nothing, worn or blocking.</summary>
        public static string ResistsNothing(bool worn) => worn ? "Resists while worn" : "Resists while blocking";

        /// <summary>The title over an item's upgrade kits: the station that takes it past its top quality.</summary>
        public static string PastTop(string upgradeStation) => "Past its top quality, at " + UpgradeStation(upgradeStation);

        /// <summary>The upgrade station by the name the game shows, or said plainly where no prefab is one.</summary>
        public static string UpgradeStation(string shown) => shown ?? "an upgrade station";

        private static string AtLevel(string station, int level) => level > 1 ? $"{station} level {Numbers.Count(level)}" : station;
    }
}
