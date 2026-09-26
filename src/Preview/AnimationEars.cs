using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

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
        private static readonly HashSet<string> Answered = new HashSet<string>
        {
            "FootStep", "Hit", "OnAttackTrigger", "Jump", "Land", "TakeOff", "Stop", "DodgeMortal",
            "TrailOn", "TrailOff", "GPower", "Die", "Speed", "Chain", "ResetChain", "FreezeFrame",
            "Effect", "Attach", "RemoveAttachments",
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
                    if (!Answered.Contains(e.functionName)) unknown.Add(e.functionName);
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

        // ----- Heard -----

        /// <summary>One event heard, kept to be answered outside the callback it came in.</summary>
        private struct Heard
        {
            public string Name;
            public string Text;
            public Object Thing;
            public int Number;
            public float Weight;
        }

        private readonly List<Heard> _waiting = new List<Heard>();

        // Unity calls these by the event's name. Nothing is made inside the callback, where Unity
        // refuses to take components off a new copy, so each is kept and answered in Update.
        public void FootStep(AnimationEvent e) => Hear(e);
        public void Hit(AnimationEvent e) => Hear(e);
        public void OnAttackTrigger(AnimationEvent e) => Hear(e);
        public void Jump(AnimationEvent e) => Hear(e);
        public void Die(AnimationEvent e) => Hear(e);
        public void Effect(AnimationEvent e) => Hear(e);
        public void Attach(AnimationEvent e) => Hear(e);
        public void RemoveAttachments(AnimationEvent e) => Hear(e);
        public void Land(AnimationEvent e) { }
        public void TakeOff(AnimationEvent e) { }
        public void Stop(AnimationEvent e) { }
        public void DodgeMortal(AnimationEvent e) { }
        public void TrailOn(AnimationEvent e) { }
        public void TrailOff(AnimationEvent e) { }
        public void GPower(AnimationEvent e) { }
        public void Speed(AnimationEvent e) { }
        public void Chain(AnimationEvent e) { }
        public void ResetChain(AnimationEvent e) { }
        public void FreezeFrame(AnimationEvent e) { }

        /// <summary>An event fired by the animator itself, which knows how strongly its clip is blended in.</summary>
        private void Hear(AnimationEvent e)
        {
            _waiting.Add(new Heard
            {
                Name = e.functionName, Text = e.stringParameter, Thing = e.objectReferenceParameter,
                Number = e.intParameter, Weight = e.animatorClipInfo.weight,
            });
        }

        private void Update()
        {
            if (_waiting.Count == 0) return;
            var now = _waiting.ToArray();
            _waiting.Clear();
            foreach (var heard in now) Answer(heard);
        }

        /// <summary>An event of a clip Scry plays itself, which is all there is on the copy then.</summary>
        public void Answer(AnimationEvent e)
        {
            Answer(new Heard
            {
                Name = e.functionName, Text = e.stringParameter, Thing = e.objectReferenceParameter,
                Number = e.intParameter, Weight = 1f,
            });
        }

        private void Answer(Heard e)
        {
            if (_copy == null) return;
            switch (e.Name)
            {
                case "FootStep": Step(e); break;
                case "Hit":
                case "OnAttackTrigger": AttackTrigger(); break;
                case "Jump": Play(_prefab.GetComponent<Character>()?.m_jumpEffects); break;
                case "Die": Play(_prefab.GetComponent<Character>()?.m_deathEffects); break;
                case "Effect": Effect(e); break;
                case "Attach": Attach(e); break;
                case "RemoveAttachments": ClipEnded(); break;
            }
        }

        // ----- Answered -----

        private void Play(EffectList list)
        {
            if (list != null) Previews.PlayOnCopy(_copy, list, null);
        }

        /// <summary>
        /// A clip's own effect, named in the event: played at the bone the event names, else at
        /// the prefab's effect root, else at the animated body.
        /// </summary>
        private void Effect(Heard e)
        {
            var prefab = e.Thing as GameObject;
            if (prefab == null) return;

            Transform at = null;
            if (!string.IsNullOrEmpty(e.Text)) at = Utils.FindChild(transform, e.Text);
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
        private void Attach(Heard e)
        {
            var prefab = e.Thing as GameObject;
            if (prefab == null || string.IsNullOrEmpty(e.Text)) return;
            var joint = Utils.FindChild(transform, e.Text);
            if (joint == null) return;

            for (var i = _attached.Count - 1; i >= 0; i--)
            {
                if (_attached[i] != null && _attached[i].transform.parent != joint) continue;
                if (_attached[i] != null) Destroy(_attached[i]);
                _attached.RemoveAt(i);
            }

            var copy = _copy == Stage.Subject ? Stage.Hang(prefab, joint) : Ghost.MakeOn(prefab, joint, joint.position, joint.rotation);
            if (copy == null) return;
            if (e.Number == 10 || e.Number == -10) copy.transform.localScale = prefab.transform.localScale;
            copy.SetActive(true);
            _attached.Add(copy);
        }

        private void Step(Heard e)
        {
            // Blended-out clips send steps too; the game ignores the faint ones, and so do we.
            if (e.Weight < 0.33f || Time.unscaledTime - _lastStep < 0.08f) return;
            _lastStep = Time.unscaledTime;

            var step = _prefab.GetComponentInChildren<global::FootStep>(true);
            var effect = step != null ? Step(step) : null;
            if (effect == null) return;

            Transform foot = null;
            if (!string.IsNullOrEmpty(e.Text)) foot = Utils.FindChild(_copy.transform, e.Text);
            Previews.PlayOnCopy(_copy, AsList(effect.m_effectPrefabs), foot);
        }

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
