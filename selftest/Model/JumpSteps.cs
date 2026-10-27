namespace Scry
{
    /// <summary>
    /// How high a body thrown up rises as the game's physics moves it: in steps of a fixed time,
    /// each lowering its speed by gravity before it moves, so a little less than its speed and
    /// gravity alone tell.
    /// </summary>
    internal static class JumpSteps
    {
        /// <summary>The height risen, in metres, at so many metres a second up, under so much gravity, stepped every so many seconds; none for no speed up, no gravity or no step.</summary>
        public static float Apex(float speed, float gravity, float step)
        {
            if (!(speed > 0f) || !(gravity > 0f) || !(step > 0f)) return 0f;
            var risen = 0f;
            for (var up = speed - gravity * step; up > 0f; up -= gravity * step) risen += up * step;
            return risen;
        }
    }
}
