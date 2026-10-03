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
    ///
    /// The clip's events are sent from here as its time passes them, to the copy's
    /// <see cref="AnimationEars"/>, rather than left to the graph, and the animator's own events
    /// are off meanwhile, so each is heard once and outside Unity's callbacks.
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
        private Animator _animator;
        private float _heardTo;

        /// <summary>The clip playing on this copy, or null.</summary>
        public AnimationClip Clip => _graph.IsValid() ? _clip : null;

        /// <summary>
        /// The playing clip's events, by time, read once as it begins: Unity makes the clip's
        /// events anew each time they are asked for, which on every frame of every copy playing
        /// a clip made garbage for nothing.
        /// </summary>
        public AnimationEvent[] Events { get; private set; } = new AnimationEvent[0];

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
            if (player == null || !player._graph.IsValid()) return;
            player._playable.SetTime(time);
            player._heardTo = time;
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

        /// <summary>
        /// The animator that drives the copy, as the game finds a character's: the first one with a
        /// controller on a part that is switched on. Some prefabs keep an old model switched off
        /// beside the one in use (a frost troll's Visual_OLD), with an animator of its own; failing
        /// a switched-on one, the first there is.
        /// </summary>
        public static Animator AnimatorOf(GameObject copy)
        {
            if (copy == null) return null;

            // Asked many times a frame (the panel, for every clip), so the answer is kept for the frame.
            if (_ofFrame == Time.frameCount && ReferenceEquals(_ofCopy, copy) && (_of == null || _of != null && _of.runtimeAnimatorController != null)) return _of;
            Animator found = null, any = null;
            foreach (var animator in copy.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null) continue;
                if (On(animator.transform, copy.transform))
                {
                    found = animator;
                    break;
                }
                if (any == null) any = animator;
            }
            _ofFrame = Time.frameCount;
            _ofCopy = copy;
            _of = found ?? any;
            return _of;
        }

        private static int _ofFrame = -1;
        private static GameObject _ofCopy;
        private static Animator _of;

        /// <summary>Whether a part and every part above it, up to the copy, is switched on.</summary>
        private static bool On(Transform part, Transform root)
        {
            for (var t = part; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == root) return true;
            }
            return true;
        }

        private void Begin(Animator animator, AnimationClip clip, bool loop, float speed)
        {
            End();
            _clip = clip;
            _loop = loop;
            _paused = false;
            _speed = speed;
            _animator = animator;
            _heardTo = -0.001f;
            _playable = AnimationPlayableUtilities.PlayClip(animator, clip, out _graph);
            _playable.SetSpeed(speed);
            _ears = animator.GetComponent<AnimationEars>();
            animator.fireEvents = false;
            // Kept in order of time, events at the same time in the order the clip has them.
            var events = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(clip.events, e => e.time));
            Events = events;

            var heard = Plugin.LogPreviews ? Heard(clip) : null;
            Listen.Start(heard, clip.length / Mathf.Max(0.1f, speed) + 0.5f);
            if (heard != null) Listen.Note(heard, $"{Numbers.Count(events.Length)} events in the clip");
            if (_ears == null) Listen.Note(heard, "no ears on this copy: its clips send events Scry does not answer, so none are played");
            if (_ears != null) _ears.ClipStarted(clip);
        }

        private void End()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _clip = null;
            Events = new AnimationEvent[0];
            if (_animator != null) _animator.fireEvents = _ears != null;
            if (_ears != null) _ears.ClipEnded();
        }

        private void Update()
        {
            if (!_graph.IsValid() || _clip == null || _paused) return;

            var length = Mathf.Max(0.05f, _clip.length);
            var time = Mathf.Min((float)_playable.GetTime(), length);
            if (time > _heardTo)
            {
                // Moved on first, so events that fail to be answered are not sent again next frame.
                var after = _heardTo;
                _heardTo = time;
                Fire(after, time);
                if (!_graph.IsValid() || _clip == null) return;
            }
            if (_playable.GetTime() < length) return;

            if (_loop)
            {
                _playable.SetTime(0.0);
                _heardTo = -0.001f;
                if (_ears != null) _ears.ClipRepeated(_clip);
            }
            else if (_playable.GetTime() >= length + 0.4f) End();
        }

        /// <summary>How a clip is named in the log: the prefab it is on and its name.</summary>
        public string Heard(AnimationClip clip) => clip == null ? null : $"{name}'s clip {clip.name}";

        /// <summary>Sends the events the clip passed since it was last looked at.</summary>
        private void Fire(float after, float upTo)
        {
            if (_ears == null) return;
            foreach (var e in Events)
            {
                if (e.time <= after) continue;
                if (e.time > upTo) break;
                try
                {
                    _ears.Answer(e);
                }
                catch (System.Exception ex)
                {
                    Faults.Tell($"answering the event {e.functionName} of the clip {_clip.name}", ex);
                }
                if (_ears == null || _clip == null) return;
            }
        }

        private void OnDestroy()
        {
            End();
        }
    }
}
