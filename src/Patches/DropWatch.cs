using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Watches the loot that comes out when a creature dies on this machine, whichever mod put it
    /// there, and keeps what was seen (<see cref="SeenDrops"/>) in <c>seen-drops.txt</c> beside
    /// Scry's other files, so it grows from session to session. A death drops its loot at once
    /// (<c>CharacterDrop.OnDeath</c>, while its drops are on) or through its ragdoll as that goes
    /// (<c>Ragdoll.SpawnLoot</c>, the creature told by <c>Ragdoll.Setup</c>); every item that
    /// comes to be while either runs, the game's and any a mod spawns in its own hook there, is
    /// counted for that creature. The hooks only watch, and change nothing. Loot another
    /// player's machine makes, and a ragdoll set up before the game was started, are not seen.
    /// </summary>
    internal static class DropWatch
    {
        private static SeenDrops _seen;
        private static string _creature;
        private static readonly List<ItemDrop> Made = new List<ItemDrop>();

        /// <summary>The ragdolls lying with loot to come, and the creature each was.</summary>
        private static readonly Dictionary<Ragdoll, string> Ragdolls = new Dictionary<Ragdoll, string>();

        private static float _savedAt;
        private static string File => Path.Combine(Settings.DataFolder, "seen-drops.txt");

        /// <summary>What was seen, read from its file the first time it is asked for.</summary>
        public static SeenDrops Seen
        {
            get
            {
                if (_seen != null) return _seen;
                if (!Guard.Run("reading the drops seen in play", () => SeenDrops.Load(System.IO.File.Exists(File) ? System.IO.File.ReadAllText(File) : null), out _seen)) _seen = new SeenDrops();
                return _seen;
            }
        }

        /// <summary>Counts up with every kill seen, so details telling what was seen are told again.</summary>
        public static int Version { get; private set; }

        /// <summary>Whether a creature's drops are on (<c>CharacterDrop.m_dropsEnabled</c>), found the first time; a game without the field has it told as a fault, and no death dropping at once is watched.</summary>
        private static AccessTools.FieldRef<CharacterDrop, bool> _dropsEnabled;
        private static bool _triedField;

        /// <summary>A death dropping at once: watched while its drops are on, as the game drops them only then.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void BeginDeath(CharacterDrop drops)
        {
            if (drops == null) return;
            if (!_triedField)
            {
                _triedField = true;
                Guard.Each("drops seen in play", "CharacterDrop.m_dropsEnabled", () => _dropsEnabled = AccessTools.FieldRefAccess<CharacterDrop, bool>("m_dropsEnabled"));
            }
            if (_dropsEnabled != null && _dropsEnabled(drops)) Begin(Utils.GetPrefabName(drops.gameObject));
        }

        /// <summary>A ragdoll given a creature's loot to drop when it goes.</summary>
        public static void RagdollSetUp(Ragdoll ragdoll, CharacterDrop drops)
        {
            if (ragdoll == null || drops == null || !ragdoll.m_dropItems) return;
            if (Ragdolls.Count > 256) Prune();
            Ragdolls[ragdoll] = Utils.GetPrefabName(drops.gameObject);
        }

        public static void BeginRagdoll(Ragdoll ragdoll)
        {
            if (ragdoll == null || !Ragdolls.TryGetValue(ragdoll, out var creature)) return;
            Ragdolls.Remove(ragdoll);
            Begin(creature);
        }

        private static void Begin(string creature)
        {
            _creature = creature;
            Made.Clear();
        }

        /// <summary>An item come to be: kept while a death's loot is being dropped.</summary>
        public static void ItemMade(ItemDrop item)
        {
            if (_creature != null && item != null) Made.Add(item);
        }

        /// <summary>The loot is out: counted for its creature, as whole stacks.</summary>
        public static void End()
        {
            if (_creature == null) return;
            var loot = new List<(string, int)>();
            foreach (var item in Made)
            {
                if (item == null) continue;
                loot.Add((Utils.GetPrefabName(item.gameObject), Mathf.Max(1, item.m_itemData?.m_stack ?? 1)));
            }
            Seen.Record(_creature, loot);
            _creature = null;
            Made.Clear();
            Version++;
        }

        /// <summary>Writes what was seen once it changed, at most every half minute, and whenever asked to now.</summary>
        public static void Save(bool now = false)
        {
            if (_seen == null || !_seen.Changed || (!now && Time.unscaledTime - _savedAt < 30f)) return;
            _savedAt = Time.unscaledTime;
            Guard.Run("keeping the drops seen in play", () =>
            {
                Directory.CreateDirectory(Settings.DataFolder);
                System.IO.File.WriteAllText(File, _seen.Save());
            });
        }

        private static void Prune()
        {
            var gone = new List<Ragdoll>();
            foreach (var ragdoll in Ragdolls.Keys) if (ragdoll == null) gone.Add(ragdoll);
            foreach (var ragdoll in gone) Ragdolls.Remove(ragdoll);
        }
    }

    [HarmonyPatch(typeof(CharacterDrop), "OnDeath")]
    internal static class DeathLoot
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(CharacterDrop __instance)
        {
            Guard.Run("watching what creatures drop", () => DropWatch.BeginDeath(__instance));
        }

        // Last, after any mod's own hook here has spawned what it adds.
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            Guard.Run("watching what creatures drop", () => DropWatch.End());
        }
    }

    [HarmonyPatch(typeof(Ragdoll), "Setup")]
    internal static class RagdollLootSetUp
    {
        private static void Postfix(Ragdoll __instance, CharacterDrop characterDrop)
        {
            Guard.Run("watching what creatures drop", () => DropWatch.RagdollSetUp(__instance, characterDrop));
        }
    }

    [HarmonyPatch(typeof(Ragdoll), "SpawnLoot")]
    internal static class RagdollLoot
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Ragdoll __instance)
        {
            Guard.Run("watching what creatures drop", () => DropWatch.BeginRagdoll(__instance));
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            Guard.Run("watching what creatures drop", () => DropWatch.End());
        }
    }

    [HarmonyPatch(typeof(ItemDrop), "Awake")]
    internal static class ItemMade
    {
        private static void Postfix(ItemDrop __instance) => DropWatch.ItemMade(__instance);
    }
}
