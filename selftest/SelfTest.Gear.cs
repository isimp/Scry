using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's cases for a creature's gear: a Dvergr mage's staff, hung on it as armour, and its robes, each a look of the mage.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// A Dvergr fire mage in its gear holds its staff in its right hand, as the game hangs it:
        /// the staff is a utility item, hung by its "attach_r.hand" part on that bone, not held as
        /// a weapon. Shown peeking, so the stage puts on all the mage carries; it is pictured. The
        /// staff is not offered to try on the person, whose hand bone has another name.
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
            // The person's hand bone is named otherwise, so the staff would show nothing on it.
            var staff = GamePrefabs.Item("DvergerStaffFire");
            p.Check(staff != null && !PrefabGear.IsWearable(staff), "the staff is not offered to try on the person, whose right hand bone is not the mage's r.hand", staff == null ? "no staff" : "offered");
            yield return Settled();
            yield return Picture(p, "a Dvergr fire mage holding its staff");
            MageAsItWas();
        }

        /// <summary>
        /// The Dvergr mage's fire, ice and support kinds, which the world places nowhere and which
        /// wear one of the sets the mage rolls, are that look of the mage: its page switches to
        /// none of them, and each one's own page says it is met as the mage in that gear. The
        /// mage's attacks table follows the look staged: in its first set it lists that set's
        /// attacks, a faint line counting the others, and at no gear every one. Pictured.
        /// </summary>
        private static IEnumerator MageRobesAreLooks(Probe p)
        {
            var mage = Pick(Kind.Creature, "DvergerMage");
            var kinds = MageKinds.Select(n => Pick(Kind.Creature, n)).ToList();
            if (mage == null || kinds.Any(k => k == null) || !(mage.Source is GameObject prefab) || prefab == null)
            {
                p.Skip("there is no Dvergr mage with its fire, ice and support kinds");
                yield break;
            }
            var family = mage.Family;
            var told = kinds.Select(k => family?.WornAs(k) ?? (null, 0)).ToList();
            var asSets = kinds.Select((k, i) => told[i].Base == mage && new System.Collections.Generic.HashSet<string>(PrefabGear.SetItems(prefab, told[i].Look)).SetEquals(k.GivenGear)).ToList();
            p.Check(asSets.All(b => b), "each of the mage's fire, ice and support kinds is the mage's look whose set it wears",
                string.Join("; ", kinds.Select((k, i) => $"{k.Name}: {told[i].Base?.Name ?? "none"} look {Numbers.Count(told[i].Look)}")));
            p.Check(family != null && family.Met(null).All(m => !kinds.Contains(m)), "the mage's page switches to none of them", family == null ? "no family" : string.Join(", ", family.Met(null).Select(m => m.Name)));
            var where = Facts.For(kinds[0]).Where;
            p.Check(where.Any(s => s.Prefab == mage.Name), "the fire kind's own page says it is met as the mage in that gear", string.Join(" | ", where.Select(s => s.Text)));

            // The stage at its own size, so the page's fight stands in view under it.
            Select(mage);
            yield return Until(() => CopyOf(mage) != null, 20);
            X.Modifiers.Look = 1;
            yield return GoTo("fight");
            var others = ScryPanel.Drawn(PanelPart.AttacksOtherGear);
            yield return Until(() => ScryPanel.Drawn(PanelPart.AttacksOtherGear) > others, 5);
            p.Check(ScryPanel.Drawn(PanelPart.AttacksOtherGear) > others, "in its first set the mage's attacks table lists that set's attacks, a faint line counting the others");
            yield return Settled();
            yield return PictureOf(p, "adjust", "the Dvergr mage's looks, its robes first and no gear last");
            yield return PictureOf(p, "fight", "the Dvergr mage's attacks in its first set");
            X.Modifiers.Look = 0;
            yield return Settled();
            yield return PictureOf(p, "fight", "the Dvergr mage's attacks at no gear");
            others = ScryPanel.Drawn(PanelPart.AttacksOtherGear);
            for (var i = 0; i < 10; i++) yield return null;
            p.Check(ScryPanel.Drawn(PanelPart.AttacksOtherGear) == others, "at no gear it lists every attack, no line counting others");
            MageAsItWas();
        }

        /// <summary>The Dvergr mage's kinds each given one of its robes.</summary>
        private static readonly string[] MageKinds = { "DvergerMageFire", "DvergerMageIce", "DvergerMageSupport" };

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
