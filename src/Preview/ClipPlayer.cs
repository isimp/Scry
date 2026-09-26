using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Scry
{
    /// <summary>
    /// Plays one of a copy's animation clips directly, around its animator's state machine.
    ///
    /// The animator's own switches (attacking, sleeping, swimming and so on) only move it between
    /// states under conditions the game sets together, and those conditions cannot be read at run
    /// time, so pulling one on its own often does nothing. A clip played here always shows. When
    /// it ends, or is stopped, the animator carries on as before.
    /// </summary>
    internal sealed class ClipPlayer : MonoBehaviour
    {
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private AnimationClip _clip;
        private AnimationEars _ears;
        private bool _loop;
        private bool _paused;
        private float _speed = 1f;

        /// <summary>The clip playing on this copy, or null.</summary>
        public AnimationClip Clip => _graph.IsValid() ? _clip : null;

        public static void Play(GameObject copy, AnimationClip clip, bool loop, float speed)
        {
            if (copy == null || clip == null) return;
            var animator = AnimatorOf(copy);
            if (animator == null) return;

            var player = copy.GetComponent<ClipPlayer>();
            if (player == null) player = copy.AddComponent<ClipPlayer>();
            player.Begin(animator, clip, loop, speed);
        }

        public static void Stop(GameObject copy)
        {
            var player = copy != null ? copy.GetComponent<ClipPlayer>() : null;
            if (player != null) player.End();
        }

        public static void SetSpeed(GameObject copy, float speed)
        {
            var player = copy != null ? copy.GetComponent<ClipPlayer>() : null;
            if (player == null) return;
            player._speed = speed;
            if (player._graph.IsValid() && !player._paused) player._playable.SetSpeed(speed);
        }

        /// <summary>Where the clip playing on a copy is, and how long it is; false when none plays.</summary>
        public static bool Position(GameObject copy, out float time, out float length)
        {
            time = length = 0f;
            var player = copy != null ? copy.GetComponent<ClipPlayer>() : null;
            if (player == null || !player._graph.IsValid() || player._clip == null) return false;
            length = Mathf.Max(0.05f, player._clip.length);
            time = Mathf.Clamp((float)player._playable.GetTime(), 0f, length);
            return true;
        }

        /// <summary>Moves the clip playing on a copy to a time; a paused clip shows that pose.</summary>
        public static void Seek(GameObject copy, float time)
        {
            var player = copy != null ? copy.GetComponent<ClipPlayer>() : null;
            if (player != null && player._graph.IsValid()) player._playable.SetTime(time);
        }

        /// <summary>Holds the clip playing on a copy on its pose, or lets it go on.</summary>
        public static void Pause(GameObject copy, bool pause)
        {
            var player = copy != null ? copy.GetComponent<ClipPlayer>() : null;
            if (player == null) return;
            player._paused = pause;
            if (player._graph.IsValid()) player._playable.SetSpeed(pause ? 0f : player._speed);
        }

        public static bool Paused(GameObject copy)
        {
            var player = copy != null ? copy.GetComponent<ClipPlayer>() : null;
            return player != null && player._paused && player._graph.IsValid();
        }

        public static void SetLoop(GameObject copy, bool loop)
        {
            var player = copy != null ? copy.GetComponent<ClipPlayer>() : null;
            if (player != null) player._loop = loop;
        }

        /// <summary>The animator that drives the copy: the first one with a controller.</summary>
        public static Animator AnimatorOf(GameObject copy)
        {
            if (copy == null) return null;
            foreach (var animator in copy.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController != null) return animator;
            }
            return null;
        }

        private void Begin(Animator animator, AnimationClip clip, bool loop, float speed)
        {
            End();
            _clip = clip;
            _loop = loop;
            _paused = false;
            _speed = speed;
            _playable = AnimationPlayableUtilities.PlayClip(animator, clip, out _graph);
            _playable.SetSpeed(speed);
            _ears = animator.GetComponent<AnimationEars>();
            if (_ears != null) _ears.ClipStarted(clip);
        }

        private void End()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _clip = null;
            if (_ears != null) _ears.ClipEnded();
        }

        private void Update()
        {
            if (!_graph.IsValid() || _clip == null || _paused) return;

            var length = Mathf.Max(0.05f, _clip.length);
            if (_playable.GetTime() < length) return;

            if (_loop) _playable.SetTime(0.0);
            else if (_playable.GetTime() >= length + 0.4f) End();
        }

        private void OnDestroy()
        {
            End();
        }
    }
}
