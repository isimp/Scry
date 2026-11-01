using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What a station makes, learned by feeding it and holding what it makes: once the character
    /// fed an input into a kind of station, holding the product that kind makes of it, wherever
    /// and whenever it came by it, pairs the station with the product, given by it, and the input
    /// with the product. A station never fed that input shows nothing.
    /// </summary>
    internal static class FedThenHeld
    {
        /// <summary>
        /// The pairs a product held makes, of the conversions making it (each station and the input
        /// it turns into it) and whether the character fed that input into that kind of station:
        /// each pair, and whether it goes as given by the station.
        /// </summary>
        public static IEnumerable<(string A, string B, bool Given)> Pairs(string product, IEnumerable<(string Station, string Input)> makers, Func<string, string, bool> fed)
        {
            if (string.IsNullOrEmpty(product) || makers == null || fed == null) yield break;
            foreach (var (station, input) in makers)
            {
                if (!fed(station, input)) continue;
                yield return (station, product, true);
                yield return (input, product, false);
            }
        }
    }
}
