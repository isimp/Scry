using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A creature's fight in words: what its stars add (health the base once more for each,
    /// <c>Character.SetupMaxHealth</c>; every hit half as much again, <c>Attack.GetLevelDamageFactor</c>),
    /// what its hits do, each attack's reach and pace as its AI uses it, how it moves, what it
    /// senses, when it gives up or flees, and where it is weak; and what a weapon's attacks cost.
    /// </summary>
    internal static class CombatWords
    {
        /// <summary>The status effect each kind of damage puts on what it hits, as <c>Character</c> adds them.</summary>
        public static readonly IReadOnlyList<(string Damage, string Effect)> DamageEffects = new[]
        {
            ("fire", "Burning"), ("frost", "Frost"), ("lightning", "Lightning"), ("poison", "Poison"), ("spirit", "Spirit"),
        };

        /// <summary>Stars in figures, as the Adjust choices and the spawn lines count them.</summary>
        private static string Stars(int stars) => Naming.Count(stars, "star", "stars");

        /// <summary>A choice of stars to show a creature with.</summary>
        public static string StarChoice(int stars) => stars == 0 ? "No stars" : Stars(stars);

        /// <summary>Health at each star up to the highest, or null without stars.</summary>
        public static string StarHealth(float health, int maxStars)
        {
            if (maxStars <= 0) return null;
            return string.Join(", ", Enumerable.Range(1, maxStars).Select(s => $"{Stars(s)} {Numbers.Amount(health * (s + 1))}"));
        }

        /// <summary>How much harder it hits at each star up to the highest, or null without stars.</summary>
        public static string StarDamage(int maxStars)
        {
            if (maxStars <= 0) return null;
            return string.Join(", ", Enumerable.Range(1, maxStars).Select(s => $"{Stars(s)} {Numbers.Times(1f + s * 0.5f)}"));
        }

        /// <summary>The damage of a hit by type, the biggest first; null when it does none.</summary>
        public static string Damage(IEnumerable<(string Type, float Amount)> damage)
        {
            var parts = damage.Where(d => d.Amount > 0f).OrderByDescending(d => d.Amount).Select(d => $"{Numbers.Amount(d.Amount)} {d.Type}").ToList();
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>The title of a creature's attacks table.</summary>
        public const string AttacksTitle = "Attacks";

        /// <summary>The columns of a creature's attacks table.</summary>
        public static readonly string[] AttackColumns = { "Attack", "Damage", "How", "Reach", "Every" };

        /// <summary>
        /// An attack as a line of its table: the item it is made with, what it does, how (a
        /// swing, a shot, around it), from how near to how far its AI uses it, and how often; a
        /// dash where it tells none.
        /// </summary>
        public static string[] AttackCells(string name, string damage, string attackType, float rangeMin, float range, float interval)
        {
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
            var reach = range <= 0f ? None : rangeMin > 0f ? $"{Numbers.Amount(rangeMin)}–{Numbers.Amount(range)} m" : $"{Numbers.Amount(range)} m";
            var every = interval > 0f ? $"{Numbers.Amount(interval)} s" : None;
            return new[] { name, string.IsNullOrEmpty(damage) ? None : damage, how, reach, every };
        }

        /// <summary>What a table's cell says where it has nothing to tell.</summary>
        private const string None = "–";

        /// <summary>How fast it moves: flying, from its slow to its fast speed, else walking and running; and swimming where it swims.</summary>
        public static string Moves(bool flying, float flySlow, float flyFast, float walk, float run, bool swims, float swim)
        {
            var moves = new List<string>();
            if (flying) moves.Add($"flies {Numbers.Amount(flySlow)}–{Numbers.Amount(flyFast)} m/s");
            else moves.Add($"walks {Numbers.Amount(walk)} m/s, runs {Numbers.Amount(run)} m/s");
            if (swims) moves.Add($"swims {Numbers.Amount(swim)} m/s");
            return string.Join(", ", moves);
        }

        /// <summary>How far it hears; the field's default of 9,999 is no limit, and is not told.</summary>
        public static string Hears(float range) => range >= 9000f ? null : Numbers.Metres(range);

        /// <summary>When it flees: below this share of its health, right after it is hurt (<c>MonsterAI.m_fleeIfLowHealth</c>); null for never.</summary>
        public static string Flees(float healthShare) => healthShare > 0f ? $"below {Numbers.Percent(healthShare)} health, right after being hurt" : null;

        /// <summary>
        /// How far and how wide it sees. <c>BaseAI.CanSeeTarget</c> turns away a target more than
        /// <c>m_viewAngle</c> off its forward on either side, so the field is twice that angle, and
        /// only while it is not alerted; once alerted it sees all round.
        /// </summary>
        public static string Sight(float range, float halfAngle)
        {
            var field = halfAngle * 2f;
            return field >= 360f ? $"{Numbers.Amount(range)} m, all round" : $"{Numbers.Amount(range)} m, {Numbers.Amount(field)}° ahead, all round once alerted";
        }

        /// <summary>
        /// How near it must see its target to turn on it. <c>MonsterAI</c> turns alerted on seeing
        /// its target nearer than <c>m_alertRange</c> times the target's stealth factor, which is
        /// below one only while the target sneaks. The field's default of 9,999 is no limit, which
        /// sight already tells, so it is not told.
        /// </summary>
        public static string Alerted(float alertRange)
        {
            if (alertRange >= 9000f) return null;
            if (alertRange <= 0f) return "never on sight alone";
            return $"on seeing you within {Numbers.Amount(alertRange)} m, nearer while you sneak";
        }

        /// <summary>
        /// How far it follows. <c>MonsterAI</c> gives up once it is farther than
        /// <c>m_maxChaseDistance</c> from where it spawned and has not seen or heard its target for
        /// a second; none set is no limit.
        /// </summary>
        public static string Chase(float maxChaseDistance)
        {
            return maxChaseDistance > 0f ? $"beyond {Numbers.Amount(maxChaseDistance)} m from where it spawned, once it has lost you" : null;
        }

        /// <summary>
        /// What a creature does in a world with the setting Passive enemies on, where the game
        /// lets no creature sense anyone until provoked (<c>BaseAI.CanSenseTarget</c>) unless it
        /// is marked <c>m_passiveAggresive</c>: such a one attacks as ever, or, an animal, flees.
        /// </summary>
        public static string PassiveEnemies(bool flees) => flees ? "flees from you all the same" : "attacks unprovoked all the same";

        /// <summary>What a prefab's name is split into words at.</summary>
        private static readonly char[] NameParts = { '_', ' ' };

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
            name = string.Join(" ", name.Split(NameParts, System.StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            return name.Length > 0 ? name : "weak spot";
        }

        /// <summary>The label of what a hit on a weak spot does, by the part of the body it sits on (<see cref="PartName"/>).</summary>
        public static string WeakSpot(string objectName) => "Hit on the " + PartName(objectName);

        /// <summary>What an attack takes: stamina, eitr, health and a share of health, each only when it takes some; null for nothing.</summary>
        public static string Costs(float stamina, float eitr, float health, float healthPercent)
        {
            var costs = new List<string>();
            if (stamina > 0f) costs.Add($"{Numbers.Amount(stamina)} stamina");
            if (eitr > 0f) costs.Add($"{Numbers.Amount(eitr)} eitr");
            if (health > 0f) costs.Add($"{Numbers.Amount(health)} health");
            if (healthPercent > 0f) costs.Add($"{Numbers.Amount(healthPercent)}% health");
            return costs.Count > 0 ? string.Join(", ", costs) : null;
        }

        /// <summary>What drawing a bow takes while it is drawn.</summary>
        public static string DrawCost(float staminaPerSecond) => $"{Numbers.Amount(staminaPerSecond)} stamina a second";

        /// <summary>A kind of damage by its type, as a link to the status effect it puts on names it: "fire damage".</summary>
        public static string OfDamage(string damageType) => damageType + " damage";

        /// <summary>The label for the status effect a kind of damage puts on what it hits (<see cref="DamageEffects"/>).</summary>
        public static string DamageCauses(string damageType) => Naming.FieldLabel(damageType) + " damage causes";

        /// <summary>A weapon's second attack: how much harder its damage, knockback and stagger are than the first's, and what it costs (<see cref="Costs"/>).</summary>
        public static string SecondaryAttack(float damage, float force, float stagger, string costs)
        {
            var parts = new List<string>();
            if (Math.Abs(damage - 1f) > 0.001f) parts.Add($"{Numbers.Times(damage)} damage");
            if (Math.Abs(force - 1f) > 0.001f) parts.Add($"{Numbers.Times(force)} knockback");
            if (Math.Abs(stagger - 1f) > 0.001f) parts.Add($"{Numbers.Times(stagger)} stagger");
            var line = parts.Count > 0 ? string.Join(", ", parts) : "as hard as the first";
            return string.IsNullOrEmpty(costs) ? line : line + "; costs " + costs;
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
