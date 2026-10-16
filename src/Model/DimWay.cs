namespace Scry
{
    /// <summary>
    /// A way of drawing a room darker on the stage: its materials' tint and glow darkened, every
    /// colour its shaders take darkened, only the stage's lights on it weaker, or one plain dark
    /// material in place of its own.
    /// </summary>
    internal enum DimWay
    {
        Tint,
        Colours,
        Light,
        Plain,
    }
}
