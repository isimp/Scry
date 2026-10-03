using System;

namespace Scry
{
    /// <summary>What a frame's watch of a point sought comes to (<see cref="SeekWatch.Step"/>).</summary>
    internal enum SeekStep
    {
        /// <summary>Nothing is watched.</summary>
        None,

        /// <summary>The watch ran out of time and ends.</summary>
        Expired,

        /// <summary>The sound is not playing yet, is near the point, or was set again too lately; the watch goes on.</summary>
        Waiting,

        /// <summary>The sound is before the point: it is to be set again.</summary>
        SetAgain,

        /// <summary>The sound has played on past the point: it held, and the watch ends.</summary>
        Held,
    }

    /// <summary>
    /// A point sought in a sound preview, kept while the sound is paused and played from on
    /// going on. A streamed clip (music) reads the point back at once yet can start from its
    /// beginning, so after going on the point is watched until the sound has played on past it,
    /// and set again, a tenth of a second apart at the most, while it is before it; two seconds
    /// at most.
    /// </summary>
    internal sealed class SeekWatch
    {
        /// <summary>How long a streamed clip is given to get to a point set before it is set again.</summary>
        public const float SeekAgainAfter = 0.1f;

        /// <summary>How far past the point the sound must have played for the point to have held.</summary>
        public const float HeldPast = 0.3f;

        /// <summary>How long a point is watched at the most after going on.</summary>
        public const float WatchFor = 2f;

        /// <summary>How near before the point the sound may be without setting it again.</summary>
        private const float Near = 0.1f;

        private float _until;
        private float _againAt;

        /// <summary>The point sought while paused, to play from on going on; null for none.</summary>
        public float? WhilePaused { get; private set; }

        /// <summary>The point watched after going on; null for none.</summary>
        public float? Watching { get; private set; }

        /// <summary>A point within a clip, short of its very end, which a clip cannot be played from.</summary>
        public static float Clamp(float time, float length) => Math.Max(0f, Math.Min(time, length - 0.05f));

        /// <summary>A point sought: kept while paused, else let go, as the sound plays from it at once.</summary>
        public void Sought(float at, bool paused) => WhilePaused = paused ? at : (float?)null;

        /// <summary>Going on after a pause: the point to play from, which is watched from now on; null when none was sought.</summary>
        public float? GoOn(float now)
        {
            if (WhilePaused == null) return null;
            var at = WhilePaused.Value;
            Watching = at;
            _until = now + WatchFor;
            _againAt = now + SeekAgainAfter;
            WhilePaused = null;
            return at;
        }

        /// <summary>A frame's watch, from where the sound is, whether it plays, and the time now.</summary>
        public SeekStep Step(float time, bool playing, float now)
        {
            if (Watching == null) return SeekStep.None;
            if (now > _until)
            {
                Watching = null;
                return SeekStep.Expired;
            }
            if (!playing) return SeekStep.Waiting;
            if (time >= Watching.Value + HeldPast)
            {
                Watching = null;
                return SeekStep.Held;
            }
            if (time + Near >= Watching.Value || now < _againAt) return SeekStep.Waiting;
            _againAt = now + SeekAgainAfter;
            return SeekStep.SetAgain;
        }

        /// <summary>Ends the watch, the point sought while paused kept.</summary>
        public void StopWatching() => Watching = null;

        /// <summary>Lets go of both, as the sound stops or another plays.</summary>
        public void Forget()
        {
            WhilePaused = null;
            Watching = null;
        }
    }
}
