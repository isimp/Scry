namespace Scry
{
    /// <summary>
    /// A skill as its entry holds it: its number and name in the game's list (<c>Skills.SkillType</c>,
    /// a mod's skill only a number), the game's description of it, and how much each rise adds
    /// before what raises it (<c>Skills.SkillDef.m_increseStep</c>).
    /// </summary>
    internal sealed class SkillSource
    {
        public int Type;
        public string Name = "";
        public string Description = "";
        public float Step = 1f;

        /// <summary>One of the game's own list, whose rules Scry knows; a mod's is only a number there.</summary>
        public bool OfTheGame;
    }
}
