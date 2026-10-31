using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Creatures that come into the world tame (<c>Tameable.m_startsTamed</c>), as a hen or a summon:
    /// never tamed, so nothing of taming is found out of them; how long a meal lasts shows once one
    /// was seen eating, where the kind eats at all.
    /// </summary>
    internal static class BornTame
    {
        /// <summary>Whether a creature comes into the world tame.</summary>
        public static bool Is(GameObject prefab) => prefab != null && prefab.GetComponent<Tameable>().OrNull()?.m_startsTamed == true;

        /// <summary>Whether one born tame fights for the players (<c>Character.m_faction</c>), as a summon: killed only by slaughtering one's own, so its health is not kept back.</summary>
        public static bool FightsForPlayers(GameObject prefab) => Is(prefab) && prefab.GetComponent<Character>().OrNull()?.m_faction == Character.Faction.Players;

        /// <summary>Whether a creature eats anything (<c>MonsterAI.m_consumeItems</c>), so a meal of it can be seen.</summary>
        public static bool Eats(GameObject prefab) => prefab != null && (prefab.GetComponent<MonsterAI>().OrNull()?.m_consumeItems?.Count ?? 0) > 0;
    }
}
