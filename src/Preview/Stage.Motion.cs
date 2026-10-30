using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How what is shown moves, told to its animators every frame as the game tells a creature's
    /// (<see cref="MotionSwitches"/>): a flyer that it flies, so a Drake or Moder shows in flight
    /// rather than landed; a swimmer that it is in water; the person swimming beside what the sea
    /// holds the same. Told every frame, as playing a clip may set an animator back.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>Whether what is shown flies (<c>Character.m_flying</c>, as the game starts it), read as it is shown.</summary>
        private static bool _flies;

        /// <summary>What is shown's animators with the game's in-water or flying switch, and which of them each has, gathered as it is shown.</summary>
        private static readonly List<(Animator Animator, bool InWater, bool Flying)> MotionAnimators = new List<(Animator, bool, bool)>();

        private static readonly List<(Animator Animator, bool InWater, bool Flying)> PersonMotionAnimators = new List<(Animator, bool, bool)>();

        private const string InWater = "inWater";

        private const string Flying = "flying";

        /// <summary>A fault in telling how what is shown moves is told once and lets go of it, its animators left as they are.</summary>
        private const string MotionPart = "telling what is shown how it moves";

        /// <summary>For the self-test: whether what is shown is told it flies.</summary>
        [ForTest]
        public static bool ShownFlies => _flies;

        /// <summary>Reads whether a copy newly shown flies, a person wearing what is shown never, and gathers its animators' switches.</summary>
        private static void ReadMotion(Entry entry)
        {
            _flies = false;
            MotionAnimators.Clear();
            var prefab = entry?.Source as GameObject;
            if (prefab == null || _subjectIsPerson || _subject == null) return;
            _flies = prefab.GetComponent<Character>().OrNull()?.m_flying == true;
            GatherSwitches(_subject, MotionAnimators);
        }

        /// <summary>Lets go of how what is shown moves, after a fault.</summary>
        private static void ForgetMotion()
        {
            _flies = false;
            MotionAnimators.Clear();
        }

        /// <summary>The animators of a copy that have the game's in-water or flying switch, and which.</summary>
        private static void GatherSwitches(GameObject copy, List<(Animator, bool, bool)> into)
        {
            into.Clear();
            foreach (var animator in copy.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null) continue;
                bool inWater = false, flying = false;
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.type != AnimatorControllerParameterType.Bool) continue;
                    inWater |= parameter.name == InWater;
                    flying |= parameter.name == Flying;
                }
                if (inWater || flying) into.Add((animator, inWater, flying));
            }
        }

        /// <summary>Each frame, once the sea is kept: what is shown and the person beside it told how they move.</summary>
        private static void KeepMotion()
        {
            Tell(MotionAnimators, MotionSwitches.Told(_flies, _sea.Held, _seaShown, _livesInWater));
            Tell(PersonMotionAnimators, (HeldInSea, false));
        }

        private static void Tell(List<(Animator Animator, bool InWater, bool Flying)> animators, (bool InWater, bool Flying) told)
        {
            for (var i = animators.Count - 1; i >= 0; i--)
            {
                var (animator, inWater, flying) = animators[i];
                if (animator == null)
                {
                    animators.RemoveAt(i);
                    continue;
                }
                if (!animator.isActiveAndEnabled) continue;
                if (inWater) Set(animator, InWater, told.InWater);
                if (flying) Set(animator, Flying, told.Flying);
            }
        }

        private static void Set(Animator animator, string name, bool on)
        {
            if (animator.GetBool(name) == on) return;
            animator.SetBool(name, on);
            Touch();
        }
    }
}
