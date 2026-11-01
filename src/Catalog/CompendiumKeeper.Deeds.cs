using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>What the character did or saw done that the keeper learns from: raids lived through and their raiders, creatures seen in their places, catches, tamings told, the weapons that hit it, and weather on it.</summary>
    internal static partial class CompendiumKeeper
    {
        /// <summary>
        /// A raid the character is in, standing at a point: known, and seen together with the biome
        /// there where the raid counts that biome as its own, as the game does for its weather
        /// (<c>RandomEvent.InEventBiome</c>), or with any biome for a raid that names none.
        /// </summary>
        public static void LivedThrough(RandomEvent raid, Vector3 at)
        {
            if (raid == null || Into == null || _catchUp == null) return;
            var key = EntryKeys.For(Kind.Raid, raid.m_name);
            _catchUp.LivedThrough(key, Into);
            if (raid.m_biome == Heightmap.Biome.None || raid.InEventBiome()) MetInBiome(key, at);
        }

        /// <summary>The bosses seen with their altars in this world, by their place in the world's data, each looked for once.</summary>
        private static readonly HashSet<ZDOID> BossesAtAltars = new HashSet<ZDOID>();

        /// <summary>What the deeds of this world left to remember, forgotten as it is left.</summary>
        private static void ForgetDeeds() => BossesAtAltars.Clear();

        /// <summary>
        /// A boss seen, standing within reach of an altar that calls its kind (<c>OfferingBowl.m_bossPrefab</c>,
        /// within <c>m_spawnBossMaxDistance</c> of it): seen together with the altar and its place by
        /// whoever stands by, whoever made the offering; with the offering too where it stood on the
        /// altar's item stands for all to see (<c>OfferingBowl.m_useItemStands</c>).
        /// </summary>
        public static void BossSeen(Character boss)
        {
            var view = boss != null ? boss.GetComponent<ZNetView>().OrNull() : null;
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null || Into == null || !BossesAtAltars.Add(zdo.m_uid)) return;
            var key = Utils.GetPrefabName(boss.gameObject);
            var altar = UnityEngine.Object.FindObjectsByType<OfferingBowl>(FindObjectsSortMode.None)
                .Where(a => a != null && a.m_bossPrefab != null && a.m_bossPrefab.name == key && Vector3.Distance(a.transform.position, boss.transform.position) <= a.m_spawnBossMaxDistance)
                .OrderBy(a => (a.transform.position - boss.transform.position).sqrMagnitude).FirstOrDefault();
            if (altar != null) TogetherWatch.AltarSeen(altar, withOffering: altar.m_useItemStands);
        }

        /// <summary>One of the running raid's own raiders seen up close: what the raid brings of its kind found out, and how the raid is rolled.</summary>
        public static void RaiderSeen(string creature)
        {
            if (Into == null || RaidNow == null || string.IsNullOrEmpty(creature)) return;
            Into.FoundOut(RaidNow, EntryKeys.Raider(creature));
            Into.FoundOut(RaidNow, EntryKeys.Did(Deed.Raided));
        }

        /// <summary>A creature seen up close standing in a place whose spawn points place its kind: the stars they give found out on the place.</summary>
        public static void SeenInPlace(string creature, Vector3 at)
        {
            if (Into == null || string.IsNullOrEmpty(creature)) return;
            var place = PlaceKeyAt(at);
            if (place == null || Into.Proved(place, GuardedProof)) return;
            if (WorldCatalog.Current?.Find(place)?.Source is PlaceSource source && source.Contents != null && source.Contents.SpawnLevels.Any(l => l.Creature == creature))
                Into.FoundOut(place, EntryKeys.Did(Deed.Guarded));
        }

        /// <summary>The proof a place's spawn stars wait for, asked after each second for each creature up close, so made once.</summary>
        private static readonly string GuardedProof = EntryKeys.Did(Deed.Guarded);

        /// <summary>A catch of the character's: counted as one catch with what it brought besides the fish, each seen together with it.</summary>
        public static void CatchBrought(string fish, IReadOnlyList<(string Item, int Amount)> brought)
        {
            if (Into == null || string.IsNullOrEmpty(fish)) return;
            foreach (var (item, _) in brought) Into.SawWith(fish, item, Tie.Gives);
            Into.Yielded(fish, new[] { (fish, 1) }.Concat(brought));
        }

        /// <summary>
        /// The game told the character a creature it tamed is tame now (<see cref="TameMessage"/>),
        /// as it tells the closest player within 30 m: the tame one of that name nearest the
        /// character, within that reach, seen tamed, however long the taming took.
        /// </summary>
        public static void ToldTamed(string text)
        {
            var token = TameMessage.Tamed(text);
            var player = Player.m_localPlayer;
            if (token == null || player == null) return;
            var tamed = Character.GetAllCharacters()
                .Where(c => c != null && !c.IsPlayer() && c.m_name == token && c.IsTamed() && Vector3.Distance(c.transform.position, player.transform.position) <= TameReach)
                .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (tamed != null) Tamed(tamed, tamedNow: true);
        }

        /// <summary>How far from a creature tamed the game looks for the closest player to tell so.</summary>
        private const float TameReach = 30f;

        /// <summary>
        /// The weapon a creature holds: only the game running it equips it, so on any other game it
        /// is read from what that game keeps in the creature's data for every game to dress it by
        /// (its right hand's item, else its left's but a torch); on the game running it, from its hands.
        /// </summary>
        private static GameObject WeaponOf(Humanoid creature)
        {
            if (creature == null) return null;
            var view = creature.GetComponent<ZNetView>().OrNull();
            var zdo = view != null && view.IsValid() && !view.IsOwner() ? view.GetZDO() : null;
            if (zdo != null)
            {
                foreach (var slot in new[] { ZDOVars.s_rightItem, ZDOVars.s_leftItem })
                {
                    var item = GamePrefabs.Hashed(zdo.GetInt(slot));
                    var shared = item != null ? item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared : null;
                    if (shared != null && shared.m_itemType != ItemDrop.ItemData.ItemType.Torch) return item;
                }
            }
            return creature.GetCurrentWeapon()?.m_dropPrefab.OrNull();
        }

        /// <summary>
        /// A weather's effect on the character (Wet, Cold, Freezing) while the weather now gives it
        /// (<c>EnvMan.IsWet</c>, <c>IsCold</c>, <c>IsFreezing</c>): seen together with the biome the
        /// character stands in where the weather is that biome's own, else with the raid putting its
        /// weather in the biome's place. Got another way, as wet from swimming, it pairs with neither.
        /// </summary>
        private static void WeatherOnYou(StatusEffect effect, string key, Vector3 at)
        {
            var env = EnvMan.instance;
            if (env == null) return;
            var hash = effect.NameHash();
            var given = (hash == SEMan.s_statusEffectWet && EnvMan.IsWet()) || (hash == SEMan.s_statusEffectCold && EnvMan.IsCold())
                || (hash == SEMan.s_statusEffectFreezing && EnvMan.IsFreezing());
            if (!given) return;
            if (BiomesOwnWeather(env))
            {
                var biome = BiomeKeyAt(at);
                if (biome != null) _book.Witness(key, biome);
            }
            else if (RaidNow != null) _book.Witness(key, RaidNow);
        }

        private static AccessTools.FieldRef<EnvMan, string> _forcedWeather;

        private static Func<EnvMan, string> _weatherInPlace;

        private static bool _triedWeather;

        /// <summary>
        /// Whether the weather now is the biome's own (<see cref="BiomeWeather.Own"/>), as the game
        /// keeps the one a zone forces (<c>EnvMan.m_forceEnv</c>) and works out the one put in the
        /// biome's place (<c>EnvMan.GetEnvironmentOverride</c>); not where those cannot be read.
        /// </summary>
        private static bool BiomesOwnWeather(EnvMan env)
        {
            if (!_triedWeather)
            {
                _triedWeather = true;
                Guard.Each(Feature.CompendiumSeen, "telling a biome's own weather", "EnvMan.m_forceEnv", () => _forcedWeather = AccessTools.FieldRefAccess<EnvMan, string>("m_forceEnv"));
                Guard.Each(Feature.CompendiumSeen, "telling a biome's own weather", "EnvMan.GetEnvironmentOverride", () => _weatherInPlace = AccessTools.MethodDelegate<Func<EnvMan, string>>(AccessTools.Method(typeof(EnvMan), "GetEnvironmentOverride")));
            }
            return _forcedWeather != null && _weatherInPlace != null && BiomeWeather.Own(_forcedWeather(env), _weatherInPlace(env));
        }
    }
}
