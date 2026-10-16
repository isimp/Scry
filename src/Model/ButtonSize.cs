namespace Scry
{
    /// <summary>
    /// How wide a button drawn in two looks is: as wide as its label is in the wider look, and
    /// padded, so a label drawn bold while it is on is never clipped and the button keeps its
    /// width as it changes look.
    /// </summary>
    internal static class ButtonSize
    {
        /// <summary>A button's width from its label's width plain and on, and its padding.</summary>
        public static float Fitting(float plain, float on, float pad) => (plain > on ? plain : on) + pad;
    }
}
