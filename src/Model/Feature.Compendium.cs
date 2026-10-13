namespace Scry
{
    /// <summary>The compendium: showing only what a character has come to know.</summary>
    internal sealed partial class Feature
    {
        public static readonly Feature CompendiumCatchUp = Listed("catching up with what a character knew before the compendium");
        public static readonly Feature CompendiumFought = Listed("knowing a creature once fought, for the compendium");
    }
}
