using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    internal static partial class SelfTest
    {
        // ----- The compendium -----

        /// <summary>
        /// The played character's book is read, holds everything the game's own records of the
        /// character tell and everything it carries, and is kept in Scry's cache folder. The test
        /// learns nothing into it: what it checks against is worked out on a copy.
        /// </summary>
        private static IEnumerator CompendiumKept(Probe p)
        {
            var player = Player.m_localPlayer;
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (!p.Check(player != null && profile != null, "a character is played")) yield break;
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "its book is read")) yield break;
            // Kept under the character's id, so one made anew under a deleted one's name starts empty.
            var file = CompendiumKeeper.FilePath;
            var source = profile.m_fileSource.ToString();
            p.Check(file != null && Path.GetFileName(file) == CompendiumBook.FileName(profile.m_filename, source, profile.GetPlayerID()), "its book is kept under the character's id", file);
            var known = book.Count(KnownLevel.Known);
            var seen = book.Count(KnownLevel.Seen);
            p.Note($"it knows {Numbers.Count(known)} entries, has seen {Numbers.Count(seen - known)} more and heard of {Numbers.Count(book.Count(KnownLevel.HeardOf) - seen)}; learned lately: {string.Join(", ", book.Lately.Take(12))}");

            // The records as the game keeps them, read whole.
            var records = new CompendiumRecords();
            CharacterRecords.Read(player, profile, records);
            var sets = new[] { records.Materials, records.Recipes, records.Stations, records.Biomes, records.Trophies, records.Uniques, records.Killed, records.PickedUp, records.Crafted, records.Eaten, records.Picked, records.Placed, records.Powers };
            var size = CharacterRecords.Size(player, profile);
            p.Check(sets.Sum(s => s.Count) == size, "the character's records are read whole", $"{Numbers.Count(sets.Sum(s => s.Count))} of {Numbers.Count(size)}");
            p.Note($"records: {Numbers.Count(records.Materials.Count)} items had, {Numbers.Count(records.Recipes.Count)} recipes, {Numbers.Count(records.Stations.Count)} stations, {Numbers.Count(records.Biomes.Count)} biomes, {Numbers.Count(records.Trophies.Count)} trophies, {Numbers.Count(records.Uniques.Count)} marks of what was done, {Numbers.Count(records.Killed.Count)} kinds killed, {Numbers.Count(records.PickedUp.Count)} picked up, {Numbers.Count(records.Crafted.Count)} crafted, {Numbers.Count(records.Eaten.Count)} eaten, {Numbers.Count(records.Picked.Count)} picked, {Numbers.Count(records.Placed.Count)} placed");

            var power = CharacterRecords.Power(player);
            p.Check(power.Length == 0 ? records.Powers.Count == 0 : records.Powers.SetEquals(new[] { power }), "the guardian power chosen is read", power.Length == 0 ? "none chosen" : power);
            var powerEntry = power.Length > 0 ? X.Find(EntryKeys.For(Kind.StatusEffect, power)) : null;
            if (powerEntry != null)
            {
                yield return Until(() => CompendiumKeeper.Book.Level(powerEntry.Key) == KnownLevel.Known, 3);
                p.Check(CompendiumKeeper.Book.Level(powerEntry.Key) == KnownLevel.Known, $"and {power} is known");
            }

            // Everything they tell is in the book: the catch-up ran on this catalog.
            var told = new CompendiumBook();
            var catchUp = CompendiumKeeper.CatchUpFor(X.Entries);
            catchUp.Fill(records, told, firstLoad: true);
            yield return Until(() => Short(told, CompendiumKeeper.Book).Count == 0, 5);
            var shortOf = Short(told, CompendiumKeeper.Book);
            p.Check(shortOf.Count == 0, $"everything its records tell is in its book ({Numbers.Count(told.Count(KnownLevel.HeardOf))} entries)", string.Join(", ", shortOf.Take(10)));

            // What gives an item counts only in the biomes entered, so the biomes a resource grows
            // in must be named as the biomes' own entries name them.
            var biomeNames = new HashSet<string>(X.Catalog.Where(e => e.Kind == Kind.Biome).SelectMany(e => e.Biomes), StringComparer.Ordinal);
            var strays = X.Catalog.Where(e => e.Kind == Kind.Resource).SelectMany(e => e.Biomes).Where(b => !biomeNames.Contains(b)).Distinct().ToList();
            p.Check(strays.Count == 0, "every biome a resource grows in is one of the biomes' own", string.Join(", ", strays.Take(5)));
            // What the character ever held is known, but what a trader sells, which the game also
            // counts as held once offered, only seen.
            var heldNames = new HashSet<string>(records.Materials, StringComparer.Ordinal);
            var held = X.Catalog.Where(e => e.Kind == Kind.Item && e.Tokens.Any(heldNames.Contains)).ToList();
            var sold = held.Where(e => Knowledge.SourceLines(e.Name).Any(line => line.Record?.Way == SourceWay.Sold)).ToList();
            var notKnown = held.Except(sold).Where(e => CompendiumKeeper.Book.Level(e.Key) != KnownLevel.Known).Select(e => e.Name).ToList();
            p.Check(notKnown.Count == 0, $"the {Numbers.Count(held.Count - sold.Count)} kinds of thing it ever held are known, {Numbers.Count(sold.Count)} a trader sells left to its offer", string.Join(", ", notKnown.Take(10)));
            // The game keeps each biome entered by its name in the language played in then (BiomeSector.GetName),
            // a save from before that by its name token ($biome_meadows), the no-biome among them:
            // every one but that names a biome its records make known.
            p.Note($"areas kept as entered: {string.Join(", ", records.Biomes)}");
            var unmatched = catchUp.AreasNotKnown(records, told);
            p.Check(unmatched.Count == 0, $"each of the {Numbers.Count(records.Biomes.Count)} areas it entered is known by the name the game keeps it under, the game's no-biome left out", string.Join(", ", unmatched.Take(5)));
            // An area kept in another language is placed by the biomes' names in every language,
            // read from the game's own tables: each biome's name in the language played now is
            // among them, and the Meadows, where every character starts, has names in others too.
            var everyLanguage = CompendiumKeeper.BiomesInEveryLanguage(X.Entries);
            var amiss = everyLanguage.Where(b => !b.Value.Contains(Localization.instance.Localize(BiomeWords.Token(b.Key)))).Select(b => b.Key).ToList();
            p.Check(everyLanguage.TryGetValue("Meadows", out var meadowsNames) && meadowsNames.Count > 1 && amiss.Count == 0,
                $"the names of {Numbers.Count(everyLanguage.Count)} biomes in the game's {Numbers.Count(Localization.instance.GetLanguages().Count)} languages are read, each one's name in the language played now among them",
                string.Join(", ", amiss.Take(5)));
            // A trophy held makes known only the creatures it falls from that live where the
            // character went: the game's own TheHive, placed nowhere, drops the drake's trophy too.
            var hive = X.Find("TheHive");
            if (hive != null && records.Trophies.Contains("TrophyHatchling"))
                p.Check(told.Level(hive.Key) != KnownLevel.Known, "the drake's trophy held does not make known TheHive, which the world never places", $"its biomes: {string.Join(", ", hive.Biomes)}");
            var wood = X.Find("Wood");
            if (wood != null)
            {
                var givers = wood.TermLinks("from").Where(e => e.Kind == Kind.Resource).ToList();
                var woodKnown = givers.Count(e => told.Level(e.Key) == KnownLevel.Known);
                p.Note($"of the {Numbers.Count(givers.Count)} resources giving wood, its records make {Numbers.Count(woodKnown)} known, those in the biomes it entered");
            }

            // The place stood in: known, a dungeon only once inside it, seen at its entrance.
            var standing = Location.GetLocation(player.transform.position);
            var standingEntry = standing != null ? X.Find(EntryKeys.For(Kind.Location, Utils.GetPrefabName(standing.gameObject))) : null;
            if (standingEntry != null)
            {
                var want = !standing.m_hasInterior || player.InInterior() ? KnownLevel.Known : KnownLevel.Seen;
                yield return Until(() => CompendiumKeeper.Book.Level(standingEntry.Key) >= want, 3);
                p.Check(CompendiumKeeper.Book.Level(standingEntry.Key) >= want, $"the place stood in, {standingEntry.Name}, is {(want == KnownLevel.Known ? "known" : "seen at least, as a dungeon not entered")}");
            }
            else p.Note("no place of the catalog is stood in now");

            // What it carries is known within a moment.
            var carried = player.GetInventory().GetAllItems().Where(i => i?.m_dropPrefab != null).Select(i => i.m_dropPrefab.name).Where(n => X.Find(n) != null).Distinct().ToList();
            yield return Until(() => carried.All(n => CompendiumKeeper.Book.Level(n) == KnownLevel.Known), 3);
            var unknown = carried.Where(n => CompendiumKeeper.Book.Level(n) != KnownLevel.Known).ToList();
            p.Check(unknown.Count == 0, $"all {Numbers.Count(carried.Count)} kinds of thing it carries are known", string.Join(", ", unknown.Take(10)));
            var cameTold = carried.Select(n => (Name: n, Came: CompendiumKeeper.Book.Came(n))).Where(c => c.Came != null).ToList();
            p.Note($"{Numbers.Count(cameTold.Count)} of them tell how they came to be known" + (cameTold.Count > 0 ? $", as {cameTold[0].Name}: {CompendiumWords.CameToKnow(cameTold[0].Came.Value.Way, cameTold[0].Came.Value.At.Day, cameTold[0].Came.Value.At.Biome, cameTold[0].Came.Value.At.Time, cameTold[0].Came.Value.At.Weather, cameTold[0].Came.Value.At.InHand)}" : ""));
            var counted = carried.Select(X.Find).Where(e => e != null).Select(e => (e, Counts: CharacterRecords.Counts(e))).FirstOrDefault(c => c.Counts != null && c.Counts.PickedUp > 0);
            p.Check(carried.Count == 0 || counted.e != null, "the game's counts tell how many of what it carries it picked up", counted.e != null ? $"{counted.e.Name}: {Numbers.Count(counted.Counts.PickedUp)}" : null);

            // Why what the book does not hold counts as known: what brought most along, and each
            // thing named after a creature never met, with what brought it.
            var loose = new CompendiumView(X.Entries);
            loose.Tell(CompendiumKeeper.Book, CompendiumRule.Loose);
            var brought = X.Catalog.Where(e => loose.Of(e) == KnownLevel.Known && loose.Because(e) != null).ToList();
            var roots = brought.GroupBy(e => loose.Chain(e).Last()).OrderByDescending(g => g.Count()).Take(15);
            p.Note($"{Numbers.Count(brought.Count)} entries known along with others; most brought by: "
                + string.Join("; ", roots.Select(g => $"{g.Key.Name} {Numbers.Count(g.Count())} ({string.Join(", ", g.Take(4).Select(e => e.Name))})")));
            var unmetNames = X.Catalog.Where(e => e.Kind == Kind.Creature && loose.Of(e) < KnownLevel.Seen).Select(e => e.Name).Where(n => n.Length >= 4).Distinct().ToList();
            foreach (var odd in brought.Where(e => e.Kind != Kind.Creature && unmetNames.Any(n => e.Name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)).Take(30))
            {
                p.Note($"{odd.Name} is known by way of {string.Join(" <- ", loose.Chain(odd).Select(o => o.Name))}");
            }

            // Kept on disk, as it is now, read back once written: the book is written on a worker.
            CompendiumKeeper.Save(now: true);
            var written = CompendiumKeeper.Written();
            var now = CompendiumKeeper.Book.All.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
            var path = CompendiumKeeper.FilePath;
            p.Note($"kept in {path}");
            yield return Until(() => written.IsCompleted, 5);
            p.Check(written.IsCompleted && KeptHolds(path, now), "its book is kept in Scry's cache folder, all of it", path);
        }

        /// <summary>The rules the character played had before the test chose others, put back after it.</summary>
        private static CompendiumRule? _ruleBefore;

        /// <summary>
        /// The mode in the title row chooses the rules for the character played: the list
        /// follows, and the choice is kept in its book at once. The rules it had are put back.
        /// </summary>
        private static IEnumerator CompendiumChosen(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "the character's book is read")) yield break;
            var path = CompendiumKeeper.FilePath;
            var chosen = book.Rule;
            _ruleBefore = chosen;
            Session.ShowKnownInstead(null);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();

            // In the compact view the title row holds no mode; in the full view it does.
            ScryPanel.Compact = true;
            var repaints = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= repaints + 2, 3);
            var drawn = ScryPanel.Drawn(PanelPart.CompendiumSwitch);
            repaints = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
            p.Check(ScryPanel.Repaints >= repaints + 3 && ScryPanel.Drawn(PanelPart.CompendiumSwitch) == drawn, "in the compact view the title row holds no mode",
                $"drawn {Numbers.Count(ScryPanel.Drawn(PanelPart.CompendiumSwitch) - drawn)} times in {Numbers.Count(ScryPanel.Repaints - repaints)} repaints");
            ScryPanel.Compact = false;
            drawn = ScryPanel.Drawn(PanelPart.CompendiumSwitch);
            yield return Until(() => ScryPanel.Drawn(PanelPart.CompendiumSwitch) > drawn + 1, 3);
            p.Check(ScryPanel.Drawn(PanelPart.CompendiumSwitch) > drawn, $"in the full view its button stands in the title row, at {chosen}");
            // Its label is drawn bold while its box is open: the button holds it whole either way.
            var (buttonW, plainW, openW) = ScryPanel.CompendiumButtonRoom();
            p.Check(buttonW > 0f && plainW <= buttonW && openW <= buttonW, "its button holds its label whole, plain and open",
                $"{Numbers.Fixed(buttonW, 1)} wide, its label {Numbers.Fixed(plainW, 1)} plain and {Numbers.Fixed(openW, 1)} open");
            // Its box opens under the button with the rules.
            var boxes = ScryPanel.Drawn(PanelPart.CompendiumBox);
            ScryPanel.HoldCompendiumBox(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.CompendiumBox) > boxes + 1, 3);
            p.Check(ScryPanel.Drawn(PanelPart.CompendiumBox) > boxes, "its box opens under the button with the rules");

            var other = chosen == CompendiumRule.Strict ? CompendiumRule.Loose : CompendiumRule.Strict;
            ScryPanel.Choose(other);
            yield return Until(() => X.Compendium?.Rule == other && KeptRule(path) == other, 3);
            p.Check(X.Compendium?.Rule == other && KeptRule(path) == other, $"choosing {other} shows the list by it and keeps it in the character's book", $"shown {X.Compendium?.Rule}, kept {KeptRule(path)}");
            // On, the floor stands in the box under the rules, once a boss fell for the character or a floor is set.
            if (CompendiumKeeper.Ceiling > 0 || book.Floor > 0)
            {
                var floors = ScryPanel.Drawn(PanelPart.FloorControl);
                yield return Until(() => ScryPanel.Drawn(PanelPart.FloorControl) > floors + 1, 3);
                p.Check(ScryPanel.Drawn(PanelPart.FloorControl) > floors, "the floor stands in the box under the rules");
            }
            yield return Picture(p, "compendium box");
            ScryPanel.HoldCompendiumBox(false);

            ScryPanel.Choose(chosen);
            yield return Until(() => X.Compendium?.Rule == chosen && KeptRule(path) == chosen, 3);
            p.Check(X.Compendium?.Rule == chosen && KeptRule(path) == chosen, $"choosing {chosen} again puts it back", $"shown {X.Compendium?.Rule}, kept {KeptRule(path)}");
            _ruleBefore = null;
        }

        /// <summary>Puts back the rules the character played had, should the part have stopped before it did.</summary>
        private static void PutRuleBack()
        {
            if (_ruleBefore.HasValue) ScryPanel.Choose(_ruleBefore.Value);
            _ruleBefore = null;
        }

        /// <summary>The rules kept in the file; none while it cannot be read.</summary>
        private static CompendiumRule? KeptRule(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (Steps.Run(() => CompendiumBook.Load(File.ReadAllText(path)), out var kept, null) != null) return null;
            return kept.Rule;
        }

        /// <summary>Keys besides the link keys whose offered values name entries: none may name what a made-up character has not seen.</summary>
        private static readonly string[] OfferKeys = { "playedby", "station" };

        /// <summary>
        /// The explored map is read: the ground the character stands on is explored, the places
        /// this game holds lie partly in it, and where this game is the world's server it answers
        /// a map sent to itself as it would a player's, with as many kinds as read here; as a
        /// client of a dedicated server it asks the Scry there, which answers. The book of the
        /// character played is caught up once, after a while in the world.
        /// </summary>
        private static IEnumerator CompendiumMapRead(Probe p)
        {
            var player = Player.m_localPlayer;
            if (!p.Check(player != null && ZNet.instance != null, "a character is played in a world")) yield break;
            var packed = CompendiumMap.PackedMap();
            if (!p.Check(packed != null, "its explored map is read")) yield break;
            var grid = ExploredGrid.Unpack(packed);
            p.Check(grid != null && grid.Explored(player.transform.position.x, player.transform.position.z), "the ground it stands on is explored", grid != null ? $"cells of {Numbers.Count(grid.Factor)} pixels a side" : null);
            // Places are told by the map's own pixels; sent packed, small enough for one of the game's messages.
            p.Check(grid != null && grid.Factor == 1, "places are told by cells of one map pixel", grid != null ? $"{Numbers.Amount(grid.PixelSize * grid.Factor, 0)} m a cell, {Numbers.Count(grid.Count)} explored" : null);
            var sent = Utils.Compress(packed);
            p.Note($"the explored map goes to the server as {Numbers.Count(sent.Length / 1024)} KB, {Numbers.Count(packed.Length / 1024)} KB before the game's compression");
            p.Check(sent.Length < 256 * 1024, "packed, it is well under a quarter of a megabyte", $"{Numbers.Count(sent.Length / 1024)} KB");
            p.Note($"asked about again as it grew {Numbers.Count(CompendiumMap.AskedMore)} times this session, {Numbers.Count(CompendiumLink.TakenMore)} answers taken");
            // The biomes are read from coarser cells, which need no finer.
            grid = ExploredGrid.Unpack(CompendiumMap.PackedMap(ExploredGrid.BiomeCellMetres)) ?? grid;
            var world = CompendiumMap.World;
            var book = CompendiumKeeper.Book;
            // Read again each session, so ground explored since counts, as news.
            p.Check(CompendiumMap.ReadAgainThisSession, "this world's map is read again this session");

            // The biomes under the explored ground are seen at least, the one stood in among them.
            if (grid != null && WorldGenerator.instance != null)
            {
                var generator = WorldGenerator.instance;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var biomes = MapCatchUp.Biomes(grid, (x, z) => generator.GetBiome(x, z).ToString());
                watch.Stop();
                var here = generator.GetBiome(player.transform.position).ToString();
                p.Check(biomes.Contains(here), $"the biomes under its explored ground ({string.Join(", ", biomes)}) hold the one it stands in, {here}", $"worked out in {Numbers.Amount(watch.Elapsed.TotalMilliseconds)} ms");
                yield return Until(() => book != null && biomes.All(b => book.Level(EntryKeys.For(Kind.Biome, b)) >= KnownLevel.Seen), 15);
                var unseen = biomes.Where(b => book == null || book.Level(EntryKeys.For(Kind.Biome, b)) < KnownLevel.Seen).ToList();
                p.Check(unseen.Count == 0, "and each is seen at least in the character's book", string.Join(", ", unseen));
                p.Check(biomes.All(b => X.Find(EntryKeys.For(Kind.Biome, b)) != null), "each has an entry");
            }

            if (!ZNet.instance.IsServer())
            {
                // Whether the server runs Scry is known from its hello, a moment after joining.
                yield return Until(() => EventLink.Server != Scry.ServerScry.Waiting, 15);
                p.Check(EventLink.Server != Scry.ServerScry.Waiting, "the server's hello, or its silence, told whether it runs Scry", EventLink.Server.ToString());
                if (ServerScries.GivesUp(EventLink.Server))
                {
                    p.Note("the server runs no Scry, or one of other messages: nothing of the explored map is asked");
                    yield break;
                }
                // A client asks the Scry on its world's server, which tells only which kinds of place lie in the map.
                p.Note(book != null && book.Mapped(world) ? "a server world, caught up with its server's answer on entering it" : "a server world not caught up on entering it");
                var taken = CompendiumLink.Taken;
                yield return Until(() =>
                {
                    CompendiumLink.Ask(world);
                    return CompendiumLink.Taken > taken;
                }, 20);
                p.Check(CompendiumLink.Taken > taken, "a map sent to the world's server is answered by the Scry there", CompendiumLink.Taken > taken ? $"{Numbers.Count(CompendiumLink.LastTold)} kinds of place told" : "no answer: no Scry on the server, or one too old");
                p.Check(book != null && book.Mapped(world), "the character's book is caught up from its server's answer");
                yield break;
            }

            var places = CompendiumMap.Places();
            var kinds = MapCatchUp.Kinds(grid, places);
            p.Check(places.Count > 0, $"this game holds the world's {Numbers.Count(places.Count)} places");
            p.Note($"{Numbers.Count(kinds.Count)} kinds of place lie in explored ground");
            yield return Until(() => book != null && book.Mapped(world), 15);
            p.Check(book != null && book.Mapped(world), "the character's book is caught up from its map in this world");
            var missing = kinds.Where(k => book != null && book.Level(EntryKeys.For(Kind.Location, k)) < KnownLevel.Seen).ToList();
            p.Check(missing.Count == 0, "and has every kind of place in explored ground as seen at least", string.Join(", ", missing.Take(5)));

            // As a server: a map sent to this game comes back answered, as a player's would.
            var answered = CompendiumLink.Answered;
            yield return Until(() =>
            {
                CompendiumLink.Ask(world);
                return CompendiumLink.Answered > answered && CompendiumLink.LastTold >= 0;
            }, 10);
            p.Check(CompendiumLink.Answered > answered && CompendiumLink.LastTold == kinds.Count, "a map sent to the world's server is answered with the same kinds", $"{Numbers.Count(CompendiumLink.LastTold)} told, {Numbers.Count(kinds.Count)} read here");
        }

        /// <summary>
        /// Things whose tier the game's progression fixes: each boss in its own, and its guardian
        /// power, which its stone in the Meadows gives only for its trophy; a metal or a drop of
        /// each biome; iron gear in the Swamp's, black metal in the Plains'; Fulings, hens and
        /// cloudberries the Plains', eitr mead the Mistlands'; the workbench, built with the
        /// hammer, the Meadows'. A name the catalog does not hold is passed over.
        /// </summary>
        private static readonly (string Name, int Tier)[] KnownTiers =
        {
            ("Eikthyr", 1), ("gd_king", 2), ("Bonemass", 3), ("Dragon", 4), ("GoblinKing", 5), ("SeekerQueen", 6), ("Fader", 7),
            ("se:GP_Eikthyr", 1), ("se:GP_TheElder", 2), ("se:GP_Bonemass", 3), ("se:GP_Moder", 4), ("se:GP_Yagluth", 5), ("se:GP_Queen", 6), ("se:GP_Ashlands", 7),
            ("Bronze", 2), ("Silver", 4), ("Carapace", 6), ("Grausten", 7), ("FlametalNew", 7),
            ("Troll", 2), ("SwordIron", 3), ("SwordBlackmetal", 5), ("Lox", 5), ("Wood", 1),
            ("Goblin", 5), ("Hen", 5), ("Egg", 5), ("Cloudberry", 5), ("MeadEitrMinor", 6), ("piece_workbench", 1),
        };

        /// <summary>
        /// The tiers are worked out over the whole catalog without fault, and land where the
        /// game's progression puts things; how many entries each tier holds is noted.
        /// </summary>
        private static IEnumerator CompendiumTierRead(Probe p)
        {
            var faults = Faults.Count;
            yield return Until(() => CompendiumTiers.Of(X.Entries) != null, 20);
            var tiers = CompendiumTiers.Of(X.Entries);
            if (!p.Check(tiers != null, "the tiers are worked out")) yield break;
            p.Check(Faults.Count == faults, "without fault", Faults.Latest);
            var counts = Enumerable.Range(1, Tiers.Last).Select(t => $"{Tiers.BiomeOf(t)} {Numbers.Count(tiers.Count(p2 => p2.Value == t))}");
            p.Note($"{Numbers.Count(tiers.Count)} of {Numbers.Count(X.Catalog.Count)} entries have a tier: {string.Join(", ", counts)}, {Numbers.Count(tiers.Count(p2 => p2.Value == 0))} need nothing");
            var wrong = new List<string>();
            var wrongKeys = new List<string>();
            foreach (var (name, tier) in KnownTiers)
            {
                var entry = X.Find(name);
                if (entry == null) continue;
                var got = tiers.TryGetValue(entry.Key, out var t) ? t : Tiers.None;
                if (got == tier) continue;
                wrong.Add($"{name} {(got == Tiers.None ? "none" : Numbers.Count(got))}, not {Numbers.Count(tier)}");
                wrongKeys.Add(entry.Key);
            }
            p.Note($"{Numbers.Count(KnownTiers.Count(k => X.Find(k.Name) != null))} of {Numbers.Count(KnownTiers.Length)} things of known tier are in the catalog");
            p.Check(wrong.Count == 0, "they follow the game's progression: each boss and its power its own, iron gear the Swamp's, black metal the Plains'", string.Join("; ", wrong));
            if (wrongKeys.Count > 0)
            {
                // Why: the way that set each, followed through its dearest need.
                var pass = CompendiumTiers.Gather(X.Entries);
                var worked = pass.Work();
                foreach (var key in wrongKeys) p.Note($"{key}'s tier comes by way of {TierChain(pass, worked, key)}");
            }
            var serpent = X.Find("Serpent");
            p.Check(serpent == null || !tiers.ContainsKey(serpent.Key), "and what lives only at sea has none");

            // Odin, glimpsed from afar and gone as one comes near: his tier and the way it comes,
            // what plays or leaves him behind, and what brought him along where the floor shows him.
            var odin = X.Find("odin");
            if (odin != null)
            {
                var odinPass = CompendiumTiers.Gather(X.Entries);
                var odinTiers = odinPass.Work();
                // Told by the character's own book under its own rules and floor (loose where it has none set),
                // not by the view the test shows, which may have the compendium off and know everything.
                var book = CompendiumKeeper.Book;
                var view = book == null ? null : new CompendiumView(X.Entries);
                if (view != null)
                {
                    var floor = System.Math.Min(book.Floor, CompendiumKeeper.Ceiling);
                    view.Milestones(floor > 0 ? odinTiers : null, floor);
                    view.SkillsLearned(YourSkills.Learned, YourSkills.LearnedVersion);
                    view.Tell(book, book.Rule != CompendiumRule.Off ? book.Rule : CompendiumRule.Loose);
                }
                var told = odinTiers.TryGetValue(odin.Key, out var odinTier) ? $"tier {Numbers.Count(odinTier)} by way of {TierChain(odinPass, odinTiers, odin.Key)}" : "no tier";
                p.Note($"odin: {told}; played or left behind by {(odin.UsedBy.Count > 0 ? string.Join(", ", odin.UsedBy.Take(6)) : "nothing")}; "
                    + $"{(view == null ? "no book" : $"in the character's book {view.Of(odin)}, by the floor {view.ByFloor(odin)}, brought along by {view.Because(odin)?.Name ?? "nothing"}")}");
                // Placed only to be glimpsed, he waits to be seen however high the floor.
                var highest = new CompendiumView(X.Entries);
                highest.Milestones(tiers, Tiers.Last);
                highest.Tell(new CompendiumBook(), CompendiumRule.Loose);
                p.Check(odin.Components.Contains("Odin") && highest.Of(odin) < KnownLevel.Seen && !highest.ByFloor(odin),
                    "Odin, placed only to be glimpsed, waits to be seen however high the floor", $"{highest.Of(odin)}, components {string.Join(", ", odin.Components.Take(6))}");
            }

            // A made-up character with the floor at the Meadows knows all of them, but what is placed
            // only to be glimpsed, which waits to be seen, and nothing of the Plains.
            var floored = new CompendiumView(X.Entries);
            floored.Milestones(tiers, 1);
            floored.Tell(new CompendiumBook(), CompendiumRule.Loose);
            var meadows = X.Catalog.Where(e => tiers.TryGetValue(e.Key, out var t) && t <= 1 && !CompendiumView.OnlyGlimpsed(e)).ToList();
            var shut = meadows.Where(e => floored.Of(e) != KnownLevel.Known).ToList();
            p.Check(meadows.Count > 0 && shut.Count == 0, $"with the floor at the Meadows, all {Numbers.Count(meadows.Count)} things of them show as known, but what is placed only to be glimpsed", string.Join(", ", shut.Take(5).Select(e => e.Name)));
            var lox = X.Find("Lox");
            p.Check(lox == null || floored.Of(lox) < KnownLevel.Seen, "and nothing of the Plains", lox != null ? floored.Of(lox).ToString() : null);

            // Shown, what the floor alone shows is marked on its row and says so on its page.
            var marked = meadows.FirstOrDefault(e => e.Kind == Kind.Creature && floored.ByFloor(e)) ?? meadows.FirstOrDefault(floored.ByFloor);
            if (p.Check(marked != null, "the floor alone shows them, the made-up character knowing nothing"))
            {
                Session.ShowKnownInstead(floored);
                if (!Session.IsOpen) Session.Show(null);
                KeepView();
                yield return Until(() => ReferenceEquals(X.Compendium, floored), 3);
                X.SearchEverything("");
                X.KindFilter = marked.Kind;
                X.Select(marked);
                var marks = ScryPanel.Drawn(PanelPart.FloorMark);
                var lines = ScryPanel.Drawn(PanelPart.FloorLine);
                var links = ScryPanel.Drawn(PanelPart.FloorLink);
                var repaints = ScryPanel.Repaints;
                yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
                p.Check(ScryPanel.Drawn(PanelPart.FloorMark) > marks && ScryPanel.Drawn(PanelPart.FloorLine) > lines, $"{marked.Name} is marked in the list and its page says the floor shows it",
                    $"marks {Numbers.Count(ScryPanel.Drawn(PanelPart.FloorMark) - marks)}, lines {Numbers.Count(ScryPanel.Drawn(PanelPart.FloorLine) - lines)}");
                // Its page names more the floor alone shows (its drops, its sounds), each chip and link marked so.
                p.Check(ScryPanel.Drawn(PanelPart.FloorLink) > links, $"the chips and links on {marked.Name}'s page to what the floor alone shows carry the floor's mark",
                    $"{Numbers.Count(ScryPanel.Drawn(PanelPart.FloorLink) - links)} drawn");
            }
            p.Note($"the character played may raise its floor to tier {Numbers.Count(CompendiumKeeper.Ceiling)}, it stands at {Numbers.Count(CompendiumKeeper.Book?.Floor ?? 0)}");
        }

        /// <summary>Terms tried on a made-up character, each of which must not find what it has not met.</summary>
        private static readonly string[] CompendiumTerms = { "kind:creature", "is:boss", "biome:meadows", "drops:wood", "has:Character", "in:crypt" };

        /// <summary>
        /// The way that sets an entry's tier and where it was read from, then the way of its
        /// dearest need, as long as that need is no cheaper than the way's own tier, down to a way
        /// needing nothing; a making needing nothing is told with its ingredients as read.
        /// </summary>
        private static string TierChain(TierPass pass, Dictionary<string, int> tiers, string key)
        {
            var steps = new List<string>();
            var been = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < 8 && key != null && been.Add(key); i++)
            {
                var way = pass.WayIn(key, tiers);
                if (way == null) break;
                var (own, needs, from) = way.Value;
                int TierOf(string n) => tiers.TryGetValue(n, out var t) ? t : 0;
                var free = needs.Length == 0 && (from == WayFrom.Crafted || from == WayFrom.Station) ? ", made of " + MakingsOf(key) : "";
                steps.Add($"{key} {Numbers.Count(tiers[key])} ({from}, own {Numbers.Count(own)}{(needs.Length > 0 ? ", needs " + string.Join(", ", needs.Take(6).Select(n => $"{n} {Numbers.Count(TierOf(n))}")) : "")}{free})");
                var dearest = needs.OrderByDescending(TierOf).FirstOrDefault();
                key = dearest != null && TierOf(dearest) >= own ? dearest : null;
            }
            return string.Join(" <- ", steps);
        }

        /// <summary>The recipes, piece costs and station makings that make an entry, each ingredient with its amount as read, marked where it is no thing of its own.</summary>
        private static string MakingsOf(string key)
        {
            var entry = X.Entries.Find(key);
            if (entry == null) return "nothing read";
            var ways = Knowledge.CraftWays.Where(w => w.Target == entry.Name).Concat(Knowledge.MadeOf(entry.Name).SelectMany(TierWays.Of)).Take(4).ToList();
            if (ways.Count == 0) return "nothing read";
            string Need((string Name, int Amount) n) => $"{n.Name} {Numbers.Count(n.Amount)}{(X.Entries.Find(n.Name) == null ? " (no thing of its own)" : "")}";
            return string.Join("; ", ways.Select(w => $"{(string.IsNullOrEmpty(w.Place) ? "anywhere" : w.Place)}: {(w.Needs.Count > 0 ? string.Join(", ", w.Needs.Select(Need)) : "no ingredients")}"));
        }

        /// <summary>The keys one book knows more of than another.</summary>
        private static List<string> Short(CompendiumBook wanted, CompendiumBook have) =>
            wanted.All.Where(e => have == null || have.Level(e.Key) < e.Value).Select(e => e.Key).ToList();

        /// <summary>Whether the file kept holds every entry at least as known as given; false while it cannot be read.</summary>
        private static bool KeptHolds(string path, Dictionary<string, KnownLevel> levels)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            if (Steps.Run(() => CompendiumBook.Load(File.ReadAllText(path)), out var kept, null) != null) return false;
            return levels.All(e => kept.Level(e.Key) >= e.Value);
        }

        /// <summary>
        /// With the panel closed nothing shows what the character knows, so it is not told again
        /// (telling it goes through the whole catalog, a frame's worth while it is learned in
        /// play); the panel opening tells it at once.
        /// </summary>
        private static IEnumerator CompendiumWaitsForPanel(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "the character's book is read")) yield break;
            // From a made-up character's knowledge shown, so it is the played one's that waits.
            var made = new CompendiumView(X.Entries);
            made.Tell(new CompendiumBook(), CompendiumRule.Loose);
            if (!Session.IsOpen) Session.Show(null);
            Session.ShowKnownInstead(made);
            yield return Until(() => ReferenceEquals(X.Compendium, made), 3);
            Session.Hide();
            Session.ShowKnownInstead(null);
            yield return Until(() => false, 0.5f);
            p.Check(!ReferenceEquals(X.Compendium?.Book, book), "with the panel closed, the character's knowledge waits");
            Session.Show(null);
            KeepView();
            yield return Until(() => ReferenceEquals(X.Compendium?.Book, book), 3);
            p.Check(ReferenceEquals(X.Compendium?.Book, book), "and is told once the panel opens");
        }

        /// <summary>Things and what they give, each a different way: a station turning it, a producer making it, a deposit's table.</summary>
        private static readonly (string Thing, string Item, string How)[] GivenThings =
        {
            ("smelter", "Copper", "turns ore into it"),
            ("piece_beehive", "Honey", "makes it over time"),
            ("rock4_copper", "CopperOre", "drops it when mined"),
        };

        /// <summary>Creatures from small to the biggest, for how far each is seen.</summary>
        private static readonly string[] BigAndSmall = { "Greydwarf", "Troll", "Serpent", "Dragon", "GoblinKing", "SeekerQueen", "Fader" };

        /// <summary>
        /// The watchers sit on the game's methods they rely on, and looking around the character
        /// finds no fault; what is in sight now is told, learned into a book of the test's own.
        /// </summary>
        private static IEnumerator CompendiumWatched(Probe p)
        {
            foreach (var (type, name) in new[] { (typeof(Character), "Damage"), (typeof(Character), "RPC_Damage"), (typeof(Destructible), "Damage"), (typeof(TreeBase), "Damage"), (typeof(TreeLog), "Damage"), (typeof(MineRock), "Damage"), (typeof(MineRock5), "Damage"),
                (typeof(Humanoid), "Pickup"), (typeof(Player), "PlacePiece"), (typeof(Tameable), "Interact"), (typeof(Tameable), "OnConsumedItem"),
                (typeof(Humanoid), "DoInteractAnimation"), (typeof(UITooltip), "OnHoverStart"), (typeof(Vegvisir), "Interact"),
                (typeof(Player), "Interact"), (typeof(Humanoid), "UseItem"), (typeof(OfferingBowl), "InitiateSpawnBoss"), (typeof(FishingFloat), "Catch"), (typeof(ItemDrop), "Awake"), (typeof(ZNetView), "Awake"),
                (typeof(Procreation), "Procreate"), (typeof(Growup), "GrowUpdate"), (typeof(Pickable), "SetPicked"), (typeof(TreeBase), "RPC_Damage"), (typeof(TreeLog), "Destroy"),
                (typeof(MineRock), "RPC_Hit"), (typeof(MineRock5), "DamageArea"), (typeof(DropOnDestroyed), "OnDestroyed"),
                (typeof(Pickable), "RPC_Pick"), (typeof(SpawnArea), "SpawnOne"), (typeof(Beehive), "Interact"), (typeof(SapCollector), "Interact"), (typeof(InventoryGui), "Show"),
                (typeof(Destructible), "Destroy"), (typeof(WearNTear), "Destroy") })
            {
                var method = AccessTools.DeclaredMethod(type, name);
                p.Check(method != null && Harmony.GetPatchInfo(method)?.Owners.Contains(About.Guid) == true, $"{type.Name}.{name} is watched");
            }

            p.Check(AccessTools.Field(typeof(StoreGui), "m_trader") != null, "a trader's open store tells whose it is");

            // What comes from a thing acted on is paired only where the catalog says the thing gives it.
            foreach (var (thing, item, how) in GivenThings)
            {
                if (X.Find(thing) == null || X.Find(item) == null) { p.Note($"no {thing} or {item} in the catalog"); continue; }
                p.Check(CompendiumKeeper.Gives(thing, item), $"{thing} gives {item}, as it {how}");
            }
            p.Check(CompendiumKeeper.TurnedInto("smelter", "Copper").Contains("CopperOre"), "the smelter turns copper ore into copper", string.Join(", ", CompendiumKeeper.TurnedInto("smelter", "Copper")));
            p.Check(!CompendiumKeeper.Gives("smelter", "Wood"), "and gives no wood");

            // An item's card is topped by its name token, which names its entry (one without a
            // name, as a creature's own attack, never shows a card); a Vegvisir marks places the
            // catalog has.
            var cardless = X.Catalog.Where(e => e.Kind == Kind.Item && e.Source is GameObject go && go.GetComponent<ItemDrop>() != null
                && !string.IsNullOrEmpty(((GameObject)e.Source).GetComponent<ItemDrop>().m_itemData.m_shared.m_name)
                && Array.IndexOf(e.Tokens, ((GameObject)e.Source).GetComponent<ItemDrop>().m_itemData.m_shared.m_name) < 0).Select(e => e.Name).ToList();
            p.Check(cardless.Count == 0, "every item goes by the name its tooltip card is topped with", string.Join(", ", cardless.Take(5)));
            // Vegvisirs are parts of the places that hold them, there only while those are loaded.
            var marks = UnityEngine.Object.FindObjectsByType<Vegvisir>(FindObjectsSortMode.None)
                .SelectMany(v => v.m_locations.Select(l => l.m_locationName)).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            var unmarked = marks.Where(name => X.Find(EntryKeys.For(Kind.Location, name)) == null).ToList();
            if (marks.Count == 0) p.Note("no Vegvisir is loaded near the character, to check the places it marks");
            else p.Check(unmarked.Count == 0, $"the {Numbers.Count(marks.Count)} places the Vegvisirs near mark are all in the catalog", string.Join(", ", unmarked.Take(5)));
            var player = Player.m_localPlayer;
            if (!p.Check(player != null, "a character is played")) yield break;
            // A creature is seen as far as its size makes it out: how far some of the game's are.
            var reaches = BigAndSmall
                .Select(n => X.Find(n)?.Source as GameObject).Where(go => go != null && go.GetComponent<CapsuleCollider>() != null)
                .Select(go => { var c = go.GetComponent<CapsuleCollider>(); return $"{go.name} {Numbers.Amount(Sighting.Reach(Mathf.Max(c.height, c.radius * 2f) * go.transform.localScale.y))} m"; }).ToList();
            p.Check(reaches.Count > 0, "creatures are seen as far as their size makes them out", string.Join(", ", reaches));
            var faults = Faults.Count;
            var sightings = new CompendiumBook();
            var lookUp = CompendiumKeeper.CatchUpFor(X.Entries);
            var failed = Steps.Run(() =>
            {
                CompendiumSight.Look(player, lookUp, sightings);
                CompendiumSight.LookAtAll(player, lookUp, sightings);
            }, null);
            p.Check(failed == null, "looking around the character finds no fault", failed?.Message);

            // Everything the game keeps in the world is seen by the one rule creatures are: how
            // many are kept to look at against those loaded, the nearest of each kind kept or
            // seen already, and how long a whole round of them takes, all it sees included.
            var loaded = ZNetScene.instance != null ? ZNetScene.instance.NrOfInstances() : 0;
            p.Check(CompendiumSight.ThingCount + CompendiumSight.ThingsLetGo + CompendiumSight.ThingsDropped >= loaded, $"the {Numbers.Count(loaded)} things loaded in the world are all kept to look at, let go or passed over",
                $"{Numbers.Count(CompendiumSight.ThingCount)} kept, {Numbers.Count(CompendiumSight.ThingsLetGo)} let go, {Numbers.Count(CompendiumSight.ThingsDropped)} passed over");
            foreach (var (kind, nearest) in new (string, Component)[]
            {
                ("tree", Nearest<TreeBase>(player)), ("rock", Nearest<MineRock5>(player)), ("small rock or stump", Nearest<Destructible>(player)),
                ("building piece", Nearest<WearNTear>(player)), ("pickable", Nearest<Pickable>(player)),
            })
            {
                var thing = nearest != null ? nearest.GetComponentInParent<ZNetView>() : null;
                if (thing == null || thing.GetZDO() == null) continue;
                var key = Utils.GetPrefabName(thing.gameObject);
                var level = CompendiumKeeper.Book?.Level(key) ?? KnownLevel.Unknown;
                p.Check(CompendiumSight.Keeps(thing) || level >= KnownLevel.Seen || X.Find(key) == null, $"the nearest {kind}, {key}, is kept to look at or seen already", level.ToString());
            }
            yield return null;
            var scratch = new CompendiumBook();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (var round = 0; round < 10; round++) CompendiumSight.Look(player, lookUp, scratch);
            var lookMs = clock.Elapsed.TotalMilliseconds / 10;
            clock.Restart();
            for (var round = 0; round < 10; round++) CompendiumSight.LookAtAll(player, lookUp, scratch);
            var roundMs = clock.Elapsed.TotalMilliseconds / 10;
            p.Note($"one look at the creatures and places, once a second, takes {Numbers.Amount(lookMs, 3)} ms; a whole round of the {Numbers.Count(CompendiumSight.ThingCount)} things kept takes {Numbers.Amount(roundMs, 3)} ms, spread over a second's frames (about {Numbers.Amount(roundMs / 60, 3)} ms a frame at 60 frames a second), every one looked through as for a character who saw none; it saw {Numbers.Count(scratch.Count(KnownLevel.Seen))} things");

            // What is met is paired with the biome and place it stands in, under keys that name entries.
            var biome = Heightmap.FindBiome(player.transform.position);
            p.Check(biome == Heightmap.Biome.None || X.Find(EntryKeys.For(Kind.Biome, biome.ToString())) != null, $"the biome stood in ({biome}) has an entry for what is met in it to pair with");
            var stoodIn = Location.GetLocation(player.transform.position);
            p.Check(stoodIn == null || X.Find(EntryKeys.For(Kind.Location, Utils.GetPrefabName(stoodIn.gameObject))) != null, "and so has the place stood in", stoodIn != null ? Utils.GetPrefabName(stoodIn.gameObject) : "none");
            p.Note($"the character saw {Numbers.Count(CompendiumKeeper.Book?.Pairs ?? 0)} pairs go together");
            // How much the strict rules take from the character's own known pages, to tell whether they look different from the loose ones.
            var own = CompendiumKeeper.Book;
            if (own != null)
            {
                var strict = new CompendiumView(X.Entries);
                var ownFloor = Math.Min(own.Floor, CompendiumKeeper.Ceiling);
                // With a floor, the tiers tell what lies below it: wait for them to be worked out.
                if (ownFloor > 0) yield return Until(() => CompendiumTiers.Of(X.Entries) != null, 20);
                strict.Milestones(ownFloor > 0 ? CompendiumTiers.Of(X.Entries) : null, ownFloor);
                strict.Tell(own, CompendiumRule.Strict);
                var pages = X.Entries.All.Where(e => e.Kind != Kind.Mod && strict.Of(e) == KnownLevel.Known && !strict.BelowFloor(e)).ToList();
                var trimmed = pages
                    .Select(e => (entry: e, left: strict.Named(e).Distinct().Where(o => !ReferenceEquals(o, e) && o.Kind != Kind.Mod && !strict.Related(e, o)).ToList()))
                    .Where(t => t.left.Count > 0).OrderByDescending(t => t.left.Count).ToList();
                p.Note($"under the strict rules {Numbers.Count(trimmed.Count)} of {Numbers.Count(pages.Count)} known pages above the floor (tier {Numbers.Count(ownFloor)}) leave something out, {Numbers.Count(trimmed.Sum(t => t.left.Count))} names in all"
                    + (trimmed.Count == 0 ? "" : "; most: " + string.Join("; ", trimmed.Take(8).Select(t => $"{t.entry.Name} ({t.entry.Kind}) {Numbers.Count(t.left.Count)}: {string.Join(", ", t.left.Take(4).Select(o => o.Name))}"))));
            }
            p.Note(sightings.Count(KnownLevel.HeardOf) == 0 ? "nothing in sight now" : $"in sight now: {string.Join(", ", sightings.All.Select(e => $"{e.Key} ({e.Value})").Take(20))}");
            yield return Until(() => false, 2);
            p.Check(Faults.Count == faults, "nothing the compendium watches told a fault meanwhile", Faults.Latest);
        }

        /// <summary>The loaded thing of a kind nearest the character; none where none is loaded.</summary>
        private static T Nearest<T>(Player player) where T : Component =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).OrderBy(t => (t.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();

        /// <summary>
        /// A dungeon a made-up character saw only from outside shows its entrance, the stage's
        /// switch on its inside or not, with no plan of its inside; once it went inside, the inside
        /// shows again.
        /// </summary>
        private static IEnumerator SeenFromOutside(Probe p)
        {
            var crypt = Pick(Kind.Location, "Crypt2");
            if (crypt == null || PlaceOf(crypt)?.Contents?.Dungeon == null)
            {
                p.Skip("there are no Burial Chambers with an example to lay out");
                yield break;
            }
            var book = new CompendiumBook();
            book.Raise(crypt.Key, KnownLevel.Seen);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Strict);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            Stage.Inside = true;
            X.Select(crypt);
            // While it is built, the stage counts none of its rooms, which only going inside tells.
            var counted = new List<string>();
            yield return Until(() =>
            {
                var progress = ScryPanel.ExampleProgressForTest(X);
                if (progress != null && progress != DungeonWords.Entrance && !counted.Contains(progress)) counted.Add(progress);
                return Stage.InsideUnknown && Stage.HasInside;
            }, 30);
            p.Check(counted.Count == 0, "while it is built the stage counts none of its rooms", string.Join(" | ", counted));
            p.Check(Stage.InsideUnknown && Stage.HasInside && !Stage.ShowsInside, "a dungeon only seen from outside shows its entrance, its inside chosen or not",
                $"inside unknown {Stage.InsideUnknown}, has an inside {Stage.HasInside}, shows it {Stage.ShowsInside}");
            var plan = ScryPanel.Drawn(PanelPart.Plan);
            yield return null;
            yield return null;
            p.Check(ScryPanel.Drawn(PanelPart.Plan) == plan, "and no plan of its inside");
            yield return Picture(p, "strict Burial Chambers, seen from outside");

            // Gone inside, it is known, and its inside shows again.
            book.Raise(crypt.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Strict);
            yield return Until(() => !Stage.InsideUnknown && Stage.ShowsInside, 10);
            p.Check(!Stage.InsideUnknown && Stage.ShowsInside, "gone inside, its inside shows again");
            Session.ShowKnownInstead(null);
        }

        /// <summary>
        /// With the compendium on, a made-up character who has fought one creature and seen
        /// another sees only that: its creatures' tab lists those two, counted as found of all; no
        /// search finds anything not seen, and nothing not seen can be gone to; a creature only
        /// seen stands on the stage with its page closed, and the creature fought shows its whole
        /// page. Off, all is as before. The played character's book is not touched.
        /// </summary>
        private static IEnumerator CompendiumShown(Probe p)
        {
            var fought = Pick(Kind.Creature, "Troll", "Greydwarf");
            var spotted = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e != fought && e.Source is GameObject && (e.Name == "Boar" || e.Name == "Neck"))
                ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e != fought && e.Source is GameObject && !e.Empty);
            if (!p.Check(fought != null && spotted != null, "two creatures to make up a character with")) yield break;
            var book = new CompendiumBook();
            book.Raise(fought.Key, KnownLevel.Known);
            book.Raise(spotted.Key, KnownLevel.Seen);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Loose);
            // Looked at before, as with the compendium off: no step back leads there once it is on.
            var stranger = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && view.Of(e) == KnownLevel.Unknown && e.Source is GameObject);
            X.Select(stranger);
            X.Select(fought);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            if (!p.Check(ReferenceEquals(X.Compendium, view) && X.CompendiumOn, "the list is the made-up character's")) yield break;
            // What it learned is told in a notice under the header.
            var news = ScryPanel.Drawn(PanelPart.LearnedStrip);
            yield return Until(() => ScryPanel.Drawn(PanelPart.LearnedStrip) > news + 1, 3);
            p.Check(ScryPanel.Drawn(PanelPart.LearnedStrip) > news, "a notice under the header tells what it learned");

            var stepped = new List<string>();
            for (var steps = 0; steps < 60 && X.Back(); steps++)
            {
                if (X.Selected != null && X.LevelOf(X.Selected) < KnownLevel.Seen) stepped.Add(X.Selected.Name);
            }
            p.Check(stranger != null && stepped.Count == 0, $"going back never leads to what it has not seen, {stranger?.Name} looked at before among them", string.Join(", ", stepped.Take(10)));

            // The creatures' tab: the two met and what came along with them (their ragdolls), nothing standing in for the rest.
            X.SearchEverything("");
            X.KindFilter = Kind.Creature;
            // A creature's body left where it fell is no creature a player meets: neither counted nor listed (Kinds.ForPlayers).
            var creatures = X.Catalog.Count(e => e.Kind == Kind.Creature && Kinds.ForPlayers(e));
            var along = X.Catalog.Where(e => e.Kind == Kind.Creature && Kinds.ForPlayers(e) && e != fought && e != spotted && view.Of(e) >= KnownLevel.Seen).ToList();
            var strays = along.Where(e => !view.Chain(e).Any(o => o == fought || o == spotted)).ToList();
            p.Check(strays.Count == 0, $"the creatures known along with them ({Numbers.Count(along.Count)}) came by way of the two", string.Join(", ", strays.Take(10).Select(e => e.Name)));
            p.Check(X.FoundOf(Kind.Creature) == 2 + along.Count, "the creatures' tab counts them as found", Numbers.Count(X.FoundOf(Kind.Creature)));
            p.Check(X.TotalOf(Kind.Creature) == creatures, "of all there are", $"{Numbers.Count(X.TotalOf(Kind.Creature))} of {Numbers.Count(creatures)}");
            p.Check(X.Results.Count == 2 + along.Count && X.Results.Contains(fought) && X.Results.Contains(spotted), "and lists only those", string.Join(", ", X.Results.Take(10).Select(e => e.Name)));

            // No search finds what was not met, by its name or by any term.
            var leaks = new List<string>();
            var unmet = X.Catalog.Where(e => view.Of(e) < KnownLevel.Seen && e.ShownName.Length > 2).Take(60).ToList();
            foreach (var text in unmet.Select(e => e.ShownName).Concat(CompendiumTerms))
            {
                X.SearchEverything(text);
                if (X.Results.Any(e => X.LevelOf(e) < KnownLevel.Seen)) leaks.Add(text);
            }
            p.Check(leaks.Count == 0, $"no search finds what was not seen ({Numbers.Count(unmet.Count)} names and {Numbers.Count(CompendiumTerms.Length)} terms tried)", string.Join(", ", leaks.Take(10)));
            X.SearchEverything("has:Character");
            p.Check(!X.Results.Contains(spotted), $"a term telling what a page holds does not find {spotted.Name}, only seen");

            // Nor does the search box offer a term naming what was not seen.
            yield return Until(() => ScryPanel.Offered(X, SearchHelp.Term("drops", "")) != null, 10);
            var seenNames = new HashSet<string>(X.Catalog.Where(e => view.Of(e) >= KnownLevel.Seen).SelectMany(e => new[] { e.Name, e.ShownName }), StringComparer.Ordinal);
            var unmetNames = new HashSet<string>(X.Catalog.Where(e => view.Of(e) < KnownLevel.Seen).SelectMany(e => new[] { e.Name, e.ShownName }).Where(n => !seenNames.Contains(n)), StringComparer.Ordinal);
            var offered = 0;
            var named = new List<string>();
            foreach (var key in Search.LinkKeys.Concat(OfferKeys))
            {
                var found = ScryPanel.Offered(X, SearchHelp.Term(key, "")) ?? new List<Suggestion>();
                offered += found.Count;
                named.AddRange(found.Where(s => unmetNames.Contains(s.Label)).Select(s => SearchHelp.Term(key, s.Label)));
            }
            p.Check(ScryPanel.Offered(X, SearchHelp.Term("drops", "")) != null, "the search box has its terms");
            p.Check(named.Count == 0, $"the search box offers no term naming what was not seen ({Numbers.Count(offered)} offered)", string.Join(", ", named.Take(10)));

            // Not seen: no link leads to it, and picked some other way its page is closed with nothing on the stage.
            var unseen = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && X.LevelOf(e) == KnownLevel.Unknown && e.Source is GameObject);
            if (!p.Check(unseen != null, "a creature not seen")) yield break;
            p.Check(!X.Jump(unseen.Key), $"going to {unseen.Name}, not seen, leads nowhere");
            var closed = ScryPanel.Drawn(PanelPart.ClosedPage);
            X.Select(unseen);
            yield return Until(() => ScryPanel.Drawn(PanelPart.ClosedPage) > closed + 2, 3);
            p.Check(ScryPanel.Drawn(PanelPart.ClosedPage) > closed, "picked anyway, it shows a closed page");
            p.Check(X.Shown == null && Stage.Showing == null && Stage.Subject == null, "and nothing on the stage", Stage.Showing?.Name);

            // Only seen: on the stage, page closed.
            X.Select(spotted);
            closed = ScryPanel.Drawn(PanelPart.ClosedPage);
            yield return Until(() => Stage.Showing == spotted && ScryPanel.Drawn(PanelPart.ClosedPage) > closed, 5);
            p.Check(Stage.Showing == spotted, $"{spotted.Name}, only seen, stands on the stage", Stage.Showing?.Name);
            p.Check(ScryPanel.Drawn(PanelPart.ClosedPage) > closed, "with its page closed");

            // Fought: the whole page, leaving out what was not met, its drops among them, and counting it faintly.
            var drop = fought.TermLinks("drops").FirstOrDefault(d => X.LevelOf(d) < KnownLevel.Seen);
            var notMetLines = ScryPanel.Drawn(PanelPart.StrictLeftOut);
            X.Select(fought);
            yield return Until(() => Stage.Showing == fought, 5);
            closed = ScryPanel.Drawn(PanelPart.ClosedPage);
            var repaints = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
            p.Check(ScryPanel.Drawn(PanelPart.ClosedPage) == closed, $"{fought.Name}, fought, shows its whole page");
            p.Check(drop != null && !X.Reaches(drop.Key), $"what it drops, not seen, is no link: {drop?.Name}");
            p.Check(drop != null && X.LeavesOut(drop.Key) && !X.LeavingOut, $"under the loose rules its page leaves out {drop?.Name}, not yet met");
            p.Check(ScryPanel.Drawn(PanelPart.StrictLeftOut) > notMetLines && X.LeftOutLine("Drops", 1) == CompendiumWords.NotYetMet("Drops", 1),
                "and says faintly how many more were not yet met", X.LeftOutLine("Drops", 1));
            p.Check(ScryPanel.Drawn(PanelPart.YouBox) > 0, "and opens with a box of what the character did with it and how it came to know it");

            // Strict: the fought creature's page keeps the drop seen fall, leaves out one that was not, and draws without fault.
            var drops = fought.TermLinks("drops").ToList();
            if (p.Check(drops.Count >= 2, $"{fought.Name} drops two things at least"))
            {
                // As in play, a drop seen falling is seen, and seen together with what dropped it.
                book.Raise(drops[0].Key, KnownLevel.Seen);
                book.Witness(fought.Key, drops[0].Key);
                view.Tell(book, CompendiumRule.Strict);
                X.Select(fought);
                var strictFaults = Faults.Count;
                var leftLines = ScryPanel.Drawn(PanelPart.StrictLeftOut);
                var emptyRows = ScryPanel.Drawn(PanelPart.EmptyRowLeftOut);
                repaints = ScryPanel.Repaints;
                yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
                p.Check(X.LeavingOut && !X.LeavesOut(drops[0].Key) && X.LeavesOut(drops[1].Key), $"under the strict rules its page keeps {drops[0].Name}, seen fall, and leaves out {drops[1].Name}");
                p.Check(ScryPanel.Drawn(PanelPart.StrictLeftOut) > leftLines, "and says faintly at the end of its loot how many more were not yet seen fall");
                // Its biomes, none seen with it, leave their row with nothing: it draws nothing, its title neither.
                p.Check(ScryPanel.Drawn(PanelPart.EmptyRowLeftOut) > emptyRows, "a row the strict rules leave with nothing draws nothing, not even its title");
                p.Check(Faults.Count == strictFaults, "and draws without fault", Faults.Latest);
                // How it takes each damage type stays back until a hit told it.
                p.Check(X.HidesDamage("Fire") && X.HidesDamage("Blunt"), "its damage grid keeps back how it takes each type, never hit with any");
                p.Check(ScryPanel.Drawn(PanelPart.DamageKeptBack) > 0, "and says faintly under it how many it keeps back");
                // The You box gathers what is still to find out at the page's top (read again once a second).
                var still = ScryPanel.Drawn(PanelPart.StillToFindOut);
                yield return Until(() => ScryPanel.Drawn(PanelPart.StillToFindOut) > still, 3);
                p.Check(ScryPanel.Drawn(PanelPart.StillToFindOut) > still, "its You box says what is still to find out and what to do for it");
                book.Witness(fought.Key, EntryKeys.Damage("Fire"));
                p.Check(!X.HidesDamage("Fire") && X.HidesDamage("Blunt"), "a hit the game told how fire lands shows fire alone");
                var drawn = ScryPanel.AsDrawn(X, $"Feared by {unseen.ShownName} alone.");
                p.Check(drawn.Contains(CompendiumWords.Hidden) && !drawn.Contains(unseen.ShownName), $"its text keeps back the name of {unseen.Name}, never met", drawn);
                var card = new Tooltips.Shown { Name = fought.ShownName };
                card.Parts.Add(new TooltipPart(TooltipPartKind.Row, "Feared by", unseen.ShownName, "", true));
                var cardShown = ScryPanel.CardShown(X, card).Parts[0].Value;
                p.Check(cardShown.Contains(CompendiumWords.Hidden) && !cardShown.Contains(unseen.ShownName), $"and so does the game's tooltip card", cardShown);
                view.Tell(book, CompendiumRule.Loose);
                // Under the loose rules only what was not met is left out, and its sections say so in those words.
                yield return Until(() => !X.LeavingOut, 3);
                var looseLines = ScryPanel.Drawn(PanelPart.StrictLeftOut);
                repaints = ScryPanel.Repaints;
                yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
                p.Check(!X.LeavesOut(drops[0].Key) && X.LeavesOut(drops[1].Key) == (X.LevelOf(drops[1]) < KnownLevel.Seen),
                    $"under the loose rules its page names {drops[0].Name}, seen, and leaves out only what was not met");
                p.Check(ScryPanel.Drawn(PanelPart.StrictLeftOut) > looseLines && X.LeftOutLine("Drops", 1) == CompendiumWords.NotYetMet("Drops", 1),
                    "and its sections count what was not yet met, not what was not seen together", X.LeftOutLine("Drops", 1));
                p.Check(!X.HidesDamage("Blunt"), "nor keeps back how a damage type lands");
            }

            // A tree known lists only the tools seen that break it: none here, so its Broken with is left out.
            var tree = Pick(Kind.Resource, "Beech1", "Birch1", "Pinetree_01");
            if (p.Check(tree != null, "a tree to know"))
            {
                book.Raise(tree.Key, KnownLevel.Known);
                view.Tell(book, CompendiumRule.Loose);
                var leftOut = ScryPanel.Drawn(PanelPart.ListLeftOut);
                // Listed as a player would find it: the list kept to creatures above would drop it again.
                X.SearchEverything("");
                X.KindFilter = tree.Kind;
                X.Select(tree);
                yield return Until(() => Stage.Showing == tree, 5);
                repaints = ScryPanel.Repaints;
                yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
                // Why not, where it fails: the row the page holds, and the tools on it the made-up character reaches.
                var broken = Facts.For(tree).Rows.FirstOrDefault(r => r.Title == GatherWords.BrokenWithTitle);
                var reached = broken?.Items.Where(i => X.Reaches(i.Prefab)).Select(i => i.Prefab).ToList() ?? new List<string>();
                p.Check(X.Selected == tree && ScryPanel.Drawn(PanelPart.ListLeftOut) > leftOut, $"{tree.Name}, known, names no tool not seen that breaks it",
                    $"selected {X.Selected?.Name}, shown {X.LevelOf(tree)}, row {(broken == null ? "none" : $"of {Numbers.Count(broken.Items.Count)} tools, {Numbers.Count(reached.Count)} reached: {string.Join(", ", reached.Take(8))}")}");
            }

            // A mod's page and the mod report list only what the character has seen of what a mod adds.
            var adder = X.Catalog.Where(e => e.Kind != Kind.Mod && e.ModName.Length > 0).GroupBy(e => e.ModName).OrderByDescending(g => g.Count())
                .Select(g => X.Find(EntryKeys.For(Kind.Mod, g.Key))).FirstOrDefault(m => m != null);
            if (adder != null)
            {
                var plain = ScryPanel.Drawn(PanelPart.UnseenName);
                X.Select(adder);
                repaints = ScryPanel.Repaints;
                yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
                p.Check(ScryPanel.Drawn(PanelPart.UnseenName) == plain, $"{adder.Name}'s page names nothing it adds that was not seen", Numbers.Count(ScryPanel.Drawn(PanelPart.UnseenName) - plain));
                ScryPanel.ShowModReport();
                var reported = ScryPanel.Drawn(PanelPart.ModReport);
                plain = ScryPanel.Drawn(PanelPart.UnseenName);
                yield return Until(() => ScryPanel.Drawn(PanelPart.ModReport) > reported + 2, 3);
                p.Check(ScryPanel.Drawn(PanelPart.ModReport) > reported && ScryPanel.Drawn(PanelPart.UnseenName) == plain, "nor does the mod report", Numbers.Count(ScryPanel.Drawn(PanelPart.UnseenName) - plain));
                ScryPanel.HideModReport();
            }
            else p.Note("no mod adds anything: mod pages not tried");

            // Knowing what something gives does not make known what else gives it: bees give Poison, bile bombs too, and Burning, as Fader's fire does.
            var hive = X.Find("piece_beehive");
            var bile = X.Find("bilebomb_explosion");
            var poison = X.Find(EntryKeys.For(Kind.StatusEffect, "Poison"));
            if (p.Check(hive != null && bile != null && poison != null, "a beehive, a bile bomb and Poison to try it with"))
            {
                var bees = new CompendiumBook();
                bees.Raise(hive.Key, KnownLevel.Known);
                var beesView = new CompendiumView(X.Entries);
                beesView.Tell(bees, CompendiumRule.Loose);
                var fader = X.Catalog.Where(e => e.Name.StartsWith("Fader", StringComparison.Ordinal) && beesView.Of(e) == KnownLevel.Known).ToList();
                p.Check(beesView.Of(poison) == KnownLevel.Known, "a character that knows only a beehive knows the Poison its bees give");
                p.Check(beesView.Of(bile) < KnownLevel.Known && fader.Count == 0, "but no bile bomb, nor anything of Fader's", string.Join(", ", fader.Take(5).Select(e => e.Name)));
            }

            // A dungeon known makes its rooms known, every one, once the places are read.
            var roomLabels = new HashSet<string>(X.Catalog.Where(e => e.Kind == Kind.Location && e.Components.Contains("Room")).SelectMany(e => e.FoundIn), StringComparer.Ordinal);
            var dungeon = X.Catalog.FirstOrDefault(e => Places.LabelOf(e) != null && roomLabels.Contains(Places.LabelOf(e)));
            if (dungeon != null)
            {
                var delver = new CompendiumBook();
                delver.Raise(dungeon.Key, KnownLevel.Known);
                var delverView = new CompendiumView(X.Entries);
                delverView.Tell(delver, CompendiumRule.Loose);
                var rooms = X.Catalog.Where(e => e.Kind == Kind.Location && e.Components.Contains("Room") && e.FoundIn.Contains(Places.LabelOf(dungeon))).ToList();
                var shut = rooms.Where(r => delverView.Of(r) != KnownLevel.Known).ToList();
                p.Check(shut.Count == 0, $"a character that knows {dungeon.Name} knows its {Numbers.Count(rooms.Count)} kinds of room", string.Join(", ", shut.Take(5).Select(r => r.Name)));
            }
            else p.Note("no dungeon with rooms read: rooms known with their dungeon not tried");

            // Learned lately: what it met, newest first, nothing only heard of; a tab after All.
            ScryPanel.Compact = false;
            X.SearchEverything("");
            var learnedTabs = ScryPanel.Drawn(PanelPart.LearnedTab);
            X.LearnedOnly = true;
            var learned = view.Lately.Select(k => X.Find(k)).Where(e => e != null && view.Of(e) >= KnownLevel.Seen).ToList();
            p.Check(learned.Count > 0 && X.Results.SequenceEqual(learned), $"learned lately lists the {Numbers.Count(learned.Count)} things it met, newest first", string.Join(", ", X.Results.Take(10).Select(e => e.Name)));
            yield return Until(() => ScryPanel.Drawn(PanelPart.LearnedTab) > learnedTabs + 1, 3);
            p.Check(ScryPanel.Drawn(PanelPart.LearnedTab) > learnedTabs && X.LearnedCount == learned.Count, $"its tab stands after All, counting {Numbers.Count(learned.Count)}", $"counted {Numbers.Count(X.LearnedCount)}");
            X.LearnedOnly = false;

            // The places' tab says the explored map counts, and offers to read it again.
            var mapBars = ScryPanel.Drawn(PanelPart.MapBar);
            X.KindFilter = Kind.Location;
            yield return Until(() => ScryPanel.Drawn(PanelPart.MapBar) > mapBars + 1, 3);
            p.Check(ScryPanel.Drawn(PanelPart.MapBar) > mapBars, "the places' tab tells that the explored map counts, with Look at the map again");
            X.KindFilter = null;

            // Peek: each page whole, its links leading anywhere, a whole name in quotes finding what
            // was not met, the list as the character knows it, under a warning, until stopped or the
            // panel closes.
            var strip = ScryPanel.Drawn(PanelPart.PeekStrip);
            var eyes = ScryPanel.Drawn(PanelPart.PeekButton);
            X.Peeking = true;
            X.SearchEverything("");
            X.KindFilter = Kind.Creature;
            yield return Until(() => ScryPanel.Drawn(PanelPart.PeekStrip) > strip + 1, 3);
            p.Check(X.Peeking && !X.Results.Contains(unseen) && ScryPanel.Drawn(PanelPart.PeekStrip) > strip, $"peeking, the list stays as the character knows it, {unseen.Name} not in it, under a warning");
            X.SearchEverything("\"" + unseen.Name + "\"");
            p.Check(X.Results.Contains(unseen), $"a whole name in quotes finds {unseen.Name}", $"{Numbers.Count(X.Results.Count)} found");
            X.Select(unseen);
            p.Check(X.Selected == unseen && X.LevelOf(unseen) == KnownLevel.Known && X.Reaches(unseen.Key), "and its page shows whole");
            X.SearchEverything("");
            p.Check(CompendiumKeeper.Book == null || ScryPanel.Drawn(PanelPart.PeekButton) > eyes, "the eye that peeks stands beside the compendium's button in the title row");
            X.Peeking = false;
            p.Check(!X.Results.Contains(unseen) && X.LevelOf(unseen) < KnownLevel.Seen, "stopped, it is not listed and its page is closed again");
            X.Peeking = true;
            Session.Hide();
            p.Check(!X.Peeking, "closing the panel ends a peek");
            Session.Show(null);

            // Off: everything as before.
            view.Tell(book, CompendiumRule.Off);
            yield return Until(() => !X.CompendiumOn, 3);
            p.Check(!X.CompendiumOn && X.LevelOf(unseen) == KnownLevel.Known, "with the compendium off, everything shows as before");
        }
    }
}
