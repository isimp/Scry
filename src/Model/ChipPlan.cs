using System.Collections.Generic;

namespace Scry
{
    /// <summary>What an effect list is to the prefab it is on, where the game does something with it besides playing it.</summary>
    internal enum ListRole
    {
        Other,
        TreeHit,
        Jump,
        Death,
        Alerted,
        Wakeup,
        Consume,
        Block,
    }

    /// <summary>What pressing an effect list's chip does first.</summary>
    internal enum ChipStep
    {
        /// <summary>The creature falls as its ragdoll.</summary>
        Ragdoll,
        /// <summary>The copy is destroyed as the game destroys the prefab, leaving what it leaves.</summary>
        Destroy,
        /// <summary>A tree's trunk shakes, as when it is struck.</summary>
        ShakeTrunk,
        /// <summary>A trigger is pulled on the copies' animators.</summary>
        Trigger,
        /// <summary>A switch is set on the copies' animators.</summary>
        Switch,
        /// <summary>An item's attack is played whole by the clip the person swings it in.</summary>
        AttackClip,
        /// <summary>The clip the game plays the list with is played.</summary>
        OwnClip,
        /// <summary>Only the list is played.</summary>
        List,
    }

    /// <summary>What is known of an effect list when its chip is pressed.</summary>
    internal sealed class ChipFacts
    {
        public ListRole Role;

        /// <summary>The trigger and switch names the copies' animators have.</summary>
        public HashSet<string> Parameters;

        /// <summary>A flyer takes off where others jump.</summary>
        public bool Flying;

        /// <summary>A creature whose list leaves a ragdoll behind.</summary>
        public bool HasRagdoll;

        /// <summary>What the game plays as it destroys the prefab.</summary>
        public bool IsDestroyed;

        /// <summary>An item's attack the person has a clip for.</summary>
        public bool HasAttackClip;

        /// <summary>A clip the game plays the list with, as the animator was seen to.</summary>
        public bool HasOwnClip;

        public ChipFacts(ListRole role, params string[] parameters)
        {
            Role = role;
            Parameters = new HashSet<string>(parameters ?? System.Array.Empty<string>());
        }
    }

    /// <summary>
    /// What pressing an effect list's chip does, as the game does it when it plays that list: a
    /// death with a ragdoll falls as it, a list played as the prefab is destroyed destroys the
    /// copy, a tree struck shakes, a jump, a death, being alerted, waking, eating and blocking set
    /// the animator as the game sets it where the animator can be set so; failing that, an item's
    /// attack plays whole by its clip, a list the game plays with a clip plays that clip, and
    /// anything else just plays. Apart from the attack, whose clip plays the list as it goes, the
    /// list itself plays too.
    /// </summary>
    internal sealed class ChipPlan
    {
        public ChipStep Step;

        /// <summary>The trigger or switch to set.</summary>
        public string Parameter;

        /// <summary>Whether the list itself is played as well.</summary>
        public bool PlaysList = true;

        /// <summary>A switch set until the list is stopped.</summary>
        public bool UndoOnStop;

        /// <summary>A switch set for this long only, in seconds; 0 for no end.</summary>
        public float OffAfter;

        public static ChipPlan For(ChipFacts facts)
        {
            if (facts.HasRagdoll) return new ChipPlan { Step = ChipStep.Ragdoll };
            if (facts.IsDestroyed) return new ChipPlan { Step = ChipStep.Destroy };

            var animated = Animated(facts);
            if (animated != null) return animated;

            if (facts.HasAttackClip) return new ChipPlan { Step = ChipStep.AttackClip, PlaysList = false };
            if (facts.HasOwnClip) return new ChipPlan { Step = ChipStep.OwnClip };
            return new ChipPlan { Step = ChipStep.List };
        }

        /// <summary>How the game animates the list, where the animator can be set so; null otherwise.</summary>
        private static ChipPlan Animated(ChipFacts facts)
        {
            bool Has(string name) => facts.Parameters.Contains(name);
            ChipPlan Trigger(string name) => new ChipPlan { Step = ChipStep.Trigger, Parameter = name };
            ChipPlan Switch(string name, bool undo, float offAfter) => new ChipPlan { Step = ChipStep.Switch, Parameter = name, UndoOnStop = undo, OffAfter = offAfter };

            switch (facts.Role)
            {
                case ListRole.TreeHit:
                    return new ChipPlan { Step = ChipStep.ShakeTrunk };
                case ListRole.Jump:
                    if (facts.Flying && Has("fly_takeoff")) return Trigger("fly_takeoff");
                    return Has("jump") ? Trigger("jump") : null;
                case ListRole.Death:
                    return Has("dead") ? Switch("dead", true, 0f) : null;
                case ListRole.Alerted:
                    return Has("alert") ? Switch("alert", true, 0f) : null;
                case ListRole.Wakeup:
                    return Has("sleeping") ? Switch("sleeping", false, 1.2f) : null;
                case ListRole.Consume:
                    if (Has("eat")) return Trigger("eat");
                    return Has("consume") ? Trigger("consume") : null;
                case ListRole.Block:
                    return Has("blocking") ? Switch("blocking", false, 1.2f) : null;
                default:
                    return null;
            }
        }
    }
}
