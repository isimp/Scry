using System;
using System.Collections.Generic;
using System.Linq;
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
            "Effect", "Attach", "RemoveAttachments", "HideObject", "ShowObject",
        };

        private static readonly HashSet<string> Told = new HashSet<string>();
        private static readonly HashSet<string> Listed = new HashSet<string>();

        /// <summary>Whether Scry answers an animation event of this name.</summary>
        public static bool Answers(string name) => Answered.Contains(name);

        private GameObject _prefab;
        private GameObject _copy;
        private float _lastStep;
        private readonly List<GameObject> _attached = new List<GameObject>();

        /// <summary>Lets a copy's animations sound, when all of their events can be answered.</summary>
        public static void Attach(GameObject prefab, GameObject copy)
        {
            var animator = ClipPlayer.AnimatorOf(copy);
            if (animator == null) return;

            // Said once per prefab: which animator plays it and every clip it has, for finding out
            // why a clip seems missing.
            if (Listed.Add(prefab.name))
            {
                var all = copy.GetComponentsInChildren<Animator>(true).Length;
                var names = animator.runtimeAnimatorController.animationClips.Where(c => c != null).Select(c => c.name).Distinct().OrderBy(n => n).ToList();
                var settings = string.Join(", ", animator.parameters.Select(p => $"{p.name} ({p.type.ToString().ToLowerInvariant()})"));
                var layers = string.Join(", ", Enumerable.Range(0, animator.layerCount).Select(l => $"{animator.GetLayerName(l)} at {animator.GetLayerWeight(l):0.##}"));
                Plugin.Log.LogInfo($"Scry plays {prefab.name} by the animator on {animator.gameObject.name} ({animator.runtimeAnimatorController.name}, {all} animators on the copy), {names.Count} clips: {string.Join(", ", names)}. Its settings: {(settings.Length > 0 ? settings : "none")}. Its layers: {layers}.");
            }

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

        /// <summary>
        /// A clip starting. An attack's clip plays what the attack plays as it begins now, and
        /// what lands when it strikes (<see cref="ClipStrike"/>). Quiet, its attack is left to
        /// whoever started it, who plays the one it was asked for.
        /// </summary>
        public void ClipStarted(AnimationClip clip, bool quiet = false)
        {
            _quiet = quiet;
            StartAttack(clip);
            PlayGameList(clip);
            WatchFeet(clip);
        }

        /// <summary>A clip played again from its start begins its attack again; its feet are still watched.</summary>
        public void ClipRepeated(AnimationClip clip)
        {
            StartAttack(clip);
            PlayGameList(clip);
        }

        /// <summary>
        /// What the game plays as it moves the animator into this clip by one of its own actions,
        /// such as a jump or waking (<see cref="Previews.ListOfClip"/>), played as the clip starts.
        /// A clip whose own Jump event plays the jump leaves it to the event.
        /// </summary>
        private void PlayGameList(AnimationClip clip)
        {
            var list = clip != null && !_quiet ? Previews.ListOfClip(_prefab, _copy, clip.name) : null;
            if (list == null) return;
            if (list == _prefab.GetComponent<Character>()?.m_jumpEffects && System.Array.Exists(clip.events, e => e.functionName == "Jump")) return;
            Listen.Note(Listening, "what the game plays with it");
            Report(Previews.PlayOnCopy(_copy, list, null));
        }

        private void StartAttack(AnimationClip clip)
        {
            _swing = null;
            _strike = null;
            _strikeKey = null;
            var part = clip != null && !_quiet ? AttackFor(clip.name) : null;
            _clipAttack = part?.Key as Attack;
            _clipStrike = null;
            _strikeAt = -1f;
            if (_clipAttack != null)
            {
                Previews.AttackParts(_clipAttack, out var begin, out _clipStrike);
                Listen.Note(Listening, "the attack " + _clipAttack.m_attackAnimation + (part.Begins ? "" : ", begun in an earlier clip"));

                // The game strikes when a clip says so; an attack whose clips never say, halfway.
                if (part.Halfway)
                {
                    _strikeAt = clip.length * 0.5f;
                    Listen.Note(Listening, "no clip of the attack says when it strikes, so it strikes halfway");
                }
                if (part.Begins) Report(Previews.PlayOnCopy(_copy, begin, null));
            }
        }

        private Attack _clipAttack;
        private EffectList _clipStrike;
        private float _strikeAt = -1f;

        /// <summary>
        /// What lands when an attack's clip strikes, where the attack strikes, as <c>Attack</c>
        /// plays its trigger and hit there, what it spawns there, and what it throws or shoots.
        /// </summary>
        private void ClipStrike()
        {
            var attack = _clipAttack;
            _strikeAt = -1f;
            if (attack == null || _copy == null) return;
            if (_clipStrike != null) Report(Previews.PlayOnCopyAt(_copy, _clipStrike, Previews.StrikePoint(_copy, attack, Previews.LandsOnGround(_clipStrike))));
            if (attack.m_spawnOnTrigger != null)
            {
                Report(Previews.PlayOnCopyAt(_copy, AsList(new[] { attack.m_spawnOnTrigger }), Previews.StrikePoint(_copy, attack, false)));
                Listen.Note(Listening, "spawned " + attack.m_spawnOnTrigger.name);
            }
            Launch(attack);
        }

        // ----- Steps -----

        /// <summary>Words of the clips in which a creature walks, runs or otherwise moves on its feet.</summary>
        private static readonly string[] Moving = { "walk", "run", "jog", "sneak", "trot", "gallop", "move", "crawl", "charge", "sprint", "stroll", "step", "turn", "strafe" };

        private bool _quiet;
        private Attack _swing;
        private float _swingUntil;

        /// <summary>
        /// An attack swung on this copy by its animator trigger: when its animation strikes, it
        /// throws or shoots what it throws, as <c>Attack</c> does at that moment.
        /// </summary>
        public void Swinging(Attack attack, EffectList strike = null, string heard = null, EffectList key = null)
        {
            _swing = attack;
            _swingUntil = Time.unscaledTime + 4f;
            _strike = strike;
            _strikeHeard = heard;
            _strikeKey = key ?? strike;
        }

        private EffectList _strike;
        private EffectList _strikeKey;
        private string _strikeHeard;

        /// <summary>
        /// Plays what lands where the swing strikes, when it strikes, as <c>Attack</c> plays its
        /// hit and trigger effects there; also when the animation never says so, late.
        /// </summary>
        private void Strike(Attack attack)
        {
            var strike = _strike;
            _strike = null;
            if (strike == null || attack == null || _copy == null) return;
            var made = Previews.PlayOnCopyAt(_copy, strike, Previews.StrikePoint(_copy, attack, Previews.LandsOnGround(strike)));
            Previews.Struck(_strikeKey ?? strike, _strikeHeard, made);
        }

        /// <summary>Throws or shoots the swung attack's projectile from where the attack sends it.</summary>
        private void Throw()
        {
            var attack = _swing;
            _swing = null;
            if (Time.unscaledTime > _swingUntil) return;
            Strike(attack);
            Launch(attack);
        }

        /// <summary>
        /// Throws or shoots an attack's projectile, as <c>Attack</c> does when its swing strikes:
        /// from where the attack sends it, along the creature's facing and its launch angle.
        /// </summary>
        private void Launch(Attack attack)
        {
            if (attack == null || _copy == null) return;
            if (attack.m_attackProjectile == null)
            {
                if (attack.m_attackType == Attack.AttackType.Projectile) Listen.Note(Listening, "a projectile attack that names no projectile");
                return;
            }

            // Where Attack.GetProjectileSpawnPoint puts it, at the copy's size.
            var t = _copy.transform;
            var size = t.lossyScale.x;
            var origin = attack.m_attackOriginJoint.Length > 0 ? Utils.FindChild(t, attack.m_attackOriginJoint) : null;
            if (origin == null) origin = t;
            var start = origin.position + t.up * attack.m_attackHeight * size + t.forward * attack.m_attackRange * size + t.right * attack.m_attackOffset * size;
            var aim = t.forward;
            if (attack.m_launchAngle != 0f) aim = Quaternion.AngleAxis(attack.m_launchAngle, Vector3.Cross(Vector3.up, aim)) * aim;

            var thrown = Previews.Launch(attack.m_attackProjectile, start, aim * attack.m_projectileVel, _copy == Stage.Subject);
            if (thrown != null)
            {
                Report(new List<GameObject> { thrown });
                var heard = Listening ?? _strikeHeard;
                if (_strikeKey != null) Previews.Struck(_strikeKey, heard, new List<GameObject> { thrown });
                Listen.Add(heard, new List<GameObject> { thrown });
                Listen.Note(heard, "threw " + attack.m_attackProjectile.name);
            }
        }
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

            var feet = new List<Transform>();
            foreach (var foot in step.m_feet)
            {
                var twin = foot != null ? Looks.Twin(_prefab.transform, _copy.transform, foot) : null;
                if (twin != null) feet.Add(twin);
            }
            if (feet.Count == 0) return;

            _feet = feet.ToArray();
            _footing = new StepDetector[_feet.Length];
            for (var i = 0; i < _footing.Length; i++) _footing[i] = new StepDetector();
            _motion = MotionOf(name);
        }

        private static global::FootStep.MotionType MotionOf(string name)
        {
            return name.Contains("run") || name.Contains("sprint") || name.Contains("gallop") || name.Contains("charge")
                ? global::FootStep.MotionType.Run
                : name.Contains("sneak") || name.Contains("crawl") ? global::FootStep.MotionType.Sneak
                : name.Contains("walk") || name.Contains("stroll") ? global::FootStep.MotionType.Walk
                : global::FootStep.MotionType.Jog;
        }

        /// <summary>Lights what a playing clip made under its chip.</summary>
        private void Report(List<GameObject> made)
        {
            var player = _copy != null ? _copy.GetComponent<ClipPlayer>() : null;
            var clip = player != null ? player.Clip : null;
            if (clip != null && made.Count > 0) Previews.Heard(clip, made);
            Listen.Add(Listening, made);
        }

        /// <summary>The clip playing on this copy, as the log names it.</summary>
        private string Listening
        {
            get
            {
                var player = _copy != null ? _copy.GetComponent<ClipPlayer>() : null;
                return player != null ? player.Heard(player.Clip) : null;
            }
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
            if (clip == null) return names;

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
                Previews.AttackParts(attack, out var begin, out var strike);
                if (part.Begins) AddList(begin);
                if (part.Strikes)
                {
                    AddList(strike);
                    Add(attack.m_spawnOnTrigger);
                    Add(attack.m_attackProjectile);
                }
            }
            return names;
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

        /// <summary>What a clip hung on the copy comes off when it ends, as it does when the game's animation moves on.</summary>
        public void ClipEnded()
        {
            foreach (var attached in _attached) if (attached != null) Destroy(attached);
            _attached.Clear();
            _feet = null;
            _quiet = false;
            _clipAttack = null;
            _strikeAt = -1f;
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
        public void HideObject(AnimationEvent e) => Hear(e);
        public void ShowObject(AnimationEvent e) => Hear(e);
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
            WatchSteps();
            if (_strikeAt >= 0f && ClipPlayer.Position(_copy, out var time, out _) && time >= _strikeAt) ClipStrike();

            // A swing whose animation sends no strike still lands, a little late.
            if (_swing != null && _strike != null && Time.unscaledTime > _swingUntil - 1.5f)
            {
                var attack = _swing;
                _swing = null;
                Strike(attack);
            }
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
            Listen.Note(Listening, "event " + e.Name);
            switch (e.Name)
            {
                case "FootStep": Step(e); break;
                case "Hit":
                case "OnAttackTrigger":
                    if (_swing != null) Throw();
                    AttackTrigger();
                    break;
                case "Jump": Play(_prefab.GetComponent<Character>()?.m_jumpEffects); break;
                case "Die": Play(_prefab.GetComponent<Character>()?.m_deathEffects); break;
                case "Effect": Effect(e); break;
                case "Attach": Attach(e); break;
                case "RemoveAttachments": ClipEnded(); break;
                case "HideObject": Toggle(e.Text, false); break;
                case "ShowObject": Toggle(e.Text, true); break;
            }
        }

        /// <summary>
        /// As <c>AnimationObjectToggle</c>: a part named in the event, under the part the script
        /// names or under the animated body, shown or hidden (a frost troll's club drawn, say).
        /// </summary>
        private void Toggle(string name, bool on)
        {
            if (string.IsNullOrEmpty(name)) return;
            var toggle = _prefab.GetComponentInChildren<AnimationObjectToggle>(true);
            var under = toggle != null && toggle.m_parentTransform != null ? Looks.Twin(_prefab.transform, _copy.transform, toggle.m_parentTransform) : null;
            var part = (under != null ? under : transform).Find(name);
            if (part != null) part.gameObject.SetActive(on);
        }

        // ----- Answered -----

        private void Play(EffectList list)
        {
            if (list != null) Report(Previews.PlayOnCopy(_copy, list, null));
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

            Report(Previews.PlayOnCopy(_copy, AsList(new[] { prefab }), at));
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
            var effect = step != null ? Step(step, global::FootStep.MotionType.Jog | global::FootStep.MotionType.Walk, Previews.StepGround) : null;
            if (effect == null) return;

            Transform foot = null;
            if (!string.IsNullOrEmpty(e.Text)) foot = Utils.FindChild(_copy.transform, e.Text);
            Report(Previews.PlayOnCopy(_copy, AsList(effect.m_effectPrefabs), foot));
        }

        // ----- Helpers -----

        /// <summary>The strike of the attack whose clip is playing, when the clip says it strikes.</summary>
        private void AttackTrigger()
        {
            var clip = _copy.GetComponent<ClipPlayer>()?.Clip;
            if (clip == null || _quiet || _clipAttack == null) return;
            ClipStrike();
        }

        /// <summary>What a clip of this prefab plays of an attack, as its animator plays it; null for a clip no attack plays.</summary>
        private ClipAttack AttackFor(string clip) => Previews.AttackOfClip(_prefab, _copy, clip);

        /// <summary>The step a walk on plain ground makes, or failing that the first there is.</summary>
        /// <summary>
        /// The step made on the chosen ground in this way of moving; failing that, any on that
        /// ground; failing those, the same on plain ground; failing that, the first there is. Ways
        /// of moving and grounds are sets of flags in the game.
        /// </summary>
        private static global::FootStep.StepEffect Step(global::FootStep step, global::FootStep.MotionType motion, global::FootStep.GroundMaterial ground)
        {
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
