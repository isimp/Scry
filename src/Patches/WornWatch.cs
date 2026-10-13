using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Watches what the player on this machine equips and keeps what it was seen to change
    /// (<see cref="SeenWorn"/>) in <c>seen-worn.txt</c> beside Scry's other files, so an item a
    /// mod gives its stats in code, once it is worn, shows them too. What the player has is noted
    /// as the game begins equipping (<c>Humanoid.EquipItem</c>), again after each thing it takes
    /// off on the way (<c>UnequipItem</c>), and compared with what they have once it is on, last
    /// among the hooks there, so a mod's own is inside. Only equips by the player's own hand are
    /// watched: loading a character equips without effects. The hooks only watch and change nothing.
    /// </summary>
    internal static class WornWatch
    {
        private static SeenWorn _seen;

        /// <summary>The player being watched equipping, from the start of the equip to its end; null otherwise.</summary>
        private static Player _equipping;

        /// <summary>What they had before the thing went on, once anything it replaced came off.</summary>
        private static WornState _before;

        private static float _savedAt;
        private static string File => Path.Combine(Settings.DataFolder, "seen-worn.txt");

        /// <summary>What was seen, read from its file the first time it is asked for.</summary>
        public static SeenWorn Seen
        {
            get
            {
                if (_seen != null) return _seen;
                if (!Guard.Run(Feature.WhatEquippingDid, "reading what equipping was seen to change", () => SeenWorn.Load(System.IO.File.Exists(File) ? System.IO.File.ReadAllText(File) : null), out _seen)) _seen = new SeenWorn();
                return _seen;
            }
        }

        /// <summary>Counts up with every equip seen, so details telling what was seen are told again.</summary>
        public static int Version { get; private set; }

        /// <summary>An equip begins: watched for the local player equipping by hand.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Begin(Humanoid who, bool byHand)
        {
            _equipping = null;
            if (!byHand || !(who is Player player) || player != Player.m_localPlayer) return;
            _equipping = player;
            _before = Snapshot(player);
        }

        /// <summary>Something came off: inside an equip watched, what is had now is what the new thing goes on over.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TookOff(Humanoid who)
        {
            if (_equipping != null && ReferenceEquals(who, _equipping)) _before = Snapshot(_equipping);
        }

        /// <summary>The equip is over: what it changed is kept, if it went on.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void End(Humanoid who, ItemDrop.ItemData item, bool equipped)
        {
            var player = _equipping;
            _equipping = null;
            if (player == null || !ReferenceEquals(who, player) || !equipped || item?.m_shared == null || item.m_dropPrefab == null) return;
            var sets = new List<string>();
            if (item.m_shared.m_setStatusEffect != null) sets.Add(item.m_shared.m_setStatusEffect.name);
            var change = WornChange.Between(_before, Snapshot(player), GearModifiers.Of(item.m_shared), sets);
            Seen.Record(Utils.GetPrefabName(item.m_dropPrefab), item.m_quality, change);
            Version++;
        }

        /// <summary>
        /// What the player has now, as the game reckons it with every mod's hooks: the most they
        /// can carry, their armour, how each damage type lands on them, and their status effects.
        /// </summary>
        private static WornState Snapshot(Player player)
        {
            var state = new WornState { Carry = player.GetMaxCarryWeight(), Armour = player.GetBodyArmor() };
            var mods = player.GetDamageModifiers();
            var degrees = new[] { mods.m_blunt, mods.m_slash, mods.m_pierce, mods.m_chop, mods.m_pickaxe, mods.m_fire, mods.m_frost, mods.m_lightning, mods.m_poison, mods.m_spirit };
            for (var i = 0; i < degrees.Length; i++) state.Resists[ResistWords.Types[i]] = (Degree)(int)degrees[i];
            foreach (var effect in player.GetSEMan().GetStatusEffects())
            {
                if (effect == null) continue;
                var shown = Localization.instance != null ? Localization.instance.Localize(effect.m_name) : effect.m_name;
                state.Effects[Utils.GetPrefabName(effect.name)] = string.IsNullOrEmpty(shown) ? Utils.GetPrefabName(effect.name) : shown;
            }
            return state;
        }

        /// <summary>Writes what was seen once it changed, at most every half minute, and whenever asked to now.</summary>
        public static void Save(bool now = false)
        {
            if (_seen == null || !_seen.Changed || (!now && Time.unscaledTime - _savedAt < 30f)) return;
            _savedAt = Time.unscaledTime;
            Guard.Run(Feature.WhatEquippingDid, "keeping what equipping was seen to change", () =>
            {
                Directory.CreateDirectory(Settings.DataFolder);
                System.IO.File.WriteAllText(File, _seen.Save());
            });
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    internal static class EquipWatched
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Humanoid __instance, bool triggerEquipEffects)
        {
            Guard.Run(Feature.WhatEquippingDid, "watching what equipping changes", () => WornWatch.Begin(__instance, triggerEquipEffects));
        }

        // Last, after any mod's own hook here has given the item what it gives.
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            Guard.Run(Feature.WhatEquippingDid, "watching what equipping changes", () => WornWatch.End(__instance, item, __result));
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
    internal static class UnequipWatched
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Humanoid __instance)
        {
            Guard.Run(Feature.WhatEquippingDid, "watching what equipping changes", () => WornWatch.TookOff(__instance));
        }
    }
}
