using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Givers of a thing going by one name, given it one way: a chip in the row of that way
    /// (<see cref="SourceGroups"/>), saying how many there are and the range of what they give.
    /// </summary>
    internal sealed class SourceGroup
    {
        /// <summary>The way's words leading its row: "Mined from", "Dropped by".</summary>
        public string Lead;

        /// <summary>The name the givers go by, without the biome or the variant's number some of them tell.</summary>
        public string Name;

        /// <summary>The givers, in the order they were given.</summary>
        public readonly List<SourceFacts> Givers = new List<SourceFacts>();

        /// <summary>How many each gives, the range across them; none where the way tells no amount or the givers count it unalike.</summary>
        public string Amount;

        /// <summary>The chip's name, saying how many givers it stands for: "Rock ×3"; its name alone for one.</summary>
        public string Chip => Naming.Repeated(Name, Givers.Count);

        /// <summary>Each giver's own line with its prefab, as the hover tells it: the first few, then how many more.</summary>
        public string Tip(Func<SourceFacts, string> line)
        {
            var shown = Givers.Take(SourceGroups.TipLines).Select(g => line(g) + " · " + g.Giver).ToList();
            if (Givers.Count > SourceGroups.TipLines) shown.Add($"and {Numbers.Count(Givers.Count - SourceGroups.TipLines)} more");
            return string.Join("\n", shown.ToArray());
        }
    }

    /// <summary>
    /// Where a thing comes from, as rows of chips: a row for each way of giving it that tells an
    /// amount or nothing (dropped, picked, grown, out of a drop table), its givers merged into
    /// one chip where they go by one name but for their biome or a variant's number, as many
    /// rocks do (<see cref="Naming.WithoutVariant"/>), each told by its own name on hover. A way with a
    /// pace, a price or a birth keeps its own line, which tells more than a chip could.
    /// </summary>
    internal static class SourceGroups
    {
        /// <summary>How many givers' lines a chip's hover tells before saying how many more.</summary>
        public const int TipLines = 8;

        /// <summary>Whether a way of giving a thing goes into a row of chips rather than a line of its own.</summary>
        public static bool Groups(SourceFacts source) =>
            source != null && (source.Way == SourceWay.Dropped || source.Way == SourceWay.Picked || source.Way == SourceWay.GrowsFrom || source.Way == SourceWay.Table);

        /// <summary>The chips of the ways that group (<see cref="Groups"/>), each way's in the order its first giver came, each chip where its first giver did.</summary>
        public static List<SourceGroup> Of(IEnumerable<SourceFacts> sources)
        {
            var groups = new List<SourceGroup>();
            foreach (var source in sources)
            {
                if (!Groups(source)) continue;
                var lead = Lead(source);
                var name = Naming.WithoutVariant(Naming.WithoutBiome(source.Shown));
                var group = groups.FirstOrDefault(g => g.Lead == lead && g.Name == name);
                if (group == null)
                {
                    group = new SourceGroup { Lead = lead, Name = name };
                    groups.Add(group);
                }
                group.Givers.Add(source);
            }
            foreach (var group in groups) group.Amount = Amount(group.Givers);
            // A way's chips stand together, the ways in the order their first giver came.
            var leads = groups.Select(g => g.Lead).Distinct().ToList();
            return groups.OrderBy(g => leads.IndexOf(g.Lead)).ToList();
        }

        /// <summary>The words leading a way's row, as its line begins (<see cref="SourceWords.Line"/>).</summary>
        public static string Lead(SourceFacts source)
        {
            switch (source.Way)
            {
                case SourceWay.Dropped: return "Dropped by";
                case SourceWay.Picked: return "Picked from";
                case SourceWay.GrowsFrom: return "Grows from";
                default: return SourceWords.Verb(source.Table);
            }
        }

        /// <summary>How many one giver gives, as its line counts it; none for a way that tells no amount, or one for each player.</summary>
        private static (int Least, int Most)? Counted(SourceFacts source)
        {
            switch (source.Way)
            {
                // CharacterDrop rolls Random.Range(min, max), which never gives the top value (DropWords.CreatureAmount).
                case SourceWay.Dropped: return source.OnePerPlayer ? ((int, int)?)null : (source.Min, Math.Max(source.Min, source.Max - 1));
                case SourceWay.Table: return (source.Drop.StackMin, source.Drop.StackMax);
                default: return null;
            }
        }

        /// <summary>The range of what the givers give; none where any of them tells no number.</summary>
        private static string Amount(List<SourceFacts> givers)
        {
            var least = int.MaxValue;
            var most = int.MinValue;
            foreach (var giver in givers)
            {
                var counted = Counted(giver);
                if (counted == null) return null;
                least = Math.Min(least, counted.Value.Least);
                most = Math.Max(most, counted.Value.Most);
            }
            return Numbers.CountRange(least, most);
        }
    }
}
