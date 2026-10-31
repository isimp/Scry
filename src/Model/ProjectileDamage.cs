using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What projectiles' own damage puts on what they hit, as the catalog is read: a projectile an
    /// attack fires takes the attack's damage instead (<c>Projectile.Setup</c> with the attack's
    /// hit, <c>Attack.FireProjectileBurst</c>), which the attack's item tells; one spawned any
    /// other way, with no hit given, deals its own, as Yagluth's meteors fall. Told once every
    /// prefab is read, as a projectile and the item firing it come in any order.
    /// </summary>
    internal sealed class ProjectileDamage
    {
        /// <summary>Notes a projectile's own damage, each type with its amount.</summary>
        public void Own(string prefab, IEnumerable<(string Type, float Amount)> damage)
        {
            foreach (var (type, amount) in damage) if (amount > 0f) _own.Add((prefab, type));
        }

        /// <summary>Notes a projectile an attack fires.</summary>
        public void Fired(string prefab) => _fired.Add(prefab);

        /// <summary>Each effect a projectile not fired by an attack gives, with the note of the damage giving it (<see cref="CombatWords.OfDamage"/>).</summary>
        public IEnumerable<(string Prefab, string Effect, string Note)> Gives()
        {
            foreach (var (prefab, type) in _own)
            {
                if (_fired.Contains(prefab)) continue;
                foreach (var (damage, effect) in CombatWords.DamageEffects) if (damage == type) yield return (prefab, effect, CombatWords.OfDamage(type));
            }
        }

        /// <summary>Forgets what was noted.</summary>
        public void Clear()
        {
            _own.Clear();
            _fired.Clear();
        }

        private readonly List<(string Prefab, string Type)> _own = new List<(string, string)>();
        private readonly HashSet<string> _fired = new HashSet<string>(System.StringComparer.Ordinal);
    }
}
