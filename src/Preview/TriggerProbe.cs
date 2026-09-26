using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// Which clip each trigger of a creature's animator plays. Its states and the ways between
    /// them cannot be read at run time, so each trigger is pulled on a hidden copy of the animated
    /// body, from where the animator starts, and the first clip it moves to is noted. Seen once
    /// per prefab and said in the log.
    /// </summary>
    internal static class TriggerProbe
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Seen = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>The clip each trigger of the animator leads to, by trigger; a trigger that leads to none is left out.</summary>
        public static Dictionary<string, string> ClipsOf(string prefab, Animator animator)
        {
            if (Seen.TryGetValue(prefab, out var known)) return known;
            var seen = new Dictionary<string, string>();
            Seen[prefab] = seen;
            if (animator == null || animator.runtimeAnimatorController == null) return seen;

            var holder = new GameObject("Scry probe");
            holder.SetActive(false);
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

                var none = new List<string>();
                foreach (var parameter in probe.parameters)
                {
                    if (parameter.type != AnimatorControllerParameterType.Trigger) continue;
                    var clip = Pull(probe, parameter.name);
                    if (clip != null) seen[parameter.name] = clip;
                    else none.Add(parameter.name);
                }
                var told = string.Join(", ", seen.Select(s => $"{s.Key} plays {s.Value}"));
                Plugin.Log.LogInfo($"Scry saw what the triggers of {prefab} play: {(told.Length > 0 ? told : "nothing")}{(none.Count > 0 ? "; no new clip for " + string.Join(", ", none) : "")}.");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not see what the triggers of {prefab} play: {ex.Message}");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
            return seen;
        }

        /// <summary>
        /// Pulls a trigger from where the animator starts and steps it on for up to a second: the
        /// first clip it moves to that was not playing before, the strongest of those, or null.
        /// </summary>
        private static string Pull(Animator animator, string trigger)
        {
            animator.Rebind();
            animator.Update(0f);
            var before = new HashSet<string>(Playing(animator).Select(p => p.Name));
            animator.SetTrigger(trigger);
            for (var i = 0; i < 40; i++)
            {
                animator.Update(0.025f);
                var moved = Playing(animator).Where(p => !before.Contains(p.Name)).OrderByDescending(p => p.Weight).FirstOrDefault();
                if (moved.Name != null) return moved.Name;
            }
            animator.ResetTrigger(trigger);
            return null;
        }

        /// <summary>The clips playing on every layer, and those it is moving to.</summary>
        private static IEnumerable<(string Name, float Weight)> Playing(Animator animator)
        {
            for (var layer = 0; layer < animator.layerCount; layer++)
            {
                foreach (var info in animator.GetCurrentAnimatorClipInfo(layer)) if (info.clip != null) yield return (info.clip.name, info.weight);
                if (!animator.IsInTransition(layer)) continue;
                foreach (var info in animator.GetNextAnimatorClipInfo(layer)) if (info.clip != null) yield return (info.clip.name, info.weight);
            }
        }
    }
}
