using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What near the character lets an untried row be tried (<see cref="Probe.Untried"/>), by a
    /// key each row names: a kind of thing loaded within <see cref="NeedReach"/>. Only a hint: a
    /// row may still want more of the thing than being near (a chest the world filled, a bush
    /// picked before), and says so again when run. Rows nothing near frees (a tool in hand, a
    /// kill to come) name no key.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>How near a thing must be loaded to count as near the character, in metres.</summary>
        private const float NeedReach = 40f;

        private static readonly Dictionary<string, Func<Vector3, bool>> Needs = new Dictionary<string, Func<Vector3, bool>>(StringComparer.Ordinal)
        {
            ["altar"] = at => Near<OfferingBowl>(at),
            ["beehive"] = at => Near<Beehive>(at),
            ["breakable"] = at => Near<Destructible>(at) || Near<TreeBase>(at) || Near<MineRock5>(at),
            ["breeding"] = at => Near<Procreation>(at, p => p.GetComponent<Character>().OrNull()?.IsTamed() == true),
            ["chest"] = at => Near<Container>(at, c => c.GetComponent<Piece>() == null),
            ["cooking"] = at => Near<CookingStation>(at),
            ["armed"] = at => Near<Humanoid>(at, h => !h.IsPlayer()),
            ["creature"] = at => Near<Character>(at, c => !c.IsPlayer()),
            ["pickable"] = at => Near<Pickable>(at),
            ["piece"] = at => Near<WearNTear>(at),
            ["place"] = at => Location.GetLocation(at) != null,
            ["plant"] = at => Near<Plant>(at),
            ["rock"] = at => Near<MineRock5>(at),
            ["runestone"] = at => Near<RuneStone>(at),
            ["spawner"] = at => Near<SpawnArea>(at),
            ["tame"] = at => Near<Tameable>(at, t => t.GetComponent<Character>().OrNull()?.IsTamed() == true),
            ["tameable"] = at => Near<Tameable>(at, t => t.GetComponent<Character>().OrNull()?.IsTamed() == false),
            ["treasure"] = at => Near<PickableItem>(at),
            ["tree"] = at => Near<TreeBase>(at),
            ["upgrades"] = at => Near<StationExtension>(at),
            ["vegvisir"] = at => Near<Vegvisir>(at),
        };

        /// <summary>The keys of what is loaded near the character now; none outside a world.</summary>
        private static HashSet<string> NeedsNear()
        {
            var near = new HashSet<string>(StringComparer.Ordinal);
            var player = Player.m_localPlayer;
            if (player == null) return near;
            var at = player.transform.position;
            foreach (var need in Needs)
            {
                if (Steps.Run(() => need.Value(at), out var found, null) == null && found) near.Add(need.Key);
            }
            return near;
        }

        private static bool Near<T>(Vector3 at, Func<T, bool> fits = null) where T : Component
        {
            foreach (var thing in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (thing != null && (thing.transform.position - at).sqrMagnitude <= NeedReach * NeedReach && (fits == null || fits(thing))) return true;
            }
            return false;
        }
    }
}
