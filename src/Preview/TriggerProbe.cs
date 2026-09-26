using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// Which clips each attack trigger of a creature's animator plays. Its states and the ways
    /// between them cannot be read at run time, so each trigger is pulled on a hidden copy of the
    /// animated body, from where the animator starts, and watched as the game watches an attack
    /// (<c>Humanoid.InAttack</c>): for as long as it is in a state tagged "attack", every clip it
    /// plays there is the attack's, in order, so a bow drawn in one clip and let go in the next is
    /// both. Where an animator tags no attack, the first clip it moves to that it never plays
    /// when left alone is taken. Seen once per prefab and said in the log.
    /// </summary>
    internal static class TriggerProbe
    {
        private static readonly int AttackTag = Animator.StringToHash("attack");
        private const float Step = 0.025f;

        private static readonly Dictionary<string, Dictionary<string, IReadOnlyList<string>>> Seen = new Dictionary<string, Dictionary<string, IReadOnlyList<string>>>();

        /// <summary>The clips each of the triggers leads through, by trigger; one that leads to none has an empty list.</summary>
        public static Dictionary<string, IReadOnlyList<string>> ClipsOf(string prefab, Animator animator, IEnumerable<string> triggers)
        {
            if (Seen.TryGetValue(prefab, out var known)) return known;
            var seen = new Dictionary<string, IReadOnlyList<string>>();
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
                // alike, so what it plays left alone is what no trigger plays.
                var idle = new HashSet<string>();
                Run(probe, null, idle);

                var told = new List<string>();
                foreach (var trigger in triggers.Distinct())
                {
                    if (!probe.parameters.Any(p => p.type == AnimatorControllerParameterType.Trigger && p.name == trigger)) continue;
                    var clips = Run(probe, trigger, idle);
                    seen[trigger] = clips;
                    told.Add(clips.Count > 0 ? $"{trigger} plays {string.Join(" then ", clips)}" : $"{trigger} plays no clip of its own");
                }
                Plugin.Log.LogInfo($"Scry saw what the attack triggers of {prefab} play: {(told.Count > 0 ? string.Join("; ", told) : "none")}.");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not see what the triggers of {prefab} play: {ex.Message}");
            }
            finally
            {
                Random.state = random;
                Object.DestroyImmediate(holder);
            }
            return seen;
        }

        /// <summary>
        /// Starts the animator over, seeded alike, and steps it on. With no trigger, every clip it
        /// plays for four seconds goes into <paramref name="idle"/>. With one, the clips of its
        /// attack states in order until the attack is over; failing any within a second, the
        /// first clip it moves to that is not in <paramref name="idle"/>.
        /// </summary>
        private static List<string> Run(Animator animator, string trigger, HashSet<string> idle)
        {
            Random.InitState(1);
            animator.Rebind();
            animator.Update(0f);
            if (trigger != null) animator.SetTrigger(trigger);

            var played = new List<string>();
            var attacking = false;
            string moved = null;
            for (var i = 0; i < 160; i++)
            {
                animator.Update(Step);
                if (trigger == null)
                {
                    for (var layer = 0; layer < animator.layerCount; layer++)
                    {
                        foreach (var info in animator.GetCurrentAnimatorClipInfo(layer)) if (info.clip != null) idle.Add(info.clip.name);
                        foreach (var info in animator.GetNextAnimatorClipInfo(layer)) if (info.clip != null) idle.Add(info.clip.name);
                    }
                    continue;
                }

                var attack = Heading(animator, true);
                if (attack != null)
                {
                    attacking = true;
                    if (!played.Contains(attack)) played.Add(attack);
                    continue;
                }
                if (attacking) break;

                var heading = Heading(animator, false);
                if (moved == null && heading != null && !idle.Contains(heading)) moved = heading;
                if (i >= 40) break;
            }
            if (!attacking && moved != null) played.Add(moved);
            if (trigger != null) animator.ResetTrigger(trigger);
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
