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
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            if (!p.Check(ReferenceEquals(X.Compendium, view) && X.CompendiumOn, "the list is the made-up character's")) yield break;

            // The creatures' tab: the two met, nothing standing in for the rest.
            X.SearchEverything("");
            X.KindFilter = Kind.Creature;
            var creatures = X.Catalog.Count(e => e.Kind == Kind.Creature);
            p.Check(X.FoundOf(Kind.Creature) == 2, "the creatures' tab counts the two met as found", Numbers.Count(X.FoundOf(Kind.Creature)));
            p.Check(X.TotalOf(Kind.Creature) == creatures, "of all there are", $"{Numbers.Count(X.TotalOf(Kind.Creature))} of {Numbers.Count(creatures)}");
            p.Check(X.Results.Count == 2 && X.Results.Contains(fought) && X.Results.Contains(spotted), "and lists only those two", string.Join(", ", X.Results.Take(10).Select(e => e.Name)));

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

            // Fought: the whole page.
            X.Select(fought);
            yield return Until(() => Stage.Showing == fought, 5);
            closed = ScryPanel.Drawn(PanelPart.ClosedPage);
            var repaints = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
            p.Check(ScryPanel.Drawn(PanelPart.ClosedPage) == closed, $"{fought.Name}, fought, shows its whole page");

            // Off: everything as before.
            view.Tell(book, CompendiumRule.Off);
            yield return Until(() => !X.CompendiumOn, 3);
            p.Check(!X.CompendiumOn && X.LevelOf(unseen) == KnownLevel.Known, "with the compendium off, everything shows as before");
        }
    }
}
