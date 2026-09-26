using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Hears a preview copy's animation events and answers them with the prefab's own sounds and
    /// effects, as the game's <c>CharacterAnimEvent</c> and <c>AnimationEffect</c> would through
    /// the scripts the copy no longer has: footsteps from its <c>FootStep</c> list, swings from
    /// the attack whose animation is playing, jumps and deaths from the character's own lists,
    /// and the effects and props a clip names itself (a wolf's howl, a tool in a hand). Events
    /// that only move or steer the character are heard and ignored.
    ///
    /// Put on a copy only when every event its clips send is one of these, since an event nothing
    /// hears makes Unity log an error each time it fires.
    /// </summary>
    internal sealed class AnimationEars : MonoBehaviour
    {
        /// <summary>The events <c>CharacterAnimEvent</c> and <c>AnimationEffect</c> answer, all of which are heard here.</summary>
        private static readonly HashSet<string> Heard = new HashSet<string>
        {
            "FootStep", "Hit", "OnAttackTrigger", "Jump", "Land", "TakeOff", "Stop", "DodgeMortal",
            "TrailOn", "TrailOff", "GPower", "Die", "Speed", "Chain", "ResetChain", "FreezeFrame",
            "Effect", "Attach",
        };

        private static readonly HashSet<string> Told = new HashSet<string>();

        private GameObject _prefab;
        private GameObject _copy;
        private float _lastStep;
        private readonly List<GameObject> _attached = new List<GameObject>();

        /// <summary>Lets a copy's animations sound, when all of their events can be answered.</summary>
        public static void Attach(GameObject prefab, GameObject copy)
        {
            var animator = ClipPlayer.AnimatorOf(copy);
            if (animator == null) return;

            var unknown = new SortedSet<string>();
            var events = 0;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                foreach (var e in clip.events)
                {
                    events++;
                    if (!Heard.Contains(e.functionName)) unknown.Add(e.functionName);
                }
            }

            // Said once per prefab, so it can be told why a creature's clips stay silent.
            if (Told.Add(prefab.name))
            {
                if (unknown.Count > 0) Plugin.Log.LogInfo($"Scry leaves the animations of {prefab.name} silent: its clips send events it cannot answer ({string.Join(", ", unknown)}).");
                else if (events == 0) Plugin.Log.LogInfo($"Scry: the animations of {prefab.name} send no events, so they have no sounds of their own.");
                else Plugin.Log.LogInfo($"Scry answers the {events} animation events of {prefab.name}.");
            }
            if (unknown.Count > 0) return;

            var ears = animator.gameObject.AddComponent<AnimationEars>();
            ears._prefab = prefab;
            ears._copy = copy;
            animator.fireEvents = true;
        }

        /// <summary>An attack's clip starting makes the attack's opening sound, as starting the attack does in the game.</summary>
        public void ClipStarted(AnimationClip clip)
        {
            var attack = clip != null ? AttackFor(clip.name) : null;
            if (attack != null) Previews.PlayOnCopy(_copy, attack.m_startEffect, null);
        }

        /// <summary>What a clip hung on the copy comes off when it ends, as it does when the game's animation moves on.</summary>
        public void ClipEnded()
        {
            foreach (var attached in _attached) if (attached != null) Destroy(attached);
            _attached.Clear();
        }

        // ----- Answered -----

        /// <summary>
        /// A clip's own effect, named in the event: played at the bone the event names, else at
        /// the prefab's effect root, else at the animated body.
        /// </summary>
        public void Effect(AnimationEvent e)
        {
            var prefab = e.objectReferenceParameter as GameObject;
            if (prefab == null) return;

            Transform at = null;
            if (!string.IsNullOrEmpty(e.stringParameter)) at = Utils.FindChild(transform, e.stringParameter);
            if (at == null)
            {
                var root = _prefab.GetComponentInChildren<global::AnimationEffect>(true)?.m_effectRoot;
                if (root != null) at = Looks.Twin(_prefab.transform, _copy.transform, root);
            }
            if (at == null) at = transform;

            Previews.PlayOnCopy(_copy, AsList(new[] { prefab }), at);
        }

        /// <summary>
        /// A prop a clip holds for as long as it plays, hung on the bone the event names. One
        /// hung there before comes off first; a scale of 10 keeps the prop's own size.
        /// </summary>
        public void Attach(AnimationEvent e)
        {
            var prefab = e.objectReferenceParameter as GameObject;
            if (prefab == null || string.IsNullOrEmpty(e.stringParameter)) return;
            var joint = Utils.FindChild(transform, e.stringParameter);
            if (joint == null) return;

            for (var i = _attached.Count - 1; i >= 0; i--)
            {
                if (_attached[i] != null && _attached[i].transform.parent != joint) continue;
                if (_attached[i] != null) Destroy(_attached[i]);
                _attached.RemoveAt(i);
            }

            var copy = _copy == Stage.Subject ? Stage.Hang(prefab, joint) : Ghost.MakeOn(prefab, joint, joint.position, joint.rotation);
            if (copy == null) return;
            if (e.intParameter == 10 || e.intParameter == -10) copy.transform.localScale = prefab.transform.localScale;
            copy.SetActive(true);
            _attached.Add(copy);
        }

        public void FootStep(AnimationEvent e)
        {
            // Blended-out clips send steps too; the game ignores the faint ones, and so do we. A
            // clip Scry plays on its own is all there is, and reports no weight to go by.
            var alone = _copy.GetComponent<ClipPlayer>()?.Clip != null;
            if ((!alone && e.animatorClipInfo.weight < 0.33f) || Time.unscaledTime - _lastStep < 0.08f) return;
            _lastStep = Time.unscaledTime;

            var step = _prefab.GetComponentInChildren<global::FootStep>(true);
            var effect = step != null ? Step(step) : null;
            if (effect == null) return;

            Transform foot = null;
            if (!string.IsNullOrEmpty(e.stringParameter)) foot = Utils.FindChild(_copy.transform, e.stringParameter);
            Previews.PlayOnCopy(_copy, AsList(effect.m_effectPrefabs), foot);
        }

        public void Hit() => AttackTrigger();
        public void OnAttackTrigger() => AttackTrigger();

        public void Jump()
        {
            var character = _prefab.GetComponent<Character>();
            if (character != null) Previews.PlayOnCopy(_copy, character.m_jumpEffects, null);
        }

        public void Die()
        {
            var character = _prefab.GetComponent<Character>();
            if (character != null) Previews.PlayOnCopy(_copy, character.m_deathEffects, null);
        }

        // ----- Heard, nothing to play -----

        public void Land() { }
        public void TakeOff() { }
        public void Stop(AnimationEvent e) { }
        public void DodgeMortal() { }
        public void TrailOn() { }
        public void TrailOff() { }
        public void GPower() { }
        public void Speed(float speedScale) { }
        public void Chain() { }
        public void ResetChain() { }
        public void FreezeFrame(float delay) { }

        // ----- Helpers -----

        /// <summary>
        /// The swing of the attack whose animation is playing. Clips and attacks are matched by
        /// name, which holds for most creatures; a clip that matches no attack stays quiet.
        /// </summary>
        private void AttackTrigger()
        {
            var clip = _copy.GetComponent<ClipPlayer>()?.Clip;
            if (clip == null) return;
            var attack = AttackFor(clip.name);
            if (attack != null) Previews.PlayOnCopy(_copy, attack.m_triggerEffect, null);
        }

        private Attack AttackFor(string clip)
        {
            var humanoid = _prefab.GetComponent<Humanoid>();
            if (humanoid == null) return null;

            var items = new List<GameObject>();
            if (humanoid.m_defaultItems != null) items.AddRange(humanoid.m_defaultItems);
            if (humanoid.m_randomWeapon != null) items.AddRange(humanoid.m_randomWeapon);
            if (humanoid.m_randomSets != null)
            {
                foreach (var set in humanoid.m_randomSets) if (set?.m_items != null) items.AddRange(set.m_items);
            }

            foreach (var item in items)
            {
                var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                if (shared == null) continue;
                foreach (var attack in new[] { shared.m_attack, shared.m_secondaryAttack })
                {
                    var name = attack?.m_attackAnimation;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (clip.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf(clip, StringComparison.OrdinalIgnoreCase) >= 0) return attack;
                }
            }
            return null;
        }

        /// <summary>The step a walk on plain ground makes, or failing that the first there is.</summary>
        private static global::FootStep.StepEffect Step(global::FootStep step)
        {
            global::FootStep.StepEffect first = null;
            foreach (var effect in step.m_effects)
            {
                if (effect?.m_effectPrefabs == null || effect.m_effectPrefabs.Length == 0) continue;
                if (first == null) first = effect;
                if (effect.m_material == global::FootStep.GroundMaterial.Default
                    && (effect.m_motionType == global::FootStep.MotionType.Jog || effect.m_motionType == global::FootStep.MotionType.Walk))
                {
                    return effect;
                }
            }
            return first;
        }

        private static EffectList AsList(GameObject[] prefabs)
        {
            var list = new EffectList();
            var data = new List<EffectList.EffectData>();
            foreach (var prefab in prefabs) if (prefab != null) data.Add(new EffectList.EffectData { m_prefab = prefab });
            list.m_effectPrefabs = data.ToArray();
            return list;
        }
    }
}
