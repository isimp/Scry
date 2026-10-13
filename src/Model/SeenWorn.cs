using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// What a player had at one moment, as far as what they equip changes it: the most they can
    /// carry, their armour, how hard each damage type lands on them, and the status effects on
    /// them by prefab name with the name each shows.
    /// </summary>
    internal sealed class WornState
    {
        public float Carry, Armour;
        public readonly Dictionary<string, Degree> Resists = new Dictionary<string, Degree>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Effects = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// What equipping something changed: the most carried and the armour by how much, the gear
    /// modifiers it brings by their field names, the damage types that land differently by how
    /// they land now, and the status effects it brought. Differences smaller than the game's
    /// figures show are none.
    /// </summary>
    internal sealed class WornChange
    {
        public float Carry, Armour;
        public readonly Dictionary<string, float> Modifiers = new Dictionary<string, float>(StringComparer.Ordinal);
        public readonly Dictionary<string, Degree> Resists = new Dictionary<string, Degree>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Effects = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Less than half a unit of weight carried, a twentieth of armour or half a percent of a modifier is none.</summary>
        private const float LeastCarry = 0.5f, LeastArmour = 0.05f, LeastModifier = 0.005f;

        public bool IsNothing => Carry == 0f && Armour == 0f && Modifiers.Count == 0 && Resists.Count == 0 && Effects.Count == 0;

        /// <summary>
        /// What equipping did, from what the player had once anything it replaced came off to
        /// what they had with it on, and the modifiers it brings as its own data holds them then
        /// (the game sums those on its next update, not at once). A set bonus it completed is left
        /// out: that needs the rest of the set.
        /// </summary>
        public static WornChange Between(WornState before, WornState after, IReadOnlyDictionary<string, float> modifiers, ICollection<string> setEffects)
        {
            var change = new WornChange
            {
                Carry = Notable(after.Carry - before.Carry, LeastCarry),
                Armour = Notable(after.Armour - before.Armour, LeastArmour),
            };
            foreach (var pair in modifiers)
            {
                if (Notable(pair.Value, LeastModifier) != 0f) change.Modifiers[pair.Key] = pair.Value;
            }
            foreach (var pair in after.Resists)
            {
                var was = before.Resists.TryGetValue(pair.Key, out var degree) ? degree : Degree.Normal;
                if (pair.Value != was) change.Resists[pair.Key] = pair.Value;
            }
            foreach (var pair in after.Effects)
            {
                if (!before.Effects.ContainsKey(pair.Key) && !setEffects.Contains(pair.Key)) change.Effects[pair.Key] = pair.Value;
            }
            return change;
        }

        private static float Notable(float value, float least) => Math.Abs(value) < least ? 0f : value;
    }

    /// <summary>
    /// What equipping each thing was seen to change in your play, by its prefab and quality, the
    /// latest seen standing. Kept from session to session as text (<see cref="Save"/>,
    /// <see cref="Load"/>), a line for each thing a change holds, in a fixed order so it reads
    /// back the same.
    /// </summary>
    internal sealed class SeenWorn
    {
        private readonly Dictionary<string, SortedDictionary<int, WornChange>> _seen = new Dictionary<string, SortedDictionary<int, WornChange>>(StringComparer.Ordinal);

        /// <summary>Whether anything differs from what was last saved.</summary>
        public bool Changed { get; private set; }

        /// <summary>What equipping an item of this quality changed, in place of what was seen before.</summary>
        public void Record(string item, int quality, WornChange change)
        {
            if (string.IsNullOrEmpty(item) || quality < 1 || change == null) return;
            if (!_seen.TryGetValue(item, out var byQuality)) _seen[item] = byQuality = new SortedDictionary<int, WornChange>();
            if (byQuality.TryGetValue(quality, out var was) && Lines(item, quality, was).SequenceEqual(Lines(item, quality, change))) return;
            byQuality[quality] = change;
            Changed = true;
        }

        /// <summary>What equipping an item was seen to change, by quality, the lowest first.</summary>
        public IReadOnlyList<(int Quality, WornChange Change)> Of(string item)
        {
            if (item == null || !_seen.TryGetValue(item, out var byQuality)) return new List<(int, WornChange)>();
            return byQuality.Select(p => (p.Key, p.Value)).ToList();
        }

        public string Save()
        {
            var text = new StringBuilder();
            foreach (var item in _seen.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                foreach (var pair in _seen[item]) foreach (var line in Lines(item, pair.Key, pair.Value)) text.Append(line).Append('\n');
            }
            Changed = false;
            return text.ToString();
        }

        /// <summary>A change's lines: that it was seen, then each thing it holds.</summary>
        private static IEnumerable<string> Lines(string item, int quality, WornChange change)
        {
            var head = "worn\t" + item + "\t" + Stored.Count(quality) + "\t";
            yield return head + "seen";
            if (change.Carry != 0f) yield return head + "carry\t" + Stored.Exact(change.Carry);
            if (change.Armour != 0f) yield return head + "armour\t" + Stored.Exact(change.Armour);
            foreach (var pair in change.Modifiers.OrderBy(p => p.Key, StringComparer.Ordinal)) yield return head + "mod\t" + pair.Key + "\t" + Stored.Exact(pair.Value);
            foreach (var pair in change.Resists.OrderBy(p => p.Key, StringComparer.Ordinal)) yield return head + "resist\t" + pair.Key + "\t" + pair.Value;
            foreach (var pair in change.Effects.OrderBy(p => p.Key, StringComparer.Ordinal)) yield return head + "effect\t" + pair.Key + "\t" + Stored.Escaped(pair.Value);
        }

        /// <summary>What was saved; a line that cannot be read is passed over.</summary>
        public static SeenWorn Load(string text)
        {
            var seen = new SeenWorn();
            if (string.IsNullOrEmpty(text)) return seen;
            foreach (var line in text.Split('\n'))
            {
                var parts = line.TrimEnd('\r').Split('\t');
                if (parts.Length < 4 || parts[0] != "worn" || parts[1].Length == 0 || !Stored.TryCount(parts[2], out var quality) || quality < 1) continue;
                switch (parts[3])
                {
                    case "seen" when parts.Length == 4:
                        seen.ChangeOf(parts[1], quality);
                        break;
                    case "carry" when parts.Length == 5 && Stored.TryExact(parts[4], out var carry):
                        seen.ChangeOf(parts[1], quality).Carry = carry;
                        break;
                    case "armour" when parts.Length == 5 && Stored.TryExact(parts[4], out var armour):
                        seen.ChangeOf(parts[1], quality).Armour = armour;
                        break;
                    case "mod" when parts.Length == 6 && parts[4].Length > 0 && Stored.TryExact(parts[5], out var value):
                        seen.ChangeOf(parts[1], quality).Modifiers[parts[4]] = value;
                        break;
                    case "resist" when parts.Length == 6 && parts[4].Length > 0 && Enum.TryParse<Degree>(parts[5], out var degree) && Enum.IsDefined(typeof(Degree), degree):
                        seen.ChangeOf(parts[1], quality).Resists[parts[4]] = degree;
                        break;
                    case "effect" when parts.Length == 6 && parts[4].Length > 0 && Stored.TryUnescaped(parts[5], out var shown):
                        seen.ChangeOf(parts[1], quality).Effects[parts[4]] = shown;
                        break;
                }
            }
            return seen;
        }

        /// <summary>The change kept for an item's quality, made when not there yet.</summary>
        private WornChange ChangeOf(string item, int quality)
        {
            if (_seen.TryGetValue(item, out var byQuality) && byQuality.TryGetValue(quality, out var change)) return change;
            if (byQuality == null) _seen[item] = byQuality = new SortedDictionary<int, WornChange>();
            return byQuality[quality] = new WornChange();
        }
    }

    /// <summary>How what equipping something was seen to change is told: a table, a column for each quality seen.</summary>
    internal static class WornWords
    {
        public const string Title = "Seen equipped in your play";

        private const string Same = "no change";

        /// <summary>The column of something of one quality, and the line of a change of nothing.</summary>
        private static readonly string[] SeenColumn = { "Seen" }, NothingLine = { "Changed" };

        /// <summary>The table's columns and lines: what changed, each line its thing and a cell for each quality seen.</summary>
        public static (string[] Columns, List<string[]> Lines) Table(IReadOnlyList<(int Quality, WornChange Change)> seen, int maxQuality)
        {
            var columns = new List<string> { "" };
            columns.AddRange(maxQuality > 1 ? seen.Select(s => "Level " + Numbers.Count(s.Quality)) : SeenColumn);
            var lines = new List<string[]>();

            void Line(string label, Func<WornChange, string> cell, string none = null)
            {
                none = none ?? Same;
                var cells = seen.Select(s => cell(s.Change)).ToList();
                if (cells.All(c => c == null)) return;
                lines.Add(new[] { label }.Concat(cells.Select(c => c ?? none)).ToArray());
            }

            Line("Carry weight", c => c.Carry != 0f ? Numbers.Amount(c.Carry, signed: true) : null);
            Line("Armour", c => c.Armour != 0f ? Numbers.Amount(c.Armour, signed: true) : null);
            Line("Movement", c => c.Modifiers.TryGetValue("m_movementModifier", out var v) ? Numbers.Percent(v, 0, signed: true) : null);
            foreach (var field in GearWords.Fields)
            {
                Line(GearWords.Label(field), c => c.Modifiers.TryGetValue(field, out var v) ? GearWords.Value(field, v) : null);
            }
            foreach (var type in ResistWords.Types)
            {
                Line(type + " damage", c => c.Resists.TryGetValue(type, out var d) ? Lands(d) : null);
            }
            Line("Brings", c => c.Effects.Count > 0 ? string.Join(", ", c.Effects.Values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase)) : null, "nothing");
            if (lines.Count == 0) lines.Add(NothingLine.Concat(seen.Select(_ => "nothing")).ToArray());
            return (columns.ToArray(), lines);
        }

        /// <summary>How a damage type lands on you now, in a cell beside its name.</summary>
        private static string Lands(Degree degree)
        {
            switch (degree)
            {
                case Degree.VeryWeak: return "very weak to it";
                case Degree.Weak: return "weak to it";
                case Degree.SlightlyWeak: return "slightly weak to it";
                case Degree.SlightlyResistant: return "slightly resisted";
                case Degree.Resistant: return "resisted";
                case Degree.VeryResistant: return "strongly resisted";
                case Degree.Immune: return "immune";
                case Degree.Ignore: return "unaffected";
                default: return "taken plainly";
            }
        }
    }
}
