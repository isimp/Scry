using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class GearTests
    {
        // What gear changes while worn, as the game's tooltip tells it (ItemData.GetTooltip, then
        // Player.AppendEquipmentModifierTooltips): eitr regeneration, stamina for each kind of
        // action and heat resistance as a percent up or down, and the most adrenaline as a
        // number; in the tooltip's order, and nothing for what it leaves alone.

        [Fact]
        public void GearTellsWhatItChangesInTheTooltipsOrder()
        {
            var lines = GearWords.Lines(new Dictionary<string, float>
            {
                ["m_runStaminaModifier"] = -0.15f,
                ["m_homeItemsStaminaModifier"] = -0.25f,
                ["m_heatResistanceModifier"] = 0.5f,
                ["m_eitrRegenModifier"] = 0.2f,
                ["m_maxAdrenaline"] = 10f,
                ["m_jumpStaminaModifier"] = 0f,
            });

            Assert.Equal(new[]
            {
                ("Eitr regeneration", "+20%"),
                ("Building tools' stamina", "-25%"),
                ("Heat resistance", "+50%"),
                ("Run stamina", "-15%"),
                ("Most adrenaline", "+10"),
            }, lines);
        }

        [Fact]
        public void EveryKindOfStaminaIsNamed()
        {
            var all = new Dictionary<string, float>();
            foreach (var field in new[] { "m_jumpStaminaModifier", "m_attackStaminaModifier", "m_blockStaminaModifier", "m_dodgeStaminaModifier", "m_swimStaminaModifier", "m_sneakStaminaModifier" }) all[field] = 0.1f;

            Assert.Equal(new[]
            {
                ("Jump stamina", "+10%"), ("Attack stamina", "+10%"), ("Block stamina", "+10%"), ("Dodge stamina", "+10%"), ("Swim stamina", "+10%"), ("Sneak stamina", "+10%"),
            }, GearWords.Lines(all));
        }

        [Fact]
        public void GearChangingNothingTellsNothing()
        {
            Assert.Empty(GearWords.Lines(new Dictionary<string, float> { ["m_runStaminaModifier"] = 0.0004f, ["m_maxAdrenaline"] = 0f }));
            Assert.Empty(GearWords.Lines(null));
        }
    }
}
