using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Scry
{
    /// <summary>
    /// The rules the world generator places a location by (<c>ZoneSystem.ZoneLocation</c>), as
    /// plain values, so they can be told without the game.
    /// </summary>
    public sealed class LocationRules
    {
        /// <summary>How many the generator tries to place in a world.</summary>
        public int Quantity;

        /// <summary>Its biomes, already in the names the game shows, and which part of them ("Edge", "Median", "Everything").</summary>
        public string Biomes = "";
        public string BiomeArea = "Everything";

        /// <summary>How far from the world's centre, by either of the two rules that say so; none set is no limit.</summary>
        public float MinDistance, MaxDistance, MinDistanceFromCenter, MaxDistanceFromCenter;

        /// <summary>Its ground's height above the sea; the defaults, -1000 and 1000, are no limit.</summary>
        public float MinAltitude = -1000f, MaxAltitude = 1000f;

        /// <summary>How far from another of its kind or group it must be, and how near one of another group.</summary>
        public float MinSimilar, MaxSimilar;
        public string Group = "", GroupMax = "";

        /// <summary>How much the ground may rise within its outer radius, from and to.</summary>
        public float MinTerrainDelta, MaxTerrainDelta = 2f, ExteriorRadius;

        public bool Prioritized, CenterFirst, Unique;
    }

    /// <summary>
    /// A location's placement rules in words, as <c>ZoneSystem.GenerateLocationsTimeSliced</c>
    /// applies them to every point it tries, and the names and groups its entries and rooms go by.
    /// </summary>
    public static class LocationWords
    {
        private static string Metres(float value) => value.ToString("#,0.##", CultureInfo.InvariantCulture) + " m";

        private static string Range(float min, float max) => $"{min.ToString("#,0.##", CultureInfo.InvariantCulture)}–{Metres(max)}";

        /// <summary>Each rule that is not at its default, a row each.</summary>
        public static List<KeyValuePair<string, string>> Rows(LocationRules rules)
        {
            var rows = new List<KeyValuePair<string, string>>();
            void Add(string label, string value)
            {
                if (!string.IsNullOrEmpty(value)) rows.Add(new KeyValuePair<string, string>(label, value));
            }

            Add("Per world", "up to " + rules.Quantity.ToString("#,0", CultureInfo.InvariantCulture));
            Add("Biome", Biome(rules));
            Add("From the centre", FromCentre(rules));
            Add("Above the sea", Altitude(rules));
            Add("Apart", Apart(rules));
            Add("Near", Near(rules));
            Add("Ground", Ground(rules));
            Add("Placed", Placed(rules));
            return rows;
        }

        /// <summary>The same rules as one line, for a prefab placed by several rule sets.</summary>
        public static string Line(LocationRules rules)
        {
            var parts = new List<string> { "up to " + rules.Quantity.ToString("#,0", CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(rules.Biomes) ? "" : " in " + Biome(rules)) };
            var centre = FromCentre(rules);
            if (centre != null) parts.Add(centre + " from the centre");
            var altitude = Altitude(rules);
            if (altitude != null) parts.Add(altitude + " above the sea");
            foreach (var part in new[] { Apart(rules), Near(rules), Ground(rules), Placed(rules) }) if (part != null) parts.Add(part);
            return string.Join(", ", parts);
        }

        private static string Biome(LocationRules rules)
        {
            if (string.IsNullOrEmpty(rules.Biomes)) return null;
            switch (rules.BiomeArea)
            {
                case "Median": return rules.Biomes + ", away from its edges";
                case "Edge": return rules.Biomes + ", only at its edges";
                default: return rules.Biomes;
            }
        }

        /// <summary>The tighter of the two distance rules on each side; the generator turns a point away by either.</summary>
        private static string FromCentre(LocationRules rules)
        {
            var min = Math.Max(rules.MinDistance, rules.MinDistanceFromCenter);
            var max = Tighter(rules.MaxDistance, rules.MaxDistanceFromCenter);
            if (min > 0f && max > 0f) return Range(min, max);
            if (max > 0f) return "within " + Metres(max);
            if (min > 0f) return "beyond " + Metres(min);
            return null;
        }

        private static float Tighter(float a, float b) => a > 0f && b > 0f ? Math.Min(a, b) : Math.Max(a, b);

        private static string Altitude(LocationRules rules)
        {
            var low = rules.MinAltitude > -1000f;
            var high = rules.MaxAltitude < 1000f;
            if (low && high) return Range(rules.MinAltitude, rules.MaxAltitude);
            if (low) return "at least " + Metres(rules.MinAltitude);
            if (high) return "at most " + Metres(rules.MaxAltitude);
            return null;
        }

        private static string Apart(LocationRules rules)
        {
            if (rules.MinSimilar <= 0f) return null;
            var other = string.IsNullOrEmpty(rules.Group) ? "another of its kind" : $"another of the group \"{rules.Group}\"";
            return $"at least {Metres(rules.MinSimilar)} from {other}";
        }

        private static string Near(LocationRules rules)
        {
            if (rules.MaxSimilar <= 0f) return null;
            var other = string.IsNullOrEmpty(rules.GroupMax) ? "another of its kind" : $"one of the group \"{rules.GroupMax}\"";
            return $"within {Metres(rules.MaxSimilar)} of {other}";
        }

        /// <summary>How much the ground may rise within its outer radius (<c>WorldGenerator.GetTerrainDelta</c>); 1000 and more is no limit.</summary>
        private static string Ground(LocationRules rules)
        {
            var within = $" within {Metres(rules.ExteriorRadius)} of it";
            var low = rules.MinTerrainDelta > 0f;
            var high = rules.MaxTerrainDelta < 1000f;
            if (low && high) return "rising " + Range(rules.MinTerrainDelta, rules.MaxTerrainDelta) + within;
            if (low) return "rising at least " + Metres(rules.MinTerrainDelta) + within;
            if (high) return "rising at most " + Metres(rules.MaxTerrainDelta) + within;
            return null;
        }

        /// <summary>
        /// How it is placed: a prioritized one goes before the others with 60,000 tries to their
        /// 12,000; one placed centre first searches outward from the world's centre; of a unique
        /// one only the first a player reaches stays, the rest are taken off the world's list.
        /// </summary>
        private static string Placed(LocationRules rules)
        {
            var parts = new List<string>();
            if (rules.Prioritized) parts.Add("before the others with five times the tries");
            if (rules.CenterFirst) parts.Add("outward from the world's centre");
            if (rules.Unique) parts.Add("one only, the first reached keeps its place");
            return parts.Count > 0 ? string.Join("; ", parts) : null;
        }

        /// <summary>
        /// A room's name from its prefab's, without the tag of its dungeon ("forestcrypt_") that
        /// its group already says, in words: "Corridor2" as "Corridor 2".
        /// </summary>
        public static string RoomName(string prefab)
        {
            var name = prefab ?? "";
            var tag = name.IndexOf('_');
            if (tag >= 0 && tag < name.Length - 1) name = name.Substring(tag + 1);

            var words = new StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (c == '_' || c == ' ')
                {
                    if (words.Length > 0 && words[words.Length - 1] != ' ') words.Append(' ');
                    continue;
                }
                var previous = i > 0 ? name[i - 1] : ' ';
                var breaks = words.Length > 0 && words[words.Length - 1] != ' '
                             && ((char.IsUpper(c) && char.IsLower(previous)) || (char.IsDigit(c) && char.IsLetter(previous)) || (char.IsLetter(c) && char.IsDigit(previous)));
                if (breaks) words.Append(' ');
                words.Append(words.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            }
            return words.ToString();
        }

        /// <summary>The game's biomes in the order players meet them; a biome a mod adds comes after.</summary>
        private static readonly string[] BiomeOrder = { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean" };

        /// <summary>
        /// The group a location is listed under: its biome, in the order players meet them, or
        /// "In several biomes" after all of them. Named by the biome's shown name when given.
        /// </summary>
        public static Group Group(string[] biomes, Func<string, string> shown = null)
        {
            if (biomes == null || biomes.Length == 0) return new Group("In no biome", 90);
            if (biomes.Length > 1) return new Group("In several biomes", 100);
            var at = Array.IndexOf(BiomeOrder, biomes[0]);
            var name = shown?.Invoke(biomes[0]) ?? Naming.FieldLabel(biomes[0]);
            return new Group(name, at >= 0 ? at : 50);
        }

        /// <summary>A dungeon's rooms, listed after every place, under the dungeon's name.</summary>
        public static Group RoomGroup(string dungeon) => new Group(dungeon + " rooms", 200);
    }
}
