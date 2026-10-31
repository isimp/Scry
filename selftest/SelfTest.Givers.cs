using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for what gives an effect by its damage, as the game's prefabs hold it.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// An effect's *Given by* holds what deals its damage: Yagluth for Lightning, by his beam,
        /// an attack item of his alone and no entry, not the beam's shot; the meteor his meteors
        /// attack drops for Burning (its own damage, as nothing gives it an attack's), not Ahri's
        /// orb (the staff's shot, dealing the staff's damage); the meteor leads back to Yagluth
        /// within the steps the compendium follows; a Writhan's death
        /// blast is its own; a fire pit by the flames lying below it, the thunderblood staff's
        /// blast by the area below its top, and a Dverger's frost nova by its own damage though a
        /// staff fires it, as it keeps its own settings.
        /// </summary>
        private static IEnumerator GiversByDamage(Probe p)
        {
            var lightning = Pick(Kind.StatusEffect, "Lightning");
            var burning = Pick(Kind.StatusEffect, "Burning");
            if (lightning == null || burning == null)
            {
                p.Skip("Lightning or Burning is not in this world's catalog");
                yield break;
            }
            HashSet<string> GivenBy(Entry effect) => new HashSet<string>(effect.Links.Where(l => l.Group == LinkWords.GivenBy && l.Notes.Any(CombatWords.IsDamageNote)).Select(l => l.Target));
            var shocks = GivenBy(lightning);
            var burns = GivenBy(burning);
            // His beam is no entry of its own, being in the game's item list but not the scene's: he gives what it deals.
            p.Check(shocks.Contains("GoblinKing") && burns.Contains("GoblinKing"), "Lightning and Burning are given by Yagluth, by his beam's damage", string.Join(", ", shocks));
            if (X.Catalog.Any(e => e.Name == "GoblinKing_Beam")) p.Note("Yagluth's beam is an entry of its own in this world");
            p.Check(!shocks.Contains("projectile_beam"), "not by the beam's shot, which deals the beam's damage");
            p.Check(burns.Contains("projectile_meteor"), "Burning is given by a falling meteor, by its own damage", string.Join(", ", burns.Take(12)));
            p.Check(!burns.Contains("staff_OrbofAhri_projectile"), "not by Ahri's orb, the staff's shot dealing the staff's damage");

            // Back from the meteor to Yagluth, whose meteors attack drops it, as the compendium follows it.
            var meteor = Pick(Kind.Projectile, "projectile_meteor") ?? X.Catalog.FirstOrDefault(e => e.Name == "projectile_meteor");
            var path = new List<string>();
            bool Leads(Entry from, int steps)
            {
                if (from == null) return false;
                if (from.Name == "GoblinKing" && from.Kind == Kind.Creature) return true;
                if (steps == 0) return false;
                foreach (var link in from.Links.Where(l => l.Group == LinkWords.SpawnedBy))
                {
                    var by = X.Catalog.FirstOrDefault(e => e.Key == link.Target);
                    if (by == null || by == from || !Leads(by, steps - 1)) continue;
                    path.Add(by.Name);
                    return true;
                }
                return false;
            }
            p.Check(Leads(meteor, 3), "the meteor leads back to Yagluth within three steps", meteor == null ? "no meteor" : string.Join(" <- ", path.AsEnumerable().Reverse()));

            var writhan = Pick(Kind.Creature, "Writhan");
            if (writhan != null) p.Check(writhan.DeathBlast == "writhan_explosion", "a Writhan's death blast is its own", writhan.DeathBlast ?? "none");

            p.Check(burns.Contains("fire_pit"), "Burning is given by a fire pit, by the flames below it");
            p.Check(shocks.Contains("staff_thunderblood_aoe"), "Lightning is given by the thunderblood staff's blast, by the area below its top");
            var frost = Pick(Kind.StatusEffect, "Frost");
            if (frost != null) p.Check(GivenBy(frost).Contains("DvergerStaffNova_aoe"), "Frost is given by a Dverger's frost nova, its own damage kept though a staff fires it");
            var pieces = burns.Count(b => X.Catalog.FirstOrDefault(e => e.Key == b)?.Kind == Kind.Piece);
            p.Note($"Burning given by {Numbers.Count(pieces)} pieces");
            p.Note($"Lightning given by {Numbers.Count(shocks.Count)} by damage, Burning by {Numbers.Count(burns.Count)}");
        }
    }
}
