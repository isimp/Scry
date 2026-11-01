using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    internal static partial class Previews
    {
        /// <summary>
        /// Works out, once what each clip plays is known, which clips the stage copy never plays and
        /// which it plays only with other gear in hand: where each of its animator's attack triggers
        /// leads, and what it plays under each stance and with each switch an item names, each seen
        /// on a hidden copy over the next frames and kept for its prefab whatever it carries; and
        /// what it carries now. A person plays any attack once it holds the item, so what its other
        /// attacks play is other gear's, not never played.
        /// </summary>
        private static void SortLeftovers()
        {
            var plays = StagePlays(out var ears);
            if (plays == null || !plays.Ready || (plays.Unplayed != null && !plays.Provisional)) return;
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            if (animator == null || animator.runtimeAnimatorController == null) return;
            var key = ears.Prefab.name + "|" + animator.runtimeAnimatorController.name;
            // Each step timed apart, so a slow frame names the one that took it.
            var step = Timing.Start();
            if (!LeadsOf.TryGetValue(key, out var leads))
            {
                var asked = ClipLeftovers.Candidates(animator.parameters.Where(p => p.type == AnimatorControllerParameterType.Trigger).Select(p => p.name));
                var seen = asked.Count > 0 ? TriggerProbe.ClipsOf(ears.Prefab.name, animator, asked, wait: false) : null;
                Timing.Add("probe clips triggers", step);
                if (seen != null && seen.Busy) return;
                leads = new Dictionary<string, IReadOnlyList<string>>();
                if (seen != null) foreach (var trigger in asked) if (seen.Triggers.TryGetValue(trigger, out var clips)) leads[trigger] = clips;
                LeadsOf[key] = leads;
            }
            step = Timing.Start();
            var gear = GameGear();
            Timing.Add("probe clips gear", step);
            // The stance it stands in swept first, answered before the rest (HeldClips.OtherGear's
            // provisional rule), and worked out again once every stance is.
            step = Timing.Start();
            var now = StanceNow(animator);
            var swept = TriggerProbe.StancesOf(ears.Prefab.name, animator, Stances.OrderBy(s => s == now ? 0 : 1).ToList(), gear.Holds, wait: false);
            Timing.Add("probe clips stances", step);
            // Only a person, whose sweep is long, is told from its own stance first; a creature's is short, and waited for.
            var partial = swept.Busy;
            if (partial && (plays.Provisional || ears.Prefab.GetComponent<Player>() == null || !HasStance(animator) || !swept.Stances.ContainsKey(now))) return;
            SortedOut(plays, ears, animator, leads, gear, swept, now, partial);
        }

        /// <summary>The clips sorted once where each leads and what each stance plays are known: those other gear plays, those never played, and the groups the page shows them in; each step timed apart.</summary>
        private static void SortedOut(ClipPlays plays, AnimationEars ears, Animator animator, Dictionary<string, IReadOnlyList<string>> leads,
            (List<(string Switch, int Stance)> Holds, Dictionary<string, int> HoldStances, Dictionary<string, int> AttackStances) gear, Probed swept, int now, bool partial)
        {
            var step = Timing.Start();
            var other = plays.Tags.Keys.Concat(plays.Actions.Keys).Concat(plays.Around.Keys).Concat(plays.ByName.Keys).Concat(plays.Attacks.Keys).ToList();
            var held = HeldClips.OtherGear(swept.Stances, swept.Holds, now, plays.Holding, gear.HoldStances,
                partial ? ClipNames() : null, partial ? other.Concat(plays.Idles) : null);
            var left = ClipLeftovers.UnplayedBy(leads, plays.Pulled, other.Where(c => !held.ContainsKey(c)));
            var unplayed = new HashSet<string>(System.StringComparer.Ordinal);
            var person = ears.Prefab.GetComponent<Player>() != null;
            foreach (var clip in left)
            {
                if (held.ContainsKey(clip.Key)) continue;
                if (person) held[clip.Key] = gear.AttackStances.TryGetValue(clip.Value, out var stance) ? stance : -1;
                else unplayed.Add(clip.Key);
            }
            // What it plays now, as its own attacks and actions, is never other gear's.
            foreach (var clip in plays.Attacks.Keys.Concat(plays.Actions.Keys)) held.Remove(clip);
            Timing.Add("probe clips sorted", step);
            step = Timing.Start();
            plays.Groups = GroupsOf(plays, swept);
            Timing.Add("probe clips grouped", step);
            plays.Stance = HasStance(animator) ? StanceNow(animator) : -1;
            plays.OtherGear = held;
            plays.Unplayed = unplayed;
            plays.Provisional = partial;
            if (unplayed.Count > 0) Log.Note($"Scry leaves out {Numbers.Count(unplayed.Count)} clips {ears.Prefab.name} never plays, as nothing it carries starts them: {string.Join(", ", unplayed.OrderBy(c => c, System.StringComparer.Ordinal))}.");
            if (held.Count > 0) Log.Note($"Scry leaves out {Numbers.Count(held.Count)} clips {ears.Prefab.name} plays only with other gear in hand.");
        }

        /// <summary>Where each attack trigger of a prefab's animator leads, by prefab and controller, seen once (<see cref="SortLeftovers"/>).</summary>
        private static readonly Dictionary<string, Dictionary<string, IReadOnlyList<string>>> LeadsOf = new Dictionary<string, Dictionary<string, IReadOnlyList<string>>>();

        /// <summary>Lets go of where the attack triggers lead, for the next world's prefabs.</summary>
        private static void ForgetLeads() => LeadsOf.Clear();
    }
}
