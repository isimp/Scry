using UnityEngine;

namespace Scry
{
    /// <summary>
    /// When the stage is filmed: only while something on it moves or changed, as a film is Unity's
    /// own cost in time and memory each time. The view compared with the last film's (yaw, pitch,
    /// zoom, the cut, the camera and the size), what was touched since (<see cref="Touch"/>: a
    /// copy made, a room added or put away, modifiers applied), what is alive on it (a creature's
    /// animator, a particle system playing, a flickering light, an effect played, a drag or a
    /// glide), and a floor of a film every few frames for anything that changed unnoticed.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>How many frames at most pass between films while nothing is seen to change.</summary>
        public const int FilmFloorFrames = 6;

        /// <summary>How many frames pass between looks for what is alive on the stage, as the look walks every part.</summary>
        private const int LiveLookFrames = 15;

        private static bool _touched = true;
        private static int _lastFilmFrame = -1000;
        private static int _liveLookedFrame = -1000;
        private static bool _live;
        private static float _filmedYaw, _filmedPitch, _filmedZoom, _filmedCut;
        private static Vector3 _filmedAt;
        private static Quaternion _filmedTurn;
        private static int _filmedWidth, _filmedHeight;
        private static Animator[] _animators;
        private static ParticleSystem[] _particles;
        private static LightFlicker[] _flickers;

        /// <summary>How many films were not made because nothing on the stage moved or changed, for the self-test.</summary>
        [Diagnostic]
        public static int FilmsSkipped { get; private set; }

        /// <summary>What the stage keeps before a film, each step timed on its own so a slow film says which step it was: the copy settled, the ground, the framing with its outlines and the player's mark, the creatures to the cut. Run before the gate, so what they touch is filmed this frame.</summary>
        private static void KeepBeforeFilm()
        {
            var step = Timing.Start();
            EnsureTexture();
            Settle();
            Timing.Add("render settle", step);
            step = Timing.Start();
            if (!Guard.Run(Feature.StageGround, GroundPart, KeepGround)) PutGroundAway();
            Timing.Add("render ground", step);
            step = Timing.Start();
            Frame();
            KeepOutline();
            KeepLitOutline();
            KeepGhosts();
            KeepYouMark();
            Timing.Add("render frame", step);
            step = Timing.Start();
            KeepCreaturesToCut();
            Timing.Add("render creatures", step);
        }

        /// <summary>Something on the stage changed: the next frame films it, and what is alive on it is looked for again.</summary>
        public static void Touch()
        {
            _touched = true;
            _liveLookedFrame = -1000;
        }

        /// <summary>Whether to film this frame; a frame not filmed keeps the last film on the panel.</summary>
        private static bool FilmDue()
        {
            var at = _camera.transform.position;
            var turn = _camera.transform.rotation;
            var cut = TheCut.At;
            var moved = Yaw != _filmedYaw || Pitch != _filmedPitch || Zoom != _filmedZoom || cut != _filmedCut || at != _filmedAt || turn != _filmedTurn
                || _width != _filmedWidth || _height != _filmedHeight;
            var due = _touched || moved || Dragging || Gliding || Played.Count > 0 || Time.frameCount - _lastFilmFrame >= FilmFloorFrames || Live();
            if (!due)
            {
                FilmsSkipped++;
                return false;
            }
            _touched = false;
            _filmedYaw = Yaw;
            _filmedPitch = Pitch;
            _filmedZoom = Zoom;
            _filmedCut = cut;
            _filmedAt = at;
            _filmedTurn = turn;
            _filmedWidth = _width;
            _filmedHeight = _height;
            _lastFilmFrame = Time.frameCount;
            return true;
        }

        /// <summary>Whether anything on the stage moves by itself: an animator running, a particle system playing or a light flickering; looked for now and then, and again after a touch.</summary>
        private static bool Live()
        {
            if (_root == null) return false;
            if (Time.frameCount - _liveLookedFrame >= LiveLookFrames)
            {
                _liveLookedFrame = Time.frameCount;
                _animators = _root.GetComponentsInChildren<Animator>(false);
                _particles = _root.GetComponentsInChildren<ParticleSystem>(false);
                _flickers = _root.GetComponentsInChildren<LightFlicker>(false);
            }
            _live = false;
            foreach (var animator in _animators)
            {
                if (animator != null && animator.enabled && animator.runtimeAnimatorController != null && animator.speed > 0f) return _live = true;
            }
            foreach (var particles in _particles)
            {
                if (particles != null && particles.isPlaying) return _live = true;
            }
            foreach (var flicker in _flickers)
            {
                if (flicker != null && flicker.enabled) return _live = true;
            }
            return false;
        }

        /// <summary>Whether the stage was found alive at the last look, for the self-test.</summary>
        [ForTest]
        public static bool LiveForTest => _live;
    }
}
