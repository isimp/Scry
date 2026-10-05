using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for the catalog, the search and the list: the startup check, every
    /// kind in the catalog, each kind of search term, favourites, recent and history, the tabs
    /// and their groups, the catalog's soundness, the search's speed, and the list's keys,
    /// filters and help.
    /// </summary>
    internal static partial class SelfTest
    {
        // ----- The catalog and the search -----

        private static IEnumerator StartupCheck(Probe p)
        {
            p.Check(Compatibility.Checked, "the startup check has run");
            var off = Compatibility.FeaturesOff();
            p.Check(off.Count == 0, "no feature is off", string.Join("; ", off));
            yield break;
        }

        private static IEnumerator Catalog(Probe p)
        {
            var counts = X.Catalog.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
            p.Note(string.Join(", ", counts.OrderBy(c => c.Key).Select(c => $"{Numbers.Count(c.Value)} {Kinds.Label(c.Key).ToLowerInvariant()}")));
            foreach (Kind kind in Enum.GetValues(typeof(Kind)))
            {
                if (kind == Kind.Other) continue;
                p.Check(counts.TryGetValue(kind, out var n) && n > 0, $"there are {Kinds.Label(kind).ToLowerInvariant()}");
            }
            var rooms = X.Catalog.Count(e => e.Source is PlaceSource place && place.IsRoom);
            p.Check(rooms > 0, "the locations include dungeon rooms", $"{Numbers.Count(rooms)} rooms");
            p.Note($"{Numbers.Count(rooms)} of the locations are dungeon rooms");

            // What the game has of its own, as the origin switch tells it.
            foreach (var kind in new[] { Kind.Location, Kind.Raid })
            {
                var of = X.Catalog.Where(e => e.Kind == kind).ToList();
                p.Note($"{Kinds.Label(kind).ToLowerInvariant()}: {Numbers.Count(of.Count(e => e.Origin == Origin.Vanilla))} the game's, {Numbers.Count(of.Count(e => e.Origin == Origin.Mod))} mods', {Numbers.Count(of.Count(e => e.Origin == Origin.Unknown))} not told");
            }
            var temple = LocationNamed("StartTemple");
            if (temple != null) p.Check(temple.Origin == Origin.Vanilla, "the start temple counts as the game's own", temple.Origin.ToString());
            var eikthyr = Pick(Kind.Raid, "army_eikthyr");
            if (eikthyr != null) p.Check(eikthyr.Origin == Origin.Vanilla, "Eikthyr's raid counts as the game's own", eikthyr.Origin.ToString());
            yield break;
        }

        private static IEnumerator Searching(Probe p)
        {
            X.SearchEverything("troll");
            p.Check(X.Results.Any(e => e.Kind == Kind.Creature && e.Name == "Troll"), "\"troll\" finds the troll");

            X.SearchEverything("kind:location");
            p.Check(X.Results.Count > 0 && X.Results.All(e => e.Kind == Kind.Location), "\"kind:location\" finds locations only", $"{Numbers.Count(X.Results.Count)} results");

            X.SearchEverything("kind:location biome:swamp");
            p.Check(X.Results.Count > 0 && X.Results.All(e => e.Kind == Kind.Location && e.Biomes.Contains("Swamp")), "\"kind:location biome:swamp\" finds the swamp's locations", $"{Numbers.Count(X.Results.Count)} results");

            X.SearchEverything("kind:raid");
            p.Check(X.Results.Count > 0 && X.Results.All(e => e.Kind == Kind.Raid), "\"kind:raid\" finds raids only", $"{Numbers.Count(X.Results.Count)} results");
            X.SearchEverything("");
            yield break;
        }

        /// <summary>Each kind of term the search's help names finds what it says it finds.</summary>
        private static IEnumerator SearchTerms(Probe p)
        {
            List<Entry> Find(string text)
            {
                X.SearchEverything(text);
                return X.Results.ToList();
            }

            var troll = Find("troll");
            p.Check(troll.Any(e => e.Name == "Troll"), "\"troll\" finds the troll", $"{Numbers.Count(troll.Count)} results");
            var noRagdoll = Find("troll -ragdoll");
            p.Check(noRagdoll.Count > 0 && noRagdoll.All(e => e.Name.IndexOf("ragdoll", StringComparison.OrdinalIgnoreCase) < 0), "a minus leaves out what matches it", $"{Numbers.Count(noRagdoll.Count)} results");
            var lights = Find("has:light");
            p.Check(lights.Count > 0 && lights.All(e => e.Components.Any(c => c.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0)), "\"has:light\" finds what has a light", $"{Numbers.Count(lights.Count)} results");
            var swamp = Find("biome:swamp");
            p.Check(swamp.Count > 0 && swamp.All(e => e.Biomes.Any(b => b.IndexOf("swamp", StringComparison.OrdinalIgnoreCase) >= 0)), "\"biome:swamp\" finds what lives in the swamp", $"{Numbers.Count(swamp.Count)} results");
            var played = Find("playedby:troll");
            p.Note("\"playedby:troll\": " + string.Join(", ", played.GroupBy(e => e.Kind).Select(g => $"{Numbers.Count(g.Count())} {Kinds.Label(g.Key).ToLowerInvariant()}")));
            p.Check(played.Count > 0 && played.All(e => e.UsedBy.Any(u => u.IndexOf("troll", StringComparison.OrdinalIgnoreCase) >= 0))
                    && played.Any(e => e.Kind == Kind.Sound) && played.Any(e => e.Kind == Kind.Effect),
                    "\"playedby:troll\" finds what the troll's effect lists play, its sounds and effects among them", $"{Numbers.Count(played.Count)} results");
            var forge = Find("station:forge");
            p.Check(forge.Count > 0 && forge.All(e => e.Stations.Any(s => s.Name.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0 || s.Shown.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0)), "\"station:forge\" finds what is made at a forge", $"{Numbers.Count(forge.Count)} results");
            var hand = Find("station:hand");
            p.Check(hand.Count > 0, "\"station:hand\" finds what needs no station", $"{Numbers.Count(hand.Count)} results");
            var creatures = Find("-has:ragdoll kind:c");
            p.Check(creatures.Count > 0 && creatures.All(e => e.Kind == Kind.Creature), "terms combine and shorten (\"-has:ragdoll kind:c\")", $"{Numbers.Count(creatures.Count)} results");
            var mod = X.Catalog.Where(e => e.Origin == Origin.Mod && e.ModName.Length > 0 && e.Kind != Kind.Mod).Select(e => e.ModName).FirstOrDefault();
            if (mod != null)
            {
                var ofMod = Find(SearchHelp.Term("mod", mod));
                p.Check(ofMod.Count > 0 && ofMod.All(e => e.ModName == mod), $"\"mod:\" finds only what {mod} added", $"{Numbers.Count(ofMod.Count)} results");
            }
            else p.Note("no mod adds anything, so mod: is not tried");
            var exact = Find("\"troll\"");
            p.Check(exact.Any(e => e.Name == "Troll") && exact.All(e => string.Equals(e.Name, "troll", StringComparison.OrdinalIgnoreCase) || string.Equals(e.DisplayName, "troll", StringComparison.OrdinalIgnoreCase)), "a name in quotes finds only what is called exactly that", string.Join(", ", exact.Select(e => e.Name)));
            var either = Find("biome:swamp,mountain kind:creature");
            p.Check(either.Any(e => e.Biomes.Contains("Swamp")) && either.Any(e => e.Biomes.Contains("Mountain")), "a comma reads as or (\"biome:swamp,mountain\")", $"{Numbers.Count(either.Count)} results");

            // What is: finds, each as the details tell it.
            var bosses = Find("is:boss");
            p.Check(bosses.Any(e => e.Name == "Eikthyr") && bosses.All(e => (e.Source as GameObject).OrNull()?.GetComponent<Character>().OrNull()?.m_boss == true), "\"is:boss\" finds Eikthyr and only bosses", string.Join(", ", bosses.Take(12).Select(e => e.Name)));
            var tame = Find("is:tameable");
            p.Check(tame.Any(e => e.Name == "Boar") && tame.Any(e => e.Name == "Wolf") && tame.All(e => (e.Source as GameObject).OrNull()?.GetComponent<Tameable>() != null), "\"is:tameable\" finds the boar and the wolf, and only what can be tamed", string.Join(", ", tame.Take(12).Select(e => e.Name)));
            var flying = Find("is:flying");
            p.Note("\"is:flying\": " + string.Join(", ", flying.Take(12).Select(e => e.Name)));
            p.Check(flying.Any(e => e.Name == "Deathsquito"), "\"is:flying\" finds the deathsquito");
            foreach (var (flag, named) in new[] { ("wearable", "HelmetBronze"), ("weapon", "SwordIron"), ("ammo", "ArrowWood"), ("food", "CookedMeat"), ("craftable", "SwordIron"), ("buildable", "piece_workbench") })
            {
                var flagged = Find(SearchHelp.Term("is", flag));
                p.Check(flagged.Any(e => e.Name == named), $"\"is:{flag}\" finds {named}", $"{Numbers.Count(flagged.Count)} results");
            }
            p.Check(Find("is:weapon kind:creature").Count == 0, "no creature is a weapon");

            // The fight terms, each against the details' own grid and figures.
            var weak = Find("weak:fire kind:creature");
            var notWeak = weak.Take(15).Where(e => !Facts.For(e).Rows.Any(r => r.Cells != null && r.Cells.Any(c => c.Type == "Fire" && c.Tone == Tone.Weak))).Select(e => e.Name).ToList();
            p.Check(weak.Count > 0 && notWeak.Count == 0, "\"weak:fire\" finds creatures whose grid shows them weak to fire", $"{Numbers.Count(weak.Count)} results; not so: {string.Join(", ", notWeak)}");
            p.Note("\"immune:chop kind:creature\": " + Numbers.Count(Find("immune:chop kind:creature").Count) + ", \"resists:frost kind:item\": " + string.Join(", ", Find("resists:frost kind:item").Take(8).Select(e => e.Name)));
            p.Check(Find("damage:spirit").Any(e => e.Name == "SwordSilver"), "\"damage:spirit\" finds the silver sword");
            p.Check(Find("damage:poison kind:creature").Any(e => e.Name == "BlobElite" || e.Name == "Blob"), "\"damage:poison kind:creature\" finds an oozer by its attack");
            p.Check(Find("skill:axes").Any(e => e.Name == "AxeBronze"), "\"skill:axes\" finds the bronze axe");

            // The link terms, each with something sure to be linked that way.
            foreach (var (text, named) in new[] { ("drops:resin", "Greydwarf"), ("from:troll", "TrollHide"), ("needs:bronze", "AxeBronze"), ("spawns:greydwarf", "Spawner_GreydwarfNest"), ("spawns:troll kind:raid", "foresttrolls") })
            {
                var linked = Find(text);
                p.Check(linked.Any(e => e.Name == named), $"\"{text}\" finds {named}", $"{Numbers.Count(linked.Count)} results: {string.Join(", ", linked.Take(8).Select(e => e.Name))}");
            }
            var given = Knowledge.Givers().Select(g => (Giver: X.Find(g.Prefab), Effect: X.Find(EntryKeys.For(Kind.StatusEffect, g.Effect)))).FirstOrDefault(g => g.Giver != null && g.Effect != null && g.Effect.ShownName.Length > 0);
            if (given.Giver != null)
            {
                var givers = Find(SearchHelp.Term("gives", given.Effect.ShownName));
                p.Check(givers.Contains(given.Giver), $"\"gives:\" finds {given.Giver.Name} by {given.Effect.ShownName}", $"{Numbers.Count(givers.Count)} results");
            }
            else p.Note("nothing gives a status effect that is listed, so gives: is not tried");
            p.Note($"\"is:silent\": {Numbers.Count(Find("is:silent").Count)}, \"is:unsure\": {Numbers.Count(Find("is:unsure").Count)}");

            p.Check(Find("zzqqxxnothing").Count == 0, "a search matching nothing finds nothing");
            X.SearchEverything("");
            yield break;
        }

        /// <summary>A favourite is kept and shown alone, what was looked at comes back newest first, and Back and Forward walk the history.</summary>
        private static IEnumerator FavouritesRecentHistory(Probe p)
        {
            var a = Pick(Kind.Creature, "Boar");
            var b = Pick(Kind.Item, "Wood");
            var c = Pick(Kind.Piece, "piece_workbench");
            if (a == null || b == null || c == null) p.Skip("there is no boar, wood or workbench");

            var was = X.Favourites.Contains(a);
            if (was) X.ToggleFavourite(a);
            X.ToggleFavourite(a);
            p.Check(X.Favourites.Contains(a), "starring an entry makes it a favourite");
            X.FavouritesOnly = true;
            p.Check(X.Results.Contains(a) && X.Results.All(e => X.Favourites.Contains(e)), "favourites alone show it, and only favourites");
            X.FavouritesOnly = false;
            X.ToggleFavourite(a);
            p.Check(!X.Favourites.Contains(a), "starring it again takes it away");
            if (was) X.ToggleFavourite(a);

            Select(a);
            yield return null;
            Select(b);
            yield return null;
            Select(c);
            yield return null;
            p.Check(X.RecentKeys.Count >= 3 && X.RecentKeys[0] == c.Key && X.RecentKeys[1] == b.Key, "what was looked at comes back newest first", string.Join(", ", X.RecentKeys.Take(3)));
            X.RecentOnly = true;
            p.Check(X.Results.Count > 0 && X.Results[0] == c, "Recent lists the last one first");
            X.RecentOnly = false;

            p.Check(X.Back() && X.Selected == b, "Back goes to the one before");
            p.Check(X.Back() && X.Selected == a, "and again to the one before that");
            p.Check(X.Forward() && X.Selected == b, "Forward goes on again");
        }

        /// <summary>Each kind's tab lists as many as its count says, each group of it in one run.</summary>
        private static IEnumerator TabsAndGroups(Probe p)
        {
            X.SearchEverything("");
            var broken = new List<string>();
            foreach (Kind kind in Enum.GetValues(typeof(Kind)))
            {
                var count = X.CountOf(kind);
                if (count == 0) continue;
                X.KindFilter = kind;
                yield return null;
                if (X.Results.Count != count) broken.Add($"{kind} lists {Numbers.Count(X.Results.Count)} of {Numbers.Count(count)}");
                var seen = new HashSet<string>();
                string last = null;
                foreach (var entry in X.Results)
                {
                    var group = entry.Group ?? "";
                    if (group == last) continue;
                    if (!seen.Add(group)) broken.Add($"{kind}: \"{group}\" comes twice");
                    last = group;
                }
                p.Note($"{Kinds.Label(kind)}: {Numbers.Count(count)} in {Numbers.Count(seen.Count)} groups");
            }
            X.KindFilter = null;
            p.Check(broken.Count == 0, "every tab lists all it counts, each group in one run", string.Join("; ", broken.Take(6)));
        }

        /// <summary>The catalog is sound: every key once, every entry named and grouped in its tab, the counts adding up.</summary>
        private static IEnumerator CatalogSound(Probe p)
        {
            var twice = X.Catalog.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            p.Check(twice.Count == 0, "every entry has a key of its own", string.Join(", ", twice.Take(8)));
            var nameless = X.Catalog.Where(e => string.IsNullOrEmpty(e.Name)).Count();
            p.Check(nameless == 0, "every entry has a name", $"{Numbers.Count(nameless)}");
            var ungrouped = X.Catalog.Where(e => string.IsNullOrEmpty(e.Group)).Select(e => e.Name).ToList();
            p.Check(ungrouped.Count == 0, "every entry is in a group of its tab", string.Join(", ", ungrouped.Take(8)));
            X.SearchEverything("");
            yield return null;
            var sum = Enum.GetValues(typeof(Kind)).Cast<Kind>().Sum(k => X.CountOf(k));
            p.Check(sum == X.CountAll, "the tabs' counts add up to all of them", $"{Numbers.Count(sum)} and {Numbers.Count(X.CountAll)}");
        }

        /// <summary>Searching the whole catalog is quick enough to run on every key typed.</summary>
        private static IEnumerator SearchSpeed(Probe p)
        {
            var slowest = 0.0;
            var slowestText = "";
            foreach (var text in new[] { "a", "troll", "kind:item s", "-has:ragdoll kind:c", "biome:swamp", "station:forge2" })
            {
                var watch = Stopwatch.StartNew();
                X.SearchEverything(text);
                var ms = watch.Elapsed.TotalMilliseconds;
                if (ms > slowest)
                {
                    slowest = ms;
                    slowestText = text;
                }
                yield return null;
            }
            X.SearchEverything("");
            p.Note($"the slowest search took {Numbers.Fixed(slowest, 1)} ms (\"{slowestText}\")");
            p.Check(slowest < 50, "every search takes under 50 ms", $"{Numbers.Fixed(slowest, 1)} ms for \"{slowestText}\"");
        }

        // ----- The list -----

        /// <summary>The arrow keys' moves, the Game and Mods filter, and a tab with nothing for the search showing every kind's matches.</summary>
        private static IEnumerator ListKeysAndFilters(Probe p)
        {
            X.SearchEverything("");
            X.KindFilter = Kind.Creature;
            yield return null;
            X.Select(X.Results[0]);
            X.Move(1);
            p.Check(X.Selected == X.Results[1], "a move down selects the next");
            X.Move(-1);
            p.Check(X.Selected == X.Results[0], "and up the one before");
            X.KindFilter = null;

            X.Origin = OriginFilter.Vanilla;
            p.Check(X.Results.Count > 0 && X.Results.All(e => e.Origin != Origin.Mod), "Game shows only the game's own");
            X.Origin = OriginFilter.Mods;
            p.Check(X.Results.All(e => e.Origin == Origin.Mod), "Mods shows only what mods added", $"{Numbers.Count(X.Results.Count)}");
            X.Origin = OriginFilter.All;

            X.SearchEverything("greydwarf");
            yield return null;
            var empty = Enum.GetValues(typeof(Kind)).Cast<Kind>().Where(k => X.CountOf(k) == 0).Select(k => (Kind?)k).FirstOrDefault();
            if (empty != null && X.CountAll > 0)
            {
                X.KindFilter = empty;
                yield return null;
                p.Check(X.ShowingEveryKind && X.Results.Count == X.CountAll, $"the {empty} tab, with nothing for \"greydwarf\", shows every kind's matches instead", $"{Numbers.Count(X.Results.Count)} of {Numbers.Count(X.CountAll)}");
                X.KindFilter = null;
            }
            X.SearchEverything("");
        }

        /// <summary>The list as a part left it when it ran out of time: every origin, every kind, no search.</summary>
        private static void ResetList()
        {
            X.Origin = OriginFilter.All;
            X.KindFilter = null;
            X.SearchEverything("");
        }

        /// <summary>The search's help opens and draws, and closes.</summary>
        private static IEnumerator HelpOpens(Probe p)
        {
            var drawn = ScryPanel.Drawn(PanelPart.Help);
            ScryPanel.ShowHelp(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Help) > drawn, 3);
            p.Check(ScryPanel.HelpShown && ScryPanel.Drawn(PanelPart.Help) > drawn, "the search's help opens and draws");
            ScryPanel.ShowHelp(false);
            p.Check(!ScryPanel.HelpShown, "and closes");
        }
    }
}
