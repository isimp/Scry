using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What is shown bent at its tail as the game bends it every frame after its animator
    /// (<c>Tail</c>, <see cref="TailBend"/>): a Serpent's long body or a Leech's, joint by joint,
    /// each trailing the one before. The copy is stripped of the game's script, so the stage bends
    /// the joints itself, after the animator has posed them and before the film, over its own
    /// sea's surface and ground in place of the world's. Only what is shown is bent, not a raid's
    /// crowd or a place's creatures; and as it swims in place, its tail trails the sway of its
    /// front rather than streaming out behind as it does at speed in the world.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>One joint of the tail: the copy's bone, where it was put last and how far from its parent it hangs.</summary>
        private sealed class TailJoint
        {
            public Transform Bone;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Length;
        }

        /// <summary>What is shown's tail joints, head first, as the game's prefab lists them; none for what has no tail.</summary>
        private static readonly List<TailJoint> TailJoints = new List<TailJoint>();

        private static TailBend.Rules _tailRules;

        /// <summary>Whether the joints were taken where the copy first stands, which the game takes as it wakes.</summary>
        private static bool _tailTaken;

        /// <summary>A fault in bending the tail is told once and lets go of it, the joints left as the animator poses them.</summary>
        private const string TailPart = "bending what is shown at its tail";

        /// <summary>For the self-test: how many joints of what is shown the stage bends.</summary>
        [ForTest]
        public static int TailJointsBent => _tailTaken ? TailJoints.Count : 0;

        /// <summary>For the self-test: lets go of the tail, left as the animator poses it until what is shown is shown again, to set beside the bent one.</summary>
        [ForTest]
        public static void LetTailGoForTest() => ForgetTail();

        /// <summary>For the self-test: how far the tail's joints stand from their parents at the most, as a share of how far they hung when taken, at the stage's size now; 0 with no tail.</summary>
        [ForTest]
        public static float TailStretch
        {
            get
            {
                if (!_tailTaken) return 0f;
                var scale = _scale > 0f ? _scale : 1f;
                var most = 0f;
                foreach (var joint in TailJoints)
                {
                    if (joint.Bone == null || joint.Length <= 0f) continue;
                    most = Mathf.Max(most, Vector3.Distance(joint.Bone.parent.position, joint.Bone.position) / (joint.Length * scale));
                }
                return most;
            }
        }

        private static void ForgetTail()
        {
            TailJoints.Clear();
            _tailTaken = false;
        }

        /// <summary>
        /// Reads a copy newly shown for the game's tail on its prefab, with its settings, and finds
        /// the copy's bone for each joint by its path from the root; none at all where one is missing.
        /// </summary>
        private static void ReadTail(Entry entry)
        {
            ForgetTail();
            if (_subject == null || _subjectIsPerson || !(entry?.Source is GameObject prefab)) return;
            var tail = prefab.GetComponentInChildren<Tail>(true);
            if (tail == null || tail.m_tailJoints == null) return;
            _tailRules = new TailBend.Rules
            {
                MaxAngle = tail.m_maxAngle,
                Gravity = tail.m_gravity,
                GravityInWater = tail.m_gravityInWater,
                WaterCheck = tail.m_waterSurfaceCheck,
                GroundCheck = tail.m_groundCheck,
                Smoothness = tail.m_smoothness,
                Radius = tail.m_tailRadius,
            };
            foreach (var joint in tail.m_tailJoints)
            {
                var bone = Counterpart(prefab.transform, joint, _subject.transform);
                if (bone == null || bone.parent == null)
                {
                    TailJoints.Clear();
                    return;
                }
                TailJoints.Add(new TailJoint { Bone = bone });
            }
        }

        /// <summary>The copy's part standing where a part stands under the prefab's root, found child by child by name; null where there is none.</summary>
        private static Transform Counterpart(Transform root, Transform part, Transform copyRoot)
        {
            if (part == null) return null;
            var names = new List<string>();
            for (var t = part; t != root; t = t.parent)
            {
                if (t == null) return null;
                names.Add(t.name);
            }
            var found = copyRoot;
            for (var i = names.Count - 1; i >= 0 && found != null; i--) found = found.Find(names[i]);
            return found;
        }

        /// <summary>
        /// Each frame, after the animator and the framing: bends each joint as the game does
        /// (<c>Tail.CustomLateUpdate</c>), its joints' lengths and settings in metres taken at the
        /// stage's size, so a size changed while it is shown keeps the tail's shape; the sea's
        /// surface where the sea's ground lies and the ground under the model.
        /// </summary>
        private static void KeepTail()
        {
            if (TailJoints.Count == 0) return;
            if (TailJoints[0].Bone == null)
            {
                ForgetTail();
                return;
            }
            var scale = _scale > 0f ? _scale : 1f;
            if (!_tailTaken)
            {
                // Lengths are kept as if at size one, as the settings are.
                foreach (var joint in TailJoints)
                {
                    joint.Position = joint.Bone.position;
                    joint.Rotation = joint.Bone.rotation;
                    joint.Length = Vector3.Distance(joint.Bone.parent.position, joint.Bone.position) / scale;
                }
                _tailTaken = true;
            }
            var rules = _tailRules;
            rules.Gravity *= scale;
            rules.GravityInWater *= scale;
            rules.Radius *= scale;
            var water = _seaLevels.Surface;
            var ground = Finite(_groundY) ? _groundY : float.NegativeInfinity;
            var dt = Time.unscaledDeltaTime;
            foreach (var joint in TailJoints)
            {
                var parent = joint.Bone.parent;
                var line = parent.up;
                var length = joint.Length * scale;
                var middle = parent.position + line * (length * 0.5f);
                var next = U(TailBend.Next(V(joint.Position), V(parent.position), V(line), length, water, ground, rules, dt));
                // Where a joint has come to its parent's middle, the game bends no more of the tail this frame.
                if (middle == next) break;
                var toward = (middle - next).normalized;
                var side = Vector3.Cross(Vector3.up, -toward);
                var turn = Quaternion.Slerp(joint.Rotation, Quaternion.LookRotation(Vector3.Cross(-toward, side), -toward), rules.Smoothness);
                joint.Bone.SetPositionAndRotation(next, turn);
                joint.Position = next;
                joint.Rotation = turn;
            }
        }
    }
}
