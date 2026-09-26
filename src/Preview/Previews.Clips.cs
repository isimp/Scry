using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>Animation clips: playing them, and what each plays of an attack, of the game's own actions, by its name or heard around it.</summary>
    internal static partial class Previews
    {
        /// <summary>What a creature's clips play: of its attacks, and what the game plays with its own actions.</summary>
        private sealed class ClipPlays
        {
            public Dictionary<string, ClipAttack> Attacks = new Dictionary<string, ClipAttack>();
            public Dictionary<string, object> Actions = new Dictionary<string, object>();
            public Dictionary<string, object> Around = new Dictionary<string, object>();

            /// <summary>The flying effect of a born flyer its animator has no flying switch for, kept going in every clip.</summary>
            public EffectList Flying;

            /// <summary>What clips play that were paired by their names alone (<see cref="ClipByName"/>).</summary>
            public Dictionary<string, object> ByName = new Dictionary<string, object>();

            /// <summary>What each clip is, in a few words, for its chip: the attack it plays, or what the creature does in it.</summary>
            public Dictionary<string, string> Tags = new Dictionary<string, string>();

            /// <summary>Whether each clip plays any sound or effect, as worked out when first asked.</summary>
            public Dictionary<string, bool> Sounding = new Dictionary<string, bool>();

            /// <summary>False while the animator is still being watched.</summary>
            public bool Ready = true;
        }

        /// <summary>
        /// Whether a clip of the stage copy plays any sound or effect: of its own, with its attack
        /// or the game's actions, found by name, or heard around it. Worked out once per clip.
        /// </summary>
        public static bool ClipSounds(AnimationClip clip)
        {
            var ears = clip != null ? ClipPlayer.AnimatorOf(Stage.Subject)?.GetComponent<AnimationEars>() : null;
            var plays = ears != null ? PlaysOf(ears.Prefab, Stage.Subject, wait: false) : null;
            if (plays == null || !plays.Ready) return false;
            if (!plays.Sounding.TryGetValue(clip.name, out var sounds))
            {
                sounds = ears.Members(clip).Count > 0 || ears.ByNameMembers(clip).Count > 0 || ears.AroundMembers(clip).Count > 0;
                plays.Sounding[clip.name] = sounds;
            }
            return sounds;
        }

        /// <summary>What the game's own actions are called on a clip's chip.</summary>
        private static readonly Dictionary<string, string> ActionWords = new Dictionary<string, string>
        {
            ["jump"] = "jumps", ["consume"] = "eats", ["sleep"] = "sleeps", ["wake"] = "wakes", ["alert"] = "alerted",
            ["spawn"] = "spawns", ["stagger"] = "staggers", ["water"] = "in water", ["swim"] = "swims", ["fly"] = "flies", ["dead"] = "dies",
        };

        private static readonly Dictionary<string, string> NoTags = new Dictionary<string, string>();

        /// <summary>What each clip of the stage copy is, in a few words, by clip name: the attack it plays or what the creature does in it.</summary>
        public static IReadOnlyDictionary<string, string> ClipTags()
        {
            var ears = ClipPlayer.AnimatorOf(Stage.Subject)?.GetComponent<AnimationEars>();
            var plays = ears != null ? PlaysOf(ears.Prefab, Stage.Subject, wait: false) : null;
            return plays != null ? plays.Tags : NoTags;
        }

        /// <summary>
        /// The stage copy's clips an effect list goes with: those that play it (as the game does
        /// with its own actions, or an item's attack), those it was found by name to go with, and
        /// those it is heard around, each said which.
        /// </summary>
        public static List<(AnimationClip Clip, string How)> ClipsOfList(EffectList list)
        {
            var found = new List<(AnimationClip, string)>();
            var ears = list != null ? ClipPlayer.AnimatorOf(Stage.Subject)?.GetComponent<AnimationEars>() : null;
            var plays = ears != null ? PlaysOf(ears.Prefab, Stage.Subject) : null;
            if (plays == null) return found;

            var clips = Clips();
            void Add(string name, string how)
            {
                var clip = clips.Find(c => c.name == name);
                if (clip != null && !found.Exists(f => f.Item1 == clip)) found.Add((clip, how));
            }
            if (AttackOf.TryGetValue(list, out var attack) && AttackClipOf(attack) is AnimationClip swing) Add(swing.name, "");
            foreach (var pair in plays.Actions) if (pair.Value == list) Add(pair.Key, "");
            foreach (var pair in plays.ByName) if (pair.Value == list) Add(pair.Key, "by name");
            foreach (var pair in plays.Around) if (pair.Value == list) Add(pair.Key, "heard around");
            return found;
        }

        private static readonly Dictionary<string, ClipPlays> ClipPlaysCache = new Dictionary<string, ClipPlays>();

        /// <summary>
        /// What a creature's clip plays of an attack (<see cref="ClipAttacks"/>), its
        /// <see cref="ClipAttack.Key"/> the <see cref="Attack"/>: of the attacks its animator can
        /// start, those of what it has now first, then those of all it may carry. Null for a clip
        /// no attack plays.
        /// </summary>
        public static ClipAttack AttackOfClip(GameObject prefab, GameObject copy, string clip)
        {
            var plays = string.IsNullOrEmpty(clip) ? null : PlaysOf(prefab, copy);
            return plays != null && plays.Attacks.TryGetValue(clip, out var part) ? part : null;
        }

        /// <summary>
        /// What the game plays as it moves a creature's animator into a clip by one of its own
        /// actions (<see cref="ClipActions"/>): a jump's effects with the clip the jump leads to,
        /// waking's with the wake-up, going to sleep's, being alerted's, eating's, even an empty
        /// list, which the game then plays as nothing. Null for none.
        /// </summary>
        public static EffectList ListOfClip(GameObject prefab, GameObject copy, string clip) => ListOfClip(prefab, copy, clip, out _);

        /// <summary>
        /// As <see cref="ListOfClip(GameObject, GameObject, string)"/>, and whether the game keeps
        /// it going for as long as the creature is there (in water, flying) rather than playing it
        /// once.
        /// </summary>
        public static EffectList ListOfClip(GameObject prefab, GameObject copy, string clip, out bool lasting)
        {
            lasting = false;
            var plays = string.IsNullOrEmpty(clip) ? null : PlaysOf(prefab, copy);
            if (plays == null) return null;
            var character = prefab.GetComponent<Character>();
            if (!plays.Actions.TryGetValue(clip, out var list))
            {
                // A born flyer the animator has no flying switch for flies whatever it plays, and
                // the game keeps its flying effect going all the while (Character.IsFlying).
                if (plays.Flying == null) return null;
                lasting = true;
                return plays.Flying;
            }
            lasting = character != null && (list == character.m_waterEffects || list == character.m_flyingContinuousEffect);
            return (EffectList)list;
        }

        /// <summary>
        /// What is heard around a creature's clip, though the game does not play it with the clip
        /// (<see cref="ClipAround"/>): with waking and a spawn roar, what it calls out once
        /// alerted; with the clips it idles in, its idle sound. Null for none.
        /// </summary>
        public static EffectList AroundOfClip(GameObject prefab, GameObject copy, string clip)
        {
            var plays = string.IsNullOrEmpty(clip) ? null : PlaysOf(prefab, copy);
            return plays != null && plays.Around.TryGetValue(clip, out var list) ? (EffectList)list : null;
        }

        /// <summary>
        /// What a clip plays that it was paired with by its name alone, where the animator could
        /// not be seen to go there (<see cref="ClipByName"/>), and whether the game keeps it
        /// going. Null for none.
        /// </summary>
        public static EffectList ByNameOfClip(GameObject prefab, GameObject copy, string clip, out bool lasting)
        {
            lasting = false;
            var plays = string.IsNullOrEmpty(clip) ? null : PlaysOf(prefab, copy);
            if (plays == null || !plays.ByName.TryGetValue(clip, out var found)) return null;
            lasting = found == prefab.GetComponent<Character>()?.m_waterEffects;
            return (EffectList)found;
        }

        /// <summary>
        /// The clip an item's attack plays first on the person trying it on, as the person's
        /// animator plays it for the attack's trigger in the stance the item gives; null for none.
        /// </summary>
        private static AnimationClip AttackClipOf(Attack attack)
        {
            var ears = ClipPlayer.AnimatorOf(Stage.Subject)?.GetComponent<AnimationEars>();
            var plays = ears != null ? PlaysOf(ears.Prefab, Stage.Subject) : null;
            if (plays == null) return null;
            foreach (var part in plays.Attacks)
            {
                if (part.Value.Key == attack && part.Value.Begins) return Clips().Find(c => c.name == part.Key);
            }
            return null;
        }

        /// <summary>What clips play while their creature's animator is still being watched: nothing yet.</summary>
        private static readonly ClipPlays NotYet = new ClipPlays { Ready = false };

        /// <summary>
        /// What each of a creature's clips plays. The animator is watched over the next frames; till
        /// then <see cref="NotYet"/>, unless <paramref name="wait"/>, when it is finished at once.
        /// </summary>
        private static ClipPlays PlaysOf(GameObject prefab, GameObject copy, bool wait = true)
        {
            if (prefab == null) return null;

            // What it has now: a creature what its look carries; the person trying an item on
            // what it wears, the item first.
            var worn = Looks.IsWorn(_entry) && !ReferenceEquals(_entry.Source, prefab) ? Looks.WornWith((GameObject)_entry.Source) : null;
            var carried = worn ?? (_entry != null && ReferenceEquals(_entry.Source, prefab) ? CarriedNow(_entry) : null);
            var cacheKey = prefab.name + "|" + (carried == null ? "" : string.Join(",", carried.Select(c => c.name)));
            if (ClipPlaysCache.TryGetValue(cacheKey, out var plays)) return plays;

            plays = new ClipPlays();
            ClipPlaysCache[cacheKey] = plays;
            var animator = ClipPlayer.AnimatorOf(copy);
            if (animator == null) return plays;

            var items = worn ?? Relations.CarriedItems(prefab);
            if (carried != null) items = items.Where(carried.Contains).Concat(items.Where(i => !carried.Contains(i))).ToList();
            var attacks = new List<(string, object)>();
            var names = new Dictionary<Attack, string>();
            foreach (var item in items)
            {
                var shared = item.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (shared == null) continue;
                foreach (var attack in new[] { shared.m_attack, shared.m_secondaryAttack })
                {
                    var trigger = attack != null ? TriggerOf(animator, attack) : null;
                    if (trigger == null) continue;
                    WeaponOf[attack] = shared;
                    attacks.Add((trigger, attack));
                    var name = (CatalogBuilder.GameName(item) ?? WeaponChoices.Readable(item.name, prefab.name)).ToLowerInvariant();
                    names[attack] = "attack " + name + (attack == shared.m_secondaryAttack ? ", second" : "");
                }
            }

            var actions = GameLists(prefab).ToList();
            if (attacks.Count == 0 && actions.Count == 0 && prefab.GetComponent<BaseAI>() == null) return plays;
            var clips = animator.runtimeAnimatorController.animationClips.Where(c => c != null).ToList();
            var striking = clips.Where(c => c.events.Any(e => e.functionName == "Hit" || e.functionName == "OnAttackTrigger")).Select(c => c.name);
            var seen = TriggerProbe.ClipsOf(prefab.name, animator, attacks.Select(a => a.Item1), wait);
            if (seen.Busy)
            {
                ClipPlaysCache.Remove(cacheKey);
                return NotYet;
            }
            plays.Attacks = ClipAttacks.Match(attacks, seen.Triggers, clips.Select(c => c.name), striking);
            plays.Actions = ClipActions.Match(actions, seen.Actions, plays.Attacks.Keys);

            // What is heard next or now and then: waking, a troll is silent and roars once it
            // spots you (BaseAI.SetAlerted); awake, it makes its idle sound now and then
            // (BaseAI.DoIdleSound), whatever it plays.
            var ai = prefab.GetComponent<BaseAI>();
            var alerted = ai != null && HasAny(ai.m_alertedEffects) ? ai.m_alertedEffects : null;
            var around = new List<(string, object)>();
            if (alerted != null)
            {
                around.Add(("wake", alerted));
                around.Add(("spawn", alerted));
            }

            // Staggered, it was hit, again and again (Character.AddStaggerDamage).
            var hit = prefab.GetComponent<Character>()?.m_hitEffects;
            if (hit != null && HasAny(hit)) around.Add(("stagger", hit));
            var idleSound = ai != null && HasAny(ai.m_idleSound) ? ai.m_idleSound : null;
            plays.Around = ClipAround.Match(around, seen.Actions, seen.Idle, idleSound, plays.Attacks.Keys);

            // Where the animator could not be seen to swim or jump, clips named so.
            var body = prefab.GetComponent<Character>();
            var named = new List<(string, string[], object)>();
            if (body != null && HasAny(body.m_waterEffects)) named.Add(("water", new[] { "swim", "tread" }, body.m_waterEffects));
            if (body != null && HasAny(body.m_jumpEffects)) named.Add(("jump", new[] { "jump" }, body.m_jumpEffects));
            var tried = new Dictionary<string, IReadOnlyList<string>>();
            IReadOnlyList<string> Saw(string action) => seen.Actions.TryGetValue(action, out var s) ? s : new string[0];
            tried["water"] = Saw("water").Concat(Saw("swim")).ToList();
            tried["jump"] = Saw("jump");
            plays.ByName = ClipByName.Match(named, tried, clips.Select(c => c.name), plays.Attacks.Keys.Concat(plays.Actions.Keys));

            if (body != null && body.m_flying && HasAny(body.m_flyingContinuousEffect) && !animator.parameters.Any(p => p.name == "flying")) plays.Flying = body.m_flyingContinuousEffect;

            // What each clip is: the attack it plays, what the animator was seen to do there,
            // what it was named for, or idling.
            foreach (var part in plays.Attacks) if (part.Value.Key is Attack attack && names.TryGetValue(attack, out var name)) plays.Tags[part.Key] = name;
            foreach (var pair in seen.Actions)
            {
                if (pair.Value.Count > 0 && ActionWords.TryGetValue(pair.Key, out var word) && !plays.Tags.ContainsKey(pair.Value[0])) plays.Tags[pair.Value[0]] = word;
            }
            foreach (var pair in plays.ByName)
            {
                if (plays.Tags.ContainsKey(pair.Key)) continue;
                var lower = pair.Key.ToLowerInvariant();
                plays.Tags[pair.Key] = (lower.Contains("jump") ? "jumps" : lower.Contains("swim") ? "swims" : "in water") + ", by name";
            }
            foreach (var clip in seen.Idle) if (!plays.Tags.ContainsKey(clip)) plays.Tags[clip] = "idles";
            if (body != null && Told.Add("lasting:" + prefab.name))
            {
                string Of(EffectList list) => HasAny(list) ? string.Join(", ", Members(list)) : "nothing";
                Plugin.Note($"Scry: {prefab.name} keeps going in water {Of(body.m_waterEffects)}; flying {Of(body.m_flyingContinuousEffect)}{(body.m_flying ? ", and it flies from birth" : "")}; its own scale {prefab.transform.localScale.x:0.##}.");
            }
            return plays;
        }

        /// <summary>
        /// The game's own actions on a creature's animator that come with an effect list, as its
        /// code plays them together: a jump (<c>Character.ForceJump</c>), eating
        /// (<c>MonsterAI.UpdateConsumeItem</c>), going to sleep and waking (<c>MonsterAI.Sleep</c>,
        /// <c>Wakeup</c>), being alerted (<c>BaseAI.SetAlerted</c>). Named as the probe names them.
        /// </summary>
        private static IEnumerable<(string, object)> GameLists(GameObject prefab)
        {
            var character = prefab.GetComponent<Character>();
            var ai = prefab.GetComponent<BaseAI>();
            var monster = ai as MonsterAI;
            if (character?.m_jumpEffects != null) yield return ("jump", character.m_jumpEffects);
            if (character is Humanoid humanoid && humanoid.m_consumeItemEffects != null) yield return ("consume", humanoid.m_consumeItemEffects);
            if (monster?.m_sleepEffects != null) yield return ("sleep", monster.m_sleepEffects);
            if (monster?.m_wakeupEffects != null) yield return ("wake", monster.m_wakeupEffects);
            if (ai?.m_alertedEffects != null) yield return ("alert", ai.m_alertedEffects);

            // In water or flying, the game keeps these going for as long (Character.UpdateContinousEffects);
            // dying, it plays its death (Character.OnDeath).
            if (character?.m_waterEffects != null)
            {
                yield return ("water", character.m_waterEffects);
                yield return ("swim", character.m_waterEffects);
            }
            if (character?.m_flyingContinuousEffect != null) yield return ("fly", character.m_flyingContinuousEffect);
            if (character?.m_deathEffects != null) yield return ("dead", character.m_deathEffects);
        }

        /// <summary>The trigger an attack starts by on this animator, as <c>Attack.Start</c> pulls it, or null when it has none.</summary>
        private static string TriggerOf(Animator animator, Attack attack)
        {
            var anim = attack.m_attackAnimation;
            if (string.IsNullOrEmpty(anim)) return null;
            if (HasTrigger(animator, anim)) return anim;
            return attack.m_attackChainLevels > 1 && HasTrigger(animator, anim + "0") ? anim + "0" : null;
        }

        private static bool HasTrigger(Animator animator, string name)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == name) return true;
            }
            return false;
        }

        /// <summary>Plays the clip again as soon as it ends.</summary>
        public static bool LoopClips;

        /// <summary>The animation clips the stage copy's animator has, by name.</summary>
        /// <summary>What the stage copy's animation clip plays of itself, by prefab name.</summary>
        public static List<string> ClipMembers(AnimationClip clip)
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            var ears = animator != null ? animator.GetComponent<AnimationEars>() : null;
            return ears != null ? ears.Members(clip) : new List<string>();
        }

        /// <summary>What the stage copy's animation clip plays that it was paired with by its name alone, by prefab name.</summary>
        public static List<string> ClipByNameMembers(AnimationClip clip)
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            var ears = animator != null ? animator.GetComponent<AnimationEars>() : null;
            return ears != null ? ears.ByNameMembers(clip) : new List<string>();
        }

        /// <summary>What is heard around the stage copy's animation clip, by prefab name.</summary>
        public static List<string> ClipAroundMembers(AnimationClip clip)
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            var ears = animator != null ? animator.GetComponent<AnimationEars>() : null;
            return ears != null ? ears.AroundMembers(clip) : new List<string>();
        }

        public static List<AnimationClip> Clips()
        {
            var clips = new List<AnimationClip>();
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            if (animator == null) return clips;

            var seen = new HashSet<string>();
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && seen.Add(clip.name)) clips.Add(clip);
            }
            clips.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            return clips;
        }

        /// <summary>
        /// The clip the stage copy's animator plays on its own right now, when Scry plays none:
        /// the strongest of the state it is in or moving to. Null when there is none.
        /// </summary>
        public static AnimationClip AnimatorClipNow()
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || PlayingClip() != null) return null;
            var infos = animator.IsInTransition(0) ? animator.GetNextAnimatorClipInfo(0) : animator.GetCurrentAnimatorClipInfo(0);
            AnimationClip strongest = null;
            var weight = -1f;
            foreach (var info in infos)
            {
                if (info.clip == null || info.weight <= weight) continue;
                strongest = info.clip;
                weight = info.weight;
            }
            return strongest;
        }

        /// <summary>The clip playing on the stage copy, or null.</summary>
        public static AnimationClip PlayingClip()
        {
            var player = Stage.Subject != null ? Stage.Subject.GetComponent<ClipPlayer>() : null;
            return player != null ? player.Clip : null;
        }

        /// <summary>
        /// Plays a clip on the stage copy and the copy in the world. Started by an effect list's
        /// button (an item's attack), what it plays lights that button too.
        /// </summary>
        public static void PlayClip(AnimationClip clip, object litWith = null)
        {
            _litWith = (clip, litWith);
            var speed = _explorer != null ? _explorer.Modifiers.AnimationSpeed : 1f;
            ClipPlayer.Play(Stage.Subject, clip, LoopClips, speed);
            ClipPlayer.Play(_world, clip, LoopClips, speed);
            _startedClip = clip;
        }

        private static string _clipOnShow;

        /// <summary>Plays the clip of that name once the next selection is shown, as when going to a creature from one of its animation's sounds.</summary>
        public static void PlayClipOnShow(string name) => _clipOnShow = name;

        /// <summary>Where the clip on the stage is, and how long it is.</summary>
        public static bool ClipPosition(out float time, out float length) => ClipPlayer.Position(Stage.Subject, out time, out length);

        public static bool ClipPaused => ClipPlayer.Paused(Stage.Subject);

        /// <summary>Moves the clip to a time, on the stage and in the world together.</summary>
        public static void SeekClip(float time)
        {
            ClipPlayer.Seek(Stage.Subject, time);
            ClipPlayer.Seek(_world, time);
        }

        public static void PauseClip(bool pause)
        {
            ClipPlayer.Pause(Stage.Subject, pause);
            ClipPlayer.Pause(_world, pause);
        }

        public static void StopClip()
        {
            ClipPlayer.Stop(Stage.Subject);
            ClipPlayer.Stop(_world);
        }

        public static void ToggleLoopClips()
        {
            LoopClips = !LoopClips;
            ClipPlayer.SetLoop(Stage.Subject, LoopClips);
            ClipPlayer.SetLoop(_world, LoopClips);
        }
    }
}
