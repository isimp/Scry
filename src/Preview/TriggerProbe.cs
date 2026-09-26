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
    }

    /// <summary>
    /// Which clips a creature's animator plays for each attack trigger and for each of the
    /// game's own actions that come with an effect list. Its states and the ways between them
    /// cannot be read at run time, so each is done on a hidden copy of the animated body, from
    /// where the animator starts, and watched. An attack is watched as the game watches one
    /// (<c>Humanoid.InAttack</c>): for as long as it is in a state tagged "attack", every clip it
    /// plays there is the attack's, in order, so a bow drawn in one clip and let go in the next is
    /// both. Otherwise the first clip it moves to that it never plays when left alone is taken.
    /// Seen once per prefab and said in the log.
    /// </summary>
    internal static class TriggerProbe
    {
        private static readonly int AttackTag = Animator.StringToHash("attack");
        private const float Step = 0.025f;

        /// <summary>
        /// The game's own actions, as its code does them: <c>Character.ForceJump</c> pulls
        /// "jump", <c>MonsterAI.UpdateConsumeItem</c> "consume", <c>MonsterAI.Sleep</c> and
        /// <c>Wakeup</c> switch "sleeping" on and off, <c>BaseAI.SetAlerted</c> switches "alert".
        /// Each is the switch or trigger, whether it is a switch, and whether it is let go after.
        /// </summary>
        private static readonly (string Action, string Name, bool Switch, bool Release)[] GameActions =
        {
            ("jump", "jump", false, false),
            ("consume", "consume", false, false),
            ("sleep", "sleeping", true, false),
            ("wake", "sleeping", true, true),
            ("alert", "alert", true, false),
        };

        private static readonly Dictionary<string, Probed> Seen = new Dictionary<string, Probed>();

        /// <summary>The clips each of the triggers and each of the game's actions leads through; one that leads to none has an empty list.</summary>
        public static Probed ClipsOf(string prefab, Animator animator, IEnumerable<string> triggers)
        {
            if (Seen.TryGetValue(prefab, out var known)) return known;
            var seen = new Probed();
            Seen[prefab] = seen;
            if (animator == null || animator.runtimeAnimatorController == null) return seen;

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

                // The game's RandomIdle picks idle clips at random; seeded alike, each run idles
                // alike, so what it plays left alone is what nothing done to it plays.
                var idle = new HashSet<string>();
                Run(probe, null, null, idle);

                bool Has(string name, AnimatorControllerParameterType type) => probe.parameters.Any(p => p.type == type && p.name == name);
                string Tell(string name, List<string> clips) => clips.Count > 0 ? $"{name} plays {string.Join(" then ", clips)}" : $"{name} plays no clip of its own";

                var attacks = new List<string>();
                foreach (var trigger in triggers.Distinct())
                {
                    if (!Has(trigger, AnimatorControllerParameterType.Trigger)) continue;
                    var clips = Run(probe, a => a.SetTrigger(trigger), null, idle);
                    seen.Triggers[trigger] = clips;
                    attacks.Add(Tell(trigger, clips));
                }

                var actions = new List<string>();
                foreach (var (action, name, isSwitch, release) in GameActions)
                {
                    if (!Has(name, isSwitch ? AnimatorControllerParameterType.Bool : AnimatorControllerParameterType.Trigger)) continue;
                    System.Action<Animator> act = isSwitch ? a => a.SetBool(name, true) : (System.Action<Animator>)(a => a.SetTrigger(name));
                    var clips = release ? Run(probe, act, a => a.SetBool(name, false), idle) : Run(probe, act, null, idle);
                    seen.Actions[action] = clips;
                    actions.Add(Tell(action, clips));
                }
                Plugin.Log.LogInfo($"Scry saw what the animator of {prefab} plays: attacks {(attacks.Count > 0 ? string.Join("; ", attacks) : "none")}; the game's own actions {(actions.Count > 0 ? string.Join("; ", actions) : "none")}.");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not see what the animator of {prefab} plays: {ex.Message}");
            }
            finally
            {
                Random.state = random;
                Object.DestroyImmediate(holder);
            }
            return seen;
        }

        /// <summary>
        /// Starts the animator over, seeded alike and with its settings as they start, and steps
        /// it on. Done nothing to, every clip it plays for four seconds goes into
        /// <paramref name="idle"/>. Otherwise it is done <paramref name="act"/> to, and, when
        /// given, <paramref name="then"/> two seconds later: the clips of its attack states in
        /// order until the attack is over; failing any within a second, the first clip it moves
        /// to that it did not play left alone or before <paramref name="then"/>.
        /// </summary>
        private static List<string> Run(Animator animator, System.Action<Animator> act, System.Action<Animator> then, HashSet<string> idle)
        {
            Random.InitState(1);
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
            animator.Update(0f);

            var played = new List<string>();
            if (act == null)
            {
                for (var i = 0; i < 160; i++)
                {
                    animator.Update(Step);
                    for (var layer = 0; layer < animator.layerCount; layer++)
                    {
                        foreach (var info in animator.GetCurrentAnimatorClipInfo(layer)) if (info.clip != null) idle.Add(info.clip.name);
                        foreach (var info in animator.GetNextAnimatorClipInfo(layer)) if (info.clip != null) idle.Add(info.clip.name);
                    }
                }
                return played;
            }

            act(animator);
            var known = idle;
            if (then != null)
            {
                known = new HashSet<string>(idle);
                for (var i = 0; i < 80; i++)
                {
                    animator.Update(Step);
                    var before = Heading(animator, false);
                    if (before != null) known.Add(before);
                }
                then(animator);
            }

            var attacking = false;
            string moved = null;
            for (var i = 0; i < 160; i++)
            {
                animator.Update(Step);
                var attack = Heading(animator, true);
                if (attack != null)
                {
                    attacking = true;
                    if (!played.Contains(attack)) played.Add(attack);
                    continue;
                }
                if (attacking) break;

                var heading = Heading(animator, false);
                if (moved == null && heading != null && !known.Contains(heading)) moved = heading;
                if (i >= 40) break;
            }
            if (!attacking && moved != null) played.Add(moved);
            return played;
        }

        /// <summary>
        /// The clip a layer is playing or moving to, the strongest of its clips there: of the
        /// first layer heading into an attack state when <paramref name="attack"/>, else of the
        /// first layer. Null when there is none.
        /// </summary>
        private static string Heading(Animator animator, bool attack)
        {
            for (var layer = 0; layer < (attack ? animator.layerCount : 1); layer++)
            {
                var moving = animator.IsInTransition(layer);
                var state = moving ? animator.GetNextAnimatorStateInfo(layer) : animator.GetCurrentAnimatorStateInfo(layer);
                if (attack && state.tagHash != AttackTag) continue;
                var infos = moving ? animator.GetNextAnimatorClipInfo(layer) : animator.GetCurrentAnimatorClipInfo(layer);
                var strongest = infos.Where(c => c.clip != null).OrderByDescending(c => c.weight).FirstOrDefault();
                if (strongest.clip != null) return strongest.clip.name;
            }
            return null;
        }
    }
}
