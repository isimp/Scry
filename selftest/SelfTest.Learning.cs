using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for the ways a player meets things, as the strict rules then show them, and for the hooks they come through.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Each way of meeting a place, a rune stone and a station's upgrades, fed through the
        /// keeper's own entry points the hooks call, into a book of the test's own: what it makes
        /// known, and what the strict rules then show of how the things go together. Standing in a
        /// place pairs it with the biome there; at a dungeon's entrance it is seen, inside known; a
        /// rune stone read keeps its text as read whatever its label, its location's page showing it
        /// read though told there in words; a station in use, an upgrade
        /// built near it and a station built near its upgrades pair the station with each; a raid
        /// lived through pairs it with the biome there. The
        /// character's book is left alone. Rows needing a stone or a station with upgrades loaded
        /// near by are left out where there is none.
        /// </summary>
        private static IEnumerator LearningPathsTaught(Probe p)
        {
            var player = Player.m_localPlayer;
            var real = CompendiumKeeper.Book;
            if (!p.Check(player != null && real != null, "a character is played and its book read")) yield break;
            var version = real.Version;
            var pairs = real.PairsVersion;
            var at = player.transform.position;
            var biome = X.Find(EntryKeys.For(Kind.Biome, Heightmap.FindBiome(at).ToString()));
            CompendiumBook Fresh()
            {
                var book = new CompendiumBook();
                CompendiumKeeper.RecordInto(book);
                return book;
            }
            bool Shows(CompendiumBook book, Entry a, Entry b)
            {
                var view = new CompendiumView(X.Entries);
                view.Tell(book, CompendiumRule.Strict);
                return view.Related(a, b);
            }

            // A place stood in, known and paired with the biome there.
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && e.Source is PlaceSource source && !source.IsRoom && source.Contents?.Dungeon == null);
            if (place != null && biome != null)
            {
                var book = Fresh();
                CompendiumKeeper.StoodIn(place.Key, hasInside: false, inside: false, at);
                p.Check(book.Level(place.Key) == KnownLevel.Known && Shows(book, place, biome), $"standing in {place.Name} makes it known, its biome shown under the strict rules", $"{book.Level(place.Key)}, paired {book.Witnessed(place.Key, biome.Key)}");
            }
            else p.Untried("standing in a place", "stand inside a camp, a village or a dungeon's entrance", "place");

            // A dungeon seen at its entrance, known once inside, paired with the biome either way.
            var crypt = Crypt();
            if (crypt != null && biome != null)
            {
                var book = Fresh();
                CompendiumKeeper.StoodIn(crypt.Key, hasInside: true, inside: false, at);
                var outside = book.Level(crypt.Key);
                CompendiumKeeper.StoodIn(crypt.Key, hasInside: true, inside: true, at);
                p.Check(outside == KnownLevel.Seen && book.Level(crypt.Key) == KnownLevel.Known && Shows(book, crypt, biome), $"at {crypt.Name}'s entrance it is seen, inside known, its biome shown under the strict rules", $"{outside}, then {book.Level(crypt.Key)}");
            }

            // A rune stone read: the text it gave kept as read, whatever its label.
            var stone = Object.FindObjectsByType<RuneStone>(FindObjectsSortMode.None).OrderBy(s => (s.transform.position - at).sqrMagnitude).FirstOrDefault();
            if (stone == null) stone = ZNetScene.instance.m_prefabs.Where(o => o != null).Select(o => o.GetComponentInChildren<RuneStone>(true)).FirstOrDefault(s => s != null);
            if (stone != null)
            {
                var book = Fresh();
                CompendiumKeeper.ReadRunes(stone);
                var texts = (stone.m_randomTexts ?? new List<RuneStone.RandomRuneText>()).Select(t => t.m_text).Append(stone.m_text).Where(t => !string.IsNullOrEmpty(t)).ToList();
                var view = stone.GetComponentInParent<ZNetView>();
                p.Check(texts.Any(book.HasRead) && (view == null || book.Level(Utils.GetPrefabName(view.gameObject)) == KnownLevel.Known),
                    $"a rune stone read ({stone.name}) keeps the text it gave as read, whatever the game keeps of it, the stone known", string.Join(", ", texts.Take(3)));
                // What its location's page shows: the texts told there in words, each asked after as the page asks, by the key the stone gives it by.
                var location = Location.GetLocation(stone.transform.position);
                var entry = location != null ? X.Find(EntryKeys.For(Kind.Location, Utils.GetPrefabName(location.gameObject))) : null;
                var told = (entry?.Source as PlaceSource)?.Contents?.Runestones.SelectMany(s => s.Texts).ToList();
                if (told != null && told.Count > 0)
                {
                    p.Check(told.Any(t => book.HasRead(RuneWords.ReadAs(t))), $"and {entry.Name}'s page shows the text read, told there in words",
                        string.Join(", ", told.Take(3).Select(t => RuneWords.ReadAs(t) + " = " + t.Text.Substring(0, System.Math.Min(24, t.Text.Length)))));
                }
                else p.Note($"the place {stone.name} stands in has no page with its texts told here: the page's side untried");
            }
            else p.Untried("reading a rune stone", "stand near a rune stone", "runestone");

            // A station and its upgrades: in use, an upgrade built near it, it built near them.
            // Each station loaded with its own upgrades, the nearest with any taken.
            var stations = new List<(CraftingStation Station, List<StationExtension> Upgrades)>();
            foreach (var each in Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None))
            {
                if (each == null) continue;
                var found = new List<StationExtension>();
                StationExtension.FindExtensions(each, each.transform.position, found);
                if (found.Count > 0) stations.Add((each, found));
            }
            var (station, near) = stations.OrderBy(t => (t.Station.transform.position - at).sqrMagnitude).FirstOrDefault();
            if (station != null && near != null && near.Count > 0)
            {
                var stationEntry = X.Find(Utils.GetPrefabName(station.gameObject));
                var upgrade = near[0];
                var upgradeEntry = X.Find(Utils.GetPrefabName(upgrade.gameObject));
                p.Note($"{stationEntry?.Name ?? station.name} with {Numbers.Count(near.Count)} upgrades near it");
                if (p.Check(stationEntry != null && upgradeEntry != null, "the station and its upgrade are in the catalog"))
                {
                    var used = Fresh();
                    CompendiumKeeper.UsedStation(station);
                    p.Check(near.All(e => used.Witnessed(stationEntry.Key, Utils.GetPrefabName(e.gameObject))) && Shows(used, stationEntry, upgradeEntry),
                        $"{stationEntry.Name} in use is paired with each upgrade near it, shown under the strict rules", string.Join(", ", near.Select(e => Utils.GetPrefabName(e.gameObject))));
                    var built = Fresh();
                    var upgradePiece = GamePrefabs.Named(upgradeEntry.Key).OrNull()?.GetComponent<Piece>();
                    if (upgradePiece != null) CompendiumKeeper.Placed(upgradePiece, upgrade.transform.position, null);
                    p.Check(upgradePiece != null && built.Level(upgradeEntry.Key) == KnownLevel.Known && built.Witnessed(stationEntry.Key, upgradeEntry.Key),
                        $"{upgradeEntry.Name} built near {stationEntry.Name} is known and paired with it", $"{built.Level(upgradeEntry.Key)}");
                    var placed = Fresh();
                    var stationPiece = GamePrefabs.Named(stationEntry.Key).OrNull()?.GetComponent<Piece>();
                    if (stationPiece != null) CompendiumKeeper.Placed(stationPiece, station.transform.position, null);
                    p.Check(stationPiece != null && placed.Witnessed(stationEntry.Key, upgradeEntry.Key), $"{stationEntry.Name} built near its upgrades is paired with them");
                }
            }
            else p.Untried("a station built near its upgrades", "stand by a workbench or forge with an upgrade beside it", "upgrades");

            RaidLivedThroughTells(p, at, biome, Fresh, Shows);
            VegvisirReadTells(p, Fresh, Shows);
            NothingToSeeTells(p);
            EffectOnYouTells(p);
            BuiltThingsTell(p, Fresh);
            AttacksToldApartTell(p);
            RaidsAndPlacesTell(p, Fresh);
            AltarTells(p, Fresh);
            FedThenHeldTells(p, Fresh);
            TwinsTell(p);
            DroplessDeathsTell(p, Fresh);
            RaidBossesTell(p);

            CompendiumKeeper.RecordInto(null);
            p.Check(real.Version == version && real.PairsVersion == pairs, "the character's book was left alone");
        }

        /// <summary>The character's records read whole: every kind counted, honey and sap taken among them, as the game grows them.</summary>
        private static void RecordsReadWhole(Probe p, Player player, PlayerProfile profile, CompendiumRecords records)
        {
            var sets = new[] { records.Materials, records.Recipes, records.Stations, records.Biomes, records.Trophies, records.Uniques, records.Killed, records.PickedUp, records.Crafted, records.Eaten, records.Picked, records.Placed, records.Powers };
            var size = CharacterRecords.Size(player, profile);
            var harvests = CharacterRecords.Harvests(profile);
            p.Check(sets.Sum(s => s.Count) + harvests == size, "the character's records are read whole", $"{Numbers.Count(sets.Sum(s => s.Count) + harvests)} of {Numbers.Count(size)}");
            p.Note($"records: {Numbers.Count(records.Materials.Count)} items had, {Numbers.Count(records.Recipes.Count)} recipes, {Numbers.Count(records.Stations.Count)} stations, {Numbers.Count(records.Biomes.Count)} biomes, {Numbers.Count(records.Trophies.Count)} trophies, {Numbers.Count(records.Uniques.Count)} marks of what was done, {Numbers.Count(records.Killed.Count)} kinds killed, {Numbers.Count(records.PickedUp.Count)} picked up, {Numbers.Count(records.Crafted.Count)} crafted, {Numbers.Count(records.Eaten.Count)} eaten, {Numbers.Count(records.Picked.Count)} picked, {Numbers.Count(records.Placed.Count)} placed");
        }

        /// <summary>
        /// What the records tell of picks, takes and catches, read from the game's prefabs: each
        /// thing picked from gives an item picked, each producer taken from is a hive or a tap, each
        /// fish caught goes by a name the records count; and the catch-up pairs or proves each.
        /// </summary>
        private static void RecordsOfDeeds(Probe p, CompendiumRecords records, CompendiumBook told)
        {
            p.Note($"records of deeds: {Numbers.Count(records.PickedFrom.Count)} things picked from, {Numbers.Count(records.Collected.Count)} producers taken from, {Numbers.Count(records.Cooked.Count)} cooking stations cooked at, {Numbers.Count(records.Caught.Count)} fish caught");
            var odd = new List<string>();
            foreach (var name in records.PickedFrom)
            {
                var item = GamePrefabs.Named(name).OrNull()?.GetComponent<Pickable>().OrNull()?.m_itemPrefab;
                if (item == null || !records.Picked.Contains(item.name) || !told.Witnessed(name, item.name, Tie.Gives)) odd.Add(name);
            }
            foreach (var name in records.Collected)
            {
                var producer = GamePrefabs.Named(name);
                if (producer == null || (producer.GetComponent<Beehive>() == null && producer.GetComponent<SapCollector>() == null) || !told.Proved(name, EntryKeys.Did(Deed.Collected))) odd.Add(name);
            }
            foreach (var name in records.Cooked)
            {
                if (GamePrefabs.Named(name).OrNull()?.GetComponent<CookingStation>() == null || !told.Proved(name, EntryKeys.Did(Deed.Finished))) odd.Add(name);
            }
            foreach (var name in records.Caught)
            {
                var fish = GamePrefabs.Named(name).OrNull()?.GetComponent<Fish>();
                if (fish == null || !records.Picked.Contains(fish.m_name) || !told.Proved(name, EntryKeys.Did(Deed.Caught))) odd.Add(name);
            }
            p.Check(odd.Count == 0, "what its records tell of picks, takes and catches names the right things, each paired or shown", string.Join(", ", odd.Take(10)));
        }

        /// <summary>Creatures with no drop list, as starred young are, named; one dying starred beside the character, told with no loot, shows its health and what its stars change.</summary>
        private static void DroplessDeathsTell(Probe p, System.Func<CompendiumBook> fresh)
        {
            var dropless = X.Catalog.Where(e => e.Kind == Kind.Creature && GamePrefabs.Named(e.Name) is GameObject prefab
                && prefab.GetComponent<Character>() is Character character && !(character is Player) && prefab.GetComponent<Trader>() == null
                && prefab.GetComponent<CharacterDrop>() == null && !BornTame.Is(prefab)).Select(e => e.Name).ToList();
            p.Note($"{Numbers.Count(dropless.Count)} creatures leave no loot: " + string.Join(", ", dropless.Take(12)));
            var player = Player.m_localPlayer;
            if (dropless.Count == 0 || player == null) return;
            var book = fresh();
            CompendiumKeeper.Tell(WorldEvents.Make(Happened.Died, dropless[0], player.transform.position, level: 2, doers: new[] { ZNet.GetUID() }));
            p.Check(book.Proved(dropless[0], EntryKeys.Did(Deed.Killed)) && book.Proved(dropless[0], EntryKeys.Did(Deed.StarredFell)), $"{dropless[0]} dying with one star beside you, no loot told, shows its health and what stars change");
        }

        /// <summary>A raid's *Can start* goes to a boss whose fall brings it, the only fall seen together with it; raids a fall only ends are named.</summary>
        private static void RaidBossesTell(Probe p)
        {
            var events = RandEventSystem.instance != null ? RandEventSystem.instance.m_events : null;
            if (events == null)
            {
                p.Note("no raid list is read: raid bosses untried");
                return;
            }
            var linked = 0;
            var endedOnly = new List<string>();
            foreach (var raid in events.Where(r => r != null && !string.IsNullOrEmpty(r.m_name)))
            {
                var entry = X.Find(EntryKeys.For(Kind.Raid, raid.m_name));
                if (entry == null) continue;
                if (Facts.For(entry).Links.TryGetValue("Can start", out var boss))
                {
                    linked++;
                    p.Check(raid.m_requiredGlobalKeys != null && raid.m_requiredGlobalKeys.Contains(Knowledge.FallKeyOf(boss)), $"{raid.m_name}'s Can start goes to {boss}, whose fall brings it");
                }
                else if ((raid.m_notRequiredGlobalKeys ?? new List<string>()).Any(k => Knowledge.BossPrefabOf(k) != null)) endedOnly.Add(raid.m_name);
            }
            p.Note($"{Numbers.Count(linked)} raids go to the boss whose fall brings them; ended by a boss's fall alone: " + (endedOnly.Count == 0 ? "none" : string.Join(", ", endedOnly)));
        }

        /// <summary>The catalog's resource twins share their health as their pages tell it, so what was found out of one may hold for each.</summary>
        private static void TwinsTell(Probe p)
        {
            var twins = ResourceTwins.For(X.Entries);
            var grouped = X.Catalog.Where(e => e.Kind == Kind.Resource && twins(e.Key).Count > 0).ToList();
            p.Note($"{Numbers.Count(grouped.Count)} resources have twins: {string.Join(", ", grouped.Take(12).Select(e => e.Name))}");
            string Health(Entry e) => Facts.For(e).Pairs.Where(x => x.Key == "Health").Select(x => x.Value).FirstOrDefault();
            var odd = grouped.Take(40).Where(e => twins(e.Key).Any(t => Health(X.Find(t)) != Health(e))).Select(e => e.Name).ToList();
            p.Check(odd.Count == 0, "twins share their health", string.Join(", ", odd.Take(8)));
        }

        private static readonly string[] CopperOre = { "CopperOre" };
        private static readonly string[] Copper = { "Copper" };

        /// <summary>Copper ore fed into a smelter, then copper held, wherever it came from: the smelter, the ore and the copper seen together.</summary>
        private static void FedThenHeldTells(Probe p, System.Func<CompendiumBook> fresh)
        {
            var use = Knowledge.UsesOf("CopperOre").FirstOrDefault(g => g.Kind == UseKind.TurnsInto && g.Place != null && g.Targets.Any(t => t.Target == "Copper"));
            if (use == null)
            {
                p.Note("nothing turns copper ore into copper here: feeding a station untried");
                return;
            }
            var book = fresh();
            CompendiumKeeper.PutInto(use.Place, CopperOre);
            CompendiumKeeper.Holding(Copper);
            p.Check(book.Witnessed(use.Place, "Copper", Tie.Gives) && book.Witnessed("CopperOre", "Copper"), $"copper ore fed into {use.Place}, then copper held, shows what {use.Place} makes of it");
        }

        /// <summary>A boss seen at its altar, loaded near by: seen together with the altar's place, whoever called it.</summary>
        private static void AltarTells(Probe p, System.Func<CompendiumBook> fresh)
        {
            var altar = Object.FindObjectsByType<OfferingBowl>(FindObjectsSortMode.None).FirstOrDefault(a => a != null && a.m_bossPrefab != null && Location.GetLocation(a.transform.position) != null);
            if (altar == null)
            {
                p.Untried("a boss seen at its altar", "stand near a boss's altar", "altar");
                return;
            }
            var book = fresh();
            TogetherWatch.AltarSeen(altar, withOffering: false);
            var place = EntryKeys.For(Kind.Location, Utils.GetPrefabName(Location.GetLocation(altar.transform.position).gameObject));
            p.Check(book.Witnessed(altar.m_bossPrefab.name, place), $"{altar.m_bossPrefab.name} seen at its altar goes with {place}, whoever called it");
        }

        /// <summary>What a raid brings waits line by line for its raiders seen up close, how likely it is to start for any of them; a place's spawn stars wait for one of its creatures seen up close there, as a loaded one standing in its place shows.</summary>
        private static void RaidsAndPlacesTell(Probe p, System.Func<CompendiumBook> fresh)
        {
            var raid = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Raid && Facts.For(e).Rows.Any(r => r.Title == RaidWords.BringsTitle));
            if (raid == null) p.Note("no raid brings anything: its lines untried");
            else
            {
                var brings = Facts.For(raid).Rows.First(r => r.Title == RaidWords.BringsTitle);
                p.Check(brings.Lines.Count > 0 && Enumerable.Range(0, brings.Lines.Count).All(i => brings.LineProofs.TryGetValue(i, out var proof) && EntryKeys.IsRaider(proof, out var kind) && kind == brings.Lines[i].Link),
                    $"each line of what {raid.Name} brings waits for one of those raiders seen up close");
            }
            var place = X.Catalog.FirstOrDefault(e => e.Source is PlaceSource source && source.Contents?.SpawnLevels.Count > 0);
            if (place == null) p.Note("no place with spawn points: its stars untried");
            else p.Check(Facts.For(place).Proofs.TryGetValue("Stars at its spawn points", out var stars) && stars == EntryKeys.Did(Deed.Guarded), $"{place.Name}'s spawn stars wait for one of its creatures seen up close there");
            // A creature loaded standing in a place whose spawn points place its kind.
            foreach (var creature in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                if (creature == null || creature.IsPlayer()) continue;
                var location = Location.GetLocation(creature.transform.position);
                var entry = location != null ? X.Find(EntryKeys.For(Kind.Location, Utils.GetPrefabName(location.gameObject))) : null;
                var key = Utils.GetPrefabName(creature.gameObject);
                if (!(entry?.Source is PlaceSource source) || source.Contents == null || !source.Contents.SpawnLevels.Any(l => l.Creature == key)) continue;
                var book = fresh();
                CompendiumKeeper.SeenInPlace(key, creature.transform.position);
                p.Check(book.Proved(entry.Key, EntryKeys.Did(Deed.Guarded)), $"{key} up close in {entry.Name} shows the stars its spawn points give");
                return;
            }
            p.Untried("a place's spawn stars", "stand in a camp or village with its creatures about", "place");
        }

        /// <summary>A trap's numbers wait for one seen springing; a hit of the character's on a piece near by tells how its damage landed, the piece's grid following its own hits; one a player built is not made known by it, one the world placed is.</summary>
        private static void BuiltThingsTell(Probe p, System.Func<CompendiumBook> fresh)
        {
            var trap = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Piece && GamePrefabs.Named(e.Name).OrNull()?.GetComponent<Trap>() != null);
            if (trap == null) p.Note("no trap in the catalog: its numbers untried");
            else p.Check(Facts.For(trap).Proofs.TryGetValue("Damage", out var proof) && proof == EntryKeys.Did(Deed.Sprang), $"{trap.Name}'s damage waits for one seen springing");
            var player = Player.m_localPlayer;
            var piece = Object.FindObjectsByType<WearNTear>(FindObjectsSortMode.None)
                .Where(w => w != null && TogetherWatch.PopupSeen(w)).OrderBy(w => (w.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (piece == null)
            {
                p.Untried("a piece's damage grid", "stand next to a piece you built", "piece");
                return;
            }
            var book = fresh();
            var hit = new HitData();
            hit.m_damage.m_blunt = 10f;
            hit.SetAttacker(player);
            PieceHitWatch.Hit(piece, hit);
            var key = Utils.GetPrefabName(piece.gameObject);
            if (piece.m_minToolTier > 0) p.Note($"{key} needs a stronger tool than a bare hit: its grid untried");
            else p.Check(ResistWords.Types.Any(t => book.Proved(key, EntryKeys.Damage(t))), $"a hit on {key} tells how its damage landed");
            // A piece a player built is learned by its recipe, never by hitting it.
            var built = piece.GetComponent<Piece>();
            if (built != null && built.GetCreator() != 0L) p.Check(book.Level(key) < KnownLevel.Known || book.Came(key)?.Way != CameBy.Hit, $"hitting {key}, which a player built, does not make it known");
            WorldPieceHitTells(p, fresh);
        }

        /// <summary>
        /// A piece the world placed, as a Dvergr crate, is known by the character's hit, as a rock
        /// is, the hit kept as how it came to be known. One loaded near by is hit where there is
        /// one; else the game's own crate, never spawned, stands in for it.
        /// </summary>
        private static void WorldPieceHitTells(Probe p, System.Func<CompendiumBook> fresh)
        {
            var player = Player.m_localPlayer;
            var piece = Object.FindObjectsByType<WearNTear>(FindObjectsSortMode.None)
                .Where(w => w != null && PlacedByTheWorld(w))
                .OrderBy(w => (w.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (piece == null) piece = GamePrefabs.Named("dvergrprops_crate").OrNull()?.GetComponent<WearNTear>();
            if (piece == null)
            {
                p.Untried("a piece the world placed, hit", "stand by a Dvergr camp or a goblin village", "piece");
                return;
            }
            var key = Utils.GetPrefabName(piece.gameObject);
            if (!p.Check(X.Find(key) != null, $"{key} is in the catalog")) return;
            var book = fresh();
            var hit = new HitData();
            hit.m_damage.m_blunt = 10f;
            hit.SetAttacker(player);
            PieceHitWatch.Hit(piece, hit);
            p.Check(book.Level(key) == KnownLevel.Known && book.Came(key)?.Way == CameBy.Hit, $"hitting {key}, which the world placed, makes it known by the hit", $"{book.Level(key)}, {book.Came(key)?.Way}");
        }

        /// <summary>
        /// A Dvergr mage's attacks, as the game data gives them: only weapons take part in its
        /// triggers, so its fire staff, a utility item, leaves the fireball's trigger to the fireball;
        /// its shield and its Mistile summon share one trigger and are told apart by the weapon in
        /// hand or by what each puts beside the mage, its shield's area and the Mistiles; each of
        /// its looks waits only for its robe, the one item of a set that shows on it. Every trigger
        /// two weapons of a creature still share is named, as the hand tells them apart.
        /// </summary>
        private static void AttacksToldApartTell(Probe p)
        {
            const string mage = "DvergerMage";
            if (GamePrefabs.Named(mage) == null)
            {
                p.Note("no Dvergr mage in the game: its attacks untried");
                return;
            }
            var cast = CompendiumSight.TriggerCandidates(mage, "attack_cast");
            p.Check(cast.Count == 1 && cast[0] == "DvergerStaffFire_fireball", "the mage's cast is its fireball alone, its staffs being no weapons", string.Join(", ", cast));
            var protect = CompendiumSight.TriggerCandidates(mage, "attack_protect");
            p.Check(protect.Count == 2 && protect.Contains("DvergerStaffSupport_buff") && protect.Contains("DvergerMistile"), "the mage's shield and Mistile summon share one trigger", string.Join(", ", protect));
            var made = CompendiumSight.MadeBy(mage);
            p.Check(made.Any(m => m.Item == "DvergerStaffSupport_buff" && m.Made == "DvergerStaffSupport_aoe") && made.Any(m => m.Item == "DvergerMistile" && m.Made == "Mistile" && m.Near > 3f),
                "what the shield and the summon put beside the mage is read from the game: its area, its Mistiles within the summon's radius", string.Join(", ", made.Select(m => $"{m.Item}>{m.Made}@{Numbers.Amount(m.Near, 1)}")));
            var gear = GamePrefabs.Named(mage);
            var looks = gear.GetComponent<Humanoid>().OrNull()?.m_randomSets?.Length ?? 0;
            var drawn = Enumerable.Range(1, looks).Select(look => PrefabGear.SetItems(gear, look).Where(i => GamePrefabs.Item(i) is GameObject item && PrefabGear.Shows(item)).ToList()).ToList();
            p.Check(looks > 0 && drawn.All(d => d.Count == 1 && d[0].StartsWith("DvergerSuit", System.StringComparison.Ordinal)), "each of the mage's looks shows only its robe, so it waits for nothing else",
                string.Join("; ", drawn.Select(d => string.Join(", ", d))));
            // Every creature's triggers two weapons still share, told apart by the hand.
            var shared = new List<string>();
            foreach (var creature in X.Catalog.Where(e => e.Kind == Kind.Creature))
            {
                foreach (var each in CompendiumSight.TriggerMap(creature.Name))
                {
                    if (each.Value.Length > 1) shared.Add($"{creature.Name} {each.Key}: {string.Join("/", each.Value)}");
                }
            }
            p.Note($"{Numbers.Count(shared.Count)} triggers two weapons share, told by the hand: {string.Join("; ", shared.Take(24))}");
        }

        /// <summary>Whether a piece stands from the world, not from a player (<see cref="WorldPiece"/>).</summary>
        private static bool PlacedByTheWorld(WearNTear piece)
        {
            var built = piece.GetComponent<Piece>();
            return WorldPiece.LearnedByHit(built != null ? built.GetCreator() : 0L);
        }

        /// <summary>A burn's and a poison's own numbers wait for the effect to be on the character: each told pair and table line keeps back for it.</summary>
        private static void EffectOnYouTells(Probe p)
        {
            foreach (var name in new[] { "Burning", "Poison" })
            {
                var entry = X.Find(EntryKeys.For(Kind.StatusEffect, name));
                if (entry == null)
                {
                    p.Note($"no {name} in the catalog: its numbers untried");
                    continue;
                }
                var facts = Facts.For(entry);
                var felt = EntryKeys.Did(Deed.Felt);
                p.Check(facts.Proofs.ContainsValue(felt) && facts.Rows.SelectMany(r => r.LineProofs.Values).All(v => v == felt), $"{name}'s own numbers wait until it was on you");
            }
        }

        /// <summary>The spawn points rooms and camps hold have nothing to see or hear, so a page names them plainly and counts none of them as left to find.</summary>
        private static void NothingToSeeTells(Probe p)
        {
            var points = X.Catalog.Where(e => e.Kind == Kind.Spawner && GamePrefabs.Named(e.Name).OrNull()?.GetComponent<CreatureSpawner>() != null).ToList();
            var seen = points.Where(e => !e.Empty).Select(e => e.Name).ToList();
            p.Check(points.Count > 0 && seen.Count == 0, $"the {Numbers.Count(points.Count)} spawn points have nothing to see, so none is left to find", string.Join(", ", seen.Take(8)));
        }

        /// <summary>A Vegvisir loaded in a place, read: the place it marks seen together with the place it stands in, under the strict rules.</summary>
        private static void VegvisirReadTells(Probe p, System.Func<CompendiumBook> fresh, System.Func<CompendiumBook, Entry, Entry, bool> shows)
        {
            var stone = Object.FindObjectsByType<Vegvisir>(FindObjectsSortMode.None)
                .FirstOrDefault(v => v != null && Location.GetLocation(v.transform.position) != null && v.m_locations != null && v.m_locations.Any(l => !string.IsNullOrEmpty(l?.m_locationName)));
            var holder = stone != null ? X.Find(EntryKeys.For(Kind.Location, Utils.GetPrefabName(Location.GetLocation(stone.transform.position).gameObject))) : null;
            var name = stone != null ? stone.m_locations.First(l => !string.IsNullOrEmpty(l?.m_locationName)).m_locationName : null;
            var marked = name != null ? X.Find(EntryKeys.For(Kind.Location, name)) : null;
            if (holder == null || marked == null || holder == marked)
            {
                p.Untried("a Vegvisir's place", "stand near a Vegvisir", "vegvisir");
                return;
            }
            var book = fresh();
            CompendiumKeeper.Marked(stone.gameObject, name);
            p.Check(shows(book, holder, marked), $"a Vegvisir read in {holder.Name} shows the place it marks, {marked.Name}, with it under the strict rules");
        }

        /// <summary>A raid lived through where the character stands, one of the world's that counts the biome there as its own: known, its biome shown under the strict rules.</summary>
        private static void RaidLivedThroughTells(Probe p, Vector3 at, Entry biome, System.Func<CompendiumBook> fresh, System.Func<CompendiumBook, Entry, Entry, bool> shows)
        {
            var here = EnvMan.instance != null ? EnvMan.instance.GetCurrentBiome() : Heightmap.Biome.None;
            var raid = RandEventSystem.instance.OrNull()?.m_events?.FirstOrDefault(e => e != null && e.m_biome != Heightmap.Biome.None && e.InEventBiome());
            var entry = raid != null ? X.Find(EntryKeys.For(Kind.Raid, raid.m_name)) : null;
            if (entry == null || biome == null)
            {
                p.Note($"no raid of the world's counts {here} as its own: living through one untried");
                return;
            }
            var book = fresh();
            CompendiumKeeper.LivedThrough(raid, at);
            p.Check(book.Level(entry.Key) == KnownLevel.Known && shows(book, entry, biome), $"living through {entry.Name} in {biome.Name} makes it known, the biome shown with it under the strict rules", $"{book.Level(entry.Key)}, paired {book.Witnessed(entry.Key, biome.Key)}");
        }

        /// <summary>
        /// Every hook of Scry's acting on a game method's answer can see that answer come true: none
        /// hooks a method whose code answers only false (<see cref="IlShape.OnlyFalse"/>), as hooks on
        /// RuneStone.Interact and MapTable.OnRead once did, which never acted. The hooks are told,
        /// and how those two read now.
        /// </summary>
        private static IEnumerator HookGatesCanOpen(Probe p)
        {
            var looked = new List<string>();
            var dead = new List<string>();
            foreach (var original in Harmony.GetAllPatchedMethods())
            {
                if (!(original is MethodInfo method) || method.ReturnType != typeof(bool)) continue;
                var info = Harmony.GetPatchInfo(original);
                if (info == null) continue;
                foreach (var patch in info.Postfixes)
                {
                    if (patch.owner != About.Guid || !patch.PatchMethod.GetParameters().Any(q => q.Name == "__result")) continue;
                    var name = $"{method.DeclaringType?.Name}.{method.Name}";
                    looked.Add(name);
                    if (IlShape.OnlyFalse(method.GetMethodBody()?.GetILAsByteArray())) dead.Add(name);
                }
            }
            p.Note("hooks acting on the game's answer: " + string.Join(", ", looked));
            var interact = AccessTools.Method(typeof(RuneStone), nameof(RuneStone.Interact));
            var read = AccessTools.Method(typeof(MapTable), "OnRead", new[] { typeof(Switch), typeof(Humanoid), typeof(ItemDrop.ItemData), typeof(bool) });
            foreach (var (name, game) in new[] { ("RuneStone.Interact", interact), ("MapTable.OnRead", read) })
            {
                p.Note(IlShape.OnlyFalse(game?.GetMethodBody()?.GetILAsByteArray())
                    ? name + " is read as answering only false: a hook acting on its answer would never act"
                    : name + " can answer true now");
            }
            p.Check(looked.Count > 0 && dead.Count == 0, "every hook acting on a game method's answer can see it come true", string.Join(", ", dead));
            yield break;
        }
    }
}
