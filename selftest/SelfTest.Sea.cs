using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for the sea on the ocean's ground holding what the game holds in it.</summary>
    internal static partial class SelfTest
    {
        private static (int Backdrop, bool Spin, string Choice)? _seaBefore;

        /// <summary>How far the Serpent's tail joints stood from their parents at the most through its attacks part, against when shown.</summary>
        private static float _tailMost;

        /// <summary>The most of the Serpent's tail links pointing back toward its head through its roar.</summary>
        private static int _tailFoldsMost;

        /// <summary>The creatures the game starts flying whose animators land them when not told: a Drake and Moder.</summary>
        private static readonly string[] FlyerNames = { "Hatchling", "Dragon" };

        /// <summary>
        /// On the ocean's ground a Serpent swims its swim depth under the surface, its animator told
        /// it is in water so it swims rather than lies stranded, its tail bent as the game bends it;
        /// a Leviathan rides the surface over a sea as deep as the world places it, its floor clear
        /// under its body; a workbench stands in the shallows; and the camera looks at each from over
        /// the surface, kept over it as the game keeps its own when turned to look from below, and
        /// still looking at what it frames from there. Each is pictured, and the backdrop, spin and
        /// ground put back.
        /// </summary>
        private static IEnumerator SeaHolds(Probe p)
        {
            var serpent = X.Catalog.FirstOrDefault(e => e.Name == "Serpent" && e.Source is GameObject);
            var leviathan = X.Catalog.FirstOrDefault(e => e.Name == "Leviathan" && e.Source is GameObject);
            var bench = X.Catalog.FirstOrDefault(e => e.Name == "piece_workbench" && e.Source is GameObject);
            if (serpent == null && leviathan == null) p.Skip("there is neither a Serpent nor a Leviathan to show");
            OnGroundChosenAutomatically();

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
                p.Check(Stage.CameraOffAim < 1f, "and still looks at what it frames, rather than up at the sky", $"{Numbers.Fixed(Stage.CameraOffAim, 1)} degrees off it");
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

        /// <summary>
        /// A Serpent's bite and roar in the sea: the bite pictured a quarter, half and three
        /// quarters through, held there a moment so its tail settles, the roar half through, and
        /// last the bite half through with its tail left as the animator poses it, to set beside
        /// the bent one. Played through once, how far its tail's joints stand from their parents at
        /// the most is noted, against how far they hung when it was shown, and kept short of 1.4
        /// times through the long frames of its size change and pictures. Roaring, it slows to its
        /// taunt's pace, its body never folding back, and swims on after, pictured rearing and
        /// coming down. Long, it opens showing
        /// its side across most of the picture, pictured so; a Troll after it opens from the front.
        /// </summary>
        private static IEnumerator SerpentAttacks(Probe p)
        {
            var serpent = X.Catalog.FirstOrDefault(e => e.Name == "Serpent" && e.Source is GameObject);
            if (serpent == null) p.Skip("there is no Serpent to show");
            OnGroundChosenAutomatically();
            Select(serpent);
            yield return Until(() => CopyOf(serpent) != null && Stage.WaterShown && Previews.Clips().Count > 0, 20);
            yield return new Wait(1f);
            var clips = Previews.Clips();
            p.Note("its clips: " + string.Join(", ", clips.Select(c => $"{c.name} {Numbers.Fixed(c.length, 2)} s")));
            var bite = clips.FirstOrDefault(c => c.name == "Bite");
            var roar = clips.FirstOrDefault(c => c.name == "Rawr");
            if (!p.Check(bite != null, "its bite is among its clips")) yield break;

            // Held in place on the stage, its tail streams out as when it swims rather than sinking.
            var deepFirst = Stage.TailDeepest;
            yield return new Wait(10f);
            var deepLater = Stage.TailDeepest;
            p.Check(!float.IsNaN(deepFirst) && !float.IsNaN(deepLater) && deepLater - deepFirst < 0.5f, "held on the stage ten seconds, its tail stays as deep as it was rather than sinking",
                $"deepest joint {Numbers.Fixed(deepFirst, 2)} m under the surface, then {Numbers.Fixed(deepLater, 2)} m");

            // Long, it opens showing its side, its length across most of the picture and all of it inside.
            var across = Stage.ShownAcross;
            var (left, right) = across ?? (float.NaN, float.NaN);
            p.Check(Stage.ShownSideOn && across != null && right - left >= 0.45f, "a Serpent opens showing its side, across nearly half the picture's width or more",
                $"{(Stage.ShownSideOn ? "side on" : "from the front")}, from {Numbers.Fixed(left, 2)} to {Numbers.Fixed(right, 2)} of the width, turned {Numbers.Fixed(Stage.Yaw, 0)} degrees");
            p.Check(across != null && left >= -0.02f && right <= 1.02f, "and none of it leaves the picture", $"from {Numbers.Fixed(left, 2)} to {Numbers.Fixed(right, 2)} of the width");
            yield return Picture(p, "a Serpent as it opens on the stage");

            // Played through once, the tail's stretch watched every frame.
            var still = Stage.TailStretch;
            Previews.PlayClip(bite);
            var most = 0f;
            var until = Time.unscaledTime + bite.length + 0.5f;
            while (Time.unscaledTime < until)
            {
                most = Mathf.Max(most, Stage.TailStretch);
                yield return null;
            }
            p.Note($"swimming, its tail's joints stand at most {Numbers.Fixed(still, 2)} times as far from their parents as when shown; through its bite {Numbers.Fixed(most, 2)} times");
            _tailMost = most;
            // Stopped for its bite, it takes two seconds to swim at full speed again.
            yield return new Wait(2.5f);

            // Made twice as big while shown, the tail keeps its joints as far apart for its size: the
            // most of a second at each size, so a swing caught at one moment decides nothing.
            var swimming = 0f;
            yield return MostTailStretch(s => swimming = s);
            var size = X.Modifiers.Scale;
            X.Modifiers.Scale = size * 2f;
            yield return new Wait(0.5f);
            var grown = 0f;
            yield return MostTailStretch(s => grown = s);
            X.Modifiers.Scale = size;
            yield return new Wait(0.5f);
            p.Check(Mathf.Abs(grown - swimming) < 0.25f, "made twice as big while shown, its tail keeps its joints as far apart for its size",
                $"at most {Numbers.Fixed(grown, 2)} times as far as when shown, swimming at its size {Numbers.Fixed(swimming, 2)}");
            _tailMost = Mathf.Max(_tailMost, Mathf.Max(swimming, grown));

            // Roaring, it slows to its taunt's pace (a fifth of its swim speed) rather than
            // stopping, so its body does not fold back into loops, and swims on after: pictured
            // rearing and coming down, played through at its pace.
            if (roar != null)
            {
                var swimmingBefore = Stage.TailSwimSpeed;
                _tailFoldsMost = 0;
                Previews.PlayClip(roar);
                yield return Until(() => Previews.PlayingClip() == roar, 3);
                yield return WatchingFolds(roar.length * 0.4f);
                var roaring = Stage.TailSwimSpeed;
                yield return Picture(p, "a Serpent rearing 40% into its Rawr, slowed to roar");
                yield return WatchingFolds(roar.length * 0.4f);
                yield return Picture(p, "a Serpent coming down near the end of its Rawr");
                yield return WatchingFolds(roar.length * 0.15f);
                Previews.StopClip();
                yield return new Wait(3f);
                var after = Stage.TailSwimSpeed;
                p.Check(swimmingBefore > 9f && roaring > 1.5f && roaring < 2.5f && after > 9f, "roaring it slows to its taunt's pace, and swims on after",
                    $"{Numbers.Fixed(swimmingBefore, 1)} m/s before, {Numbers.Fixed(roaring, 1)} m/s roaring, {Numbers.Fixed(after, 1)} m/s three seconds after");
                p.Check(_tailFoldsMost == 0, "through its roar its body never folds back toward its head",
                    $"at most {Numbers.Count(_tailFoldsMost)} of its tail's links pointing back");
            }

            foreach (var share in new[] { 0.25f, 0.5f, 0.75f }) yield return HeldAt(p, bite, share, "bent");
            if (roar != null) yield return HeldAt(p, roar, 0.5f, "bent");
            // Swimming at ten metres a second its first joint, 0.7 m from the body, trails a sixtieth
            // of that behind (1.24 times); stepped once a frame, a long frame such as a picture's
            // stretched it several times over and let it spring back.
            p.Check(_tailMost > 0f && _tailMost < 1.4f, "through its bite, a size change and the pictures' long frames, its tail keeps its length",
                $"its joints at most {Numbers.Fixed(_tailMost, 2)} times as far from their parents as when shown");
            // Last, as the tail stays let go of until the Serpent is shown again.
            Stage.LetTailGoForTest();
            yield return HeldAt(p, bite, 0.5f, "as the animator poses it");
            Previews.StopClip();

            // A round creature opens from the front, as everything did before.
            var sideYaw = Stage.Yaw;
            var troll = Pick(Kind.Creature, "Troll");
            if (troll != null)
            {
                Select(troll);
                yield return Until(() => CopyOf(troll) != null, 10);
                p.Check(CopyOf(troll) != null && !Stage.ShownSideOn && Mathf.Abs(Mathf.DeltaAngle(Stage.Yaw, sideYaw)) > 30f, "a Troll still opens from the front",
                    $"{(Stage.ShownSideOn ? "side on" : "from the front")}, turned {Numbers.Fixed(Stage.Yaw, 0)} degrees");
            }
            SerpentAttacksAsItWas();
        }

        /// <summary>Waits a while, noting each frame the most of the tail's links pointing back toward the head (<see cref="Stage.TailFoldedBack"/>).</summary>
        private static IEnumerator WatchingFolds(float seconds)
        {
            var until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                _tailFoldsMost = Mathf.Max(_tailFoldsMost, Stage.TailFoldedBack);
                yield return null;
            }
        }

        /// <summary>The most the shown tail's joints stand from their parents over a second, against when it was shown (<see cref="Stage.TailStretch"/>).</summary>
        private static IEnumerator MostTailStretch(System.Action<float> most)
        {
            var found = 0f;
            var until = Time.unscaledTime + 1f;
            while (Time.unscaledTime < until)
            {
                found = Mathf.Max(found, Stage.TailStretch);
                yield return null;
            }
            most(found);
        }

        /// <summary>A clip played, paused at a share of its length and held a moment, then pictured with its tail's stretch noted.</summary>
        private static IEnumerator HeldAt(Probe p, AnimationClip clip, float share, string tail)
        {
            Previews.PlayClip(clip);
            yield return Until(() => Previews.PlayingClip() == clip, 3);
            Previews.PauseClip(true);
            Previews.SeekClip(clip.length * share);
            yield return new Wait(0.8f);
            var name = $"a Serpent's {clip.name} {Numbers.Count(Mathf.RoundToInt(share * 100f))}% through, its tail {tail}";
            p.Note($"{name}: its tail's joints at most {Numbers.Fixed(Stage.TailStretch, 2)} times as far from their parents as when shown");
            if (tail == "bent") _tailMost = Mathf.Max(_tailMost, Stage.TailStretch);
            yield return Picture(p, name);
            // The frame after the picture's long one.
            if (tail == "bent") _tailMost = Mathf.Max(_tailMost, Stage.TailStretch);
            Previews.PauseClip(false);
            Previews.StopClip();
            yield return null;
        }

        /// <summary>The clip stopped, and the backdrop, spin and ground put back.</summary>
        private static void SerpentAttacksAsItWas()
        {
            Previews.StopClip();
            SeaAsItWas();
        }

        private static void CameraOverSurface(Probe p, float surface)
        {
            var eye = Stage.CameraHeight;
            p.Check(eye > surface, "the camera looks at it from over the surface", $"{Numbers.Fixed(eye - surface, 2)} m over it");
        }

        /// <summary>The stage set to lay the ground of what is shown, chosen automatically, standing still; put back by <see cref="SeaAsItWas"/>.</summary>
        private static void OnGroundChosenAutomatically()
        {
            _seaBefore = (Stage.BackdropIndex, Stage.Spin, Stage.GroundChoice);
            Stage.Spin = false;
            Stage.GroundChoice = "Auto";
            Stage.BackdropIndex = 5;
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
