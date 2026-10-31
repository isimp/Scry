using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How what is shown moves, told to its animators every frame as the game tells a creature's
    /// (<see cref="MotionSwitches"/>): a flyer that it flies, so a Drake or Moder shows in flight
    /// rather than landed; a swimmer that it is in water; the person swimming beside what the sea
    /// holds the same; each creature of a raid's crowd by its own kind. Told every frame, as
    /// playing a clip may set an animator back. A place's creatures, held in their first pose,
    /// are told once as they are made (<see cref="TellOnce"/>).
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>Whether what is shown flies (<c>Character.m_flying</c>, as the game starts it), read as it is shown.</summary>
        private static bool _flies;

        /// <summary>An animator with the game's in-water or flying switch, which of them it has, and how the creature it belongs to moves.</summary>
        private struct Switches
        {
            public Animator Animator;
            public bool InWater, Flying;

            /// <summary>Whether its creature flies, and lives only in water (<c>MonsterAI.m_avoidLand</c>).</summary>
            public bool Flies, LivesInWater;

            /// <summary>Whether it is what is shown, which the sea may hold, rather than one of a raid's crowd or a place's creatures.</summary>
            public bool Shown;
        }

        /// <summary>What is shown's animators with the game's switches, a raid's crowd's each by its own kind, gathered as it is shown.</summary>
        private static readonly List<Switches> MotionAnimators = new List<Switches>();

        private static readonly List<Switches> PersonMotionAnimators = new List<Switches>();

        private static readonly List<Switches> Gathered = new List<Switches>();

        private const string InWater = "inWater";

        private const string Flying = "flying";

        /// <summary>A fault in telling how what is shown moves is told once and lets go of it, its animators left as they are.</summary>
        private const string MotionPart = "telling what is shown how it moves";

        /// <summary>For the self-test: whether what is shown is told it flies.</summary>
        [ForTest]
        public static bool ShownFlies => _flies;

        /// <summary>
        /// Reads whether a copy newly shown flies, a person wearing what is shown never, and
        /// gathers its animators' switches, a raid's crowd's by each creature's own kind, telling
        /// them at once: the copy is made before its animators first run, and one first run untold
        /// can leave a state it cannot soon leave again (a Serpent stranded, its way back to
        /// swimming open only late in a clip played at a tenth of its speed).
        /// </summary>
        private static void ReadMotion(Entry entry)
        {
            _flies = false;
            MotionAnimators.Clear();
            if (_subject == null || _subjectIsPerson) return;
            if (entry?.Source is RandomEvent)
            {
                foreach (var (copy, kind) in RaidCrowd.LastCopies) GatherSwitches(copy, kind, shown: false, MotionAnimators);
            }
            else if (entry?.Source is GameObject prefab)
            {
                _flies = Flies(prefab);
                GatherSwitches(_subject, prefab, shown: true, MotionAnimators);
            }
            Tell(MotionAnimators);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Flies(GameObject prefab) => prefab != null && prefab.GetComponent<Character>().OrNull()?.m_flying == true;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool LivesInWater(GameObject prefab) => prefab != null && prefab.GetComponent<MonsterAI>().OrNull()?.m_avoidLand == true;

        /// <summary>The person beside what is shown, its in-water switch told while the sea holds what is shown, at once as for what is shown.</summary>
        private static void GatherPersonSwitches(GameObject person)
        {
            PersonMotionAnimators.Clear();
            GatherSwitches(person, null, shown: false, PersonMotionAnimators);
            TellPerson();
        }

        /// <summary>A place's creature, held in its first pose, told how it moves before it is held, as the game would tell it there.</summary>
        private static void TellOnce(GameObject copy, GameObject prefab)
        {
            // A fault here lets the copy stand untold rather than unmade, and is told as this part's.
            Guard.Run(Feature.MotionToldOnStage, MotionPart, TellMade, (copy, prefab));
        }

        /// <summary>Tells a place's creature just made how it moves (<see cref="TellOnce"/>).</summary>
        private static void TellMade((GameObject Copy, GameObject Prefab) made)
        {
            try
            {
                GatherSwitches(made.Copy, made.Prefab, shown: false, Gathered);
                Tell(Gathered);
            }
            finally
            {
                Gathered.Clear();
            }
        }

        /// <summary>Lets go of how what is shown moves, after a fault.</summary>
        private static void ForgetMotion()
        {
            _flies = false;
            MotionAnimators.Clear();
        }

        /// <summary>The animators of a copy of a prefab that have the game's in-water or flying switch, and which, added to a list.</summary>
        private static void GatherSwitches(GameObject copy, GameObject prefab, bool shown, List<Switches> into)
        {
            if (copy == null) return;
            var flies = Flies(prefab);
            var livesInWater = LivesInWater(prefab);
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
                if (inWater || flying) into.Add(new Switches { Animator = animator, InWater = inWater, Flying = flying, Flies = flies, LivesInWater = livesInWater, Shown = shown });
            }
        }

        /// <summary>Each frame, once the sea is kept: what is shown and the person beside it told how they move.</summary>
        private static void KeepMotion()
        {
            Tell(MotionAnimators);
            TellPerson();
        }

        private static void TellPerson()
        {
            for (var i = PersonMotionAnimators.Count - 1; i >= 0; i--)
            {
                var person = PersonMotionAnimators[i];
                if (person.Animator == null) PersonMotionAnimators.RemoveAt(i);
                else if (person.Animator.isActiveAndEnabled && person.InWater) Set(person.Animator, InWater, HeldInSea);
            }
        }

        /// <summary>Tells each animator how its creature moves: what is shown as the sea holds it, a raid's or a place's creature as on open ground.</summary>
        private static void Tell(List<Switches> animators)
        {
            for (var i = animators.Count - 1; i >= 0; i--)
            {
                var each = animators[i];
                if (each.Animator == null)
                {
                    animators.RemoveAt(i);
                    continue;
                }
                if (!each.Animator.isActiveAndEnabled) continue;
                var told = each.Shown
                    ? MotionSwitches.Told(each.Flies, _sea.Held, _seaShown, each.LivesInWater)
                    : MotionSwitches.Told(each.Flies, SeaHeld.Not, false, each.LivesInWater);
                if (each.InWater) Set(each.Animator, InWater, told.InWater);
                if (each.Flying) Set(each.Animator, Flying, told.Flying);
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
