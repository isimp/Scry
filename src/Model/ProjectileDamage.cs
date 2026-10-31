using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What projectiles' and lingering areas' own damage puts on what they hit, as the catalog is
    /// read: a projectile an attack fires takes the attack's damage instead (<c>Projectile.Setup</c>
    /// with the attack's hit, <c>Attack.FireProjectileBurst</c>), which the attack's item tells, and
    /// so does a fired area using the attack's settings (<c>Aoe.Setup</c>); one spawned any other
    /// way, with no hit given, deals its own, as Yagluth's meteors fall, and so does an area keeping
    /// its own settings or lying below the prefab, which the hit never reaches. Told once every
    /// prefab is read, as a projectile and the item firing it come in any order.
    /// </summary>
    internal sealed class ProjectileDamage
    {
        /// <summary>Notes a projectile's or area's own damage, each type with its amount; one that keeps it even when an attack fires the prefab, as an area keeping its own settings.</summary>
        public void Own(string prefab, IEnumerable<(string Type, float Amount)> damage, bool keepsOwn = false)
        {
            foreach (var (type, amount) in damage)
            {
                if (amount <= 0f) continue;
                var key = (prefab, type);
                _own[key] = keepsOwn || _own.TryGetValue(key, out var kept) && kept;
            }
        }

        /// <summary>Notes a projectile an attack fires.</summary>
        public void Fired(string prefab) => _fired.Add(prefab);

        /// <summary>Each effect a projectile not fired by an attack gives, with the note of the damage giving it (<see cref="CombatWords.OfDamage"/>).</summary>
        public IEnumerable<(string Prefab, string Effect, string Note)> Gives()
        {
            foreach (var pair in _own)
            {
                var (prefab, type) = pair.Key;
                if (!pair.Value && _fired.Contains(prefab)) continue;
                foreach (var (damage, effect) in CombatWords.DamageEffects) if (damage == type) yield return (prefab, effect, CombatWords.OfDamage(type));
            }
        }

        /// <summary>Forgets what was noted.</summary>
        public void Clear()
        {
            _own.Clear();
            _fired.Clear();
        }

        /// <summary>Each prefab's own damage by type, and whether it keeps it when fired.</summary>
        private readonly Dictionary<(string Prefab, string Type), bool> _own = new Dictionary<(string, string), bool>();
        private readonly HashSet<string> _fired = new HashSet<string>(System.StringComparer.Ordinal);
    }
}
