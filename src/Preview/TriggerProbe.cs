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

        /// <summary>Where the animator is at one step: the state it is in or moving to, its strongest clip there, and the clip of an attack state it is in or moving to.</summary>
        private struct Frame
        {
            public int State;
            public string Clip;
            public string Attack;
        }

        private static readonly Dictionary<string, Probed> Seen = new Dictionary<string, Probed>();

        /// <summary>The clips each of the triggers and each of the game's actions leads through; one that leads to none has an empty list.</summary>
        public static Probed ClipsOf(string prefab, Animator animator, IEnumerable<string> triggers)
        {
            // Seen for each stance it stands in: what a trigger plays depends on the weapon held
            // (Humanoid.SetupAnimationState sets statei and statef).
            var stance = new List<(string Name, AnimatorControllerParameterType Type, float Value)>();
            if (animator != null)
            {
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.name == "statei" && parameter.type == AnimatorControllerParameterType.Int) stance.Add((parameter.name, parameter.type, animator.GetInteger(parameter.name)));
                    if (parameter.name == "statef" && parameter.type == AnimatorControllerParameterType.Float) stance.Add((parameter.name, parameter.type, animator.GetFloat(parameter.name)));
                }
            }
            var key = prefab + string.Concat(stance.Select(s => $"|{s.Name}={s.Value}"));

            // Seen before in this stance, only triggers not pulled yet are (a sword's and a mace's
            // differ, while their stance is the same).
            var wanted = triggers.Distinct().ToList();
            var first = !Seen.TryGetValue(key, out var seen);
            if (first)
            {
                seen = new Probed();
                Seen[key] = seen;
            }
            else
            {
                wanted = wanted.Where(t => !seen.Triggers.ContainsKey(t)).ToList();
                if (wanted.Count == 0) return seen;
            }
            if (animator == null || animator.runtimeAnimatorController == null) return seen;
            _stance = a =>
            {
                foreach (var (name, type, value) in stance)
                {
                    if (type == AnimatorControllerParameterType.Int) a.SetInteger(name, (int)value);
                    else a.SetFloat(name, value);
                }
            };

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var holder = new GameObject("Scry probe");
            holder.SetActive(false);
            var random = Random.state;
            try
            {
                // Drawn by nothing, heard by nothing, sending no events: only the animator runs.
                // Its scripts go before it wakes, which would wake them even switched off.
                var body = Object.Instantiate(animator.gameObject, holder.transform, false);
                foreach (var script in body.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                foreach (var behaviour in body.GetComponentsInChildren<Behaviour>(true)) if (!(behaviour is Animator)) behaviour.enabled = false;
                foreach (var renderer in body.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                var probe = body.GetComponent<Animator>();
                probe.fireEvents = false;
                probe.applyRootMotion = false;
                probe.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                holder.SetActive(true);

                var alone = Trace(probe, null, false, null, -1, first ? 2 * ActionPatience + Settle : 160);
                if (first)
                {
                    foreach (var frame in alone) if (frame.Clip != null) seen.Idle.Add(frame.Clip);
                    for (var seed = 2; seed <= IdleSeeds + 1; seed++)
                    {
                        foreach (var frame in Trace(probe, null, false, null, -1, 160, seed)) if (frame.Clip != null) seen.Idle.Add(frame.Clip);
                    }
                }
                bool Has(string name, AnimatorControllerParameterType type) => probe.parameters.Any(p => p.type == type && p.name == name);
                string Tell(string name, List<string> clips) => clips.Count > 0 ? $"{name} plays {string.Join(" then ", clips)}" : $"{name} plays no clip of its own";

                var attacks = new List<string>();
                foreach (var trigger in wanted)
                {
                    if (!Has(trigger, AnimatorControllerParameterType.Trigger)) continue;
                    var run = Trace(probe, a => a.SetTrigger(trigger), false, null, -1, 160);
                    var clips = AttackClips(run);
                    if (clips.Count == 0)
                    {
                        var at = Parting(run, alone, 0, AttackPatience);
                        if (at >= 0 && run[at].Clip != null) clips.Add(run[at].Clip);
                    }
                    seen.Triggers[trigger] = clips;
                    attacks.Add(Tell(trigger, clips));
                }

                var actions = new List<string>();
                foreach (var (action, name, isSwitch, release, speed) in first ? GameActions : new (string, string, bool, bool, float)[0])
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
                        beside = Trace(probe, null, false, null, -1, 2 * ActionPatience + Settle, 1, setup);
                    }

                    // A switch is set as the animator starts, since some go to sleep only from
                    // their start; a trigger is pulled once it runs.
                    var run = Trace(probe, act, isSwitch, null, -1, 2 * ActionPatience + Settle, 1, setup);
                    var at = Parting(run, beside, 0, ActionPatience);
                    var clips = new List<string>();
                    if (!release)
                    {
                        if (at >= 0 && run[at].Clip != null) clips.Add(run[at].Clip);
                    }
                    else if (at >= 0)
                    {
                        // Let go only once it has gone there (fallen asleep), beside a run kept there.
                        var letGo = at + Settle;
                        var back = Trace(probe, act, isSwitch, a => a.SetBool(name, false), letGo, letGo + ActionPatience, 1, setup);
                        var woke = Parting(back, run, letGo, back.Count - 1);
                        if (woke >= 0 && back[woke].Clip != null) clips.Add(back[woke].Clip);
                    }
                    seen.Actions[action] = clips;
                    actions.Add(Tell(action, clips));
                }
                if (!first) Plugin.Note($"Scry saw in {watch.ElapsedMilliseconds} ms what more triggers of {key} play: {string.Join("; ", attacks)}.");
                else Plugin.Note($"Scry saw in {watch.ElapsedMilliseconds} ms what the animator of {key} plays: attacks {(attacks.Count > 0 ? string.Join("; ", attacks) : "none")}; the game's own actions {(actions.Count > 0 ? string.Join("; ", actions) : "none")}; left alone {string.Join(", ", seen.Idle.OrderBy(c => c))}.");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not see what the animator of {prefab} plays: {ex.Message}");
            }
            finally
            {
                Random.state = random;
                _stance = null;
                Object.DestroyImmediate(holder);
            }
            return seen;
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
        private static List<Frame> Trace(Animator animator, System.Action<Animator> act, bool first, System.Action<Animator> then, int thenAt, int steps, int seed = 1, System.Action<Animator> setup = null)
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

            var frames = new List<Frame> { Where(animator) };
            for (var i = 1; i <= steps; i++)
            {
                if (i == thenAt && then != null) then(animator);
                animator.Update(Step);
                frames.Add(Where(animator));
            }
            return frames;
        }

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
                    if (!clips.Contains(frame.Attack)) clips.Add(frame.Attack);
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
        private static string AttackIn(Animator animator)
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

        /// <summary>The strongest clip of the state a layer is in, or is moving to.</summary>
        private static string Strongest(Animator animator, int layer, bool moving)
        {
            var infos = moving ? animator.GetNextAnimatorClipInfo(layer) : animator.GetCurrentAnimatorClipInfo(layer);
            var strongest = infos.Where(c => c.clip != null).OrderByDescending(c => c.weight).FirstOrDefault();
            return strongest.clip != null ? strongest.clip.name : null;
        }
    }
}
