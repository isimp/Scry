using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What a station, producer, fire, trader or altar does, in words: what it burns and holds,
    /// how many it takes at a time and how fast it makes, what it needs around it, what anything
    /// else becomes in it, and whom it summons.
    /// </summary>
    internal static class StationWords
    {
        public const string Roof = "a roof";
        public const string FireUnder = "a fire under it";

        /// <summary>What a fermenter needs: a roof and cover on most sides (<c>Fermenter.UpdateCover</c>).</summary>
        public const string Sheltered = "a roof, and cover on most sides";

        /// <summary>What an upgrader takes: what is past its top quality, with the kits its recipe names.</summary>
        public const string Upgrader = "takes items past their top quality, with the upgrade kits their recipes name";

        /// <summary>The fuel a smelter burns, and how much of it goes into each product.</summary>
        public static string BurnsForEach(string fuel, int perProduct) => $"{fuel}, {Numbers.Count(perProduct)} for each";

        /// <summary>The fuel a fire or oven burns, one at a time.</summary>
        public static string BurnsOneEvery(string fuel, float seconds) => $"{fuel}, one every {Numbers.Duration(seconds)}";

        /// <summary>What a smelter holds to process, and the fuel it holds when it burns any.</summary>
        public static string Holds(int toProcess, int? fuel) =>
            $"{Numbers.Count(toProcess)} to process" + (fuel.HasValue ? $", {Fuel(fuel.Value)}" : "");

        public static string Fuel(double amount) => $"{Numbers.Amount(amount)} fuel";

        public static string AtATime(int slots) => $"{Numbers.Count(slots)} at a time";

        /// <summary>What a producer makes, with its pace (<see cref="SourceWords.Pace"/>).</summary>
        public static string Makes(string product, string pace) => $"{product}, {pace}";

        /// <summary>What a hive needs: no more of the sky around it covered than this share.</summary>
        public static string OpenSky(float maxCover) => $"open sky, less than {Numbers.Percent(maxCover)} covered";

        /// <summary>What a tap needs: the root it is built on, whose sap it takes until none is left.</summary>
        public static string BuiltOn(string root) => $"to be built on {root}, and takes only the sap it has left";

        /// <summary>What crafting at a station needs, or null for nothing.</summary>
        public static string CraftingNeeds(bool roof, bool fire)
        {
            var needs = new List<string>();
            if (roof) needs.Add(Roof);
            if (fire) needs.Add("a fire");
            return needs.Count > 0 ? string.Join(" and ", needs) : null;
        }

        /// <summary>What an incinerator turns anything without a conversion into, and how many it takes for one.</summary>
        public static string Incinerates(string result, int cost) => $"becomes {result}, one for every {Numbers.Count(cost)}";

        /// <summary>The title of what a shield burns, any one of several where it takes more than one.</summary>
        public static string BurnsAnyOf(bool several) => several ? "Burns any one of these" : "Burns";

        /// <summary>The title of what a trader sells, with when it sells them where a world key holds them back.</summary>
        public static string Sells(string when) => string.IsNullOrEmpty(when) ? "Sells" : "Sells " + when;

        public static string Summons(string boss) => $"Summons {boss} with";

        /// <summary>The title of what is offered at an altar, on its item stands where it takes them there.</summary>
        public static string SummonedAt(string place, bool onStands) => $"Summoned at {place} with" + (onStands ? " these on its item stands" : "");
    }
}
