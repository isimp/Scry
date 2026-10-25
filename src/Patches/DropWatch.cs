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
    /// counted for that creature, and told as the creature's death (<see cref="WorldEvents"/>),
    /// with the players flagged on it as its attackers and where its loot fell. The hooks only
    /// watch, and change nothing. A death another player's machine runs is seen here by the
    /// ragdoll it makes for every player (<c>Ragdoll.Awake</c>, <see cref="RemainsSeen"/>): the
    /// creature's level and what it drops stand on the ragdoll, and the death is told with no
    /// one doing it, marked by the ragdoll, so that the owner's own telling of it, where its
    /// game has Scry, adds to it rather than counting again. Loot another player's machine makes
    /// is not counted among the drops seen.
    /// </summary>
    internal static class DropWatch
    {
        private static SeenDrops _seen;
        private static string _creature;
        private static readonly List<ItemDrop> Made = new List<ItemDrop>();

        /// <summary>The ragdolls lying with loot to come, the creature each was and the players who hit it.</summary>
        private static readonly Dictionary<Ragdoll, (string Creature, int Level, IReadOnlyList<long> Doers, Vector3 Stood, bool Seen)> Ragdolls = new Dictionary<Ragdoll, (string, int, IReadOnlyList<long>, Vector3, bool)>();

        /// <summary>The stars of the creature whose loot is coming out, a level above 1 for each.</summary>
        private static int _level;

        /// <summary>The players who hit the creature whose loot is coming out, and where it comes out.</summary>
        private static IReadOnlyList<long> _doers;

        private static Vector3 _at;
        /// <summary>The ragdoll the loot comes out of, as the death's mark; none for loot dropped at once.</summary>
        private static string _mark;
        /// <summary>Where the character stood as the creature died and whether it saw the death, for a telling that comes later (<see cref="CompendiumKeeper.Tell(WorldEvent, Vector3?, bool?)"/>); none for one told at once.</summary>
        private static Vector3? _stood;
        private static bool? _sawDeath;

        private static float _savedAt;
        private static string File => Path.Combine(Settings.DataFolder, "seen-drops.txt");

        /// <summary>What was seen, read from its file the first time it is asked for.</summary>
        public static SeenDrops Seen
        {
            get
            {
                if (_seen != null) return _seen;
                if (!Guard.Run(Feature.DropsSeenInPlay, "reading the drops seen in play", () => SeenDrops.Load(System.IO.File.Exists(File) ? System.IO.File.ReadAllText(File) : null), out _seen)) _seen = new SeenDrops();
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
                Guard.Each(Feature.DropsSeenInPlay, "drops seen in play", "CharacterDrop.m_dropsEnabled", () => _dropsEnabled = AccessTools.FieldRefAccess<CharacterDrop, bool>("m_dropsEnabled"));
            }
            if (_dropsEnabled == null || !_dropsEnabled(drops)) return;
            var creature = drops.GetComponent<Character>();
            var doers = WorldEvents.Attackers(creature);
            var name = Utils.GetPrefabName(drops.gameObject);
            CompendiumKeeper.KillToldByThisGame(name, doers);
            Begin(name, creature != null ? creature.GetLevel() : 1, doers, drops.transform.position);
        }

        /// <summary>A ragdoll given a creature's loot to drop when it goes.</summary>
        public static void RagdollSetUp(Ragdoll ragdoll, CharacterDrop drops, int level)
        {
            if (ragdoll == null || drops == null || !ragdoll.m_dropItems) return;
            if (Ragdolls.Count > 256) Prune();
            var doers = WorldEvents.Attackers(drops.GetComponent<Character>());
            var name = Utils.GetPrefabName(drops.gameObject);
            CompendiumKeeper.KillToldByThisGame(name, doers);
            // Judged as at the death, as a death seen by its remains is, though the loot comes out later.
            var player = Player.m_localPlayer;
            var stood = player != null ? player.transform.position : ragdoll.transform.position;
            var at = ragdoll.transform.position;
            Ragdolls[ragdoll] = (name, level, doers, stood, CompendiumSight.SeesDeed(at.x, at.y, at.z));
        }

        public static void BeginRagdoll(Ragdoll ragdoll)
        {
            if (ragdoll == null || !Ragdolls.TryGetValue(ragdoll, out var dead)) return;
            Ragdolls.Remove(ragdoll);
            Begin(dead.Creature, dead.Level, dead.Doers, ragdoll.transform.position, WorldEvents.MarkOf(ragdoll), dead.Stood, dead.Seen);
        }

        private static void Begin(string creature, int level, IReadOnlyList<long> doers, Vector3 at, string mark = null, Vector3? stood = null, bool? seen = null)
        {
            _creature = creature;
            _level = level;
            _doers = doers;
            _at = at;
            _mark = mark;
            _stood = stood;
            _sawDeath = seen;
            Made.Clear();
        }

        /// <summary>
        /// A ragdoll come to be on this game for a death another game ran: the death told as seen,
        /// by the creature the catalog found leaving such remains, with the level and the loot the
        /// ragdoll carries (<c>Ragdoll.Setup</c> writes them for every player), marked by the
        /// ragdoll. One this game set up itself is told by its loot as it comes out.
        /// </summary>
        public static void RemainsMade(Ragdoll ragdoll)
        {
            var view = ragdoll != null ? ragdoll.GetComponent<ZNetView>().OrNull() : null;
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            // A ragdoll that takes no loot (Ragdoll.m_dropItems off) is no telling of the death: its game drops the loot at once and tells that itself.
            if (zdo == null || view.IsOwner() || !ragdoll.m_dropItems) return;
            var creature = RemainsSeen.CreatureOf(Utils.GetPrefabName(ragdoll.gameObject), CompendiumKeeper.LeftBy);
            if (creature == null) return;
            var loot = new List<(string, int)>();
            var count = zdo.GetInt(ZDOVars.s_drops);
            for (var i = 0; i < count && i < 100; i++)
            {
                var item = GamePrefabs.Hashed(zdo.GetInt(RemainsSeen.DropKey(RemainsSeen.DropHash, i)));
                var amount = zdo.GetInt(RemainsSeen.DropKey(RemainsSeen.DropAmount, i));
                if (item != null && amount > 0) loot.Add((item.name, amount));
            }
            // The game's own credit for the kill, come within seconds, makes the death the character's (KillCredits); else the remains wait for it as long.
            var doers = CompendiumKeeper.CreditTaken(creature) ? new[] { ZNet.GetUID() } : null;
            var told = WorldEvents.Seen(Happened.Died, creature, ragdoll.transform.position, loot, zdo.GetInt(ZDOVars.s_level, 1), doers, zdo.m_uid.ToString());
            if (doers == null) CompendiumKeeper.RemainsTold(creature, told);
        }

        /// <summary>An item come to be: kept while a death's loot is being dropped.</summary>
        public static void ItemMade(ItemDrop item)
        {
            if (_creature != null && item != null) Made.Add(item);
        }

        /// <summary>The loot is out: counted for its creature, as whole stacks, and the death told.</summary>
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
            WorldEvents.Tell(Happened.Died, _creature, _at, loot, _level, _doers, mark: _mark, stood: _stood, seen: _sawDeath);
            _creature = null;
            _doers = null;
            _mark = null;
            _stood = null;
            _sawDeath = null;
            Made.Clear();
            Version++;
        }

        /// <summary>Writes what was seen once it changed, at most every half minute, and whenever asked to now.</summary>
        public static void Save(bool now = false)
        {
            if (_seen == null || !_seen.Changed || (!now && Time.unscaledTime - _savedAt < 30f)) return;
            _savedAt = Time.unscaledTime;
            Guard.Run(Feature.DropsSeenInPlay, "keeping the drops seen in play", () =>
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
            Guard.Run(Feature.DropsSeenInPlay, "watching what creatures drop", () => DropWatch.BeginDeath(__instance));
        }

        // Last, after any mod's own hook here has spawned what it adds.
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            Guard.Run(Feature.DropsSeenInPlay, "watching what creatures drop", () => DropWatch.End());
        }
    }

    [HarmonyPatch(typeof(Ragdoll), "Setup")]
    internal static class RagdollLootSetUp
    {
        private static void Postfix(Ragdoll __instance, CharacterDrop characterDrop, int level)
        {
            Guard.Run(Feature.DropsSeenInPlay, "watching what creatures drop", () => DropWatch.RagdollSetUp(__instance, characterDrop, level));
        }
    }

    /// <summary>The game crediting the character with a kill (<c>Game.RPC_RegisterKill</c>, on the character's own game whichever ran the death): the remains seen of it are the character's.</summary>
    [HarmonyPatch(typeof(Game), "RPC_RegisterKill")]
    internal static class KillCreditWatched
    {
        private static void Postfix(string enemyName)
        {
            Guard.Run(Feature.DropsSeenInPlay, "taking the game's credit for a kill", () => CompendiumKeeper.KillCredited(enemyName));
        }
    }

    [HarmonyPatch(typeof(Ragdoll), "Awake")]
    internal static class RemainsSeenWatched
    {
        private static void Postfix(Ragdoll __instance)
        {
            Guard.Run(Feature.DropsSeenInPlay, "seeing a creature's remains", () => DropWatch.RemainsMade(__instance));
        }
    }

    [HarmonyPatch(typeof(Ragdoll), "SpawnLoot")]
    internal static class RagdollLoot
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Ragdoll __instance)
        {
            Guard.Run(Feature.DropsSeenInPlay, "watching what creatures drop", () => DropWatch.BeginRagdoll(__instance));
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            Guard.Run(Feature.DropsSeenInPlay, "watching what creatures drop", () => DropWatch.End());
        }
    }

    [HarmonyPatch(typeof(ItemDrop), "Awake")]
    internal static class ItemMade
    {
        private static void Postfix(ItemDrop __instance) => DropWatch.ItemMade(__instance);
    }
}
