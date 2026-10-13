using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character being played knows, for the compendium. Its book is read from Scry's
    /// cache folder when the character is first played (a file per character,
    /// <see cref="CompendiumBook.FileName"/>, which Gale's profile sync leaves alone), caught up
    /// with what the game keeps of the character (<see cref="CharacterRecords"/>) once the catalog
    /// is read, quietly the first time and then whenever those records grow, looked at once a
    /// second along with what the character has, is in and points at (<see cref="Look"/>); and
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
        private static float _lookedAt;
        private static float _savedAt;

        /// <summary>What the character being played knows; none before its file is read.</summary>
        public static CompendiumBook Book => _book;

        /// <summary>Reads, catches up and writes as due; called each frame.</summary>
        public static void Update()
        {
            foreach (var (doing, error) in Files.Failures()) Faults.Tell(Feature.CompendiumCatchUp, doing, error);
            var player = Player.m_localPlayer;
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (player == null || profile == null) return;

            var file = CompendiumBook.FileName(profile.m_filename, profile.m_fileSource.ToString());
            if (file != _file) Begin(file);
            if (_reading != null && !Opened()) return;

            var catalog = WorldCatalog.Current;
            if (catalog == null) return;
            if (!ReferenceEquals(catalog, _catchUpOf))
            {
                _catchUp = new CompendiumCatchUp(catalog);
                _catchUpOf = catalog;
                _recordsSize = -1;
            }
            if (Time.unscaledTime - _lookedAt >= 1f)
            {
                _lookedAt = Time.unscaledTime;
                var size = CharacterRecords.Size(player, profile);
                if (size != _recordsSize)
                {
                    _recordsSize = size;
                    CharacterRecords.Read(player, profile, Records);
                    _catchUp.Fill(Records, _book, quietly: !_caughtUp);
                    _caughtUp = true;
                }
                Look(player);
            }
            Save();
        }

        /// <summary>
        /// What the character has and is in, as news: what it carries, the status effects on it,
        /// the raid around it (<c>RandEventSystem.GetActiveEvent</c>, the one the game plays for
        /// it) and the place it stands in are known; what it points at is seen.
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
            if (raid != null) _catchUp.Met(EntryKeys.For(Kind.Raid, raid.m_name), KnownLevel.Known, _book);
            var place = Location.GetLocation(player.transform.position);
            if (place != null) _catchUp.Met(EntryKeys.For(Kind.Location, Utils.GetPrefabName(place.gameObject)), KnownLevel.Known, _book);
            var hovered = player.GetHoverObject();
            var thing = hovered != null ? hovered.GetComponentInParent<ZNetView>() : null;
            if (thing != null) _catchUp.Met(Utils.GetPrefabName(thing.gameObject), KnownLevel.Seen, _book);
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
        private static void Begin(string file)
        {
            Save(now: true);
            _file = file;
            _book = null;
            _unreadable = false;
            _caughtUp = false;
            _recordsSize = -1;
            var path = PathOf(file);
            _reading = Files.Then(() => Read(path));
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

        /// <summary>The book kept for a character; an empty one for a character never kept. On a worker thread.</summary>
        private static CompendiumBook Read(string path)
        {
            var file = FileSwap.ToRead(path);
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
        }
    }
}
