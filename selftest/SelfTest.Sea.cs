using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for the sea on the ocean's ground holding what the game holds in it.</summary>
    internal static partial class SelfTest
    {
        private static (int Backdrop, bool Spin, string Choice)? _seaBefore;

        /// <summary>The creatures the game starts flying whose animators land them when not told: a Drake and Moder.</summary>
        private static readonly string[] FlyerNames = { "Hatchling", "Dragon" };

        /// <summary>
        /// On the ocean's ground a Serpent swims its swim depth under the surface, its animator told
        /// it is in water so it swims rather than lies stranded, its tail bent as the game bends it;
        /// a Leviathan rides the surface over a sea as deep as the world places it, its floor clear
        /// under its body; a workbench stands in the shallows; and the camera looks at each from over
        /// the surface, kept over it as the game keeps its own when turned to look from below. Each
        /// is pictured, and the backdrop, spin and ground put back.
        /// </summary>
        private static IEnumerator SeaHolds(Probe p)
        {
            var serpent = X.Catalog.FirstOrDefault(e => e.Name == "Serpent" && e.Source is GameObject);
            var leviathan = X.Catalog.FirstOrDefault(e => e.Name == "Leviathan" && e.Source is GameObject);
            var bench = X.Catalog.FirstOrDefault(e => e.Name == "piece_workbench" && e.Source is GameObject);
            if (serpent == null && leviathan == null) p.Skip("there is neither a Serpent nor a Leviathan to show");
            _seaBefore = (Stage.BackdropIndex, Stage.Spin, Stage.GroundChoice);
            Stage.Spin = false;
            Stage.GroundChoice = "Auto";
            Stage.BackdropIndex = 5;

            if (serpent != null)
            {
                Select(serpent);
                yield return Until(() => CopyOf(serpent) != null && Stage.WaterShown, 20);
                yield return new Wait(1.5f);
                var swimDepth = (serpent.Source as GameObject).GetComponent<Character>().m_swimDepth;
                var (held, shown) = Stage.SeaHolds;
                p.Check(held == SeaHeld.Swimming && shown, "a Serpent swims in the sea on its own ground", $"{held}, sea {(shown ? "shown" : "not shown")}, ground {Stage.GroundShown}");
                var (surface, floor) = Stage.SeaLevels;
                var root = CopyOf(serpent).transform.position.y;
                p.Check(Mathf.Abs(surface - root - swimDepth) < 0.05f, "its root lies its swim depth under the surface, as the game holds it",
                    $"{Numbers.Fixed(surface - root, 2)} m, its swim depth {Numbers.Fixed(swimDepth, 2)} m");
                p.Check(surface - floor >= SeaHold.DeepLook - 0.01f, "the sea under it is deep enough for the game's water to look deep", $"{Numbers.Fixed(surface - floor, 1)} m");
                p.Note($"the world places it in a sea at least {(Knowledge.SeaDepth("Serpent") is float d ? Numbers.Fixed(d, 1) + " m" : "of any")} deep");
                var animator = CopyOf(serpent).GetComponentsInChildren<Animator>().FirstOrDefault(a => a.parameters.Any(x => x.name == "inWater"));
                p.Check(animator != null && animator.GetBool("inWater") && !animator.GetCurrentAnimatorStateInfo(0).IsName("OnLand"),
                    "its animator is told it is in water and swims rather than lying stranded",
                    animator == null ? "no animator with the in-water switch" : $"in water {animator.GetBool("inWater")}, on land {animator.GetCurrentAnimatorStateInfo(0).IsName("OnLand")}");
                var top = CopyOf(serpent).GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).Select(r => r.bounds.max.y).DefaultIfEmpty(surface).Max();
                p.Note($"a Serpent shows {Numbers.Fixed(top - surface, 2)} m over the surface at its highest");
                var prefabTail = (serpent.Source as GameObject).GetComponentInChildren<Tail>(true);
                p.Check(prefabTail != null && Stage.TailJointsBent == prefabTail.m_tailJoints.Count, "its tail is bent joint by joint as the game bends it",
                    $"{Numbers.Count(Stage.TailJointsBent)} of {(prefabTail != null ? Numbers.Count(prefabTail.m_tailJoints.Count) : "no")} joints");
                CameraOverSurface(p, surface);
                yield return Picture(p, "a Serpent in the sea");
                // Turned to look up from under the water, the camera stays over it, as the game's does.
                var pitch = Stage.Pitch;
                Stage.TurnTo(Stage.Yaw, -40f);
                yield return null;
                yield return null;
                var over = Stage.CameraHeight - surface;
                p.Check(over >= StageCamera.OverWaterMetres * 0.99f * Stage.Scale, "turned to look from below, the camera stays over the surface as the game keeps its own",
                    $"{Numbers.Fixed(over, 2)} m over it");
                yield return Picture(p, "a Serpent in the sea looked at from below");
                Stage.TurnTo(Stage.Yaw, pitch);
                yield return null;

                // On the plain floor it still swims: it is never seen out of water but stranded.
                Stage.BackdropIndex = 0;
                yield return null;
                yield return null;
                p.Check(animator != null && animator.GetBool("inWater"), "on the plain floor a Serpent, living only in water, still swims");
                Stage.BackdropIndex = 5;
                yield return null;
            }

            if (leviathan != null)
            {
                Select(leviathan);
                yield return Until(() => CopyOf(leviathan) != null && Stage.WaterShown, 20);
                yield return new Wait(1.5f);
                var (held, _) = Stage.SeaHolds;
                p.Check(held == SeaHeld.AtSurface, "a Leviathan rides the sea's surface", held.ToString());
                var (surface, floor) = Stage.SeaLevels;
                var copy = CopyOf(leviathan);
                var offset = (leviathan.Source as GameObject).GetComponent<Leviathan>().m_floatOffset;
                p.Check(Mathf.Abs(copy.transform.position.y - surface - offset) < 0.05f, "its root lies at the surface by its float offset, as the game holds it",
                    $"{Numbers.Fixed(copy.transform.position.y - surface, 2)} m, its offset {Numbers.Fixed(offset, 2)} m");
                var lowest = copy.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).Select(r => r.bounds.min.y).DefaultIfEmpty(surface).Min();
                var placed = Knowledge.SeaDepth("Leviathan");
                p.Check(floor < lowest && (placed == null || surface - floor >= placed.Value - 0.01f), "the sea under it is as deep as the world places it, its floor under its body",
                    $"{Numbers.Fixed(surface - floor, 1)} m deep, its body {Numbers.Fixed(surface - lowest, 1)} m under the surface, the world's {(placed is float d ? Numbers.Fixed(d, 1) + " m" : "none")}");
                CameraOverSurface(p, surface);
                yield return Picture(p, "a Leviathan in the sea");
            }

            if (bench != null)
            {
                Stage.GroundBiomeOverride = "Ocean";
                Select(bench);
                yield return Until(() => CopyOf(bench) != null && Stage.WaterShown, 20);
                yield return new Wait(1f);
                var (held, _) = Stage.SeaHolds;
                var (surface, floor) = Stage.SeaLevels;
                p.Check(held == SeaHeld.Not && surface - floor > 0f && surface - floor <= SeaHold.Shallows, "a workbench stands on the sea's floor in the shallows",
                    $"{held}, {Numbers.Fixed(surface - floor, 2)} m of water");
                CameraOverSurface(p, surface);
                yield return Picture(p, "a workbench on the sea's ground");
                Stage.GroundBiomeOverride = null;
            }
            SeaAsItWas();
        }

        /// <summary>
        /// A Drake and Moder, which the game starts flying, are told they fly and stay in their
        /// flight rather than dropping to their landed pose; each is pictured.
        /// </summary>
        private static IEnumerator FlyersFly(Probe p)
        {
            var flyers = FlyerNames.Select(name => X.Catalog.FirstOrDefault(e => e.Name == name && e.Source is GameObject)).Where(e => e != null).ToList();
            if (flyers.Count == 0) p.Skip("there is neither a Drake nor Moder to show");
            foreach (var flyer in flyers)
            {
                Select(flyer);
                yield return Until(() => CopyOf(flyer) != null, 20);
                yield return new Wait(1.5f);
                var animator = CopyOf(flyer).GetComponentsInChildren<Animator>().FirstOrDefault(a => a.parameters.Any(x => x.name == "flying"));
                var landed = animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Movement_Ground");
                p.Check(Stage.ShownFlies && animator != null && animator.GetBool("flying") && !landed, $"{flyer.Name} is told it flies and stays in its flight",
                    animator == null ? "no animator with the flying switch" : $"flying {animator.GetBool("flying")}, landed {landed}");
                var lift = PlaceSpawns.FlyingHeight(flyer.Source as GameObject);
                p.Check(lift > 0f && Mathf.Abs(-Stage.Ground - lift) < 0.05f, $"{flyer.Name} flies as high over the ground as the game keeps it at the least",
                    $"{Numbers.Fixed(-Stage.Ground, 2)} m over it, the game's least {Numbers.Fixed(lift, 2)} m");
                yield return Picture(p, flyer.Name + " in flight");
            }

            // A raid bringing flyers: each comes flying, as high as the game keeps it, told it flies.
            var raid = X.Catalog.FirstOrDefault(e => e.Source is RandomEvent ev && ev.m_spawn != null && ev.m_spawn.Any(s => s?.m_prefab != null && PlaceSpawns.FlyingHeight(s.m_prefab) > 0f));
            if (raid == null)
            {
                p.Note("no raid brings flyers");
                yield break;
            }
            Select(raid);
            yield return Until(() => CopyOf(raid) != null && RaidCrowd.LastCopies.Count > 0, 20);
            yield return new Wait(1.5f);
            var holder = CopyOf(raid);
            var flying = RaidCrowd.LastCopies.Where(c => c.Copy != null && PlaceSpawns.FlyingHeight(c.Prefab) > 0f).ToList();
            var low = flying.Count(c => Mathf.Abs(c.Copy.transform.position.y - holder.transform.position.y - PlaceSpawns.FlyingHeight(c.Prefab)) > 0.05f);
            var untold = flying.Count(c => c.Copy.GetComponentsInChildren<Animator>().Any(a => a.parameters.Any(x => x.name == "flying") && !a.GetBool("flying")));
            p.Check(flying.Count > 0 && low == 0 && untold == 0, $"{raid.Name}'s flyers come flying as high as the game keeps them, told they fly",
                $"{Numbers.Count(flying.Count)} flyers, {Numbers.Count(low)} off their height, {Numbers.Count(untold)} not told");
            yield return Picture(p, raid.Name + " with its flyers");
        }

        private static void CameraOverSurface(Probe p, float surface)
        {
            var eye = Stage.CameraHeight;
            p.Check(eye > surface, "the camera looks at it from over the surface", $"{Numbers.Fixed(eye - surface, 2)} m over it");
        }

        /// <summary>The backdrop, spin and ground put back as they were before <see cref="SeaHolds"/>.</summary>
        private static void SeaAsItWas()
        {
            Stage.GroundBiomeOverride = null;
            if (_seaBefore is (int backdrop, bool spin, string choice))
            {
                Stage.BackdropIndex = backdrop;
                Stage.Spin = spin;
                Stage.GroundChoice = choice;
            }
            _seaBefore = null;
        }
    }
}
