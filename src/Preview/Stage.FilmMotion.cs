using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A lighter film while the camera moves: turned, zoomed, dragged, gliding or its cut moved,
    /// a full stage whose last film took long whole (<see cref="StageFilm.Lighter"/>, from
    /// <see cref="Settings.LighterFilmFromMs"/>) is filmed into a picture of <see cref="StageFilm.MotionShare"/>
    /// its size with less smoothing of its edges (<see cref="MotionAntiAliasing"/>), drawn stretched
    /// to the stage's, as a big dungeon's film costs tens of milliseconds a frame while the camera
    /// moves and the eye follows the motion rather than the edges; once the camera holds still, the
    /// next frame films it whole again. A stage quick to film, a creature or an item, stays sharp
    /// as it turns. A fast stage, already filmed at half the size, stays as it is, and so does a
    /// still camera with something moving on the stage.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>How many samples a film in motion smooths its edges with, a still film's being 4.</summary>
        private const int MotionAntiAliasing = 2;

        private static RenderTexture _motionTexture;

        /// <summary>Whether the last film was one in motion, shown until the camera holds still and the stage is filmed whole.</summary>
        private static bool _lastInMotion;

        /// <summary>How long the last film of what is shown took, or would have taken whole where it was lighter (<see cref="StageFilm.AsWhole"/>), in milliseconds; nought until one is made, so what is newly shown is first filmed whole.</summary>
        public static double WholeFilmMs { get; private set; }

        /// <summary>Whether a film in motion is shown, so the panel draws that picture and the next still frame films whole.</summary>
        public static bool FilmedInMotion => _lastInMotion && _motionTexture != null;

        /// <summary>The picture this film goes into: the lighter one while the camera moves (<see cref="FilmInMotionNow"/>), else the stage's own.</summary>
        private static RenderTexture FilmTarget()
        {
            if (!FilmInMotionNow || _texture == null) return _texture;
            var width = Mathf.Max(32, Mathf.RoundToInt(_texture.width * StageFilm.MotionShare));
            var height = Mathf.Max(32, Mathf.RoundToInt(_texture.height * StageFilm.MotionShare));
            if (_motionTexture != null && (_motionTexture.width != width || _motionTexture.height != height)) ForgetMotionTexture();
            if (_motionTexture == null)
            {
                _motionTexture = Kept.Add(new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = MotionAntiAliasing,
                    name = "Scry stage in motion",
                });
                _motionTexture.Create();
            }
            return _motionTexture;
        }

        /// <summary>A film made into a picture: shown from now on.</summary>
        private static void FilmedInto(RenderTexture target)
        {
            _lastInMotion = target != null && target == _motionTexture;
            WholeFilmMs = StageFilm.AsWhole(LastFilmMs, _lastInMotion);
        }

        /// <summary>Forgets how long a film took, as something else is shown: its first film is whole.</summary>
        private static void ForgetFilmCost() => WholeFilmMs = 0;

        /// <summary>Lets go of the picture filmed in motion, as the stage goes or its size changes.</summary>
        private static void ForgetMotionTexture()
        {
            if (_motionTexture == null) return;
            if (_camera != null && _camera.targetTexture == _motionTexture) _camera.targetTexture = null;
            _motionTexture.Release();
            Object.Destroy(_motionTexture);
            _motionTexture = null;
            _lastInMotion = false;
        }
    }
}
