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

        /// <summary>The lines for what the gear changes, by the shared data's field names; none for what it leaves alone.</summary>
        public static List<(string Label, string Value)> Lines(IReadOnlyDictionary<string, float> values)
        {
            var lines = new List<(string, string)>();
            if (values == null) return lines;
            foreach (var (field, label) in Order)
            {
                if (!values.TryGetValue(field, out var value)) continue;
                var percent = (int)Math.Round(value * 100f, MidpointRounding.AwayFromZero);
                if (percent != 0) lines.Add((label, Numbers.Count(percent, signed: true) + "%"));
            }
            if (values.TryGetValue("m_maxAdrenaline", out var adrenaline) && Math.Abs(adrenaline) >= 0.5f)
            {
                lines.Add(("Most adrenaline", (adrenaline > 0f ? "+" : "") + Numbers.Amount(adrenaline)));
            }
            return lines;
        }
    }
}
