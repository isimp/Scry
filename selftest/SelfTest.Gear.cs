using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for gear the game hangs on a creature as armour: a Dvergr mage's staff.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// A Dvergr fire mage in its gear holds its staff in its right hand, as the game hangs it:
        /// the staff is a utility item, hung by its "attach_r.hand" part on that bone, not held as
        /// a weapon. Shown peeking, so the stage puts on all the mage carries; it is pictured.
        /// </summary>
        private static IEnumerator MageHoldsItsStaff(Probe p)
        {
            var mage = X.Catalog.FirstOrDefault(e => e.Source is GameObject prefab && prefab != null && prefab.name == "DvergerMageFire");
            if (mage == null) p.Skip("there is no Dvergr fire mage");
            BigStage();
            _peekingWas = X.Peeking;
            X.Peeking = true;
            Select(mage);
            yield return Until(() => CopyOf(mage) != null, 20);
            // Its one set is its first look after no gear.
            X.Modifiers.Look = 1;
            yield return Until(() => StaffIn(CopyOf(mage)) != null, 10);
            var copy = CopyOf(mage);
            var hand = copy != null ? copy.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "r.hand") : null;
            p.Check(StaffIn(copy) != null, "a Dvergr fire mage in its gear holds its staff in its right hand, the utility item hung there by its attach_r.hand part",
                hand == null ? "no right hand on the copy" : $"under the right hand: {string.Join(", ", hand.Cast<Transform>().Select(t => t.name))}");
            yield return Settled();
            yield return Picture(p, "a Dvergr fire mage holding its staff");
            MageAsItWas();
        }

        private static bool? _peekingWas;

        /// <summary>Stops peeking if the mage's case started it, and puts the stage's size back (<see cref="StageAsItWas"/>).</summary>
        private static void MageAsItWas()
        {
            if (_peekingWas is bool peeking) X.Peeking = peeking;
            _peekingWas = null;
            X.Modifiers.Reset();
            StageAsItWas();
        }

        /// <summary>The part hung on a copy's right hand that draws something, as a staff hung there does; none on no copy.</summary>
        private static Hung StaffIn(GameObject copy)
        {
            if (copy == null) return null;
            return copy.GetComponentsInChildren<Hung>(true).FirstOrDefault(h => h.transform.parent != null && h.transform.parent.name == "r.hand" && h.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled));
        }
    }
}
