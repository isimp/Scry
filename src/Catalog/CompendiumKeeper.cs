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
    /// is read, as news, the first time and then whenever those records grow, looked at once a
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

        /// <summary>The character's file as last worked out from what names it; worked out again only when one of those changes, not each frame.</summary>
        private static string _fileOf;
        private static string _namedBy;
        private static FileHelpers.FileSource _sourceOf;
        private static long _idOf;
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

        /// <summary>
        /// The book what the hooks see in play is recorded into: the self-test's own while it feeds
        /// made-up events through them (<see cref="RecordInto"/>), else the character's. The
        /// keeper's own looking, catching up and saving keep to the character's.
        /// </summary>
        private static CompendiumBook Into => _into ?? _book;

        private static CompendiumBook _into;

        /// <summary>Records what the hooks see into a book of the self-test's own until given none again; the character's book and the play counts are left alone meanwhile.</summary>
        public static void RecordInto(CompendiumBook book) => _into = book;

        /// <summary>How often each hook fired this session (<see cref="PlayCounts"/>), what the self-test feeds them left out.</summary>
        public static PlayCounts Play { get; } = new PlayCounts();

        /// <summary>Counts a hook firing in play, unless the self-test is feeding it (<see cref="RecordInto"/>).</summary>
        public static void Noted(PlayEvent what)
        {
            if (_into == null) Play.Note(what);
        }

        /// <summary>How far the character's milestone floor may be raised: the tiers whose bosses fell for it, in a row (<see cref="Milestones"/>).</summary>
        public static int Ceiling { get; private set; }

        private static List<BossMarks> _bosses;
        private static EntryCatalog _bossesOf;

        /// <summary>Where the book of the character being played is kept; none before a character is played.</summary>
        public static string FilePath => _file != null ? PathOf(_file) : null;

        /// <summary>Reads, catches up and writes as due; called each frame.</summary>
        public static void Update()
        {
            Faults.TellEach(Feature.CompendiumCatchUp, Files.Failures());
            var player = Player.m_localPlayer;
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (player == null || profile == null) return;

            if (_fileOf == null || !ReferenceEquals(profile.m_filename, _namedBy) || profile.m_fileSource != _sourceOf || profile.GetPlayerID() != _idOf)
            {
                _namedBy = profile.m_filename;
                _sourceOf = profile.m_fileSource;
                _idOf = profile.GetPlayerID();
                _fileOf = CompendiumBook.FileName(_namedBy, _sourceOf.ToString(), _idOf);
            }
            if (_fileOf != _file) Begin(_fileOf);
            if (_reading != null && !Opened()) return;

            var catalog = WorldCatalog.Current;
            if (catalog == null) return;
            if (!ReferenceEquals(catalog, _catchUpOf))
            {
                _catchUp = CatchUpFor(catalog);
                _catchUpOf = catalog;
                _recordsSize = -1;
            }
            // Each part timed apart, so the monitor and the self-test tell which one costs what.
            if (Time.unscaledTime - _lookedAt >= 1f)
            {
                _lookedAt = Time.unscaledTime;
                var records = Timing.Start();
                var size = CharacterRecords.Size(player, profile);
                var power = CharacterRecords.Power(player);
                if (size != _recordsSize || power != _recordsPower)
                {
                    _recordsSize = size;
                    _recordsPower = power;
                    CharacterRecords.Read(player, profile, Records);
                    _catchUp.Fill(Records, _book, firstLoad: !_caughtUp);
                    if (!ReferenceEquals(_bossesOf, catalog))
                    {
                        _bosses = CompendiumTiers.Bosses(catalog);
                        _bossesOf = catalog;
                    }
                    Ceiling = Milestones.Ceiling(Records, _bosses);
                    _caughtUp = true;
                }
                Timing.Add("update compendium records", records);
                var looked = Timing.Start();
                Look(player);
                Timing.Add("update compendium look", looked);
                Guard.Run(Feature.CompendiumMap, "catching up from the explored map", () => CompendiumMap.Update(_book), "update compendium map");
            }
            Guard.Run(Feature.CompendiumSeen, "looking at what the character sees", Sweeping, player, "update compendium sight");
            var saved = Timing.Start();
            Save();
            Timing.Add("update compendium save", saved);
        }

        /// <summary>The share of the things in the world looked at this frame (<see cref="CompendiumSight.Sweep"/>), made once.</summary>
        private static readonly System.Action<Player> Sweeping = player => CompendiumSight.Sweep(player, _catchUp, _book, Time.unscaledDeltaTime);

        /// <summary>
        /// What catches a character up in a catalog, told what the game knows besides: the tools
        /// that build a piece, whether a trader sells an item, and each biome's name as the game
        /// shows it in the player's language (<c>BiomeSector.GetName</c>), as the entered biomes are kept.
        /// </summary>
        internal static CompendiumCatchUp CatchUpFor(EntryCatalog catalog)
        {
            var shown = new Dictionary<string, string>(System.StringComparer.Ordinal);
            if (Localization.instance != null)
            {
                foreach (var biome in catalog.All) if (biome.Kind == Kind.Biome) shown[biome.Name] = Localization.instance.Localize(BiomeWords.Token(biome.Name));
            }
            return new CompendiumCatchUp(catalog, piece => Knowledge.Tools.ToolsOf(piece).Select(u => u.Tool).Distinct().ToList(),
                item => Knowledge.SourceLines(item).Any(line => line.Record?.Way == SourceWay.Sold), shown);
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
                if (item?.m_dropPrefab != null) _catchUp.Met(item.m_dropPrefab.name, KnownLevel.Known, _book, CameBy.Held);
            }
            foreach (var effect in player.GetSEMan().GetStatusEffects())
            {
                if (effect != null) _catchUp.Met(EntryKeys.For(Kind.StatusEffect, Utils.GetPrefabName(effect.name)), KnownLevel.Known, _book, CameBy.OnYou);
            }
            var raid = RandEventSystem.instance.OrNull()?.GetActiveEvent();
            if (raid != null) _catchUp.LivedThrough(EntryKeys.For(Kind.Raid, raid.m_name), _book);
            // A trader's store open: the trader known, its wares on show seen and paired with it.
            var trader = StoreGui.IsVisible() && StoreGui.instance != null ? TraderOf(StoreGui.instance) : null;
            if (trader != null) _catchUp.Traded(Utils.GetPrefabName(trader.gameObject), trader.GetAvailableItems().Where(t => t?.m_prefab != null).Select(t => t.m_prefab.name), _book);
            Weather();
            Tamings(player, creature => creature.IsTamed());
            if (!_book.WithOthers && ZNet.instance != null && ZNet.instance.GetPlayerList().Count > 1) _book.PlayedWithOthers();
            var place = Location.GetLocation(player.transform.position);
            if (place != null) _catchUp.StoodIn(EntryKeys.For(Kind.Location, Utils.GetPrefabName(place.gameObject)), place.m_hasInterior, player.InInterior(), _book);
            var hovered = player.GetHoverObject();
            var thing = hovered != null ? hovered.GetComponentInParent<ZNetView>() : null;
            if (thing != null)
            {
                var key = Utils.GetPrefabName(thing.gameObject);
                _catchUp.Met(key, KnownLevel.Seen, _book);
                MetIn(key, thing.transform.position);
            }
            Guard.Run(Feature.CompendiumSeen, "looking at what the character sees", () => CompendiumSight.Look(player, _catchUp, _book));
        }

        /// <summary>Learns of a creature the character fought, hitting it or hit by it, as news, and that it met it where it stands.</summary>
        public static void Fought(Character creature)
        {
            if (Into == null || _catchUp == null || creature == null || creature == Player.m_localPlayer) return;
            var key = Utils.GetPrefabName(creature.gameObject);
            _catchUp.Met(key, KnownLevel.Known, Into, CameBy.Fought);
            MetIn(key, creature.transform.position);
        }

        /// <summary>A creature's attack hit the character: the two seen together, for the strict rules' attack table.</summary>
        public static void HitBy(Character creature)
        {
            if (Into == null || creature == null || creature == Player.m_localPlayer) return;
            var weapon = (creature as Humanoid).OrNull()?.GetCurrentWeapon()?.m_dropPrefab.OrNull();
            if (weapon == null) return;
            Into.FoundOut(Utils.GetPrefabName(creature.gameObject), weapon.name);
            Noted(PlayEvent.HitTaken);
        }

        /// <summary>Learns of a resource the character hit, as news, what it hit it with and where it stands.</summary>
        public static void Hit(Component resource)
        {
            if (Into == null || _catchUp == null || resource == null) return;
            var key = Utils.GetPrefabName(resource.gameObject);
            _catchUp.Met(key, KnownLevel.Known, Into, CameBy.Hit);
            ActedOnThing(key, resource.transform.position);
            var tool = Player.m_localPlayer.OrNull()?.GetCurrentWeapon()?.m_dropPrefab.OrNull();
            if (tool != null) Into.Witness(key, tool.name);
            MetIn(key, resource.transform.position);
            var view = resource.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && !view.IsOwner()) HitRunElsewhere(resource);
        }

        /// <summary>The creatures the character hit lately, by the name the game tells a kill by (<see cref="KilledElsewhere"/>).</summary>
        private static readonly Lately<string> CreaturesHit = new Lately<string>(60.0);

        /// <summary>The things the character hit lately that another player's game runs, by their id in the world's data (<see cref="Gone"/>).</summary>
        private static readonly Lately<ZDOID> HitElsewhere = new Lately<ZDOID>(30.0);

        /// <summary>A creature the character hit, remembered by its name a minute, for a kill another player's game tells of.</summary>
        public static void Struck(Character creature)
        {
            if (creature == null || creature == Player.m_localPlayer) return;
            CreaturesHit.Hit(creature.m_name, Utils.GetPrefabName(creature.gameObject), Time.unscaledTime);
        }

        /// <summary>
        /// A kill the character took part in that another player's game ran, told by the
        /// creature's name: counted for the creature of that name the character hit last within a
        /// minute, else for the one creature going by it; none where several do.
        /// </summary>
        public static void KilledElsewhere(string name)
        {
            if (Into == null || _catchUp == null || string.IsNullOrEmpty(name)) return;
            var key = CreaturesHit.Of(name, Time.unscaledTime) ?? _catchUp.OnlyNamed(name, Kind.Creature);
            if (key == null) return;
            Into.RanElsewhere(Elsewhere.Kill, key);
            Noted(PlayEvent.KillElsewhere);
        }

        /// <summary>A thing the character hit that another player's game runs: remembered half a minute, for it going (<see cref="Gone"/>).</summary>
        internal static void HitRunElsewhere(Component thing)
        {
            var view = thing != null ? thing.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid()) return;
            HitElsewhere.Hit(view.GetZDO().m_uid, Utils.GetPrefabName(thing.gameObject), Time.unscaledTime);
        }

        /// <summary>
        /// A thing in the world gone, by its id, and whether this game ran it as it went: one the
        /// character hit within half a minute that another player's game ran broke there, so is
        /// counted as such; one this game ran was counted as it broke.
        /// </summary>
        public static void Gone(ZDOID id, bool ranHere)
        {
            if (Into == null) return;
            var key = HitElsewhere.Take(id, Time.unscaledTime);
            if (key == null || ranHere) return;
            Into.RanElsewhere(Elsewhere.Break, key);
            Noted(PlayEvent.BreakElsewhere);
        }

        /// <summary>A pick of the character's that another player's game runs: what it gave goes uncounted, counted as such.</summary>
        public static void PickedElsewhere(Pickable pickable)
        {
            if (Into == null || pickable == null) return;
            Into.RanElsewhere(Elsewhere.Pick, Utils.GetPrefabName(pickable.gameObject));
            Noted(PlayEvent.PickElsewhere);
        }

        /// <summary>A piece the character placed, as news: known, and paired with the tool in hand that built it.</summary>
        public static void Placed(string piece, string tool)
        {
            if (Into == null || _catchUp == null || string.IsNullOrEmpty(piece)) return;
            _catchUp.Met(piece, KnownLevel.Known, Into, CameBy.Built);
            if (!string.IsNullOrEmpty(tool)) Into.Witness(piece, tool);
        }

        /// <summary>A creature seen tamed near the character, or one it petted, as news: known, and met where it stands; one tamed shows how long taming takes.</summary>
        public static void Tamed(Character creature, bool tamedNow)
        {
            if (Into == null || _catchUp == null || creature == null) return;
            var key = Utils.GetPrefabName(creature.gameObject);
            _catchUp.Met(key, KnownLevel.Known, Into, CameBy.Tamed);
            MetIn(key, creature.transform.position);
            if (!tamedNow) return;
            Into.FoundOut(key, EntryKeys.Did(Deed.Tamed));
            Noted(PlayEvent.Tamed);
        }

        /// <summary>Something the character saw done to a thing, which shows a number the game never does (<see cref="Deed"/>).</summary>
        public static void SawDone(GameObject thing, Deed deed)
        {
            if (Into == null || thing == null) return;
            Into.FoundOut(Utils.GetPrefabName(thing), EntryKeys.Did(deed));
            if (deed == Deed.Born) Noted(PlayEvent.Born);
            else if (deed == Deed.GrewUp) Noted(PlayEvent.GrewUp);
            else if (deed == Deed.GrewBack) Noted(PlayEvent.GrewBack);
            else if (deed == Deed.Spawned) Noted(PlayEvent.Spawned);
            else if (deed == Deed.Collected) Noted(PlayEvent.Collected);
        }

        /// <summary>Something the strict rules keep back of an entry, found out in play, as news (<see cref="CompendiumBook.FoundOut"/>).</summary>
        public static void FoundOut(string key, string proof) => Into?.FoundOut(key, proof);

        /// <summary>A weak spot a hit of the character found on a creature, by the part it is on.</summary>
        public static void FoundWeakSpot(Character creature, string part)
        {
            if (Into == null || creature == null || string.IsNullOrEmpty(part)) return;
            Into.FoundOut(Utils.GetPrefabName(creature.gameObject), EntryKeys.WeakSpot(part));
            Noted(PlayEvent.WeakSpotHit);
        }

        /// <summary>A creature's loot seen to fall as it died, counted for the character.</summary>
        public static void Dropped(string creature, List<(string Item, int Amount)> loot)
        {
            if (Into == null) return;
            Into.Dropped(creature, loot);
            Noted(PlayEvent.LootCounted);
        }

        /// <summary>
        /// What a tree, rock or other thing was seen to give as it broke, or a pickable as it was
        /// picked, counted for the character, and each seen to come from it; broken, its health shows.
        /// </summary>
        public static void Yielded(string thing, List<(string Item, int Amount)> loot, bool picked = false)
        {
            if (Into == null) return;
            Into.Yielded(thing, loot);
            foreach (var (item, _) in loot) Into.Witness(thing, item);
            if (picked)
            {
                Noted(PlayEvent.PickCounted);
                return;
            }
            Into.FoundOut(thing, EntryKeys.Did(Deed.Broke));
            Noted(PlayEvent.BreakCounted);
        }

        /// <summary>A starred creature seen to fall, as its loot came out: what its stars change shows.</summary>
        public static void StarredFell(string creature)
        {
            if (Into == null || string.IsNullOrEmpty(creature)) return;
            Into.FoundOut(creature, EntryKeys.Did(Deed.StarredFell));
            Noted(PlayEvent.StarredFell);
        }

        /// <summary>
        /// A chest the character opened: the first time for each one, what it holds then is counted
        /// as what it held, each item seen in it. Only a chest the world fills (<c>Container.m_defaultItems</c>), not one built.
        /// </summary>
        public static void Opened(Container container)
        {
            if (Into == null || container == null || container.m_defaultItems?.m_drops == null || container.m_defaultItems.m_drops.Count == 0) return;
            var zdo = container.GetComponent<ZNetView>().OrNull()?.GetZDO();
            if (zdo == null || !Into.FirstOpen(CompendiumBook.ChestId(CompendiumMap.World, zdo.m_uid.ToString()))) return;
            var chest = Utils.GetPrefabName(container.gameObject);
            var loot = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var held in container.GetInventory().GetAllItems())
            {
                if (held?.m_dropPrefab == null) continue;
                loot.TryGetValue(held.m_dropPrefab.name, out var so);
                loot[held.m_dropPrefab.name] = so + held.m_stack;
            }
            Into.Held(chest, loot.Select(l => (l.Key, l.Value)));
            foreach (var item in loot.Keys) Into.Witness(chest, item);
            Noted(PlayEvent.ChestCounted);
        }

        /// <summary>Something the character used, as news: known, and met where it stands.</summary>
        public static void Used(GameObject used)
        {
            if (Into == null || _catchUp == null || used == null) return;
            var thing = used.GetComponentInParent<ZNetView>();
            if (thing == null) return;
            var key = Utils.GetPrefabName(thing.gameObject);
            ActedOnThing(key, thing.transform.position);
            if (_catchUp.Used(key, Into)) MetIn(key, thing.transform.position);
        }

        /// <summary>An item's tooltip card the game showed, by the item's name token, as news: known.</summary>
        public static void CardShown(string token)
        {
            if (Into == null || _catchUp == null) return;
            _catchUp.CardShown(token, Into);
        }

        /// <summary>A place a Vegvisir marked on the character's map, as news: seen, and paired with the Vegvisir.</summary>
        public static void Marked(GameObject vegvisir, string place)
        {
            if (Into == null || _catchUp == null || vegvisir == null) return;
            var key = EntryKeys.For(Kind.Location, place);
            _catchUp.Met(key, KnownLevel.Seen, Into);
            var stone = vegvisir.GetComponentInParent<ZNetView>();
            Into.Witness(Utils.GetPrefabName((stone != null ? stone.gameObject : vegvisir)), key);
        }

        /// <summary>What the character used or hit lately, for what comes from it (<see cref="ActedOn"/>).</summary>
        private static readonly ActedOn Acted = new ActedOn();

        /// <summary>The character acted on a thing, used or hit, where it stands: an item coming to be near it soon after may be from it.</summary>
        public static void ActedOnThing(string key, Vector3 at) => Acted.Add(key, at.x, at.z, Time.unscaledTime);

        /// <summary>
        /// An item came to be or was picked up: paired with each thing acted on lately near it that
        /// gives it, and with what the character put into a station that turned into it.
        /// </summary>
        public static void Arrived(string item, Vector3 at)
        {
            if (Into == null || string.IsNullOrEmpty(item) || Acted.Count == 0) return;
            foreach (var giver in Acted.Givers(item, at.x, at.z, Time.unscaledTime, Gives))
            {
                Into.Witness(giver, item);
                foreach (var input in ActedOn.TurnedFrom(giver, TurnedInto(giver, item), Into.Witnessed)) Into.Witness(input, item);
            }
        }

        /// <summary>What the character put into a thing it used: each paired with it.</summary>
        public static void PutInto(string thing, IEnumerable<string> items)
        {
            if (Into == null || string.IsNullOrEmpty(thing)) return;
            foreach (var item in items) Into.Witness(thing, item);
        }

        /// <summary>
        /// Whether a thing gives an item, as the catalog tells it: drops it, makes it over time, or
        /// turns something into it, itself or through what it leaves behind (<see cref="ActedOn.GivesThrough"/>).
        /// </summary>
        internal static bool Gives(string thing, string item) =>
            ActedOn.GivesThrough(thing, item, at => WorldCatalog.Current?.Find(at)?.LeavesBehind ?? (IEnumerable<string>)System.Array.Empty<string>(), GivesItself);

        /// <summary>Whether a thing itself gives an item, as the catalog tells it.</summary>
        private static bool GivesItself(string thing, string item)
        {
            var entry = WorldCatalog.Current?.Find(thing);
            if (entry != null && entry.TermLinks("drops").Any(e => e.Name == item)) return true;
            if (Knowledge.SourceLines(item).Any(line => line.Record?.Way == SourceWay.Made && line.Record.Giver == thing)) return true;
            return TurnedInto(thing, item).Any();
        }

        /// <summary>What a station turns into an item, by prefab (<c>UseKind.TurnsInto</c> at it).</summary>
        internal static IEnumerable<string> TurnedInto(string station, string item)
        {
            var made = WorldCatalog.Current?.Find(item);
            if (made == null) yield break;
            foreach (var input in made.TermLinks("needs"))
            {
                if (Knowledge.UsesOf(input.Name).Any(g => g.Kind == UseKind.TurnsInto && g.Place == station && g.Targets.Any(t => t.Target == item))) yield return input.Name;
            }
        }

        /// <summary>A creature the character saw eat: paired with what it ate, for the strict rules.</summary>
        public static void Fed(Character creature, string food)
        {
            if (Into == null || creature == null || string.IsNullOrEmpty(food)) return;
            Into.Witness(Utils.GetPrefabName(creature.gameObject), food);
        }

        /// <summary>The character saw two things go together, by their entries' keys (<see cref="CompendiumBook.Witness"/>).</summary>
        public static void Saw(string a, string b) => Into?.Witness(a, b);

        /// <summary>The character met a thing where it stands: with the biome there, and the place it is in.</summary>
        public static void MetIn(string key, Vector3 at)
        {
            if (Into == null || string.IsNullOrEmpty(key)) return;
            var biome = BiomeKeyAt(at);
            if (biome != null) Into.Witness(key, biome);
            var place = PlaceKeyAt(at);
            if (place != null) Into.Witness(key, place);
        }

        /// <summary>Whether a thing was paired with the biome where it stands and the place it is in already: nothing more is learned by meeting it there.</summary>
        public static bool PairedIn(string key, Vector3 at)
        {
            if (Into == null || string.IsNullOrEmpty(key)) return false;
            var biome = BiomeKeyAt(at);
            if (biome != null && !Into.Witnessed(key, biome)) return false;
            var place = PlaceKeyAt(at);
            return place == null || Into.Witnessed(key, place);
        }

        private static readonly Dictionary<int, string> BiomeKeys = new Dictionary<int, string>();
        private static readonly Dictionary<Location, string> PlaceKeys = new Dictionary<Location, string>();

        /// <summary>The key of the biome at a point, made once a biome; none where there is none.</summary>
        private static string BiomeKeyAt(Vector3 at)
        {
            var biome = Heightmap.FindBiome(at);
            if (biome == Heightmap.Biome.None) return null;
            if (!BiomeKeys.TryGetValue((int)biome, out var key)) BiomeKeys[(int)biome] = key = EntryKeys.For(Kind.Biome, biome.ToString());
            return key;
        }

        /// <summary>The key of the place a point is in, made once a place in this world; none outside any.</summary>
        private static string PlaceKeyAt(Vector3 at)
        {
            var place = Location.GetLocation(at);
            if (place == null) return null;
            if (!PlaceKeys.TryGetValue(place, out var key)) PlaceKeys[place] = key = EntryKeys.For(Kind.Location, Utils.GetPrefabName(place.gameObject));
            return key;
        }

        /// <summary>Writes the book once it changed, at most every half minute unless asked to now, its text worked out and written on a worker thread.</summary>
        public static void Save(bool now = false)
        {
            if (_book == null || _unreadable || !_book.Changed || (!now && Time.unscaledTime - _savedAt < 30f)) return;
            _savedAt = Time.unscaledTime;
            var text = _book.ToSave();
            var path = PathOf(_file);
            Files.Then("keeping what the character knows", () => FileSwap.Write(path, aside => File.WriteAllText(aside, text())));
        }

        /// <summary>Done once every write of the book asked for before it is, for what reads the file back.</summary>
        internal static Task<bool> Written() => Files.Then(() => true);

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
            _book.Now = Moment;
            return true;
        }

        /// <summary>The game's day and the biome the character stands in, for what it comes to know now.</summary>
        private static (int Day, string Biome) Moment()
        {
            var player = Player.m_localPlayer;
            var day = EnvMan.instance != null ? EnvMan.instance.GetDay() : -1;
            var biome = player != null ? Heightmap.FindBiome(player.transform.position) : Heightmap.Biome.None;
            return (day, biome == Heightmap.Biome.None ? null : biome.ToString());
        }

        /// <summary>The book kept for a character; an empty one for a character never kept. On a worker thread.</summary>
        private static CompendiumBook Read(string path)
        {
            var file = FileSwap.ToRead(path);
            return CompendiumBook.Load(file != null ? File.ReadAllText(file) : null);
        }

        private static string PathOf(string file) => Path.Combine(Settings.CacheFolder, "compendium", file);

        /// <summary>The weather last seen and the biome it was seen in, so each change of weather is counted once.</summary>
        private static (Heightmap.Biome Biome, string Weather) _weatherSeen;

        /// <summary>The weather now in the biome the character stands in, counted where it changed (<see cref="CompendiumBook.Weathered"/>).</summary>
        private static void Weather()
        {
            var env = EnvMan.instance;
            var now = env != null ? env.GetCurrentEnvironment() : null;
            if (now == null) return;
            var biome = env.GetCurrentBiome();
            if (biome == Heightmap.Biome.None) return;
            if (_weatherSeen.Biome == biome && _weatherSeen.Weather == now.m_name) return;
            _weatherSeen = (biome, now.m_name);
            _book.Weathered(biome.ToString(), now.m_name);
            Noted(PlayEvent.WeatherSeen);
        }

        /// <summary>The tameable creatures seen wild near the character (<see cref="Tamings"/>).</summary>
        private static readonly TameWatch<ZDOID> WildNear = new TameWatch<ZDOID>();

        /// <summary>The tameable creatures near the character at the last look, by their place in the world's data.</summary>
        private static readonly Dictionary<ZDOID, Character> TameableNear = new Dictionary<ZDOID, Character>();

        /// <summary>
        /// A taming seen happen: a tameable creature near the character seen wild, then tame.
        /// Whether a creature is tame is kept on it for every player to see (<c>Character.IsTamed</c>),
        /// so a taming is seen whichever player's game tames it; the game tames a creature only
        /// in the game that owns it. Whether each is tame is told by <paramref name="tame"/>, the
        /// game's own flag in play.
        /// </summary>
        internal static void Tamings(Player player, System.Func<Character, bool> tame)
        {
            TameableNear.Clear();
            foreach (var creature in Character.GetAllCharacters())
            {
                if (creature == null || creature.IsPlayer() || creature.GetComponent<Tameable>() == null) continue;
                if (Vector3.Distance(player.transform.position, creature.transform.position) > Sighting.PersonReach) continue;
                var id = creature.GetZDOID();
                if (id != ZDOID.None) TameableNear[id] = creature;
            }
            foreach (var id in WildNear.Look(TameableNear.Select(c => (c.Key, tame(c.Value))))) Tamed(TameableNear[id], tamedNow: true);
        }

        /// <summary>On leaving a world: writes the book, and lets go of the catalog it was caught up with.</summary>
        private static void Forget()
        {
            Save(now: true);
            _weatherSeen = default;
            WildNear.Forget();
            CreaturesHit.Forget();
            HitElsewhere.Forget();
            TameableNear.Clear();
            _catchUp = null;
            _catchUpOf = null;
            _recordsSize = -1;
            _bosses = null;
            _bossesOf = null;
            Ceiling = 0;
            Acted.Clear();
            PlaceKeys.Clear();
        }
    }
}
