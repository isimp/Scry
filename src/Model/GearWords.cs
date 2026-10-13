using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What gear changes while worn, in the order the game's tooltip tells it (eitr
    /// regeneration in <c>ItemData.GetTooltip</c>, the rest in
    /// <c>Player.AppendEquipmentModifierTooltips</c>): each as a percent up or down, the most
    /// adrenaline as a number. Movement is told on its own line already.
    /// </summary>
    internal static class GearWords
    {
        private static readonly (string Field, string Label)[] Order =
        {
            ("m_eitrRegenModifier", "Eitr regeneration"),
            ("m_homeItemsStaminaModifier", "Building tools' stamina"),
            ("m_heatResistanceModifier", "Heat resistance"),
            ("m_jumpStaminaModifier", "Jump stamina"),
            ("m_attackStaminaModifier", "Attack stamina"),
            ("m_blockStaminaModifier", "Block stamina"),
            ("m_dodgeStaminaModifier", "Dodge stamina"),
            ("m_swimStaminaModifier", "Swim stamina"),
            ("m_sneakStaminaModifier", "Sneak stamina"),
            ("m_runStaminaModifier", "Run stamina"),
        };

        /// <summary>A creature's gear set by what of it is drawn (its attacks' names), else by its number.</summary>
        public static string Set(IReadOnlyList<string> drawn, int index) =>
            drawn.Count > 0 ? string.Join(" + ", drawn) : $"Set {Numbers.Count(index + 1)}, nothing drawn";

        /// <summary>The meshes an item draws in the hand, by name, or null for none.</summary>
        public static string Meshes(IReadOnlyList<string> meshes) => meshes.Count > 0 ? string.Join(",", meshes) : null;

        private const string Adrenaline = "m_maxAdrenaline";

        /// <summary>The fields told, in the tooltip's order, the most adrenaline last.</summary>
        public static IEnumerable<string> Fields
        {
            get
            {
                foreach (var (field, _) in Order) yield return field;
                yield return Adrenaline;
            }
        }

        /// <summary>A field's label: "Run stamina".</summary>
        public static string Label(string field)
        {
            if (field == Adrenaline) return "Most adrenaline";
            foreach (var (each, label) in Order) if (each == field) return label;
            return Naming.FieldLabel(field);
        }

        /// <summary>A field's value as a percent up or down, the most adrenaline as a number; null where it changes nothing the game would show.</summary>
        public static string Value(string field, float value)
        {
            if (field == Adrenaline) return Math.Abs(value) >= 0.5f ? (value > 0f ? "+" : "") + Numbers.Amount(value) : null;
            var percent = (int)Math.Round(value * 100f, MidpointRounding.AwayFromZero);
            return percent != 0 ? Numbers.Count(percent, signed: true) + "%" : null;
        }

        /// <summary>The lines for what the gear changes, by the shared data's field names; none for what it leaves alone.</summary>
        public static List<(string Label, string Value)> Lines(IReadOnlyDictionary<string, float> values)
        {
            var lines = new List<(string, string)>();
            if (values == null) return lines;
            foreach (var field in Fields)
            {
                if (values.TryGetValue(field, out var value) && Value(field, value) is string told) lines.Add((Label(field), told));
            }
            return lines;
        }
    }
}
