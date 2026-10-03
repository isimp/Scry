using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>What is known of a location that goes into its name, each as the game shows it; empty for none.</summary>
    internal sealed class PlaceFacts
    {
        public string Prefab = "";

        /// <summary>The name the game shows on entering it (a dungeon's) or discovering it.</summary>
        public string GameName = "";

        /// <summary>The boss its altar summons.</summary>
        public string Boss = "";

        /// <summary>The trader who stands there.</summary>
        public string Trader = "";

        /// <summary>The biomes it is placed in; empty when that is none or every one.</summary>
        public string Biome = "";
    }

    /// <summary>
    /// Where things are found in the world's locations and dungeon rooms, once they are read (on
    /// request, since the game keeps them in asset bundles loaded only while it builds a zone).
    /// A place goes by the name the game gives it where it has one, and says its biome; a room
    /// goes by the dungeons built with it.
    /// </summary>
    internal static class Places
    {
        /// <summary>Between a place's name and its biome.</summary>
        public const string BiomeMark = " · ";

        /// <summary>What rooms no dungeon here is built with are called (a mod's own kind of room).</summary>
        public const string AnyDungeon = "Dungeon rooms";

        /// <summary>Words in prefab names that tell the game's makers apart versions of a place, not players.</summary>
        private static readonly HashSet<string> BuildTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "new", "dn", "leet" };

        /// <summary>
        /// A location's name, then its biome: the name the game shows for it; else its altar's
        /// boss ("The Elder's altar") or its trader ("Haldor's camp"); else its prefab name in
        /// words, without the number of its variant or the makers' tags, with creatures called as
        /// the game calls them ("GoblinCamp2" is a Fuling camp) and without the biome it says
        /// anyway. A name with no words in it (a mod's, all digits) keeps its own, so no place
        /// goes unnamed.
        /// </summary>
        /// <param name="place">What is known of the location: its prefab, the game's name for it, its biome, its altar's boss and its trader.</param>
        /// <param name="creatures">Creatures' shown names by their prefab names.</param>
        public static string LocationLabel(PlaceFacts place, IReadOnlyDictionary<string, string> creatures)
        {
            string name;
            if (!string.IsNullOrEmpty(place.GameName)) name = place.GameName;
            else if (!string.IsNullOrEmpty(place.Boss)) name = place.Boss + "'s altar";
            else if (!string.IsNullOrEmpty(place.Trader)) name = place.Trader + "'s camp";
            else name = WordsOf(place.Prefab, place.Biome, creatures);
            return string.IsNullOrEmpty(place.Biome) ? name : name + BiomeMark + place.Biome;
        }

        /// <summary>
        /// The places a dungeon room is in: every dungeon here built with its kind of room (the
        /// game's <c>Room.Theme</c> flags against each <c>DungeonGenerator.m_themes</c>), each
        /// once; rooms none is built with are only said to be a dungeon's.
        /// </summary>
        public static List<string> RoomLabels(int theme, IEnumerable<KeyValuePair<int, string>> dungeons)
        {
            var labels = new List<string>();
            if (theme != 0)
            {
                foreach (var dungeon in dungeons)
                {
                    if ((dungeon.Key & theme) != 0 && !labels.Contains(dungeon.Value)) labels.Add(dungeon.Value);
                }
            }
            if (labels.Count == 0) labels.Add(AnyDungeon);
            return labels;
        }

        /// <summary>
        /// The location a place something is found in goes to: the first location in the catalog
        /// whose label it is, or none when no location goes by it (a mod's room no dungeon here is
        /// built with). A dungeon's rooms carry its label too, and are passed over.
        /// </summary>
        public static Entry LocationNamed(IEnumerable<Entry> catalog, string place)
        {
            if (string.IsNullOrEmpty(place)) return null;
            foreach (var entry in catalog)
            {
                if (entry.Kind != Kind.Location || entry.FoundIn.Length == 0 || !entry.Components.Contains("Location")) continue;
                if (string.Equals(entry.FoundIn[0], place, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        /// <summary>A place's name without its biome, as the search and its suggestions take it.</summary>
        public static string NameOf(string place)
        {
            if (string.IsNullOrEmpty(place)) return "";
            var mark = place.IndexOf(BiomeMark, StringComparison.Ordinal);
            return mark > 0 ? place.Substring(0, mark) : place;
        }

        private static string WordsOf(string prefab, string biome, IReadOnlyDictionary<string, string> creatures)
        {
            var words = new List<(string Text, bool Name)>();
            foreach (var raw in Split(prefab))
            {
                // "PlaceofMystery" runs "of" into the word before it.
                if (raw.Length >= 6 && raw.EndsWith("of", StringComparison.OrdinalIgnoreCase))
                {
                    words.Add((raw.Substring(0, raw.Length - 2), false));
                    words.Add(("of", false));
                    continue;
                }
                if (BuildTags.Contains(raw)) continue;
                words.Add(Creature(raw, creatures) ?? (raw, false));
            }

            var withoutBiome = WithoutBiome(words, biome);
            if (withoutBiome.Count > 0) words = withoutBiome;
            if (words.Count == 0) return (prefab ?? "").Trim();

            var shown = new StringBuilder();
            foreach (var (text, name) in words)
            {
                var capitals = text.Length > 1 && text.All(char.IsUpper);
                if (shown.Length > 0) shown.Append(' ');
                if (name || capitals) shown.Append(text);
                else if (shown.Length == 0) shown.Append(char.ToUpperInvariant(text[0]) + text.Substring(1).ToLowerInvariant());
                else shown.Append(text.ToLowerInvariant());
            }
            return shown.ToString();
        }

        /// <summary>A word that is a creature's prefab name, alone or more than one, as the game calls it, if that differs.</summary>
        private static (string, bool)? Creature(string word, IReadOnlyDictionary<string, string> creatures)
        {
            if (creatures == null) return null;
            foreach (var plural in new[] { false, true })
            {
                var single = plural ? (word.Length > 1 && word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? word.Substring(0, word.Length - 1) : null) : word;
                if (single == null) continue;
                foreach (var creature in creatures)
                {
                    if (!string.Equals(creature.Key, single, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.IsNullOrEmpty(creature.Value) || string.Equals(creature.Value, single, StringComparison.OrdinalIgnoreCase)) return null;
                    return (creature.Value + (plural ? "s" : ""), true);
                }
            }
            return null;
        }

        /// <summary>How a location's biomes are listed, and what a biome's name is split into words at.</summary>
        private static readonly string[] BiomeSeparator = { ", " };
        private static readonly char[] WordSeparator = { ' ' };

        /// <summary>The words without any whole biome's name the place is in ("Runestone_Swamps" in the swamp is a runestone).</summary>
        private static List<(string Text, bool Name)> WithoutBiome(List<(string Text, bool Name)> words, string biomes)
        {
            var left = new List<(string, bool)>(words);
            if (string.IsNullOrEmpty(biomes)) return left;
            foreach (var biome in biomes.Split(BiomeSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = biome.Split(WordSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                for (var i = 0; i + parts.Length <= left.Count; i++)
                {
                    var whole = true;
                    for (var j = 0; j < parts.Length && whole; j++)
                    {
                        var word = left[i + j].Item1;
                        var part = parts[j];
                        var last = j == parts.Length - 1;
                        whole = string.Equals(word, part, StringComparison.OrdinalIgnoreCase)
                                || (last && string.Equals(word, part + "s", StringComparison.OrdinalIgnoreCase));
                    }
                    if (!whole) continue;
                    left.RemoveRange(i, parts.Length);
                    i--;
                }
            }
            return left;
        }

        /// <summary>A prefab name's words, without the number of its variant ("WoodHouse10" is wood, house).</summary>
        private static List<string> Split(string name)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            void End()
            {
                var text = word.ToString().TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (text.Length > 0) words.Add(text);
                word.Clear();
            }

            var source = name ?? "";
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (c == '_' || c == ' ' || c == '-') { End(); continue; }
                var startsWord = char.IsUpper(c) && word.Length > 0
                                 && (!char.IsUpper(source[i - 1]) || (i + 1 < source.Length && char.IsLower(source[i + 1])));
                var afterDigit = word.Length > 0 && char.IsDigit(source[i - 1]) && !char.IsDigit(c);
                if (startsWord || afterDigit) End();
                word.Append(c);
            }
            End();
            return words;
        }

        /// <summary>
        /// Puts what was found where on the catalog's entries (by prefab name; a status effect is
        /// no prefab), each place once, by the biome players reach first of the location going by
        /// it, then by name; a place no location goes by comes last. An effect or sound nothing was
        /// found to play, or a projectile nothing was found to fire, goes under "In locations" when
        /// one names it.
        /// </summary>
        public static void Apply(IReadOnlyList<Entry> catalog, IDictionary<string, HashSet<string>> found)
        {
            var unplayed = Groups.Purpose(Array.Empty<string>(), false, false).Order;
            var unfired = Groups.Projectile(Array.Empty<Shooter>()).Order;

            // Each place's biome rank, from the first location going by it (LocationNamed).
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in catalog)
            {
                if (entry.Kind != Kind.Location || entry.FoundIn.Length == 0 || !entry.Components.Contains("Location") || ranks.ContainsKey(entry.FoundIn[0])) continue;
                ranks[entry.FoundIn[0]] = ContentOrder.Earliest(entry.Biomes);
            }
            int RankOf(string place) => ranks.TryGetValue(place, out var rank) ? rank : int.MaxValue;

            foreach (var entry in catalog)
            {
                if (EntryKeys.HasOwnNamespace(entry.Kind) || !found.TryGetValue(entry.Name, out var places) || places.Count == 0) continue;
                entry.FoundIn = places.Distinct().OrderBy(RankOf).ThenBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

                Group? group = null;
                if ((entry.Kind == Kind.Effect || entry.Kind == Kind.Sound) && entry.GroupOrder == unplayed) group = Groups.EffectsInLocations;
                else if (entry.Kind == Kind.Projectile && entry.GroupOrder == unfired) group = Groups.ProjectilesInLocations;
                if (group == null) continue;
                entry.Group = group.Value.Name;
                entry.GroupOrder = group.Value.Order;
            }
        }
    }
}
