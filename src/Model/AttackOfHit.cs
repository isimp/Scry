using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What marks an attack's hit, as the game fills a hit from the weapon (<c>Attack</c>; a
    /// projectile copies them from it, <c>Projectile.Setup</c>): whether it flew, which a
    /// projectile or an area hit is and a swing is not (<c>HitData.m_ranged</c>), its damage
    /// types, and its status effect by name hash, 0 for none.
    /// </summary>
    internal readonly struct AttackMarks
    {
        public readonly string[] Types;
        public readonly bool Ranged;
        public readonly int Status;

        public AttackMarks(string[] types, bool ranged, int status)
        {
            Types = types ?? Array.Empty<string>();
            Ranged = ranged;
            Status = status;
        }

        /// <summary>Whether a hit could have come from this attack: flown as it flies, its damage types among the attack's, its status effect the attack's where it carries one.</summary>
        public bool Fits(AttackMarks hit)
        {
            if (hit.Ranged != Ranged) return false;
            if (hit.Status != 0 && hit.Status != Status) return false;
            foreach (var type in hit.Types) if (Array.IndexOf(Types, type) < 0) return false;
            return true;
        }
    }

    /// <summary>
    /// The attack a hit taken came from, of a creature's weapons, by the hit's own marks
    /// (<see cref="AttackMarks"/>). The weapon the creature holds when the hit lands may be another
    /// by then: a projectile flies up to three seconds at its weapon's full reach (a Gjall's spit
    /// 2 s, a Dvergr mage's cluster bomb 2.7 s), and a creature picks its weapon again once a
    /// second when its attack is over (<c>MonsterAI</c>). So the one weapon the marks fit names it;
    /// of several, the one held; where none fits, as an area an attack leaves behind deals damage
    /// of its own, the one held, as before.
    /// </summary>
    internal static class AttackOfHit
    {
        public static string Pick(IReadOnlyList<(string Item, AttackMarks[] Attacks)> weapons, AttackMarks hit, string held)
        {
            if (weapons == null || weapons.Count == 0) return held;
            string only = null;
            var fitting = 0;
            var heldFits = false;
            foreach (var (item, attacks) in weapons)
            {
                if (attacks == null || !Array.Exists(attacks, a => a.Fits(hit))) continue;
                fitting++;
                only = item;
                if (item == held) heldFits = true;
            }
            if (fitting == 1) return only;
            if (fitting == 0) return held;
            return heldFits ? held : null;
        }
    }
}
