namespace Scry
{
    /// <summary>The compendium: showing only what a character has come to know.</summary>
    internal sealed partial class Feature
    {
        public static readonly Feature CompendiumCatchUp = Listed("catching up with what a character knew before the compendium");
        public static readonly Feature CompendiumFought = Listed("knowing a creature once fought, for the compendium");
        public static readonly Feature CompendiumHit = Listed("knowing a resource once hit, for the compendium");
        public static readonly Feature CompendiumSeen = Listed("knowing what a character sees, for the compendium");
        public static readonly Feature CompendiumShown = Listed("showing only what a character knows");
        public static readonly Feature CompendiumMap = Listed("catching up from the explored map, for the compendium");
        public static readonly Feature CompendiumTogether = Listed("noting what the character sees go together, for the compendium's strict rules");
        public static readonly Feature CompendiumTiers = Listed("working out what each boss's fall opens, for the compendium's floor");
    }
}
