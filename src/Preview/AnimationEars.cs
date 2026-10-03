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
    internal sealed partial class AnimationEars : MonoBehaviour
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static AnimationEars() => WorldCaches.Register(nameof(AnimationEars), Forget);

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
            if (Plugin.LogPreviews && Listed.Add(prefab.name))
            {
                var all = copy.GetComponentsInChildren<Animator>(true).Length;
                var names = animator.runtimeAnimatorController.animationClips.Where(c => c != null).Select(c => c.name).Distinct().OrderBy(n => n).ToList();
                var settings = string.Join(", ", animator.parameters.Select(p => $"{p.name} ({p.type.ToString().ToLowerInvariant()})"));
                var layers = string.Join(", ", Enumerable.Range(0, animator.layerCount).Select(l => $"{animator.GetLayerName(l)} at {Numbers.Amount(animator.GetLayerWeight(l), 2)}"));
                Plugin.Note($"Scry plays {prefab.name} by the animator on {animator.gameObject.name} ({animator.runtimeAnimatorController.name}, {Numbers.Count(all)} animators on the copy), {Numbers.Count(names.Count)} clips: {string.Join(", ", names)}. Its settings: {(settings.Length > 0 ? settings : "none")}. Its layers: {layers}.");
            }

            var (events, unknown) = EventsOf(animator.runtimeAnimatorController);

            // Said once per prefab, so it can be told why a creature's clips stay silent.
            if (Told.Add(prefab.name))
            {
                if (unknown.Count > 0) Plugin.Note($"Scry leaves the animations of {prefab.name} silent: its clips send events it cannot answer ({string.Join(", ", unknown)}).");
                else if (events == 0) Plugin.Note($"Scry: the animations of {prefab.name} send no events, so they have no sounds of their own.");
                else Plugin.Note($"Scry answers the {Numbers.Count(events)} animation events of {prefab.name}.");
            }
            if (unknown.Count > 0) return;

            var ears = animator.gameObject.AddComponent<AnimationEars>();
            ears._prefab = prefab;
            ears._copy = copy;
            animator.fireEvents = true;
        }

        /// <summary>How many events a controller's clips send, and those Scry does not answer.</summary>
        private static readonly Dictionary<RuntimeAnimatorController, (int Events, SortedSet<string> Unknown)> EventsByController =
            new Dictionary<RuntimeAnimatorController, (int, SortedSet<string>)>();

        /// <summary>
        /// Read once per controller: every copy made asks, on the stage and in the world at each
        /// selection, and each clip's events are made anew each time they are read (a person's
        /// controller has hundreds of clips).
        /// </summary>
        private static (int Events, SortedSet<string> Unknown) EventsOf(RuntimeAnimatorController controller)
        {
            if (EventsByController.TryGetValue(controller, out var known)) return known;
            var unknown = new SortedSet<string>();
            var events = 0;
            foreach (var clip in controller.animationClips)
            {
                if (clip == null) continue;
                foreach (var e in clip.events)
                {
                    events++;
                    if (!Answered.Contains(e.functionName)) unknown.Add(e.functionName);
                }
            }
            known = (events, unknown);
            EventsByController[controller] = known;
            return known;
        }

        /// <summary>What a controller's clips send, as read while the catalog was, kept for its copies.</summary>
        public static void Remember(RuntimeAnimatorController controller, int events, SortedSet<string> unknown)
        {
            if (controller != null && !EventsByController.ContainsKey(controller)) EventsByController[controller] = (events, unknown);
        }

        /// <summary>Lets go of what was read of the controllers, for a world that was left.</summary>
        public static void Forget() => EventsByController.Clear();

        /// <summary>
        /// A clip starting: an attack's plays what the attack plays as it begins now, and what
        /// lands when it strikes (<see cref="ClipStrike"/>); any plays what the game plays with it,
        /// what it was found by name to play, and what is heard around it; and walking, its feet
        /// are watched. While the animator is still being watched, what the clip plays is not
        /// known yet: the clip shows at once, and the rest follows once it is (<see cref="Update"/>).
        /// </summary>
        public void ClipStarted(AnimationClip clip)
        {
            if (_prefab != null && _copy != null) Safely("its feet", () => WatchFeet(clip));
            Begin(clip);
        }

        /// <summary>A clip played again from its start begins its attack again; its feet are still watched.</summary>
        public void ClipRepeated(AnimationClip clip) => Begin(clip);

        /// <summary>The clip that started while what it plays was not known yet, to be begun once it is.</summary>
        private AnimationClip _pending;

        private void Begin(AnimationClip clip)
        {
            _pending = null;
            if (_prefab == null || _copy == null || clip == null) return;
            if (!Previews.ClipsKnown(_prefab, _copy))
            {
                _pending = clip;
                Listen.Note(Listening, "what it plays is still being worked out; it follows once it is");
                return;
            }
            Safely("its attack", () => StartAttack(clip));
            Safely("what the game plays with it", () => PlayGameList(clip));
            Safely("what it was found by name to play", () => PlayByName(clip));
            Safely("what is heard around it", () => PlayAround(clip));
        }

        /// <summary>
        /// A clip that had to wait, begun now that what it plays is known, if it still plays. Its
        /// attack's strike already passed goes off at once, since the clip will not say so again.
        /// </summary>
        private void BeginPending()
        {
            if (_pending == null || _prefab == null || !Previews.ClipsKnown(_prefab, _copy)) return;
            var clip = _pending;
            _pending = null;
            var player = _copy != null ? _copy.GetComponent<ClipPlayer>() : null;
            if (player == null || player.Clip != clip || !ClipPlayer.Position(_copy, out var time, out _)) return;

            Begin(clip);
            if (_clipAttack == null || _strikeAt >= 0f) return;
            foreach (var e in player.Events)
            {
                if (e.time > time) break;
                if (e.functionName != "Hit" && e.functionName != "OnAttackTrigger") continue;
                Safely("its attack's strike", ClipStrike);
                break;
            }
        }

        /// <summary>One thing answered for a clip or an event; one that fails is left out and told once, and the rest still go on.</summary>
        private void Safely(string what, Action act)
        {
            try
            {
                act();
            }
            catch (Exception ex)
            {
                Faults.Tell($"{(_prefab != null ? _prefab.name : "a copy")}'s animations playing {what}", ex);
            }
        }

        // ----- Clips -----

        /// <summary>The copy's body, as the game finds a character's: its part called "Visual", else the copy.</summary>
        private Transform Body
        {
            get
            {
                var body = _copy.transform.Find("Visual");
                return body != null ? body : _copy.transform;
            }
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
                // Only named while diagnostics are logged; otherwise nothing is listened to.
                if (!Plugin.LogPreviews) return null;
                var player = _copy != null ? _copy.GetComponent<ClipPlayer>() : null;
                return player != null ? player.Heard(player.Clip) : null;
            }
        }

        /// <summary>What a clip hung on the copy comes off when it ends, as it does when the game's animation moves on.</summary>
        public void ClipEnded()
        {
            _pending = null;
            foreach (var attached in _attached) if (attached != null) Destroy(attached);
            _attached.Clear();
            foreach (var lasting in _lasting) if (lasting != null) Destroy(lasting);
            _lasting.Clear();
            _feet = null;
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
            if (_prefab == null || _copy == null)
            {
                _waiting.Clear();
                return;
            }
            Safely("its footsteps", WatchSteps);
            BeginPending();
            if (_strikeAt >= 0f && ClipPlayer.Position(_copy, out var time, out _) && time >= _strikeAt) Safely("its attack's strike", ClipStrike);

            if (_waiting.Count == 0) return;
            var now = _waiting.ToArray();
            _waiting.Clear();
            foreach (var heard in now) Safely("the event " + heard.Name, () => Answer(heard));
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
            if (_copy == null || _prefab == null) return;
            if (Plugin.LogPreviews) Listen.Note(Listening, "event " + e.Name);
            switch (e.Name)
            {
                case "FootStep": Step(e); break;
                case "Hit":
                case "OnAttackTrigger":
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

        // ----- Helpers -----

        /// <summary>The prefab this copy is of: a creature, or the person trying items on.</summary>
        public GameObject Prefab => _prefab;

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
