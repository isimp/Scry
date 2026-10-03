using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>What the probe saw an animator play: for each attack trigger, and for each of the game's own actions.</summary>
    internal sealed class Probed
    {
        public readonly Dictionary<string, IReadOnlyList<string>> Triggers = new Dictionary<string, IReadOnlyList<string>>();
        public readonly Dictionary<string, IReadOnlyList<string>> Actions = new Dictionary<string, IReadOnlyList<string>>();

        /// <summary>The clips it plays when left alone, over several seeds, since it picks among them at random.</summary>
        public readonly HashSet<string> Idle = new HashSet<string>();

        /// <summary>Still being watched: what is here so far is only part.</summary>
        public bool Busy;
    }

    /// <summary>
    /// Which clips a creature's animator plays for each attack trigger and for each of the
    /// game's own actions that come with an effect list. Its states and the ways between them
    /// cannot be read at run time, so each is done on a hidden copy of the animated body, from
    /// where the animator starts, and watched beside a run left alone. The game's RandomIdle
    /// picks idle clips at random, so every run is seeded alike: the two go the same way until
    /// what was done takes effect. An attack is watched as the game watches one
    /// (<c>Humanoid.InAttack</c>): for as long as it is in a state tagged "attack", every clip it
    /// plays there is the attack's, in order, so a bow drawn in one clip and let go in the next is
    /// both. Anything else plays the clip of the first state it goes to that the run left alone
    /// does not, even one it also idles in (an asksvin eats in its idle too). Seen once per
    /// prefab and said in the log.
    /// </summary>
    internal static class TriggerProbe
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static TriggerProbe() => WorldCaches.Register(nameof(TriggerProbe), Forget);

        private static readonly int AttackTag = Animator.StringToHash("attack");
        private const float Step = 0.025f;

        /// <summary>
        /// How many steps to wait for what was done to take effect: an attack starts at once,
        /// while going to sleep or jumping can wait for the idle clip playing to end, some of
        /// which last several seconds. Asleep, a creature is left a second before it is woken.
        /// </summary>
        private const int AttackPatience = 40;
        private const int ActionPatience = 240;
        private const int Settle = 40;

        /// <summary>
        /// The game's own actions, as its code does them: <c>Character.ForceJump</c> pulls
        /// "jump", <c>MonsterAI.UpdateConsumeItem</c> "consume", <c>MonsterAI.Sleep</c> and
        /// <c>Wakeup</c> switch "sleeping" on and off, <c>BaseAI.SetAlerted</c> switches "alert",
        /// a spawner that wakes what it spawns (<c>CreatureSpawner</c>, <c>SpawnAbility</c>) switches
        /// "wakeup" on as it appears, <c>Character.RPC_Stagger</c> pulls "stagger", a character
        /// in water or flying switches "inWater" or "flying" on (still, and swimming along), and
        /// <c>Character.OnDeath</c> switches "dead" on. Each is the switch or trigger, whether it
        /// is a switch, whether it is let go after, and how fast it moves meanwhile.
        /// </summary>
        private static readonly (string Action, string Name, bool Switch, bool Release, float Speed)[] GameActions =
        {
            ("jump", "jump", false, false, 0f),
            ("consume", "consume", false, false, 0f),
            ("sleep", "sleeping", true, false, 0f),
            ("wake", "sleeping", true, true, 0f),
            ("alert", "alert", true, false, 0f),
            ("spawn", "wakeup", true, false, 0f),
            ("stagger", "stagger", false, false, 0f),
            ("water", "inWater", true, false, 0f),
            ("swim", "inWater", true, false, SwimSpeed),
            ("fly", "flying", true, false, 0f),
            ("dead", "dead", true, false, 0f),
        };

        /// <summary>How fast a swimming creature is moved along, beside one walking as fast out of water.</summary>
        private const float SwimSpeed = 2f;

        /// <summary>How many more seeded runs, a few seconds each, gather the clips it idles in.</summary>
        private const int IdleSeeds = 4;

        /// <summary>
        /// Where the animator is at one step: the state it is in or moving to, its strongest clip
        /// there, and the clip of an attack state it is in or moving to. The clips are kept as
        /// they are and named only where a run is told, since a person's probe takes thousands of
        /// steps and a clip's name is a new string each time it is asked for.
        /// </summary>
        private struct Frame
        {
            public int State;
            public AnimationClip Clip;
            public AnimationClip Attack;
        }

        private static readonly Dictionary<string, Probed> Seen = new Dictionary<string, Probed>();

        /// <summary>A probe under way: the hidden copy, what it stands in, the triggers still to pull, and the work left, done a run at a time.</summary>
        private sealed class Job
        {
            public string Key;
            public Probed Seen;
            public GameObject Holder;
            public Animator Probe;

            /// <summary>The copy's animator the hidden one is made from, once the probe begins.</summary>
            public Animator Source;
            public bool Started;
            public System.Action<Animator> Stance;
            public bool First;
            public readonly Queue<string> Triggers = new Queue<string>();
            public IEnumerator<bool> Work;
            public long Ms;
            public int Frames;
        }

        private static readonly Dictionary<string, Job> Jobs = new Dictionary<string, Job>();

        /// <summary>
        /// How long the probes may run each frame, so seeing an animator never stalls the game: a
        /// run's next piece is begun only while one like those before still fits.
        /// </summary>
        private static readonly FrameShare Share = new FrameShare(4);

        /// <summary>A probe that failed is tried once more; one that fails again is given up, and said so once.</summary>
        private static Attempts Failures = new Attempts(2);

        /// <summary>Which stance settings each controller has, found once rather than from its parameters on every ask.</summary>
        private static readonly Dictionary<RuntimeAnimatorController, (bool Int, bool Float)> Stances = new Dictionary<RuntimeAnimatorController, (bool, bool)>();

        /// <summary>
        /// The clips each of the triggers and each of the game's actions leads through; one that
        /// leads to none has an empty list. The watching is done a run at a time over the next
        /// frames, and until it is done the answer is <see cref="Probed.Busy"/>; with
        /// <paramref name="wait"/>, it is finished at once, as when a clip is about to play.
        /// </summary>
        public static Probed ClipsOf(string prefab, Animator animator, IEnumerable<string> triggers, bool wait)
        {
            // Seen for each stance it stands in: what a trigger plays depends on the weapon held
            // (Humanoid.SetupAnimationState sets statei and statef).
            var stance = StanceOf(animator);
            var key = KeyOf(prefab, stance);

            // A probe given up on answers nothing, rather than being made and failing again.
            if (Failures.GivenUp(key))
            {
                if (!Seen.TryGetValue(key, out var left)) Seen[key] = left = new Probed();
                return left;
            }

            var first = !Seen.TryGetValue(key, out var seen);
            if (first)
            {
                seen = new Probed();
                Seen[key] = seen;
            }

            // Seen before in this stance, only triggers not pulled yet are (a sword's and a mace's
            // differ, while their stance is the same).
            Jobs.TryGetValue(key, out var job);
            var missing = new List<string>();
            foreach (var trigger in triggers)
            {
                if (seen.Triggers.ContainsKey(trigger) || missing.Contains(trigger) || (job != null && job.Triggers.Contains(trigger))) continue;
                missing.Add(trigger);
            }
            if (job == null && (first || missing.Count > 0) && animator != null && animator.runtimeAnimatorController != null)
            {
                job = Begin(key, animator, stance, seen, first);
                if (job == null) return AfterFailure(key);
            }
            if (job != null)
            {
                foreach (var trigger in missing) job.Triggers.Enqueue(trigger);
                if (wait) while (Advance(job)) { }
            }
            return Seen.TryGetValue(key, out var now) ? now : AfterFailure(key);
        }

        /// <summary>
        /// The answer for a probe that just failed: still to come while it is to be tried again,
        /// so nothing takes the part seen for the whole; nothing once it is given up.
        /// </summary>
        private static Probed AfterFailure(string key)
        {
            if (!Failures.GivenUp(key)) return new Probed { Busy = true };
            if (!Seen.TryGetValue(key, out var left)) Seen[key] = left = new Probed();
            return left;
        }

        /// <summary>The stance an animator stands in now: its statei and statef, those of them it has.</summary>
        private static List<(string Name, AnimatorControllerParameterType Type, float Value)> StanceOf(Animator animator)
        {
            var stance = new List<(string Name, AnimatorControllerParameterType Type, float Value)>(2);
            var controller = animator != null ? animator.runtimeAnimatorController : null;
            if (controller == null) return stance;
            if (!Stances.TryGetValue(controller, out var has))
            {
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.name == "statei" && parameter.type == AnimatorControllerParameterType.Int) has.Int = true;
                    if (parameter.name == "statef" && parameter.type == AnimatorControllerParameterType.Float) has.Float = true;
                }
                Stances[controller] = has;
            }
            if (has.Int) stance.Add(("statei", AnimatorControllerParameterType.Int, animator.GetInteger("statei")));
            if (has.Float) stance.Add(("statef", AnimatorControllerParameterType.Float, animator.GetFloat("statef")));
            return stance;
        }

        private static string KeyOf(string prefab, List<(string Name, AnimatorControllerParameterType Type, float Value)> stance)
        {
            var key = prefab;
            foreach (var (name, _, value) in stance) key += $"|{name}={Numbers.Amount(value)}";
            return key;
        }

        /// <summary>Whether a probe of this prefab is under way, whatever stance it is in.</summary>
        public static bool IsBusy(string prefab)
        {
            foreach (var key in Jobs.Keys) if (Of(key, prefab)) return true;
            return false;
        }

        /// <summary>Whether a probe's key is of this prefab, in any stance.</summary>
        private static bool Of(string key, string prefab) => key == prefab || key.StartsWith(prefab + "|", System.StringComparison.Ordinal);

        /// <summary>
        /// Stops the probes of every prefab but these, as when another entry is selected: browsing
        /// quickly would otherwise leave a hidden copy watched for each entry passed, the first
        /// taking the whole of each frame's share. What they saw so far is let go, so they start
        /// afresh when their prefab is shown again.
        /// </summary>
        public static void CancelAllBut(ICollection<string> keep)
        {
            foreach (var job in Jobs.Values.ToList())
            {
                var kept = false;
                foreach (var prefab in keep) kept |= prefab != null && Of(job.Key, prefab);
                if (kept) continue;
                Plugin.Note($"Scry stopped watching the animator of {job.Key}, as it is no longer shown.");
                Drop(job);
            }
        }

        /// <summary>Stops every probe and forgets all that was seen, for a world that was left.</summary>
        public static void Forget()
        {
            foreach (var job in Jobs.Values.ToList()) Drop(job);
            Seen.Clear();
            Stances.Clear();
            Failures = new Attempts(2);
        }

        /// <summary>A probe ended before it was done: its copy destroyed and its part answer let go.</summary>
        private static void Drop(Job job)
        {
            if (job.Holder != null) Object.DestroyImmediate(job.Holder);
            Jobs.Remove(job.Key);
            job.Seen.Busy = false;
            if (Seen.TryGetValue(job.Key, out var seen) && ReferenceEquals(seen, job.Seen)) Seen.Remove(job.Key);
        }

        /// <summary>Goes on with the probes under way, a run at a time, for as long as this frame allows.</summary>
        public static void Update()
        {
            if (Jobs.Count == 0) return;
            var frame = System.Diagnostics.Stopwatch.StartNew();
            var done = 0;
            foreach (var job in Jobs.Values.ToList())
            {
                job.Frames++;
                while (Share.MayBegin(frame.Elapsed.TotalMilliseconds, done))
                {
                    var piece = frame.Elapsed.TotalMilliseconds;
                    var more = Advance(job);
                    Share.Took(frame.Elapsed.TotalMilliseconds - piece);
                    done++;
                    if (!more) break;
                }
                if (!Share.MayBegin(frame.Elapsed.TotalMilliseconds, done)) return;
            }
        }

        /// <summary>
        /// A probe set to begin: its hidden copy is made as its first piece of work, in a frame of
        /// its own, rather than in the one that asked (a person's whole body takes some 15 ms).
        /// </summary>
        private static Job Begin(string key, Animator animator, List<(string Name, AnimatorControllerParameterType Type, float Value)> stance, Probed seen, bool first)
        {
            var job = new Job
            {
                Key = key, Seen = seen, Source = animator, First = first,
                Stance = a =>
                {
                    foreach (var (name, type, value) in stance)
                    {
                        if (type == AnimatorControllerParameterType.Int) a.SetInteger(name, (int)value);
                        else a.SetFloat(name, value);
                    }
                },
            };
            seen.Busy = true;
            Jobs[key] = job;
            return job;
        }

        /// <summary>Makes a probe's hidden copy and its runs; false when it could not be made.</summary>
        private static bool Make(Job job)
        {
            var animator = job.Source;
            GameObject holder = null;
            try
            {
                // Drawn by nothing, heard by nothing, sending no events, touching nothing: only
                // the animator runs. Its scripts go before it wakes, which would wake them even
                // switched off. Particle systems are no behaviour to switch off and would play
                // on awake, and colliders would stand unseen in the world.
                holder = new GameObject("Scry probe");
                holder.SetActive(false);
                var body = Object.Instantiate(animator.gameObject, holder.transform, false);
                foreach (var script in body.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                foreach (var behaviour in body.GetComponentsInChildren<Behaviour>(true)) if (!(behaviour is Animator)) behaviour.enabled = false;
                foreach (var renderer in body.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var particles in body.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particles.main;
                    main.playOnAwake = false;
                    var emission = particles.emission;
                    emission.enabled = false;
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                foreach (var collider in body.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var rigidbody in body.GetComponentsInChildren<Rigidbody>(true))
                {
                    rigidbody.isKinematic = true;
                    rigidbody.detectCollisions = false;
                }
                var probe = body.GetComponent<Animator>();
                probe.fireEvents = false;
                probe.applyRootMotion = false;
                probe.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                probe.keepAnimatorStateOnDisable = true;
                holder.SetActive(true);
                job.Holder = holder;
                job.Probe = probe;
                job.Work = Work(job);
                return true;
            }
            catch (System.Exception ex)
            {
                if (holder != null) Object.DestroyImmediate(holder);
                Drop(job);
                Failed(job.Key, job.Seen, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// A probe that could not be made or did not finish: what it saw is let go, and it is tried
        /// again when next asked, once; failing again, it is given up and said so.
        /// </summary>
        private static void Failed(string key, Probed seen, string why)
        {
            seen.Busy = false;
            if (Seen.TryGetValue(key, out var kept) && ReferenceEquals(kept, seen)) Seen.Remove(key);
            if (Failures.Failed(key)) Plugin.Note($"Scry could not see what the animator of {key} plays ({why}); it tries once more.");
            else Plugin.Log.LogWarning($"Scry could not see what the animator of {key} plays ({why}), and leaves its clips unmatched.");
        }

        /// <summary>
        /// One run of a probe. The game's randomness is kept as it was around it, since each run
        /// seeds its own. False once the probe is done, or its copy is gone with the world.
        /// </summary>
        private static bool Advance(Job job)
        {
            if (!job.Started)
            {
                job.Started = true;

                // Its copy went before it began (another entry was shown): nothing to watch.
                if (job.Source == null)
                {
                    Drop(job);
                    return false;
                }
                return Make(job);
            }
            if (job.Holder == null)
            {
                Drop(job);
                Failed(job.Key, job.Seen, "its hidden copy was gone");
                return false;
            }
            var random = Random.state;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _stance = job.Stance;
            bool more;
            string failure = null;
            try
            {
                more = job.Work.MoveNext();
            }
            catch (System.Exception ex)
            {
                failure = ex.Message;
                more = false;
            }
            finally
            {
                _stance = null;
                Random.state = random;
                job.Ms += watch.ElapsedMilliseconds;
            }

            // A run stops early, as done, when its copy goes with the scene; that is no answer either.
            if (failure == null && !more && job.Holder == null) failure = "its hidden copy was gone";
            if (failure != null)
            {
                Drop(job);
                Failed(job.Key, job.Seen, failure);
                return false;
            }
            if (!more) End(job);
            return more;
        }

        private static void End(Job job)
        {
            if (job.Holder != null) Object.DestroyImmediate(job.Holder);
            Jobs.Remove(job.Key);
            job.Seen.Busy = false;
        }

        /// <summary>The runs of a probe, each yielded after: left alone, each trigger, each of the game's actions.</summary>
        private static IEnumerator<bool> Work(Job job)
        {
            var probe = job.Probe;
            var seen = job.Seen;
            bool Has(string name, AnimatorControllerParameterType type) => probe.parameters.Any(p => p.type == type && p.name == name);
            string Tell(string name, List<string> clips) => clips.Count > 0 ? $"{name} plays {string.Join(" then ", clips)}" : $"{name} plays no clip of its own";

            var alone = new List<Frame>();
            foreach (var _ in Trace(alone, probe, null, false, null, -1, job.First ? 2 * ActionPatience + Settle : 160)) yield return true;
            yield return true;
            if (job.First)
            {
                foreach (var frame in alone) if (frame.Clip != null) seen.Idle.Add(frame.Clip.name);
                for (var seed = 2; seed <= IdleSeeds + 1; seed++)
                {
                    var idle = new List<Frame>();
                    foreach (var _ in Trace(idle, probe, null, false, null, -1, 160, seed)) yield return true;
                    foreach (var frame in idle) if (frame.Clip != null) seen.Idle.Add(frame.Clip.name);
                    yield return true;
                }
            }

            var attacks = new List<string>();
            var actions = new List<string>();
            var actionsDone = !job.First;
            while (true)
            {
                while (job.Triggers.Count > 0)
                {
                    var trigger = job.Triggers.Dequeue();
                    if (seen.Triggers.ContainsKey(trigger) || !Has(trigger, AnimatorControllerParameterType.Trigger)) continue;
                    var run = new List<Frame>();
                    foreach (var _ in Trace(run, probe, a => a.SetTrigger(trigger), false, null, -1, 160)) yield return true;
                    var clips = AttackClips(run);
                    if (clips.Count == 0)
                    {
                        var at = Parting(run, alone, 0, AttackPatience);
                        if (at >= 0 && run[at].Clip != null) clips.Add(run[at].Clip.name);
                    }
                    seen.Triggers[trigger] = clips;
                    attacks.Add(Tell(trigger, clips));
                    yield return true;
                }
                if (actionsDone) break;
                actionsDone = true;

                foreach (var (action, name, isSwitch, release, speed) in GameActions)
                {
                    if (!Has(name, isSwitch ? AnimatorControllerParameterType.Bool : AnimatorControllerParameterType.Trigger)) continue;
                    System.Action<Animator> act = isSwitch ? a => a.SetBool(name, true) : (System.Action<Animator>)(a => a.SetTrigger(name));

                    // Moving along, it is watched beside a run moving as fast without it, so what
                    // moving alone does (walking) is not taken for it.
                    System.Action<Animator> setup = null;
                    var beside = alone;
                    if (speed > 0f)
                    {
                        if (!Has("forward_speed", AnimatorControllerParameterType.Float)) continue;
                        setup = a => a.SetFloat("forward_speed", speed);
                        beside = new List<Frame>();
                        foreach (var _ in Trace(beside, probe, null, false, null, -1, 2 * ActionPatience + Settle, 1, setup)) yield return true;
                        yield return true;
                    }

                    // A switch is set as the animator starts, since some go to sleep only from
                    // their start; a trigger is pulled once it runs.
                    var run = new List<Frame>();
                    foreach (var _ in Trace(run, probe, act, isSwitch, null, -1, 2 * ActionPatience + Settle, 1, setup)) yield return true;
                    var at = Parting(run, beside, 0, ActionPatience);
                    var clips = new List<string>();
                    if (!release)
                    {
                        if (at >= 0 && run[at].Clip != null) clips.Add(run[at].Clip.name);
                    }
                    else if (at >= 0)
                    {
                        // Let go only once it has gone there (fallen asleep), beside a run kept there.
                        yield return true;
                        var letGo = at + Settle;
                        var back = new List<Frame>();
                        foreach (var _ in Trace(back, probe, act, isSwitch, a => a.SetBool(name, false), letGo, letGo + ActionPatience, 1, setup)) yield return true;
                        var woke = Parting(back, run, letGo, back.Count - 1);
                        if (woke >= 0 && back[woke].Clip != null) clips.Add(back[woke].Clip.name);
                    }
                    seen.Actions[action] = clips;
                    actions.Add(Tell(action, clips));
                    yield return true;
                }
            }

            var took = $"{Numbers.Count(job.Ms)} ms over {Numbers.Count(job.Frames)} frames";
            if (!job.First) Plugin.Note($"Scry saw in {took} what more triggers of {job.Key} play: {string.Join("; ", attacks)}.");
            else Plugin.Note($"Scry saw in {took} what the animator of {job.Key} plays: attacks {(attacks.Count > 0 ? string.Join("; ", attacks) : "none")}; the game's own actions {(actions.Count > 0 ? string.Join("; ", actions) : "none")}; left alone {string.Join(", ", seen.Idle.OrderBy(c => c))}.");
        }

        /// <summary>Sets the stance the copy stands in on every run.</summary>
        private static System.Action<Animator> _stance;

        /// <summary>
        /// Runs the animator from its start, seeded alike and with its settings as they start
        /// (and as <paramref name="setup"/> sets them), for <paramref name="steps"/> steps: done
        /// <paramref name="act"/> to as it starts (<paramref name="first"/>) or once it runs, and
        /// <paramref name="then"/> at step <paramref name="thenAt"/>. Where it is at each step,
        /// the first before any step.
        /// </summary>
        private static IEnumerable<bool> Trace(List<Frame> frames, Animator animator, System.Action<Animator> act, bool first, System.Action<Animator> then, int thenAt, int steps, int seed = 1, System.Action<Animator> setup = null)
        {
            Random.InitState(seed);
            animator.Rebind();
            foreach (var parameter in animator.parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Trigger: animator.ResetTrigger(parameter.name); break;
                    case AnimatorControllerParameterType.Bool: animator.SetBool(parameter.name, parameter.defaultBool); break;
                    case AnimatorControllerParameterType.Float: animator.SetFloat(parameter.name, parameter.defaultFloat); break;
                    case AnimatorControllerParameterType.Int: animator.SetInteger(parameter.name, parameter.defaultInt); break;
                }
            }
            _stance?.Invoke(animator);
            setup?.Invoke(animator);
            if (act != null && first) act(animator);
            animator.Update(0f);
            if (act != null && !first) act(animator);

            frames.Add(Where(animator));
            for (var i = 1; i <= steps; i++)
            {
                if (i == thenAt && then != null) then(animator);
                animator.Update(Step);
                frames.Add(Where(animator));

                // A pause every few dozen steps, so a run stays within a frame's share: the copy
                // switched off meanwhile so its animator does not run on by itself, and the run's
                // own randomness kept for when it goes on.
                if (i % StepsPerPause != 0 || i == steps) continue;
                var mine = Random.state;
                var holder = animator.transform.parent.gameObject;
                holder.SetActive(false);
                yield return true;
                if (holder == null) yield break;
                holder.SetActive(true);
                Random.state = mine;
            }
        }

        /// <summary>How many steps a run takes before it pauses for the next frame.</summary>
        private const int StepsPerPause = 20;

        /// <summary>The first step from <paramref name="from"/> up to <paramref name="upTo"/> where a run is in another state than the one beside it, or -1.</summary>
        private static int Parting(List<Frame> run, List<Frame> beside, int from, int upTo)
        {
            for (var i = from; i <= upTo && i < run.Count && i < beside.Count; i++)
            {
                if (run[i].State != beside[i].State) return i;
            }
            return -1;
        }

        /// <summary>The clips of the attack states a run goes through, in order, until the attack is over.</summary>
        private static List<string> AttackClips(List<Frame> run)
        {
            var clips = new List<string>();
            foreach (var frame in run)
            {
                if (frame.Attack != null)
                {
                    var name = frame.Attack.name;
                    if (!clips.Contains(name)) clips.Add(name);
                }
                else if (clips.Count > 0) break;
            }
            return clips;
        }

        private static Frame Where(Animator animator)
        {
            var moving = animator.IsInTransition(0);
            var state = moving ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            return new Frame { State = state.fullPathHash, Clip = Strongest(animator, 0, moving), Attack = AttackIn(animator) };
        }

        /// <summary>The clip of the first layer that is in or moving to a state tagged "attack", or null.</summary>
        private static AnimationClip AttackIn(Animator animator)
        {
            for (var layer = 0; layer < animator.layerCount; layer++)
            {
                var moving = animator.IsInTransition(layer);
                var state = moving ? animator.GetNextAnimatorStateInfo(layer) : animator.GetCurrentAnimatorStateInfo(layer);
                if (state.tagHash != AttackTag) continue;
                var clip = Strongest(animator, layer, moving);
                if (clip != null) return clip;
            }
            return null;
        }

        /// <summary>Filled again on each step, so watching an animator makes no garbage.</summary>
        private static readonly List<AnimatorClipInfo> Infos = new List<AnimatorClipInfo>();

        /// <summary>The strongest clip of the state a layer is in, or is moving to; of equally strong ones, the first.</summary>
        private static AnimationClip Strongest(Animator animator, int layer, bool moving)
        {
            if (moving) animator.GetNextAnimatorClipInfo(layer, Infos);
            else animator.GetCurrentAnimatorClipInfo(layer, Infos);
            AnimationClip strongest = null;
            var weight = float.NegativeInfinity;
            foreach (var info in Infos)
            {
                if (info.clip == null || info.weight <= weight) continue;
                strongest = info.clip;
                weight = info.weight;
            }
            Infos.Clear();
            return strongest;
        }
    }
}
