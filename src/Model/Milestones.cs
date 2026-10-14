using System.Collections.Generic;

namespace Scry
{
    /// <summary>A boss as a character's records know it: its name as kills are counted, the key its fall sets, its trophy's prefab.</summary>
    internal readonly struct BossMarks
    {
        public BossMarks(string name, string key, string trophy)
        {
            Name = name;
            Key = key;
            Trophy = trophy;
        }

        public string Name { get; }

        public string Key { get; }

        public string Trophy { get; }
    }

    /// <summary>
    /// Which tiers a character has beaten, for the compendium's milestone floor: a boss fell for it
    /// when its kill is in the character's statistics (credited to everyone who hurt it), its
    /// defeat key among the character's uniques (where its death reached that game), or its trophy
    /// is held; the three together cover characters from before the game kept statistics.
    /// </summary>
    internal static class Milestones
    {
        /// <summary>Whether the records show the boss fell for the character.</summary>
        public static bool Beaten(CompendiumRecords records, BossMarks boss) =>
            Has(records.Killed, boss.Name) || Has(records.Uniques, boss.Key) || Has(records.Trophies, boss.Trophy);

        /// <summary>How far the floor may be raised: the tiers whose bosses fell, in a row from the first.</summary>
        public static int Ceiling(CompendiumRecords records, IReadOnlyList<BossMarks> bosses)
        {
            var tier = 0;
            while (tier < bosses.Count && Beaten(records, bosses[tier])) tier++;
            return tier;
        }

        private static bool Has(HashSet<string> set, string name) => !string.IsNullOrEmpty(name) && set.Contains(name);
    }
}
