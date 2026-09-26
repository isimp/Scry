namespace Scry
{
    /// <summary>
    /// Tells when a foot comes down, from its height over time. The game steps by a value its
    /// walk animations carry for the animator, which a clip played on its own does not set, so
    /// a step is heard where the foot itself touches down: the lowest point of its stride, near
    /// the bottom of how high it goes, and not again within a fifth of a second. A foot that
    /// only sways, or dips high in the air, makes no step.
    /// </summary>
    public sealed class StepDetector
    {
        /// <summary>A stride lower than this, in metres, is swaying rather than walking.</summary>
        private const float SmallestStride = 0.02f;

        /// <summary>How far up its stride a foot may be and still count as down.</summary>
        private const float Low = 0.35f;

        private readonly float _interval;
        private int _count;
        private float _min, _max, _before, _last;
        private float _stepped = float.NegativeInfinity;

        public StepDetector(float interval = 0.2f)
        {
            _interval = interval;
        }

        /// <summary>Takes the foot's height now; true when it has just come down.</summary>
        public bool Feed(float time, float height)
        {
            if (_count == 0)
            {
                _min = _max = height;
            }
            else
            {
                if (height < _min) _min = height;
                if (height > _max) _max = height;
            }

            var range = _max - _min;
            var step = _count >= 2
                       && _last < _before && height >= _last
                       && range >= SmallestStride
                       && _last <= _min + Low * range
                       && time - _stepped >= _interval;
            if (step) _stepped = time;

            _before = _last;
            _last = height;
            _count++;
            return step;
        }
    }
}
