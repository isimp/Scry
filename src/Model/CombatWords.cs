using System;
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

        private static string Number(float value) => Naming.Number(value);

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

        /// <summary>
        /// How near it must see its target to turn on it. <c>MonsterAI</c> turns alerted on seeing
        /// its target nearer than <c>m_alertRange</c> times the target's stealth factor, which is
        /// below one only while the target sneaks. The field's default of 9999 is no limit, which
        /// sight already tells, so it is not told.
        /// </summary>
        public static string Alerted(float alertRange)
        {
            if (alertRange >= 9000f) return null;
            if (alertRange <= 0f) return "never on sight alone";
            return $"on seeing you within {Number(alertRange)} m, nearer while you sneak";
        }

        /// <summary>
        /// How far it follows. <c>MonsterAI</c> gives up once it is farther than
        /// <c>m_maxChaseDistance</c> from where it spawned and has not seen or heard its target for
        /// a second; none set is no limit.
        /// </summary>
        public static string Chase(float maxChaseDistance)
        {
            return maxChaseDistance > 0f ? $"beyond {Number(maxChaseDistance)} m from where it spawned, once it has lost you" : null;
        }

        /// <summary>
        /// A weak spot's part of the body in plain words, from the name of the object it sits on:
        /// "WEAKSPOT_HEAD" is the head. The game gives weak spots no names of their own.
        /// </summary>
        public static string PartName(string objectName)
        {
            var name = objectName ?? "";
            var copy = name.IndexOf(" (", System.StringComparison.Ordinal);
            if (copy >= 0) name = name.Substring(0, copy);
            const string marker = "weakspot";
            if (name.StartsWith(marker, System.StringComparison.OrdinalIgnoreCase)) name = name.Substring(marker.Length);
            name = string.Join(" ", name.Split(new[] { '_', ' ' }, System.StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            return name.Length > 0 ? name : "weak spot";
        }

        /// <summary>What an attack takes: stamina, eitr, health and a share of health, each only when it takes some.</summary>
        public static List<string> Costs(float stamina, float eitr, float health, float healthPercent)
        {
            var costs = new List<string>();
            if (stamina > 0f) costs.Add($"{Number(stamina)} stamina");
            if (eitr > 0f) costs.Add($"{Number(eitr)} eitr");
            if (health > 0f) costs.Add($"{Number(health)} health");
            if (healthPercent > 0f) costs.Add($"{Number(healthPercent)}% health");
            return costs;
        }

        /// <summary>A weapon's second attack: how much harder its damage, knockback and stagger are than the first's, and what it costs.</summary>
        public static string SecondaryAttack(float damage, float force, float stagger, IReadOnlyList<string> costs)
        {
            var parts = new List<string>();
            if (Math.Abs(damage - 1f) > 0.001f) parts.Add($"\u00d7{Number(damage)} damage");
            if (Math.Abs(force - 1f) > 0.001f) parts.Add($"\u00d7{Number(force)} knockback");
            if (Math.Abs(stagger - 1f) > 0.001f) parts.Add($"\u00d7{Number(stagger)} stagger");
            var line = parts.Count > 0 ? string.Join(", ", parts) : "as hard as the first";
            return costs != null && costs.Count > 0 ? line + "; costs " + string.Join(", ", costs) : line;
        }

        /// <summary>
        /// Resistances as one line, each degree with the damage it applies to, in the order given
        /// (from the most harm taken to the least). A part that resists nothing takes every hit in
        /// full, whatever the rest of the body resists.
        /// </summary>
        public static string Resistances(IEnumerable<(string Words, string[] Types)> byDegree)
        {
            var parts = byDegree.Where(d => d.Types.Length > 0).Select(d => d.Words.ToLowerInvariant() + " " + string.Join(", ", d.Types)).ToList();
            return parts.Count > 0 ? string.Join("; ", parts) : "takes every hit in full";
        }
    }
}
