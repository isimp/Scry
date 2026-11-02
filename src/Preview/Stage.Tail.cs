using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What is shown bent at its tail as the game bends it every frame after its animator
    /// (<c>Tail</c>, <see cref="TailBend"/>): a Serpent's long body or a Leech's, joint by joint,
    /// each trailing the one before. The copy is stripped of the game's script, so the stage bends
    /// the joints itself, after the animator has posed them and before the film, over its own
    /// sea's surface and ground in place of the world's. It is bent in steps of its own
    /// (<see cref="TailBend.Step"/>) and shown part way between the last two, so its shape does not
    /// change with how long the stage's frames take. Held swimming, it stops while one of its
    /// attacks plays and sets off again after, as the game stops it once what it attacks is in
    /// reach, so it rears and bites in place. Only what is shown is bent, not a raid's
    /// crowd or a place's creatures.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>One joint of the tail: the copy's bone, where the last step put it and the step before, and how far from its parent it hangs.</summary>
        private sealed class TailJoint
        {
            public Transform Bone;
            public Vector3 Position, WasPosition;
            public Quaternion Rotation, WasRotation;
            public float Length;
        }

        /// <summary>The time since the tail's last step, carried from frame to frame.</summary>
        private static float _tailCarried;

        /// <summary>What is shown's tail joints, head first, as the game's prefab lists them; none for what has no tail.</summary>
        private static readonly List<TailJoint> TailJoints = new List<TailJoint>();

        private static TailBend.Rules _tailRules;

        /// <summary>How fast what is shown swims (<c>Character.m_swimSpeed</c>), and whether it lives only in water, for its tail to stream out as when it swims.</summary>
        private static float _tailSwimSpeed;

        private static bool _tailLivesInWater;

        /// <summary>
        /// How fast what is shown swims this moment, and how fast it gains speed
        /// (<c>Character.m_swimAcceleration</c>): the game stops a creature once what it attacks
        /// is in reach (<c>MonsterAI.StopMoving</c>) and sets it off again after, so it rears and
        /// bites in place, its tail no longer streaming out.
        /// </summary>
        private static float _tailSpeed, _tailAcceleration;

        /// <summary>The prefab shown, whose clips' attacks tell when it stops.</summary>
        private static GameObject _tailPrefab;

        /// <summary>The clip last looked at, whether the clips' attacks were known then, and whether it plays an attack.</summary>
        private static AnimationClip _tailClip;

        private static bool _tailClipKnown, _tailClipAttacks;

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

        /// <summary>For the self-test: how far under the sea's surface the tail's deepest joint lies, as if at size one; NaN with no tail or no sea.</summary>
        [ForTest]
        public static float TailDeepest
        {
            get
            {
                if (!_tailTaken || !Finite(_seaLevels.Surface)) return float.NaN;
                var scale = _scale > 0f ? _scale : 1f;
                var most = float.NaN;
                foreach (var joint in TailJoints)
                {
                    if (joint.Bone == null) continue;
                    var depth = (_seaLevels.Surface - joint.Bone.position.y) / scale;
                    if (float.IsNaN(most) || depth > most) most = depth;
                }
                return most;
            }
        }

        private static void ForgetTail()
        {
            TailJoints.Clear();
            _tailTaken = false;
            _tailCarried = 0f;
            _tailPrefab = null;
            _tailClip = null;
            _tailClipKnown = _tailClipAttacks = false;
        }

        /// <summary>For the self-test: how fast what is shown swims this moment, in metres a second at size one.</summary>
        [ForTest]
        public static float TailSwimSpeed => _tailTaken ? _tailSpeed : 0f;

        /// <summary>How fast what is shown is going toward: its swim speed while it swims, none while it attacks or does not swim.</summary>
        private static float SwimTarget() => SeaHold.InWater(_sea.Held, _seaShown, _tailLivesInWater) && !AttackPlaying() ? _tailSwimSpeed : 0f;

        /// <summary>Whether one of the creature's attacks plays on the stage, looked up once a clip starts and again until its clips are known.</summary>
        private static bool AttackPlaying()
        {
            var clip = _subject != null ? _subject.GetComponent<ClipPlayer>().OrNull()?.Clip : null;
            if (clip == _tailClip && _tailClipKnown) return _tailClipAttacks;
            _tailClip = clip;
            _tailClipKnown = clip == null || Previews.ClipsKnown(_tailPrefab, _subject);
            _tailClipAttacks = clip != null && _tailClipKnown && Previews.AttackOfClip(_tailPrefab, _subject, clip.name) != null;
            return _tailClipAttacks;
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
            _tailSwimSpeed = SwimSpeed(prefab);
            _tailAcceleration = SwimAcceleration(prefab);
            _tailLivesInWater = LivesInWater(prefab);
            _tailPrefab = prefab;
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float SwimSpeed(GameObject prefab) => prefab.GetComponent<Character>().OrNull()?.m_swimSpeed ?? 0f;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float SwimAcceleration(GameObject prefab) => prefab.GetComponent<Character>().OrNull()?.m_swimAcceleration ?? 0f;

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
        /// Each frame, after the animator and the framing: bends the tail the steps the frame
        /// spans (<see cref="TailBend.Steps"/>), then shows each joint part way between its last
        /// two steps, head first so each is set after its parent. The animator poses the joints
        /// anew every frame, so they are set even in a frame that takes no step.
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
                    joint.Position = joint.WasPosition = joint.Bone.position;
                    joint.Rotation = joint.WasRotation = joint.Bone.rotation;
                    joint.Length = Vector3.Distance(joint.Bone.parent.position, joint.Bone.position) / scale;
                }
                // Shown swimming, as the game's always does, at its full speed.
                _tailSpeed = SwimTarget();
                _tailTaken = true;
            }
            var steps = TailBend.Steps(ref _tailCarried, Time.unscaledDeltaTime, out var share);
            for (var step = 0; step < steps; step++) StepTail(scale);
            foreach (var joint in TailJoints)
                joint.Bone.SetPositionAndRotation(Vector3.Lerp(joint.WasPosition, joint.Position, share), Quaternion.Slerp(joint.WasRotation, joint.Rotation, share));
        }

        /// <summary>
        /// One step of the tail as the game bends it each frame (<c>Tail.CustomLateUpdate</c>),
        /// its joints' lengths and settings in metres taken at the stage's size, so a size changed
        /// while it is shown keeps the tail's shape; the sea's surface where the sea's ground lies
        /// and the ground under the model.
        /// </summary>
        private static void StepTail(float scale)
        {
            foreach (var joint in TailJoints)
            {
                joint.WasPosition = joint.Position;
                joint.WasRotation = joint.Rotation;
            }
            var rules = _tailRules;
            rules.Gravity *= scale;
            rules.GravityInWater *= scale;
            rules.Radius *= scale;
            var water = _seaLevels.Surface;
            var ground = Finite(_groundY) ? _groundY : float.NegativeInfinity;
            var dt = TailBend.Step;
            // The game bends the tail of a creature that always swims, its tail streaming out
            // behind; on the stage, where it stays put while told it swims, the water goes by it as fast.
            _tailSpeed = TailBend.Eased(_tailSpeed, SwimTarget(), _tailAcceleration, dt, Time.fixedDeltaTime);
            var swim = _tailSpeed * scale;
            var forward = V(_subject != null ? _subject.transform.forward : Vector3.forward);
            foreach (var joint in TailJoints)
            {
                if (swim > 0f) joint.Position = U(TailBend.Swum(V(joint.Position), forward, swim, dt));
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
