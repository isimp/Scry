using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for what is shown standing where the game has it, its ground chosen automatically.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// With the ground chosen automatically, what spawns or grows nowhere of its own stands
        /// where the game has it: a Karve afloat on the sea, held at the height the game's push
        /// up holds it, its hull through the surface; a Charred warlock, which only a fortress's
        /// spawn points bring, in the Ashlands; an iron sword in the Swamp, its tier's biome; a
        /// goblin raid on the Plains, where its creatures live. Each is pictured.
        /// </summary>
        private static IEnumerator ThingsStandWhereTheGameHasThem(Probe p)
        {
            OnGroundChosenAutomatically();

            var karve = Pick(Kind.Piece, "Karve");
            if (karve != null)
            {
                Select(karve);
                yield return Until(() => CopyOf(karve) != null && Stage.WaterShown, 20);
                yield return new Wait(1f);
                var (held, shown) = Stage.SeaHolds;
                var surface = Stage.SeaLevels.Surface;
                var copy = CopyOf(karve);
                var bounds = copy != null ? copy.GetComponentsInChildren<Renderer>().Where(r => r.enabled).Select(r => r.bounds).ToList() : null;
                var low = bounds != null && bounds.Count > 0 ? bounds.Min(b => b.min.y) : float.NaN;
                var high = bounds != null && bounds.Count > 0 ? bounds.Max(b => b.max.y) : float.NaN;
                p.Check(Stage.GroundUnderfoot == "Ocean" && held == SeaHeld.AtSurface && shown, "a Karve stands on the sea, held at its surface",
                    $"on {Stage.GroundUnderfoot ?? "no ground"}, held {held}, sea shown {shown}");
                // A copy of the test's own, held still far off, read after the physics has stepped a few times: its centre of mass as Unity has it worked out.
                var probe = karve.Source is GameObject prefab && prefab != null ? Ghost.Make(prefab, null, new Vector3(0f, -10000f, 0f), Quaternion.identity, falling: true) : null;
                var probeBody = probe != null ? probe.GetComponent<Rigidbody>() : null;
                if (probeBody != null) probeBody.isKinematic = true;
                yield return new Wait(0.2f);
                var stepped = probeBody != null ? probeBody.centerOfMass.y : float.NaN;
                if (probe != null) Object.Destroy(probe);
                var read = Stage.ShownCentreOfMass;
                p.Check(!float.IsNaN(read) && !float.IsNaN(stepped) && Mathf.Abs(read - stepped) < 0.02f, "its centre of mass is worked out from its hull, as the physics has it",
                    $"{Numbers.Fixed(read, 2)} m over its root on the stage, {Numbers.Fixed(stepped, 2)} m on a copy the physics stepped");
                p.Check(low < surface && high > surface, "its hull goes through the surface, neither sunk nor lifted clear",
                    $"from {Numbers.Fixed(low - surface, 2)} m to {Numbers.Fixed(high - surface, 2)} m against the surface");
                yield return Picture(p, "a Karve afloat on the sea");
            }
            else p.Note("there is no Karve to show");

            foreach (var (kind, name, biome, why) in new[]
            {
                (Kind.Creature, "Charred_Mage", "AshLands", "a creature only a place's spawn points bring stands in that place's biome"),
                (Kind.Item, "SwordIron", "Swamp", "a crafted item stands in the biome of its tier"),
                (Kind.Raid, "army_goblin", "Plains", "a raid stands where its creatures live"),
            })
            {
                var entry = Pick(kind, name);
                if (entry == null)
                {
                    p.Note($"there is no {name} to show");
                    continue;
                }
                // The item shown before any tiers are worked out, as in a fresh session with no
                // milestone floor: shown once, it comes to its tier's biome as they are, nothing
                // else asking for them.
                if (kind == Kind.Item)
                {
                    CompendiumTiers.ForgetForTest();
                    var floor = System.Math.Min(CompendiumKeeper.Book?.Floor ?? 0, CompendiumKeeper.Ceiling);
                    if (floor > 0) p.Note($"the milestone floor is {Numbers.Count(floor)}, which works the tiers out too, so this does not show the stage working them out itself");
                }
                Select(entry);
                // Tiers are worked out on a worker; the ground follows once they are in, laid as the stage next films.
                yield return Until(() => CopyOf(entry) != null && Stage.HomeShown == biome && Stage.GroundUnderfoot == biome, 20);
                p.Check(Stage.HomeShown == biome && Stage.GroundUnderfoot == biome, $"{why}: {entry.Name} on {biome}",
                    $"home {Stage.HomeShown}, ground {Stage.GroundUnderfoot ?? "none"}");
                yield return Picture(p, $"{entry.Name} where it stands");
            }
        }
    }
}
