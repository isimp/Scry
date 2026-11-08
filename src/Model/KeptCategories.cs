using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What a kind kept on the map counts as, for its mark's shape and where the lists stand it.</summary>
    internal enum KeptCategory
    {
        Dungeons,
        Locations,
        Resources,
    }

    /// <summary>
    /// The categories kinds stand under on the map's kinds list and the On the map list, and the
    /// shape each gives its marks: a place's a circle, and what is picked, mined or tapped, all else
    /// that can stand on the map (<see cref="SeenOnMap.Findable"/>), a triangle. Colour tells the kinds
    /// of a category apart (<see cref="KeptMarks.Dealt"/>). A dungeon's marks keep their amber discs
    /// (<see cref="KeptMarks.Dungeons"/>). Which are folded is a number, a bit each.
    /// </summary>
    internal static class KeptCategories
    {
        /// <summary>Every category, in the order the lists stand them.</summary>
        public static readonly KeptCategory[] All = { KeptCategory.Dungeons, KeptCategory.Locations, KeptCategory.Resources };

        /// <summary>The first kind of a key, which may hold several kinds of one place.</summary>
        private static string FirstOf(string key)
        {
            var plus = key.IndexOf('+');
            return plus < 0 ? key : key.Substring(0, plus);
        }

        private static bool IsPlace(string key) => key != null && FirstOf(key).StartsWith(EntryKeys.Location, StringComparison.Ordinal);

        /// <summary>The shape a kept kind's mark is dealt in.</summary>
        public static MarkShape ShapeOf(string key) => IsPlace(key) ? MarkShape.Circle : MarkShape.Triangle;

        /// <summary>The shape a category's marks are dealt in, a dungeon's discs round.</summary>
        public static MarkShape ShapeIn(KeptCategory category) => category == KeptCategory.Resources ? MarkShape.Triangle : MarkShape.Circle;

        /// <summary>The category a kept line stands under: every kind of dungeon's, a dungeon's by its first kind, another place's, or what is found.</summary>
        public static KeptCategory Of(string key, Func<string, bool> isDungeon)
        {
            if (key == CompendiumBook.Dungeons) return KeptCategory.Dungeons;
            if (!IsPlace(key)) return KeptCategory.Resources;
            return isDungeon(FirstOf(key)) ? KeptCategory.Dungeons : KeptCategory.Locations;
        }

        /// <summary>Whether a category is folded in a number of those folded.</summary>
        public static bool Folded(int folded, KeptCategory category) => (folded & (1 << (int)category)) != 0;

        /// <summary>A number of those folded with a category folded or opened the other way.</summary>
        public static int Toggled(int folded, KeptCategory category) => folded ^ (1 << (int)category);
    }

    /// <summary>What a line of the On the map list is: a category's heading, a kind kept by its own line, or a type of dungeon kept with every kind.</summary>
    internal enum KeptLineLook
    {
        Heading,
        Kept,
        Type,
    }

    /// <summary>A line of the On the map list: its look, its category, the key it stands for; a heading how many lines stand under it open and how many of them it takes off.</summary>
    internal readonly struct KeptLine
    {
        public readonly KeptLineLook Look;
        public readonly KeptCategory Category;
        public readonly string Key;
        public readonly int Under;
        public readonly int Keeps;

        public KeptLine(KeptLineLook look, KeptCategory category, string key, int under = 0, int keeps = 0)
        {
            Look = look;
            Category = category;
            Key = key;
            Under = under;
            Keeps = keeps;
        }

        /// <summary>
        /// The list's lines: the dungeons' heading always, to take them off or keep them again, every
        /// other category's only with something kept in it; under each open heading its lines, the
        /// dungeons' types first while every kind is kept.
        /// </summary>
        public static List<KeptLine> Of(IReadOnlyList<string> kept, IReadOnlyList<string> dungeonTypes, Func<string, bool> isDungeon, int folded)
        {
            var lines = new List<KeptLine>();
            var every = kept.Contains(CompendiumBook.Dungeons);
            foreach (var category in KeptCategories.All)
            {
                var own = kept.Where(k => k != CompendiumBook.Dungeons && KeptCategories.Of(k, isDungeon) == category).ToList();
                var types = category == KeptCategory.Dungeons && every ? dungeonTypes : Array.Empty<string>();
                var keeps = own.Count + (category == KeptCategory.Dungeons && every ? 1 : 0);
                if (category != KeptCategory.Dungeons && own.Count == 0) continue;
                lines.Add(new KeptLine(KeptLineLook.Heading, category, category == KeptCategory.Dungeons ? CompendiumBook.Dungeons : null, types.Count + own.Count, keeps));
                if (KeptCategories.Folded(folded, category)) continue;
                foreach (var type in types) lines.Add(new KeptLine(KeptLineLook.Type, category, type));
                foreach (var line in own) lines.Add(new KeptLine(KeptLineLook.Kept, category, line));
            }
            return lines;
        }
    }
}
