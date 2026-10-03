namespace Scry
{
    /// <summary>
    /// A Unity object that was destroyed, or a component that is not there, is not C#'s null:
    /// Unity's own check (<c>== null</c>, <c>!= null</c>) knows it, but <c>?.</c> and <c>??</c>
    /// skip that check and go on into the dead object. <see cref="OrNull"/> makes such an object
    /// a real null first, so <c>thing.OrNull()?.name</c> reads as Unity's own check would.
    /// </summary>
    internal static class UnityNull
    {
        /// <summary>The object, or null where Unity holds it to be none.</summary>
        public static T OrNull<T>(this T thing) where T : UnityEngine.Object => thing != null ? thing : null;
    }
}
