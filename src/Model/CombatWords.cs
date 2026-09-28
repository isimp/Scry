using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A creature's fight in words: what its stars add (health the base once more for each,
    /// <c>Character.SetupMaxHealth</c>; every hit half as much again, <c>Attack.GetLevelDamageFactor</c>),
    /// what its hits do, and each attack's reach and pace as its AI uses it.
    /// </summary>
    public static class CombatWords
    {
        /// <summary>Stars in figures, as the Adjust choices and the spawn lines count them.</summary>
        private static string Stars(int stars) => stars.ToString(CultureInfo.InvariantCulture) + (stars == 1 ? " star" : " stars");

        private static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>Health at each star up to the highest, or null without stars.</summary>
        public static string StarHealth(float health, int maxStars)
        {
            if (maxStars <= 0) return null;
            return string.Join(", ", Enumerable.Range(1, maxStars).Select(s => $"{Stars(s)} {Number(health * (s + 1))}"));
        }

        /// <summary>How much harder it hits at each star up to the highest, or null without stars.</summary>
        public static string StarDamage(int maxStars)
        {
            if (maxStars <= 0) return null;
            return string.Join(", ", Enumerable.Range(1, maxStars).Select(s => $"{Stars(s)} ×{Number(1f + s * 0.5f)}"));
        }

        /// <summary>The damage of a hit by type, the biggest first; null when it does none.</summary>
        public static string Damage(IEnumerable<(string Type, float Amount)> damage)
        {
            var parts = damage.Where(d => d.Amount > 0f).OrderByDescending(d => d.Amount).Select(d => $"{Number(d.Amount)} {d.Type}").ToList();
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>
        /// An attack: what it does, how (a swing, a shot, around it), from how near and how far
        /// its AI uses it, and how often. Parts it has nothing for are left out.
        /// </summary>
        public static string Attack(string damage, string attackType, float rangeMin, float range, float interval)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(damage)) parts.Add(damage);

            string how;
            switch (attackType)
            {
                case "Horizontal":
                case "Vertical":
                    how = "a swing";
                    break;
                case "Projectile":
                case "TriggerProjectile":
                    how = "a shot";
                    break;
                case "Area":
                    how = "around it";
                    break;
                default:
                    how = "an attack";
                    break;
            }
            if (range > 0f) how += rangeMin > 0f ? $", from {Number(rangeMin)} to {Number(range)} m" : $", reaching {Number(range)} m";
            parts.Add(how);

            if (interval > 0f) parts.Add($"every {Number(interval)} s");
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// How far and how wide it sees. <c>BaseAI.CanSeeTarget</c> turns away a target more than
        /// <c>m_viewAngle</c> off its forward on either side, so the field is twice that angle, and
        /// only while it is not alerted; once alerted it sees all round.
        /// </summary>
        public static string Sight(float range, float halfAngle)
        {
            var field = halfAngle * 2f;
            return field >= 360f ? $"{Number(range)} m, all round" : $"{Number(range)} m, {Number(field)}° ahead, all round once alerted";
        }
    }
}
