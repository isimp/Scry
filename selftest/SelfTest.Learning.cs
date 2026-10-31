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
            else p.Note("no place or no biome where the character stands: standing in a place untried");

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
            else p.Note("no rune stone loaded or among the prefabs: reading one untried");

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
            else p.Note("no crafting station with upgrades loaded near by: its rows untried");

            RaidLivedThroughTells(p, at, biome, Fresh, Shows);
            VegvisirReadTells(p, Fresh, Shows);
            NothingToSeeTells(p);
            EffectOnYouTells(p);

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
            p.Note($"records of deeds: {Numbers.Count(records.PickedFrom.Count)} things picked from, {Numbers.Count(records.Collected.Count)} producers taken from, {Numbers.Count(records.Caught.Count)} fish caught");
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
            foreach (var name in records.Caught)
            {
                var fish = GamePrefabs.Named(name).OrNull()?.GetComponent<Fish>();
                if (fish == null || !records.Picked.Contains(fish.m_name) || !told.Proved(name, EntryKeys.Did(Deed.Caught))) odd.Add(name);
            }
            p.Check(odd.Count == 0, "what its records tell of picks, takes and catches names the right things, each paired or shown", string.Join(", ", odd.Take(10)));
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
                p.Note("no Vegvisir is loaded in a place to read: its place untried");
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
