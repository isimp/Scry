using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character being played knows, for the compendium. Its book is read from Scry's
    /// cache folder when the character is first played (a file per character,
    /// <see cref="CompendiumBook.FileName"/>, which Gale's profile sync leaves alone), caught up
    /// with what the game keeps of the character (<see cref="CharacterRecords"/>) once the catalog
    /// is read, quietly the first time and then whenever those records grow, looked at once a
    /// second along with what the character has, is in, points at and sees (<see cref="Look"/>,
    /// <see cref="CompendiumSight"/>), and fed what it fights and hits as it happens; and
    /// written back once it changed, at most every half minute, on leaving a world and on
    /// quitting. A book that could not be read is never written over.
    /// </summary>
    internal static class CompendiumKeeper
    {
        static CompendiumKeeper() => WorldCaches.Register(nameof(CompendiumKeeper), Forget);

        /// <summary>How long a quitting game waits for the book to be written.</summary>
        private const int QuitWaitMs = 2000;

        private static readonly FileQueue Files = new FileQueue();
        private static readonly CompendiumRecords Records = new CompendiumRecords();

        /// <summary>The file of the character whose book is held or being read.</summary>
        private static string _file;
        private static Task<CompendiumBook> _reading;
        private static CompendiumBook _book;
        private static bool _unreadable;
        private static bool _caughtUp;
        private static CompendiumCatchUp _catchUp;
        private static EntryCatalog _catchUpOf;
        private static int _recordsSize = -1;
        private static string _recordsPower;
        private static float _lookedAt;
        private static float _savedAt;

        /// <summary>What the character being played knows; none before its file is read.</summary>
        public static CompendiumBook Book => _book;

        /// <summary>How far the character's milestone floor may be raised: the tiers whose bosses fell for it, in a row (<see cref="Milestones"/>).</summary>
        public static int Ceiling { get; private set; }

        private static List<BossMarks> _bosses;
        private static EntryCatalog _bossesOf;

        /// <summary>Where the book of the character being played is kept; none before a character is played.</summary>
        public static string FilePath => _file != null ? PathOf(_file) : null;

        /// <summary>Reads, catches up and writes as due; called each frame.</summary>
        public static void Update()
        {
            foreach (var (doing, error) in Files.Failures()) Faults.Tell(Feature.CompendiumCatchUp, doing, error);
            var player = Player.m_localPlayer;
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (player == null || profile == null) return;

            var file = CompendiumBook.FileName(profile.m_filename, profile.m_fileSource.ToString(), profile.GetPlayerID());
            if (file != _file) Begin(file, CompendiumBook.OldFileName(profile.m_filename, profile.m_fileSource.ToString()));
            if (_reading != null && !Opened()) return;

            var catalog = WorldCatalog.Current;
            if (catalog == null) return;
            if (!ReferenceEquals(catalog, _catchUpOf))
            {
                _catchUp = new CompendiumCatchUp(catalog, piece => Knowledge.Tools.ToolsOf(piece).Select(u => u.Tool).Distinct().ToList(),
                    item => Knowledge.SourceLines(item).Any(line => line.Record?.Way == SourceWay.Sold));
                _catchUpOf = catalog;
                _recordsSize = -1;
            }
            if (Time.unscaledTime - _lookedAt >= 1f)
            {
                _lookedAt = Time.unscaledTime;
                var size = CharacterRecords.Size(player, profile);
                var power = CharacterRecords.Power(player);
                if (size != _recordsSize || power != _recordsPower)
                {
                    _recordsSize = size;
                    _recordsPower = power;
                    CharacterRecords.Read(player, profile, Records);
                    _catchUp.Fill(Records, _book, quietly: !_caughtUp);
                    if (!ReferenceEquals(_bossesOf, catalog))
                    {
                        _bosses = CompendiumTiers.Bosses(catalog);
                        _bossesOf = catalog;
                    }
                    Ceiling = Milestones.Ceiling(Records, _bosses);
                    _caughtUp = true;
                }
                Look(player);
                Guard.Run(Feature.CompendiumMap, "catching up from the explored map", () => CompendiumMap.Update(_book));
            }
            Save();
        }

        /// <summary>The trader whose store is open.</summary>
        private static readonly AccessTools.FieldRef<StoreGui, Trader> TraderOf = AccessTools.FieldRefAccess<StoreGui, Trader>("m_trader");

        /// <summary>
        /// What the character has and is in, as news: what it carries, the status effects on it,
        /// the raid around it (<c>RandEventSystem.GetActiveEvent</c>, the one the game plays for
        /// it), paired with the creatures it brings that the character met, and the place it
        /// stands in are known, a dungeon only once inside it (seen at its entrance); a trader
        /// whose store is open is known, its wares seen; what it points at is seen.
        /// </summary>
        private static void Look(Player player)
        {
            foreach (var item in player.GetInventory().GetAllItems())
            {
                if (item?.m_dropPrefab != null) _catchUp.Met(item.m_dropPrefab.name, KnownLevel.Known, _book);
            }
            foreach (var effect in player.GetSEMan().GetStatusEffects())
            {
                if (effect != null) _catchUp.Met(EntryKeys.For(Kind.StatusEffect, Utils.GetPrefabName(effect.name)), KnownLevel.Known, _book);
            }
            var raid = RandEventSystem.instance.OrNull()?.GetActiveEvent();
            if (raid != null) _catchUp.LivedThrough(EntryKeys.For(Kind.Raid, raid.m_name), _book);
            // A trader's store open: the trader known, its wares on show seen and paired with it.
            var trader = StoreGui.IsVisible() && StoreGui.instance != null ? TraderOf(StoreGui.instance) : null;
            if (trader != null) _catchUp.Traded(Utils.GetPrefabName(trader.gameObject), trader.GetAvailableItems().Where(t => t?.m_prefab != null).Select(t => t.m_prefab.name), _book);
            var place = Location.GetLocation(player.transform.position);
            if (place != null) _catchUp.StoodIn(EntryKeys.For(Kind.Location, Utils.GetPrefabName(place.gameObject)), place.m_hasInterior, player.InInterior(), _book);
            var hovered = player.GetHoverObject();
            var thing = hovered != null ? hovered.GetComponentInParent<ZNetView>() : null;
            if (thing != null) _catchUp.Met(Utils.GetPrefabName(thing.gameObject), KnownLevel.Seen, _book);
            Guard.Run(Feature.CompendiumSeen, "looking at what the character sees", () => CompendiumSight.Look(player, _catchUp, _book));
        }

        /// <summary>Learns of a creature the character fought, hitting it or hit by it, as news, and that it met it where it stands.</summary>
        public static void Fought(Character creature)
        {
            if (_book == null || _catchUp == null || creature == null || creature == Player.m_localPlayer) return;
            var key = Utils.GetPrefabName(creature.gameObject);
            _catchUp.Met(key, KnownLevel.Known, _book);
            MetIn(key, creature.transform.position);
        }

        /// <summary>A creature's attack hit the character: the two seen together, for the strict rules' attack table.</summary>
        public static void HitBy(Character creature)
        {
            if (_book == null || creature == null || creature == Player.m_localPlayer) return;
            var weapon = (creature as Humanoid).OrNull()?.GetCurrentWeapon()?.m_dropPrefab.OrNull();
            if (weapon != null) _book.Witness(Utils.GetPrefabName(creature.gameObject), weapon.name);
        }

        /// <summary>Learns of a resource the character hit, as news, what it hit it with and where it stands.</summary>
        public static void Hit(Component resource)
        {
            if (_book == null || _catchUp == null || resource == null) return;
            var key = Utils.GetPrefabName(resource.gameObject);
            _catchUp.Met(key, KnownLevel.Known, _book);
            var tool = Player.m_localPlayer.OrNull()?.GetCurrentWeapon()?.m_dropPrefab.OrNull();
            if (tool != null) _book.Witness(key, tool.name);
            MetIn(key, resource.transform.position);
        }

        /// <summary>A piece the character placed, as news: known, and paired with the tool in hand that built it.</summary>
        public static void Placed(string piece, string tool)
        {
            if (_book == null || _catchUp == null || string.IsNullOrEmpty(piece)) return;
            _catchUp.Met(piece, KnownLevel.Known, _book);
            if (!string.IsNullOrEmpty(tool)) _book.Witness(piece, tool);
        }

        /// <summary>A creature the character tamed or petted, as news: known, and met where it stands.</summary>
        public static void Tamed(Character creature)
        {
            if (_book == null || _catchUp == null || creature == null) return;
            var key = Utils.GetPrefabName(creature.gameObject);
            _catchUp.Met(key, KnownLevel.Known, _book);
            MetIn(key, creature.transform.position);
        }

        /// <summary>A creature the character saw eat: paired with what it ate, for the strict rules.</summary>
        public static void Fed(Character creature, string food)
        {
            if (_book == null || creature == null || string.IsNullOrEmpty(food)) return;
            _book.Witness(Utils.GetPrefabName(creature.gameObject), food);
        }

        /// <summary>The character saw two things go together, by their entries' keys (<see cref="CompendiumBook.Witness"/>).</summary>
        public static void Saw(string a, string b) => _book?.Witness(a, b);

        /// <summary>The character met a thing where it stands: with the biome there, and the place it is in.</summary>
        public static void MetIn(string key, Vector3 at)
        {
            if (_book == null || string.IsNullOrEmpty(key)) return;
            var biome = Heightmap.FindBiome(at);
            if (biome != Heightmap.Biome.None) _book.Witness(key, EntryKeys.For(Kind.Biome, biome.ToString()));
            var place = Location.GetLocation(at);
            if (place != null) _book.Witness(key, EntryKeys.For(Kind.Location, Utils.GetPrefabName(place.gameObject)));
        }

        /// <summary>Writes the book once it changed, at most every half minute unless asked to now, on a worker thread.</summary>
        public static void Save(bool now = false)
        {
            if (_book == null || _unreadable || !_book.Changed || (!now && Time.unscaledTime - _savedAt < 30f)) return;
            _savedAt = Time.unscaledTime;
            var text = _book.Save();
            var path = PathOf(_file);
            Files.Then("keeping what the character knows", () => FileSwap.Write(path, aside => File.WriteAllText(aside, text)));
        }

        /// <summary>Writes the book and waits a moment for it to be written, as the game quits.</summary>
        public static void Finish()
        {
            Save(now: true);
            Files.Then(() => true).Wait(QuitWaitMs);
        }

        /// <summary>Starts reading another character's book, once the one held is written.</summary>
        private static void Begin(string file, string oldFile)
        {
            Save(now: true);
            _file = file;
            _book = null;
            _unreadable = false;
            _caughtUp = false;
            _recordsSize = -1;
            var path = PathOf(file);
            var oldPath = PathOf(oldFile);
            _reading = Files.Then(() => Read(path, oldPath));
        }

        /// <summary>Takes the book read, once it is; an empty one where it could not be read, never written over.</summary>
        private static bool Opened()
        {
            if (!_reading.IsCompleted) return false;
            if (_reading.Status == TaskStatus.RanToCompletion)
            {
                _book = _reading.Result;
            }
            else
            {
                Faults.Tell(Feature.CompendiumCatchUp, "reading what the character knows", _reading.Exception?.GetBaseException());
                _book = new CompendiumBook();
                _unreadable = true;
            }
            _reading = null;
            return true;
        }

        /// <summary>
        /// The book kept for a character; an empty one for a character never kept. One kept before
        /// the character's id was in its name is taken over first (<see cref="CompendiumBook.TakeOver"/>);
        /// where it cannot be moved, it is read where it is. On a worker thread.
        /// </summary>
        private static CompendiumBook Read(string path, string oldPath)
        {
            var failed = Steps.Run(() => CompendiumBook.TakeOver(oldPath, path), null);
            var file = FileSwap.ToRead(path) ?? (failed != null ? FileSwap.ToRead(oldPath) : null);
            return CompendiumBook.Load(file != null ? File.ReadAllText(file) : null);
        }

        private static string PathOf(string file) => Path.Combine(Settings.CacheFolder, "compendium", file);

        /// <summary>On leaving a world: writes the book, and lets go of the catalog it was caught up with.</summary>
        private static void Forget()
        {
            Save(now: true);
            _catchUp = null;
            _catchUpOf = null;
            _recordsSize = -1;
            _bosses = null;
            _bossesOf = null;
            Ceiling = 0;
        }
    }
}
