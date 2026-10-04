using System;
using System.Collections.Generic;
using System.Text;

namespace Scry
{
    /// <summary>
    /// The rules the world generator places a location by (<c>ZoneSystem.ZoneLocation</c>), as
    /// plain values, so they can be told without the game.
    /// </summary>
    internal sealed class LocationRules
    {
        /// <summary>How many the generator tries to place in a world.</summary>
        public int Quantity;

        /// <summary>Its biomes, already in the names the game shows, and which part of them ("Edge", "Median", "Everything").</summary>
        public string Biomes = "";
        public string BiomeArea = "Everything";

        /// <summary>How far from the world's centre, by either of the two rules that say so; none set is no limit.</summary>
        public float MinDistance, MaxDistance, MinDistanceFromCenter, MaxDistanceFromCenter;

        /// <summary>Its ground's height above the sea; the defaults, -1,000 and 1,000, are no limit.</summary>
        public float MinAltitude = -1000f, MaxAltitude = 1000f;

        /// <summary>How far from another of its kind or group it must be, and how near one of another group.</summary>
        public float MinSimilar, MaxSimilar;
        public string Group = "", GroupMax = "";

        /// <summary>How much the ground may rise within its outer radius, from and to.</summary>
        public float MinTerrainDelta, MaxTerrainDelta = 2f, ExteriorRadius;

        public bool Prioritized, CenterFirst, Unique;

        /// <summary>Its biomes by the game's names for them ("AshLands"), which say what its ground value is.</summary>
        public string[] BiomeKeys = Array.Empty<string>();

        /// <summary>Whether it keeps to a range of the forest factor (<c>m_inForest</c>), and the range.</summary>
        public bool InForest;
        public float ForestMin, ForestMax = 1f;

        /// <summary>The ground value it asks for, over the first and under the second; 0 and 1 are no limit.</summary>
        public float MinVegetation, MaxVegetation = 1f;

        /// <summary>Whether it keeps to spots whose surroundings have more of the ground value than most tried, how far round it looks, and how much more.</summary>
        public bool SurroundCheck;
        public float SurroundDistance = 20f, SurroundBetter;
    }

    /// <summary>
    /// A location's placement rules in words, as <c>ZoneSystem.GenerateLocationsTimeSliced</c>
    /// applies them to every point it tries, and the names and groups its entries and rooms go by.
    /// </summary>
    internal static class LocationWords
    {
        private static string Range(float min, float max) => $"{Numbers.Amount(min)}–{Numbers.Metres(max)}";

        /// <summary>Each rule that is not at its default, a row each.</summary>
        public static List<KeyValuePair<string, string>> Rows(LocationRules rules)
        {
            var rows = new List<KeyValuePair<string, string>>();
            void Add(string label, string value)
            {
                if (!string.IsNullOrEmpty(value)) rows.Add(new KeyValuePair<string, string>(label, value));
            }

            Add("Per world", "up to " + Numbers.Count(rules.Quantity));
            Add("Biome", Biome(rules));
            Add("From the centre", FromCentre(rules));
            Add("Above the sea", Altitude(rules));
            Add("Apart", Apart(rules));
            Add("Near", Near(rules));
            Add("Ground", Ground(rules));
            var woods = Woods(rules);
            if (woods != null) Add("Woods", woods + ForestScale);
            var cover = Cover(rules);
            if (cover.Words != null) Add(cover.Label, cover.Words + cover.Note);
            Add("Surroundings", Surroundings(rules));
            Add("Placed", Placed(rules));
            return rows;
        }

        /// <summary>The same rules as one line, for a prefab placed by several rule sets.</summary>
        public static string Line(LocationRules rules)
        {
            var parts = new List<string> { "up to " + Numbers.Count(rules.Quantity) + (string.IsNullOrEmpty(rules.Biomes) ? "" : " in " + Biome(rules)) };
            var centre = FromCentre(rules);
            if (centre != null) parts.Add(centre + " from the centre");
            var altitude = Altitude(rules);
            if (altitude != null) parts.Add(altitude + " above the sea");
            foreach (var part in new[] { Apart(rules), Near(rules), Ground(rules), Woods(rules), Cover(rules).Words, Surroundings(rules), Placed(rules) }) if (part != null) parts.Add(part);
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
            if (max > 0f) return "within " + Numbers.Metres(max);
            if (min > 0f) return "beyond " + Numbers.Metres(min);
            return null;
        }

        private static float Tighter(float a, float b) => a > 0f && b > 0f ? Math.Min(a, b) : Math.Max(a, b);

        private static string Altitude(LocationRules rules)
        {
            var low = rules.MinAltitude > -1000f;
            var high = rules.MaxAltitude < 1000f;
            if (low && high) return Range(rules.MinAltitude, rules.MaxAltitude);
            if (low) return "at least " + Numbers.Metres(rules.MinAltitude);
            if (high) return "at most " + Numbers.Metres(rules.MaxAltitude);
            return null;
        }

        private static string Apart(LocationRules rules)
        {
            if (rules.MinSimilar <= 0f) return null;
            var other = string.IsNullOrEmpty(rules.Group) ? "another of its kind" : $"another of the group \"{rules.Group}\"";
            return $"at least {Numbers.Metres(rules.MinSimilar)} from {other}";
        }

        private static string Near(LocationRules rules)
        {
            if (rules.MaxSimilar <= 0f) return null;
            var other = string.IsNullOrEmpty(rules.GroupMax) ? "another of its kind" : $"one of the group \"{rules.GroupMax}\"";
            return $"within {Numbers.Metres(rules.MaxSimilar)} of {other}";
        }

        /// <summary>How much the ground may rise within its outer radius (<c>WorldGenerator.GetTerrainDelta</c>); 1,000 and more is no limit.</summary>
        private static string Ground(LocationRules rules)
        {
            var within = $" within {Numbers.Metres(rules.ExteriorRadius)} of it";
            var low = rules.MinTerrainDelta > 0f;
            var high = rules.MaxTerrainDelta < 1000f;
            if (low && high) return "rising " + Range(rules.MinTerrainDelta, rules.MaxTerrainDelta) + within;
            if (low) return "rising at least " + Numbers.Metres(rules.MinTerrainDelta) + within;
            if (high) return "rising at most " + Numbers.Metres(rules.MaxTerrainDelta) + within;
            return null;
        }

        /// <summary>How high the forest factor goes: three layers of noise, halved in weight each time and a bit (<c>WorldGenerator.GetForestFactor</c>).</summary>
        private const float ForestTop = 2.19f;

        private const string ForestScale = " (0 is the thickest woods, about 2.2 the most open; the Meadows are wooded below 1.15)";

        /// <summary>The range of the forest factor it keeps to, or null when it keeps to none or to all of it.</summary>
        private static string Woods(LocationRules rules)
        {
            if (!rules.InForest) return null;
            var low = rules.ForestMin > 0f;
            var high = rules.ForestMax < ForestTop;
            if (low && high) return $"forest factor {Numbers.Amount(rules.ForestMin)}–{Numbers.Amount(rules.ForestMax)}";
            if (high) return "forest factor at most " + Numbers.Amount(rules.ForestMax);
            if (low) return "forest factor at least " + Numbers.Amount(rules.ForestMin);
            return null;
        }

        private static bool Only(LocationRules rules, string biome) => rules.BiomeKeys.Length > 0 && Array.TrueForAll(rules.BiomeKeys, b => b == biome);

        private static bool HasValue(LocationRules rules) => Array.Exists(rules.BiomeKeys, b => b == "AshLands" || b == "Mistlands");

        /// <summary>
        /// The ground value it asks for, as its biomes have it: the Ashlands' lava, the Mistlands'
        /// growth, and 0 anywhere else, where a minimum is never met and a maximum always is.
        /// </summary>
        private static (string Label, string Words, string Note) Cover(LocationRules rules)
        {
            var low = rules.MinVegetation > 0f;
            var high = rules.MaxVegetation < 1f;
            if (!low && !high) return (null, null, null);
            if (!HasValue(rules))
            {
                return low ? ("Ground value", "never met: it asks for lava or Mistlands growth, which its biome does not have", "") : (null, null, null);
            }
            if (Only(rules, "AshLands"))
            {
                var lava = low && high ? $"{Numbers.Percent(rules.MinVegetation, 1)}–{Numbers.Percent(rules.MaxVegetation, 1)}".Replace("%–", "–")
                    : low ? "more than " + Numbers.Percent(rules.MinVegetation, 1) : "less than " + Numbers.Percent(rules.MaxVegetation, 1);
                return ("Lava", $"only on ground {lava} lava", " (the game counts 60% and more as lava)");
            }
            var range = low && high ? $"{Numbers.Amount(rules.MinVegetation)}–{Numbers.Amount(rules.MaxVegetation)}"
                : low ? "over " + Numbers.Amount(rules.MinVegetation) : "under " + Numbers.Amount(rules.MaxVegetation);
            if (Only(rules, "Mistlands")) return ("Growth", "only where the Mistlands' growth value, which their plants grow by, is " + range, "");
            return ("Ground value", range, " (lava in the Ashlands, growth in the Mistlands, none elsewhere)");
        }

        /// <summary>
        /// How its surroundings must compare with the other spots tried: the ground value summed
        /// round it, at least so far from the average of the spots that met its other rules to the
        /// most found. Left out where its biome has no ground value, as there it measures nothing.
        /// </summary>
        private static string Surroundings(LocationRules rules)
        {
            if (!rules.SurroundCheck || !HasValue(rules)) return null;
            var what = Only(rules, "AshLands") ? "lava" : Only(rules, "Mistlands") ? "Mistlands growth" : "lava or Mistlands growth";
            var within = $"within {Numbers.Metres(rules.SurroundDistance)}";
            if (rules.SurroundBetter <= 0f) return $"more {what} {within} than the average spot that meets its other rules";
            return $"{what} {within} at least {Numbers.Percent(rules.SurroundBetter, 1)} of the way from the average spot that meets its other rules to the most found";
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

        /// <summary>A biome's place in the order players meet them, by its enum name; -1 for one not among them.</summary>
        public static int BiomeRank(string biome) => Array.IndexOf(BiomeOrder, biome);

        /// <summary>How many biomes that order holds.</summary>
        public static int BiomeCount => BiomeOrder.Length;

        /// <summary>
        /// The group a location is listed under: its biome, in the order players meet them, or
        /// "In several biomes" after all of them. Named by the biome's shown name when given.
        /// </summary>
        public static Group Group(string[] biomes, Func<string, string> shown = null)
        {
            // Ten apart, so the dungeons and camps of a biome can follow it (DungeonGroup).
            if (biomes == null || biomes.Length == 0) return new Group("In no biome", 900);
            if (biomes.Length > 1) return new Group("In several biomes", 1000);
            var at = Array.IndexOf(BiomeOrder, biomes[0]);
            var name = shown?.Invoke(biomes[0]) ?? Naming.FieldLabel(biomes[0]);
            return new Group(name, at >= 0 ? at * 10 : 500);
        }

        /// <summary>A dungeon's rooms, listed after every place, under the dungeon's name.</summary>
        public static Group RoomGroup(string dungeon) => new Group(dungeon + " rooms", 2000);

        /// <summary>A dungeon or camp with its rooms, right after the other places of its biome, the dungeons of one biome by their names.</summary>
        public static Group DungeonGroup(Group biome, string dungeon) => new Group(biome.Name + " \u00b7 " + dungeon, biome.Order + 1);

        /// <summary>What the stage says in place of the copy of a location or room while its model is not in; null once it is.</summary>
        public static string StageNote(PlaceLoad load)
        {
            switch (load)
            {
                case PlaceLoad.Ready: return null;
                case PlaceLoad.Failed: return "Its model could not be loaded.";
                default: return "Loading its model";
            }
        }

        /// <summary>The music a place plays, each with when, and that Enter plays the first; null for none.</summary>
        public static string Music(IReadOnlyList<PlaceMusic> music)
        {
            if (music == null || music.Count == 0) return null;
            var lines = new List<string>();
            foreach (var tune in music)
            {
                switch (tune.When)
                {
                    case MusicWhen.Near:
                        lines.Add(MusicWords.Name(tune.Name) + " when you come near");
                        break;
                    case MusicWhen.Inside:
                        lines.Add(tune.Chance >= 1f
                            ? MusicWords.Name(tune.Name) + " each time you step inside"
                            : MusicWords.Name(tune.Name) + " on " + Numbers.Percent(tune.Chance, 1) + " of the times you step inside");
                        break;
                    default:
                        lines.Add(MusicWords.Name(tune.Name) + " while you are inside");
                        break;
                }
            }
            return string.Join("; ", lines) + (music.Count == 1 ? ". Enter plays it" : ". Enter plays the first");
        }

        /// <summary>Reading the locations, asked for again while it reads, and how far it has got.</summary>
        public static string AlreadyReading(int done, int total) => $"already reading the locations and dungeons, {Numbers.Count(done)} of {Numbers.Count(total)} so far.";

        public static string ReadingInBackground(int queued) => $"reading {Numbers.Count(queued)} locations and dungeon rooms in the background.";

        /// <summary>How far reading the locations has got, where something waits on it (<see cref="CatalogWords.Progress(string, int, int)"/>).</summary>
        public static string ReadingProgress(int done, int total) => CatalogWords.Progress("Reading locations and dungeons", done, total);

        /// <summary>A place's link where it has no page: a search for what is found in it.</summary>
        public static string SearchFoundIn(string place) => "Search for what is found in " + place;

        /// <summary>Why a search for what is in a place finds nothing: the locations are being read, or have not been.</summary>
        public static string NoPlacesYet(bool reading) => reading
            ? "Reading this world's locations and dungeons. What is found in them shows here once they are read."
            : "Nothing is known to be in a location yet: this world's locations and dungeons are read only when asked.";

        /// <summary>The header's button while the locations are read, which stops it.</summary>
        public static string StopReading(int done, int total) => $"Stop reading  {Numbers.Count(done)} of {Numbers.Count(total)}";

        /// <summary>What its details say it holds before it has been read.</summary>
        public static string HoldsNote(PlaceLoad load) =>
            load == PlaceLoad.Failed ? "not known, its model could not be loaded" : "read once its model has loaded";

        /// <summary>The part of its biome a place placed by one set keeps to (<c>ZoneLocation.m_biomeArea</c>'s alternative biome).</summary>
        public static string OnlyInPart(string part) => $"the {Naming.FieldLabel(part).ToLowerInvariant()} part of its biome";

        /// <summary>One set's line, with the part of its biome it keeps to where it keeps to one.</summary>
        public static string WithPart(string line, string part) =>
            string.IsNullOrEmpty(part) ? line : $"{line}, only in the {Naming.FieldLabel(part).ToLowerInvariant()} part of it";

        /// <summary>The label of a set's line: the first placed, each after it also placed.</summary>
        public static string Placed(bool first) => first ? "Placed" : "Also placed";

        /// <summary>How near a place nothing can be built.</summary>
        public static string NoBuild(float radius) => $"not within {Numbers.Metres(radius)}";
    }

    /// <summary>How far the model of the location or room shown has got.</summary>
    internal enum PlaceLoad
    {
        None,
        Loading,
        Ready,
        Failed,
    }
}
