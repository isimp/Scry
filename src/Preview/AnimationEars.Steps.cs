using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>Footsteps: the feet watched while a clip moves the creature along, and the step it makes.</summary>
    internal sealed partial class AnimationEars
    {
        /// <summary>Words of the clips in which a creature walks, runs or otherwise moves on its feet.</summary>
        private static readonly string[] Moving = { "walk", "run", "jog", "sneak", "trot", "gallop", "move", "crawl", "charge", "sprint", "stroll", "step", "turn", "strafe", "swim" };
        private Transform[] _feet;
        private StepDetector[] _footing;
        private global::FootStep.MotionType _motion;

        /// <summary>
        /// The game steps by a value its walk animations set on the animator as a foot comes down,
        /// which a clip played on its own does not set. So while such a clip plays, the feet the
        /// prefab's <c>FootStep</c> names are watched, and a step is heard where one comes down.
        /// </summary>
        private void WatchFeet(AnimationClip clip)
        {
            _feet = null;
            var heard = _copy.GetComponent<ClipPlayer>()?.Heard(clip);
            var step = _prefab.GetComponentInChildren<global::FootStep>(true);
            if (clip == null || step == null || step.m_feet == null || step.m_feet.Length == 0)
            {
                Listen.Note(heard, step == null ? "no footsteps: the prefab has no FootStep" : "no feet named in its FootStep, so steps cannot be told");
                return;
            }

            // The game only steps while the creature moves; an attack or a stagger in place makes
            // none. Turning is moving here: the feet are set down as it turns.
            var name = clip.name.ToLowerInvariant();
            if (!System.Array.Exists(Moving, m => name.Contains(m)))
            {
                Listen.Note(heard, "feet not watched: the clip does not move the creature along");
                return;
            }
            Listen.Note(heard, $"watching {step.m_feet.Length} feet");

            // A foot the game names in an old model kept switched off beside the body (a frost
            // troll's) never moves; the body's bone of that name does.
            var feet = new List<Transform>();
            var body = Body;
            var moved = 0;
            foreach (var foot in step.m_feet)
            {
                var twin = foot != null ? Looks.Twin(_prefab.transform, _copy.transform, foot) : null;
                if (twin != null && !twin.IsChildOf(body))
                {
                    var same = Utils.FindChild(body, twin.name);
                    if (same != null)
                    {
                        twin = same;
                        moved++;
                    }
                }
                if (twin != null) feet.Add(twin);
            }
            if (moved > 0) Listen.Note(heard, $"{moved} feet found by name in the body, the ones named being in a part switched off");
            if (feet.Count == 0) return;

            _feet = feet.ToArray();
            _footing = new StepDetector[_feet.Length];
            for (var i = 0; i < _footing.Length; i++) _footing[i] = new StepDetector();
            _motion = MotionOf(name);
        }

        private static global::FootStep.MotionType MotionOf(string name)
        {
            if (name.Contains("swim")) return global::FootStep.MotionType.Swimming;
            return name.Contains("run") || name.Contains("sprint") || name.Contains("gallop") || name.Contains("charge")
                ? global::FootStep.MotionType.Run
                : name.Contains("sneak") || name.Contains("crawl") ? global::FootStep.MotionType.Sneak
                : name.Contains("walk") || name.Contains("stroll") ? global::FootStep.MotionType.Walk
                : global::FootStep.MotionType.Jog;
        }

        private void WatchSteps()
        {
            if (_feet == null || _copy == null) return;
            var root = _copy.transform;
            for (var i = 0; i < _feet.Length; i++)
            {
                if (_feet[i] == null) continue;
                var height = root.InverseTransformPoint(_feet[i].position).y;
                if (!_footing[i].Feed(Time.unscaledTime, height)) continue;
                Listen.Note(Listening, "a foot came down");

                var step = _prefab.GetComponentInChildren<global::FootStep>(true);
                var effect = step != null ? Step(step, _motion, Previews.StepGround) : null;
                if (effect != null) Report(Previews.PlayOnCopy(_copy, AsList(effect.m_effectPrefabs), _feet[i]));
            }
        }

        private void Step(Heard e)
        {
            // Blended-out clips send steps too; the game ignores the faint ones, and so are they here.
            if (e.Weight < 0.33f || Time.unscaledTime - _lastStep < 0.08f) return;
            _lastStep = Time.unscaledTime;

            var step = _prefab.GetComponentInChildren<global::FootStep>(true);
            var effect = step != null ? Step(step, global::FootStep.MotionType.Jog | global::FootStep.MotionType.Walk, Previews.StepGround) : null;
            if (effect == null) return;

            Transform foot = null;
            if (!string.IsNullOrEmpty(e.Text)) foot = Utils.FindChild(Body, e.Text) ?? Utils.FindChild(_copy.transform, e.Text);
            Report(Previews.PlayOnCopy(_copy, AsList(effect.m_effectPrefabs), foot));
        }

        /// <summary>
        /// The step made on the chosen ground in this way of moving; failing that, any on that
        /// ground; failing those, the same on plain ground; failing that, the first there is. Ways
        /// of moving and grounds are sets of flags in the game.
        /// </summary>
        private static global::FootStep.StepEffect Step(global::FootStep step, global::FootStep.MotionType motion, global::FootStep.GroundMaterial ground)
        {
            // In water the game steps on water, and only where a creature has a swimming step
            // (FootStep.FindBestStepEffect: the motion must match; the last match wins).
            if ((motion & global::FootStep.MotionType.Swimming) != 0)
            {
                global::FootStep.StepEffect swim = null;
                foreach (var effect in step.m_effects)
                {
                    if (effect?.m_effectPrefabs == null || (effect.m_motionType & global::FootStep.MotionType.Swimming) == 0) continue;
                    if ((effect.m_material & global::FootStep.GroundMaterial.Water) != 0 || (swim == null && (effect.m_material & global::FootStep.GroundMaterial.Default) != 0)) swim = effect;
                }
                return swim;
            }

            global::FootStep.StepEffect Find(global::FootStep.GroundMaterial on, bool matchMotion)
            {
                foreach (var effect in step.m_effects)
                {
                    if (effect?.m_effectPrefabs == null || effect.m_effectPrefabs.Length == 0) continue;
                    if ((effect.m_material & on) == 0) continue;
                    if (matchMotion && (effect.m_motionType & motion) == 0) continue;
                    return effect;
                }
                return null;
            }

            return Find(ground, true) ?? Find(ground, false)
                   ?? Find(global::FootStep.GroundMaterial.Default, true) ?? Find(global::FootStep.GroundMaterial.Default, false)
                   ?? step.m_effects.Find(e => e?.m_effectPrefabs != null && e.m_effectPrefabs.Length > 0);
        }
    }
}
