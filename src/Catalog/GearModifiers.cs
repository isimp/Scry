using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What an item changes on whoever equips it, by the shared data's field names: its movement
    /// and the other modifiers the game sums over what is equipped (<c>Player.UpdateModifiers</c>),
    /// and its eitr regeneration (<c>ItemData.GetTooltip</c>).
    /// </summary>
    internal static class GearModifiers
    {
        public static Dictionary<string, float> Of(ItemDrop.ItemData.SharedData shared) => new Dictionary<string, float>
        {
            ["m_movementModifier"] = shared.m_movementModifier,
            ["m_eitrRegenModifier"] = shared.m_eitrRegenModifier,
            ["m_homeItemsStaminaModifier"] = shared.m_homeItemsStaminaModifier,
            ["m_heatResistanceModifier"] = shared.m_heatResistanceModifier,
            ["m_jumpStaminaModifier"] = shared.m_jumpStaminaModifier,
            ["m_attackStaminaModifier"] = shared.m_attackStaminaModifier,
            ["m_blockStaminaModifier"] = shared.m_blockStaminaModifier,
            ["m_dodgeStaminaModifier"] = shared.m_dodgeStaminaModifier,
            ["m_swimStaminaModifier"] = shared.m_swimStaminaModifier,
            ["m_sneakStaminaModifier"] = shared.m_sneakStaminaModifier,
            ["m_runStaminaModifier"] = shared.m_runStaminaModifier,
            ["m_maxAdrenaline"] = shared.m_maxAdrenaline,
        };
    }
}
