using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The animator that drives a prefab or its copy, as the game finds a character's: the first
    /// one with a controller on a part that is switched on. Some prefabs keep an old model
    /// switched off beside the one in use (a frost troll's Visual_OLD), with an animator of its
    /// own; failing a switched-on one, the first there is.
    /// </summary>
    internal static class Animators
    {
        public static Animator Main(GameObject root) => root == null ? null : Main(root, root.GetComponentsInChildren<Animator>(true));

        /// <summary>The same among a root's animators found before, in the order found; one taken away since is passed over.</summary>
        public static Animator Main(GameObject root, Animator[] animators)
        {
            if (root == null || animators == null) return null;
            Animator any = null;
            foreach (var animator in animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                if (SwitchedOn(animator.transform, root.transform)) return animator;
                if (any == null) any = animator;
            }
            return any;
        }

        /// <summary>Whether a part and every part above it, up to the root, is switched on, as the prefab has it.</summary>
        public static bool SwitchedOn(Transform part, Transform root)
        {
            for (var t = part; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == root) return true;
            }
            return true;
        }
    }
}
