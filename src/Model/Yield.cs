using System;

namespace Scry
{
    /// <summary>
    /// How much something picked gives over time, from how many it gives and how long it takes to
    /// grow back, in the game's days (<c>EnvMan.m_dayLengthSec</c>): so many a day, or so many
    /// every so many days.
    /// </summary>
    internal static class Yield
    {
        /// <summary>The words for it, or null when it never grows back.</summary>
        public static string PerDay(int amount, float respawnMinutes, float daySeconds)
        {
            if (amount <= 0 || respawnMinutes <= 0f || daySeconds <= 0f) return null;
            var respawnSeconds = respawnMinutes * 60f;
            if (respawnSeconds <= daySeconds)
            {
                var perDay = (int)Math.Round(amount * daySeconds / respawnSeconds);
                return $"about {Numbers.Count(perDay)} a day";
            }
            var days = Math.Round(respawnSeconds / daySeconds * 2.0) / 2.0;
            return $"{Numbers.Count(amount)} every {Numbers.Amount(days, 1)} days";
        }
    }
}
