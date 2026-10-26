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

        /// <summary>The clips it moves, crouches, blocks and jumps in under each stance, by the stance's number (<see cref="HeldClips"/>); only from a sweep of its stances.</summary>
        public readonly Dictionary<int, HashSet<string>> Stances = new Dictionary<int, HashSet<string>>();

        /// <summary>The clips each switch an item names plays (drawing a bow, reloading), by the switch.</summary>
        public readonly Dictionary<string, HashSet<string>> Holds = new Dictionary<string, HashSet<string>>();

        /// <summary>The clips each run of the sweep saw, whatever the stance (<see cref="ClipGroups.MoveVisit"/> and the others).</summary>
        public readonly Dictionary<string, HashSet<string>> Visits = new Dictionary<string, HashSet<string>>();

        /// <summary>The clips each switch or trigger of a known use leads to (<see cref="ClipGroups.OfSwitch"/>), by its name.</summary>
        public readonly Dictionary<string, HashSet<string>> Switched = new Dictionary<string, HashSet<string>>();
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

            /// <summary>A sweep of its stances (<see cref="StancesOf"/>), not of triggers: the stances, and each switch with the stance it is swept in.</summary>
            public bool Sweep;
            public IReadOnlyList<int> SweptStances;
            public IReadOnlyList<(string Switch, int Stance)> SweptHolds;
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

        /// <summary>Whether a probe of this prefab's triggers is under way, whatever stance it is in; a sweep of its stances, which no attack waits for, aside.</summary>
        public static bool IsBusy(string prefab)
        {
            foreach (var pair in Jobs) if (!pair.Value.Sweep && Of(pair.Key, prefab)) return true;
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
                Log.Note($"Scry stopped watching the animator of {job.Key}, as it is no longer shown.");
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

        /// <summary>Whether any clip is being probed, for the self-test to measure an idle stage.</summary>
        [ForTest]
        public static bool Busy => Jobs.Count > 0;

        /// <summary>
        /// Goes on with the probes under way, a run of each in turn, for as long as this frame
        /// allows: those a page asks now first, a sweep of stances after, so a long sweep never
        /// keeps a weapon's attack waiting.
        /// </summary>
        public static void Update()
        {
            if (Jobs.Count == 0) return;
            var frame = System.Diagnostics.Stopwatch.StartNew();
            var done = 0;
            var going = Jobs.Values.OrderBy(j => j.Sweep ? 1 : 0).ToList();
            foreach (var job in going) job.Frames++;
            while (going.Count > 0)
            {
                for (var i = 0; i < going.Count; i++)
                {
                    if (!Share.MayBegin(frame.Elapsed.TotalMilliseconds, done)) return;
                    var piece = frame.Elapsed.TotalMilliseconds;
                    var more = Advance(going[i]);
                    Share.Took(frame.Elapsed.TotalMilliseconds - piece);
                    done++;
                    if (more) continue;
                    going.RemoveAt(i);
                    i--;
                }
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
            if (Guard.Each(Feature.Animations, "probes of what animators play", job.Key, () =>
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
                    job.Work = job.Sweep ? SweepWork(job) : Work(job);
            })) return true;
            if (holder != null) Object.DestroyImmediate(holder);
            Drop(job);
            Failed(job.Key, job.Seen, "it failed, as the log says");
            return false;
        }

        /// <summary>
        /// A probe that could not be made or did not finish: what it saw is let go, and it is tried
        /// again when next asked, once; failing again, it is given up and said so.
        /// </summary>
        private static void Failed(string key, Probed seen, string why)
        {
            seen.Busy = false;
            if (Seen.TryGetValue(key, out var kept) && ReferenceEquals(kept, seen)) Seen.Remove(key);
            if (Failures.Failed(key)) Log.Note($"Scry could not see what the animator of {key} plays ({why}); it tries once more.");
            else Log.Note($"Scry could not see what the animator of {key} plays ({why}), and leaves its clips unmatched.");
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
            string failure = null;
            bool more;
            try
            {
                if (!Guard.Each(Feature.Animations, "probes of what animators play", job.Key, job.Work.MoveNext, out more))
                {
                    failure = "it failed, as the log says";
                    more = false;
                }
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

        /// <summary>What a trigger or action played, for the log.</summary>
        [Diagnostic]
        private static string Told(string name, List<string> clips) => clips.Count > 0 ? $"{name} plays {string.Join(" then ", clips)}" : $"{name} plays no clip of its own";

        /// <summary>The runs of a probe, each yielded after: left alone, each trigger, each of the game's actions.</summary>
        private static IEnumerator<bool> Work(Job job)
        {
            var probe = job.Probe;
            var seen = job.Seen;
            bool Has(string name, AnimatorControllerParameterType type) => probe.parameters.Any(p => p.type == type && p.name == name);

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
                    attacks.Add(Told(trigger, clips));
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
                    actions.Add(Told(action, clips));
                    yield return true;
                }
            }

            var took = $"{Numbers.Count(job.Ms)} ms over {Numbers.Count(job.Frames)} frames";
            if (!job.First) Log.Note($"Scry saw in {took} what more triggers of {job.Key} play: {string.Join("; ", attacks)}.");
            else Log.Note($"Scry saw in {took} what the animator of {job.Key} plays: attacks {(attacks.Count > 0 ? string.Join("; ", attacks) : "none")}; the game's own actions {(actions.Count > 0 ? string.Join("; ", actions) : "none")}; left alone {string.Join(", ", seen.Idle.OrderBy(c => c, System.StringComparer.Ordinal))}.");
        }

        /// <summary>What a stance sweep is kept under, beside the prefab's name.</summary>
        private const string SweepKey = "|stances";

        /// <summary>
        /// The clips a prefab's animator moves, crouches, blocks and jumps in under each of the
        /// stances given (once, without stance settings), plays with each switch given in its stance,
        /// and each switch of a known use leads to (<see cref="Probed.Stances"/>, <see cref="Probed.Holds"/>,
        /// <see cref="Probed.Visits"/>, <see cref="Probed.Switched"/>): seen once for the prefab over the
        /// next frames, and until then <see cref="Probed.Busy"/>.
        /// </summary>
        public static Probed StancesOf(string prefab, Animator animator, IReadOnlyList<int> stances, IReadOnlyList<(string Switch, int Stance)> holds, bool wait)
        {
            var key = prefab + SweepKey;
            if (Seen.TryGetValue(key, out var seen) && !Jobs.ContainsKey(key)) return seen;
            if (Failures.GivenUp(key)) return Seen[key] = new Probed();
            if (!Jobs.TryGetValue(key, out var job))
            {
                if (animator == null || animator.runtimeAnimatorController == null) return new Probed();
                seen = new Probed();
                Seen[key] = seen;
                job = Begin(key, animator, new List<(string, AnimatorControllerParameterType, float)>(), seen, first: true);
                job.Sweep = true;
                job.SweptStances = stances;
                job.SweptHolds = holds;
            }
            if (wait) while (Advance(job)) { }
            return Seen.TryGetValue(key, out var now) ? now : AfterFailure(key);
        }

        /// <summary>
        /// Where a blend of moving sideways and forward places its clips on a person's animator: each
        /// on an axis or a diagonal at one of these, so stepping through them weighs in every one.
        /// </summary>
        private static readonly float[] Speeds = { 0f, 0.7f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 7.9f, 9f, 10.5f, -0.7f, -1f, -2f, -3f, -4f, -7f };

        /// <summary>
        /// The runs of a sweep: under each stance (or once, for an animator without stances),
        /// standing, crouching, blocking, laden and in a jump, each settled and stepped through every
        /// speed and direction; then each switch an item names, in that item's stance; then each
        /// switch and trigger whose name tells what it is for (<see cref="ClipGroups.OfSwitch"/>),
        /// pulled once. What each run saw is kept by the stance, by the run, and by the switch.
        /// </summary>
        private static IEnumerator<bool> SweepWork(Job job)
        {
            var probe = job.Probe;
            var seen = job.Seen;
            bool Has(string name, AnimatorControllerParameterType type) => probe.parameters.Any(p => p.type == type && p.name == name);
            var statei = Has("statei", AnimatorControllerParameterType.Int);
            var statef = Has("statef", AnimatorControllerParameterType.Float);
            var stances = statei || statef ? job.SweptStances : new[] { 0 };
            var sideways = Has("sideway_speed", AnimatorControllerParameterType.Float);
            var forward = Has("forward_speed", AnimatorControllerParameterType.Float);

            // Standing, crouching, blocking, laden: settled, then stepped through every speed and direction.
            var visits = new List<(string Visit, System.Action<Animator> Act)> { (ClipGroups.MoveVisit, null) };
            foreach (var (switched, visit) in new[] { ("crouching", ClipGroups.MoveVisit), ("blocking", ClipGroups.BlockVisit), ("encumbered", ClipGroups.MoveVisit) })
            {
                if (Has(switched, AnimatorControllerParameterType.Bool)) visits.Add((visit, a => a.SetBool(switched, true)));
            }
            var jumps = Has("jump", AnimatorControllerParameterType.Trigger);
            var grounded = Has("onGround", AnimatorControllerParameterType.Bool);
            HashSet<string> Visit(string name) => seen.Visits.TryGetValue(name, out var kept) ? kept : seen.Visits[name] = new HashSet<string>();

            void Stand(Animator a, int stance)
            {
                if (statei) a.SetInteger("statei", stance);
                if (statef) a.SetFloat("statef", stance);
            }

            foreach (var stance in stances)
            {
                var clips = new HashSet<string>();
                foreach (var (visit, act) in visits)
                {
                    var run = new HashSet<string>();
                    foreach (var _ in Swept(probe, a => { Stand(a, stance); act?.Invoke(a); }, null, run, sideways, forward)) yield return true;
                    clips.UnionWith(run);
                    Visit(visit).UnionWith(run);
                    yield return true;
                }
                // A jump goes through its clips by itself: off the ground and back on.
                if (jumps)
                {
                    var run = new HashSet<string>();
                    System.Action<Animator> landing = grounded ? a => a.SetBool("onGround", true) : (System.Action<Animator>)null;
                    foreach (var _ in Swept(probe, a => { Stand(a, stance); if (grounded) a.SetBool("onGround", false); }, a => a.SetTrigger("jump"), run, false, false, landing)) yield return true;
                    clips.UnionWith(run);
                    Visit(ClipGroups.JumpVisit).UnionWith(run);
                    yield return true;
                }
                seen.Stances[stance] = clips;
            }
            foreach (var (switched, stance) in job.SweptHolds)
            {
                if (!Has(switched, AnimatorControllerParameterType.Bool) || seen.Holds.ContainsKey(switched)) continue;
                var clips = new HashSet<string>();
                foreach (var _ in Swept(probe, a => Stand(a, stance), a => a.SetBool(switched, true), clips, sideways, forward)) yield return true;
                seen.Holds[switched] = clips;
                yield return true;
            }

            // Each switch and trigger of a known use, in the first stance: emotes, seats, beds,
            // mounts, stations, getting hurt, water. Attacks, the stance's own settings and the
            // switches items name are seen already.
            var first = stances.Count > 0 ? stances[0] : 0;
            foreach (var (name, act) in Switches(probe, job.SweptHolds))
            {
                // A station's work number is tried for each station, all kept under the one name.
                if (!seen.Switched.TryGetValue(name, out var clips)) seen.Switched[name] = clips = new HashSet<string>();
                foreach (var _ in Swept(probe, a => Stand(a, first), act, clips, sideways, forward)) yield return true;
                yield return true;
            }
            Log.Note($"Scry saw in {Numbers.Count(job.Ms)} ms what the animator of {job.Key} plays by stance: {string.Join("; ", seen.Stances.Select(p => $"{Numbers.Count(p.Key)}: {Numbers.Count(p.Value.Count)} clips"))}; by what items switch on: {string.Join("; ", seen.Holds.Select(p => $"{p.Key}: {Numbers.Count(p.Value.Count)}"))}; by the switches it was sent: {Numbers.Count(seen.Switched.Count)}.");
        }

        /// <summary>The settings a sweep sets itself, never pulled as switches of their own.</summary>
        private static readonly string[] SweepSettings = { "statei", "statef", "forward_speed", "sideway_speed", "crouching", "blocking", "encumbered", "onGround", "jump" };

        /// <summary>How many stations' work an animator's "crafting" number is tried for (<c>CraftingStation.m_useAnimation</c>).</summary>
        private const int StationAnimations = 10;

        /// <summary>
        /// The switches and triggers of an animator a sweep pulls, each by what pulling it does: those
        /// whose name tells what they are for, not an attack's (<see cref="ClipLeftovers"/>) nor
        /// one an item names; a station's work number tried for each station's, as one.
        /// </summary>
        private static IEnumerable<(string Name, System.Action<Animator> Act)> Switches(Animator animator, IReadOnlyList<(string Switch, int Stance)> holds)
        {
            var attacks = new HashSet<string>(ClipLeftovers.Candidates(animator.parameters.Select(p => p.name)));
            foreach (var parameter in animator.parameters)
            {
                var name = parameter.name;
                if (System.Array.IndexOf(SweepSettings, name) >= 0 || attacks.Contains(name) || holds.Any(h => h.Switch == name) || ClipGroups.OfSwitch(name) == null) continue;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Trigger: yield return (name, a => a.SetTrigger(name)); break;
                    case AnimatorControllerParameterType.Bool: yield return (name, a => a.SetBool(name, true)); break;
                    case AnimatorControllerParameterType.Int:
                        for (var value = 1; value <= StationAnimations; value++)
                        {
                            var number = value;
                            yield return (name, a => a.SetInteger(name, number));
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// One run of a sweep: from its start, set up, done <paramref name="act"/> to once it runs,
        /// and let settle; then, where it moves sideways or forward, stepped through every speed and
        /// direction (<see cref="Speeds"/>) with every clip weighed in kept; else every clip weighed in
        /// as it goes kept, and done <paramref name="then"/> to halfway.
        /// </summary>
        private static IEnumerable<bool> Swept(Animator animator, System.Action<Animator> setup, System.Action<Animator> act, HashSet<string> clips, bool sideways, bool forward,
            System.Action<Animator> then = null)
        {
            Restart(animator, 1);
            setup?.Invoke(animator);
            animator.Update(0f);
            act?.Invoke(animator);
            var moving = sideways || forward;
            var steps = moving ? Settle : 2 * Settle;
            for (var i = 1; i <= steps; i++)
            {
                if (i == Settle && then != null) then(animator);
                animator.Update(Step);
                if (!moving) Weighed(animator, clips);
                if (i % StepsPerPause != 0) continue;
                if (!Paused(animator)) yield break;
                yield return true;
                if (!Resumed(animator)) yield break;
            }
            if (!moving) yield break;
            var done = 0;
            foreach (var speed in Speeds)
            {
                foreach (var (x, y) in Directions(speed))
                {
                    if (sideways) animator.SetFloat("sideway_speed", x);
                    if (forward) animator.SetFloat("forward_speed", y);
                    // Moving off takes a transition, which a step of no time never begins.
                    for (var step = 0; step < MoveSteps; step++)
                    {
                        animator.Update(Step);
                        Weighed(animator, clips);
                    }
                }
                if (++done % 2 != 0) continue;
                if (!Paused(animator)) yield break;
                yield return true;
                if (!Resumed(animator)) yield break;
            }
        }

        /// <summary>How many steps each speed and direction of a sweep is run for: enough for a transition to begin, its next state weighed in.</summary>
        private const int MoveSteps = 2;

        /// <summary>The ways a sweep moves at a speed: forward, sideways and both diagonals.</summary>
        private static (float X, float Y)[] Directions(float speed) => new[] { (0f, speed), (speed, 0f), (speed, speed), (speed, -speed) };

        /// <summary>Every clip weighed in on any layer of the state it is in now, and of the one it is moving to, by name.</summary>
        private static void Weighed(Animator animator, HashSet<string> clips)
        {
            for (var layer = 0; layer < animator.layerCount; layer++)
            {
                animator.GetCurrentAnimatorClipInfo(layer, Infos);
                foreach (var info in Infos) if (info.clip != null && info.weight > 0.001f) clips.Add(info.clip.name);
                Infos.Clear();
                if (!animator.IsInTransition(layer)) continue;
                animator.GetNextAnimatorClipInfo(layer, Infos);
                foreach (var info in Infos) if (info.clip != null && info.weight > 0.001f) clips.Add(info.clip.name);
                Infos.Clear();
            }
        }

        /// <summary>The randomness of a run paused, kept for when it goes on.</summary>
        private static Random.State _paused;

        /// <summary>Pauses a run: its copy switched off so its animator does not run on by itself; false where the copy is gone.</summary>
        private static bool Paused(Animator animator)
        {
            if (animator == null) return false;
            _paused = Random.state;
            animator.transform.parent.gameObject.SetActive(false);
            return true;
        }

        /// <summary>Goes on with a paused run; false where its copy went meanwhile.</summary>
        private static bool Resumed(Animator animator)
        {
            if (animator == null) return false;
            animator.transform.parent.gameObject.SetActive(true);
            Random.state = _paused;
            return true;
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
            Restart(animator, seed);
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

        /// <summary>An animator back where it starts, its settings as they start, seeded alike.</summary>
        private static void Restart(Animator animator, int seed)
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
