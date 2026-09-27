using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>What a clip plays besides its own events: what the game plays with it, what it was found by name to play, what is heard around it, and what it is made of.</summary>
    internal sealed partial class AnimationEars
    {
        /// <summary>
        /// What is heard around this clip, though the game does not play it with the clip
        /// (<see cref="Previews.AroundOfClip"/>), played as it starts and told apart in the panel.
        /// </summary>
        private void PlayAround(AnimationClip clip)
        {
            var list = clip != null && _prefab != null ? Previews.AroundOfClip(_prefab, _copy, clip.name) : null;
            if (list == null) return;
            Listen.Note(Listening, "heard around it, though the game does not play it with this clip");
            Report(Previews.PlayOnCopy(_copy, list, null));
        }

        /// <summary>What is heard around a clip, by prefab name, apart from what it plays itself.</summary>
        public List<string> AroundMembers(AnimationClip clip)
        {
            var names = new List<string>();
            var list = clip != null && _prefab != null ? Previews.AroundOfClip(_prefab, _copy, clip.name) : null;
            if (list?.m_effectPrefabs == null) return names;
            var own = Members(clip);
            foreach (var data in list.m_effectPrefabs)
            {
                if (data?.m_prefab == null || !data.m_enabled || names.Contains(data.m_prefab.name) || own.Contains(data.m_prefab.name)) continue;
                names.Add(data.m_prefab.name);
            }
            return names;
        }

        /// <summary>
        /// What the game plays as it moves the animator into this clip by one of its own actions,
        /// such as a jump or waking (<see cref="Previews.ListOfClip"/>), played as the clip starts.
        /// A clip whose own Jump event plays the jump leaves it to the event.
        /// </summary>
        private void PlayGameList(AnimationClip clip)
        {
            if (clip == null) return;
            var list = Previews.ListOfClip(_prefab, _copy, clip.name, out var lasting);
            if (list == null) return;
            var character = _prefab.GetComponent<Character>();
            if (list == character?.m_jumpEffects && System.Array.Exists(clip.events, e => e.functionName == "Jump")) return;
            if (list == character?.m_deathEffects && System.Array.Exists(clip.events, e => e.functionName == "Die")) return;

            if (list.m_effectPrefabs == null || !System.Array.Exists(list.m_effectPrefabs, d => d != null && d.m_enabled && d.m_prefab != null))
            {
                Listen.Note(Listening, "the game plays nothing with it: this creature's list for it is empty");
                return;
            }

            if (lasting)
            {
                Listen.Note(Listening, "what the game keeps going while it is in this state");
                PlayLasting(character, list);
                return;
            }
            Listen.Note(Listening, "what the game plays with it");
            Report(Previews.PlayOnCopy(_copy, list, null));
        }

        /// <summary>A list the game keeps going, kept going while the clip plays and gone with it; played again only once gone.</summary>
        private void PlayLasting(Character character, EffectList list)
        {
            if (_lasting.Exists(l => l != null)) return;
            _lasting.Clear();
            var made = Previews.PlayOnCopy(_copy, list, LastingPoint(character, list));
            foreach (var thing in made) if (thing != null) thing.transform.SetParent(_copy.transform, true);
            _lasting.AddRange(made);
            Report(made);
        }

        /// <summary>
        /// What a clip plays that it was paired with by its name alone (<see cref="Previews.ByNameOfClip"/>),
        /// played as it starts and told apart as found by name.
        /// </summary>
        private void PlayByName(AnimationClip clip)
        {
            if (clip == null) return;
            var list = Previews.ByNameOfClip(_prefab, _copy, clip.name, out var lasting);
            if (list == null) return;
            var character = _prefab.GetComponent<Character>();
            if (list == character?.m_jumpEffects && System.Array.Exists(clip.events, e => e.functionName == "Jump")) return;
            Listen.Note(Listening, "found by its name, not by the animator");
            if (lasting) PlayLasting(character, list);
            else Report(Previews.PlayOnCopy(_copy, list, null));
        }

        /// <summary>What a clip plays that it was paired with by its name alone, by prefab name, apart from what it plays itself.</summary>
        public List<string> ByNameMembers(AnimationClip clip)
        {
            var names = new List<string>();
            var list = clip != null && _prefab != null ? Previews.ByNameOfClip(_prefab, _copy, clip.name, out _) : null;
            if (list?.m_effectPrefabs == null) return names;
            var own = Members(clip);
            foreach (var data in list.m_effectPrefabs)
            {
                if (data?.m_prefab == null || !data.m_enabled || names.Contains(data.m_prefab.name) || own.Contains(data.m_prefab.name)) continue;
                names.Add(data.m_prefab.name);
            }
            return names;
        }

        private readonly List<GameObject> _lasting = new List<GameObject>();
        private Transform _lastingPoint;

        /// <summary>
        /// Where the game keeps a lasting list: the water effect at the water's surface, which a
        /// swimming creature is its swim depth below (Character.UpdateContinousEffects), the flying
        /// effect at the creature.
        /// </summary>
        private Transform LastingPoint(Character character, EffectList list)
        {
            if (_lastingPoint == null)
            {
                _lastingPoint = new GameObject("Scry lasting point").transform;
                _lastingPoint.SetParent(_copy.transform, false);
            }
            var depth = list == character?.m_waterEffects ? character.m_swimDepth + 0.05f : 0f;
            _lastingPoint.localPosition = Vector3.up * depth;
            return _lastingPoint;
        }

        /// <summary>
        /// What a clip plays of itself, by prefab name: the effects and props its events name,
        /// its jump and death, its footsteps on the chosen ground when it moves the feet, and its
        /// attack's start and swing when it is an attack.
        /// </summary>
        public List<string> Members(AnimationClip clip)
        {
            var names = new List<string>();
            void Add(GameObject prefab)
            {
                if (prefab != null && !names.Contains(prefab.name)) names.Add(prefab.name);
            }
            void AddList(EffectList list)
            {
                if (list?.m_effectPrefabs == null) return;
                foreach (var data in list.m_effectPrefabs) if (data != null && data.m_enabled) Add(data.m_prefab);
            }
            if (clip == null || _prefab == null) return names;

            var character = _prefab.GetComponent<Character>();
            var step = _prefab.GetComponentInChildren<global::FootStep>(true);
            foreach (var e in clip.events)
            {
                switch (e.functionName)
                {
                    case "Effect":
                    case "Attach":
                        Add(e.objectReferenceParameter as GameObject);
                        break;
                    case "Jump": AddList(character?.m_jumpEffects); break;
                    case "Die": AddList(character?.m_deathEffects); break;
                    case "FootStep":
                        var stepped = step != null ? Step(step, global::FootStep.MotionType.Jog | global::FootStep.MotionType.Walk, Previews.StepGround) : null;
                        if (stepped != null) foreach (var p in stepped.m_effectPrefabs) Add(p);
                        break;
                }
            }

            var name = clip.name.ToLowerInvariant();
            if (step != null && step.m_feet != null && step.m_feet.Length > 0 && System.Array.Exists(Moving, m => name.Contains(m)))
            {
                // Listed only for clips that walk, where a step is sure; others may step too.
                var moving = Step(step, MotionOf(name), Previews.StepGround);
                if (moving != null) foreach (var p in moving.m_effectPrefabs) Add(p);
            }

            AddList(Previews.ListOfClip(_prefab, _copy, clip.name));
            var part = AttackFor(clip.name);
            if (part?.Key is Attack attack)
            {
                Previews.AttackParts(attack, out var begin, out var trigger, out var hit);
                if (part.Begins) AddList(begin);
                if (part.Strikes)
                {
                    AddList(trigger);
                    AddList(hit);
                    if (Previews.IsMelee(attack) || attack.m_attackType == Attack.AttackType.Area) Add(attack.m_spawnOnTrigger);
                    if (attack.m_attackType == Attack.AttackType.Projectile) Add(attack.m_attackProjectile);
                }
            }
            return names;
        }
    }
}
