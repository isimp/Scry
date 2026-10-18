using System;

namespace Scry
{
    /// <summary>
    /// The wheel's turns that move a dungeon's cut told as whole steps, a level or a room's mark,
    /// one a notch however the wheel tells its turns: a mouse's notch as a turn of three or of one,
    /// a touchpad's as many small ones. Turns add up until they are <see cref="Notch"/>, which is
    /// one step, never more for one turn; a turn the other way starts afresh.
    /// </summary>
    internal sealed class WheelNotches
    {
        /// <summary>How far the wheel turns for a step: a mouse's least notch.</summary>
        public const float Notch = 1f;

        private float _turned;

        /// <summary>The step a turn makes: 1 down, -1 up, 0 for none yet.</summary>
        public int Take(float turn)
        {
            if (turn == 0f || float.IsNaN(turn)) return 0;
            if (Math.Sign(turn) != Math.Sign(_turned)) _turned = 0f;
            _turned += turn;
            if (Math.Abs(_turned) < Notch) return 0;
            var step = Math.Sign(_turned);
            _turned = 0f;
            return step;
        }
    }
}
