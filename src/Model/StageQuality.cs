namespace Scry
{
    /// <summary>How the stage is filmed: whole, or faster for a big dungeon on a weaker machine.</summary>
    internal enum StageQuality
    {
        /// <summary>At the panel's own size, with grass, the lights below a dungeon's cut as set.</summary>
        Full,

        /// <summary>At half the size, without grass or the lights below a dungeon's cut.</summary>
        Fast,
    }
}
