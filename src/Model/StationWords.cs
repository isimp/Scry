using System;
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
        /// <summary>How long a windmill takes for each, at full wind (<c>Windmill.GetPowerOutput</c> paces its <c>Smelter</c>).</summary>
        public static string InFullWind(float seconds) => Numbers.Duration(seconds) + " in full wind, longer as the wind weakens or is blocked";

        /// <summary>
        /// What a windmill runs on (<c>Windmill.GetPowerOutput</c>): the wind's strength stepped
        /// from its least to full, whichever way the windmill faces, as its turning cap is for show.
        /// </summary>
        public static string RunsOnWind(float least) => $"wind: none below {Numbers.Percent(least)} strength, full at 100%, whichever way it faces";

        /// <summary>
        /// What blocks a windmill's wind (<c>Windmill.CheckCover</c>, the game's cover at the
        /// propeller's hub): 17 rays of 30 m from it, one up, eight level and eight halfway up,
        /// none down, each that hits something solid taking its share.
        /// </summary>
        public const string WindBlockedBy = "anything solid within 30 m of its hub, level with it or above; each of 17 directions blocked takes 6%";

        /// <summary>From what share of power a windmill's blades hurt (<c>Windmill.Update</c>: the propeller's harm is on above its least speed); none where they never turn.</summary>
        public static string BladesHurt(float leastSpeed, float turnSpeed) =>
            turnSpeed == 0f ? null : $"hurt what they touch above {Numbers.Percent(Math.Min(1f, leastSpeed / Math.Abs(turnSpeed)))} power";

        /// <summary>The most time away a smelter makes up for when its ground loads again, in seconds (<c>Smelter.UpdateSmelter</c>).</summary>
        public const float SmelterCatchUp = 3600f;

        /// <summary>How much time away a windmill makes up for when its ground loads again (<c>Smelter.UpdateSmelter</c>), at the wind of that moment.</summary>
        public static string CatchesUp(float seconds) => (seconds == 3600f ? "an hour" : Numbers.Duration(seconds)) + " at most, at the wind blowing on return";

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
