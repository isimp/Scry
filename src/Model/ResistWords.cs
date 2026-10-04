using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How hard a damage type lands, in the order and with the numbers of the game's own <c>HitData.DamageModifier</c>.</summary>
    internal enum Degree
    {
        Normal,
        Resistant,
        Weak,
        Immune,
        Ignore,
        VeryResistant,
        VeryWeak,
        SlightlyResistant,
        SlightlyWeak,
    }

    /// <summary>The colour a grid cell is drawn in: plain, resisting, weak, or taking nothing.</summary>
    internal enum Tone
    {
        Plain,
        Resists,
        Weak,
        Immune,

        /// <summary>Taking none of a damage that does not concern it, such as a creature and the tools' damage: drawn quietly.</summary>
        Quiet,
    }

    /// <summary>One damage type in the resistance grid: its name, the share of it taken, and the words on hover.</summary>
    internal struct ResistCell
    {
        public string Type, Value, Tip;
        public Tone Tone;
    }

    /// <summary>
    /// Resistances as a grid of every damage type, so whatever a creature, piece or rock
    /// resists, every one of them shows the same cells. Each is the share of that damage taken,
    /// as <c>HitData.ApplyModifier</c> scales it: a quarter, half or three quarters resisted, a
    /// quarter, half or all again for weakness, none for immune or unaffected.
    /// </summary>
    internal static class ResistWords
    {
        /// <summary>The degrees from the most harm taken to the least, the order resistances are told in.</summary>
        private static readonly Degree[] FromWeakest =
        {
            Degree.VeryWeak, Degree.Weak, Degree.SlightlyWeak, Degree.SlightlyResistant, Degree.Resistant, Degree.VeryResistant, Degree.Immune, Degree.Ignore,
        };

        /// <summary>How a degree reads as a label: "Weak to", "Resists".</summary>
        public static string Label(Degree degree)
        {
            switch (degree)
            {
                case Degree.VeryWeak: return "Very weak to";
                case Degree.Weak: return "Weak to";
                case Degree.SlightlyWeak: return "Slightly weak to";
                case Degree.SlightlyResistant: return "Slightly resists";
                case Degree.Resistant: return "Resists";
                case Degree.VeryResistant: return "Strongly resists";
                case Degree.Immune: return "Immune to";
                case Degree.Ignore: return "Unaffected by";
                default: return Naming.FieldLabel(degree.ToString());
            }
        }

        /// <summary>
        /// Each damage type that is not taken plainly, grouped by its degree, the groups from the
        /// most harm taken to the least, as the game orders them rather than by their words.
        /// </summary>
        public static List<(string Words, string[] Types)> ByDegree(IEnumerable<(string Type, Degree Degree)> modifiers)
        {
            var groups = new SortedDictionary<int, (string Words, List<string> Types)>();
            foreach (var (type, degree) in modifiers)
            {
                if (degree == Degree.Normal) continue;
                var order = System.Array.IndexOf(FromWeakest, degree);
                if (order < 0) order = FromWeakest.Length + (int)degree;
                if (!groups.TryGetValue(order, out var group)) groups[order] = group = (Label(degree), new List<string>());
                group.Types.Add(type);
            }
            var told = new List<(string, string[])>();
            foreach (var group in groups.Values) told.Add((group.Words, group.Types.ToArray()));
            return told;
        }
        /// <summary>The damage types, in the order of the game's <c>HitData.DamageModifiers</c>.</summary>
        public static readonly string[] Types = { "Blunt", "Slash", "Pierce", "Chop", "Pickaxe", "Fire", "Frost", "Lightning", "Poison", "Spirit" };

        /// <summary>The damage of tools, which fell trees and break rocks.</summary>
        private static readonly string[] ToolTypes = { "Chop", "Pickaxe" };

        /// <summary>
        /// A creature's grid: the tools' damage, which is for trees and rocks, is drawn quietly
        /// where the creature takes none of it, keeping its place so the grid's rows stay the
        /// same; any it does take shows as the rest.
        /// </summary>
        public static List<ResistCell> ForCreature(IEnumerable<ResistCell> cells) =>
            cells.Select(c => c.Tone == Tone.Immune && Array.IndexOf(ToolTypes, c.Type) >= 0
                ? new ResistCell { Type = c.Type, Value = c.Value, Tip = c.Type + ": takes none; tools are for trees and rocks", Tone = Tone.Quiet }
                : c).ToList();

        public static ResistCell Cell(string type, Degree degree)
        {
            string value, word;
            var tone = Tone.Plain;
            switch (degree)
            {
                case Degree.Normal: value = "100%"; word = "takes it in full"; break;
                case Degree.SlightlyResistant: value = "75%"; word = "slightly resists it: takes three quarters"; tone = Tone.Resists; break;
                case Degree.Resistant: value = "50%"; word = "resists it: takes half"; tone = Tone.Resists; break;
                case Degree.VeryResistant: value = "25%"; word = "strongly resists it: takes a quarter"; tone = Tone.Resists; break;
                case Degree.SlightlyWeak: value = "125%"; word = "slightly weak to it: takes a quarter more"; tone = Tone.Weak; break;
                case Degree.Weak: value = "150%"; word = "weak to it: takes half again"; tone = Tone.Weak; break;
                case Degree.VeryWeak: value = "200%"; word = "very weak to it: takes double"; tone = Tone.Weak; break;
                case Degree.Immune: value = "0%"; word = "immune to it: takes none"; tone = Tone.Immune; break;
                case Degree.Ignore: value = "0%"; word = "unaffected by it: takes none"; tone = Tone.Immune; break;
                default: value = "?"; word = $"a degree Scry does not know ({Numbers.Count((int)degree)})"; break;
            }
            return new ResistCell { Type = type, Value = value, Tip = $"{type}: {word}", Tone = tone };
        }
    }
}
