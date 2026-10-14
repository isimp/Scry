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
            var known = book.Count(KnownLevel.Known);
            var seen = book.Count(KnownLevel.Seen);
            p.Note($"it knows {Numbers.Count(known)} entries, has seen {Numbers.Count(seen - known)} more and heard of {Numbers.Count(book.Count(KnownLevel.HeardOf) - seen)}; learned lately: {string.Join(", ", book.Lately.Take(12))}");

            // The records as the game keeps them, read whole.
            var records = new CompendiumRecords();
            CharacterRecords.Read(player, profile, records);
            var sets = new[] { records.Materials, records.Recipes, records.Stations, records.Biomes, records.Trophies, records.Killed, records.PickedUp, records.Crafted, records.Eaten, records.Picked, records.Placed };
            var size = CharacterRecords.Size(player, profile);
            p.Check(sets.Sum(s => s.Count) == size, "the character's records are read whole", $"{Numbers.Count(sets.Sum(s => s.Count))} of {Numbers.Count(size)}");
            p.Note($"records: {Numbers.Count(records.Materials.Count)} items had, {Numbers.Count(records.Recipes.Count)} recipes, {Numbers.Count(records.Stations.Count)} stations, {Numbers.Count(records.Biomes.Count)} biomes, {Numbers.Count(records.Trophies.Count)} trophies, {Numbers.Count(records.Killed.Count)} kinds killed, {Numbers.Count(records.PickedUp.Count)} picked up, {Numbers.Count(records.Crafted.Count)} crafted, {Numbers.Count(records.Eaten.Count)} eaten, {Numbers.Count(records.Picked.Count)} picked, {Numbers.Count(records.Placed.Count)} placed");

            // Everything they tell is in the book: the catch-up ran on this catalog.
            var told = new CompendiumBook();
            new CompendiumCatchUp(X.Entries).Fill(records, told, quietly: true);
            yield return Until(() => Short(told, CompendiumKeeper.Book).Count == 0, 5);
            var shortOf = Short(told, CompendiumKeeper.Book);
            p.Check(shortOf.Count == 0, $"everything its records tell is in its book ({Numbers.Count(told.Count(KnownLevel.HeardOf))} entries)", string.Join(", ", shortOf.Take(10)));

            // What it carries is known within a moment.
            var carried = player.GetInventory().GetAllItems().Where(i => i?.m_dropPrefab != null).Select(i => i.m_dropPrefab.name).Where(n => X.Find(n) != null).Distinct().ToList();
            yield return Until(() => carried.All(n => CompendiumKeeper.Book.Level(n) == KnownLevel.Known), 3);
            var unknown = carried.Where(n => CompendiumKeeper.Book.Level(n) != KnownLevel.Known).ToList();
            p.Check(unknown.Count == 0, $"all {Numbers.Count(carried.Count)} kinds of thing it carries are known", string.Join(", ", unknown.Take(10)));

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

            // Kept on disk, as it is now.
            CompendiumKeeper.Save(now: true);
            var now = CompendiumKeeper.Book.All.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
            var path = CompendiumKeeper.FilePath;
            p.Note($"kept in {path}");
            yield return Until(() => KeptHolds(path, now), 5);
            p.Check(KeptHolds(path, now), "its book is kept in Scry's cache folder, all of it", path);
        }

        /// <summary>Keys besides the link keys whose offered values name entries: none may name what a made-up character has not seen.</summary>
        private static readonly string[] OfferKeys = { "playedby", "station" };

        /// <summary>
        /// The explored map is read: the ground the character stands on is explored, the places
        /// this game holds lie partly in it, and where this game is the world's server it answers
        /// a map sent to itself as it would a player's, with as many kinds as read here. The book
        /// of the character played is caught up once, after a while in the world.
        /// </summary>
        private static IEnumerator CompendiumMapRead(Probe p)
        {
            var player = Player.m_localPlayer;
            if (!p.Check(player != null && ZNet.instance != null, "a character is played in a world")) yield break;
            var packed = CompendiumMap.PackedMap();
            if (!p.Check(packed != null, "its explored map is read")) yield break;
            var grid = ExploredGrid.Unpack(packed);
            p.Check(grid != null && grid.Explored(player.transform.position.x, player.transform.position.z), "the ground it stands on is explored", grid != null ? $"cells of {Numbers.Count(grid.Factor)} pixels a side" : null);
            var world = CompendiumMap.World;
            var book = CompendiumKeeper.Book;
            if (!ZNet.instance.IsServer())
            {
                p.Note(book != null && book.Mapped(world) ? "a server world, caught up with its server's answer" : "a server world not caught up: its server answers no map (no Scry there)");
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

        /// <summary>Things whose tier the game's progression fixes: iron gear in the Swamp's, black metal in the Plains', each boss in its own.</summary>
        private static readonly (string Name, int Tier)[] KnownTiers =
        {
            ("Eikthyr", 1), ("gd_king", 2), ("Bonemass", 3), ("Dragon", 4), ("GoblinKing", 5), ("SeekerQueen", 6), ("Fader", 7),
            ("Troll", 2), ("SwordIron", 3), ("SwordBlackmetal", 5), ("Lox", 5), ("Wood", 1),
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
            foreach (var (name, tier) in KnownTiers)
            {
                var entry = X.Find(name);
                if (entry == null) continue;
                var got = tiers.TryGetValue(entry.Key, out var t) ? t : Tiers.None;
                if (got != tier) wrong.Add($"{name} {(got == Tiers.None ? "none" : Numbers.Count(got))}, not {Numbers.Count(tier)}");
            }
            p.Check(wrong.Count == 0, "they follow the game's progression: each boss its own, iron gear the Swamp's, black metal the Plains'", string.Join("; ", wrong));
            var serpent = X.Find("Serpent");
            p.Check(serpent == null || !tiers.ContainsKey(serpent.Key), "and what lives only at sea has none");
        }

        /// <summary>Terms tried on a made-up character, each of which must not find what it has not met.</summary>
        private static readonly string[] CompendiumTerms = { "kind:creature", "is:boss", "biome:meadows", "drops:wood", "has:Character", "in:crypt" };

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
        /// The watchers sit on the game's methods they rely on, and looking around the character
        /// finds no fault; what is in sight now is told, learned into a book of the test's own.
        /// </summary>
        private static IEnumerator CompendiumWatched(Probe p)
        {
            foreach (var (type, name) in new[] { (typeof(Character), "Damage"), (typeof(Character), "RPC_Damage"), (typeof(Destructible), "Damage"), (typeof(TreeBase), "Damage"), (typeof(TreeLog), "Damage"), (typeof(MineRock), "Damage"), (typeof(MineRock5), "Damage") })
            {
                var method = AccessTools.DeclaredMethod(type, name);
                p.Check(method != null && Harmony.GetPatchInfo(method)?.Owners.Contains(About.Guid) == true, $"{type.Name}.{name} is watched");
            }

            var player = Player.m_localPlayer;
            if (!p.Check(player != null, "a character is played")) yield break;
            var faults = Faults.Count;
            var sightings = new CompendiumBook();
            var failed = Steps.Run(() => CompendiumSight.Look(player, new CompendiumCatchUp(X.Entries), sightings), null);
            p.Check(failed == null, "looking around the character finds no fault", failed?.Message);
            p.Note(sightings.Count(KnownLevel.HeardOf) == 0 ? "nothing in sight now" : $"in sight now: {string.Join(", ", sightings.All.Select(e => $"{e.Key} ({e.Value})").Take(20))}");
            yield return Until(() => false, 2);
            p.Check(Faults.Count == faults, "nothing the compendium watches told a fault meanwhile", Faults.Latest);
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
            var creatures = X.Catalog.Count(e => e.Kind == Kind.Creature);
            var along = X.Catalog.Where(e => e.Kind == Kind.Creature && e != fought && e != spotted && view.Of(e) >= KnownLevel.Seen).ToList();
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

            // Fought: the whole page, naming what was not seen, its drops among them, by name only.
            var drop = fought.TermLinks("drops").FirstOrDefault(d => X.LevelOf(d) < KnownLevel.Seen);
            var unseenNames = ScryPanel.Drawn(PanelPart.UnseenName);
            X.Select(fought);
            yield return Until(() => Stage.Showing == fought, 5);
            closed = ScryPanel.Drawn(PanelPart.ClosedPage);
            var repaints = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
            p.Check(ScryPanel.Drawn(PanelPart.ClosedPage) == closed, $"{fought.Name}, fought, shows its whole page");
            p.Check(drop != null && !X.Reaches(drop.Key), $"what it drops, not seen, is no link: {drop?.Name}");
            p.Check(ScryPanel.Drawn(PanelPart.UnseenName) > unseenNames, "its page tells the names of what was not seen as plain text");

            // A tree known lists only the tools seen that break it: none here, so its Broken with is left out.
            var tree = Pick(Kind.Resource, "Beech1", "Birch1", "Pinetree_01");
            if (p.Check(tree != null, "a tree to know"))
            {
                book.Raise(tree.Key, KnownLevel.Known);
                view.Tell(book, CompendiumRule.Loose);
                var leftOut = ScryPanel.Drawn(PanelPart.ListLeftOut);
                X.Select(tree);
                repaints = ScryPanel.Repaints;
                yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
                p.Check(X.Selected == tree && ScryPanel.Drawn(PanelPart.ListLeftOut) > leftOut, $"{tree.Name}, known, names no tool not seen that breaks it");
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

            // Learned lately: what it met, newest first, nothing only heard of.
            X.SearchEverything("");
            X.LearnedOnly = true;
            var learned = view.Lately.Select(k => X.Find(k)).Where(e => e != null && view.Of(e) >= KnownLevel.Seen).ToList();
            p.Check(learned.Count > 0 && X.Results.SequenceEqual(learned), $"learned lately lists the {Numbers.Count(learned.Count)} things it met, newest first", string.Join(", ", X.Results.Take(10).Select(e => e.Name)));
            X.LearnedOnly = false;

            // Peek: everything shows, with a warning, until stopped or the panel closes.
            var strip = ScryPanel.Drawn(PanelPart.PeekStrip);
            X.Peeking = true;
            X.SearchEverything("");
            X.KindFilter = Kind.Creature;
            yield return Until(() => ScryPanel.Drawn(PanelPart.PeekStrip) > strip + 1, 3);
            p.Check(X.Peeking && X.Results.Contains(unseen) && ScryPanel.Drawn(PanelPart.PeekStrip) > strip, $"peeking, {unseen.Name} is listed, under a warning");
            X.Peeking = false;
            p.Check(!X.Results.Contains(unseen), "stopped, it is not");
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
