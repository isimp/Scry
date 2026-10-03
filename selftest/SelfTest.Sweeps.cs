using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's sweeps over the whole catalog: a spread of every kind on the stage, every
    /// raid, and every entry's details with every chip and link in them.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>A spread of every kind of prefab stands on the stage, measured to a sane size.</summary>
        private static IEnumerator KindsOnStage(Probe p)
        {
            var failed = new List<string>();
            var odd = new List<string>();
            var shown = 0;
            foreach (var kind in new[] { Kind.Creature, Kind.Item, Kind.Piece, Kind.Resource, Kind.Projectile, Kind.Effect, Kind.Other })
            {
                var all = X.Catalog.Where(e => e.Kind == kind && e.Source is GameObject && Stage.IsStaged(e)).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
                foreach (var entry in Spread(all, 10))
                {
                    Select(entry);
                    yield return Until(() => CopyOf(entry) != null, 4);
                    if (CopyOf(entry) == null)
                    {
                        if (entry.Kind != Kind.Effect) failed.Add($"{entry.Name} ({(entry.Origin == Origin.Vanilla ? "game" : entry.ModName)})");
                        continue;
                    }
                    shown++;
                    var size = Stage.SubjectSize;
                    if (float.IsNaN(size.magnitude) || float.IsInfinity(size.magnitude) || size.magnitude > 2000f) odd.Add($"{entry.Name} {size}");
                }
            }
            p.Note($"{Numbers.Count(shown)} copies stood on the stage");
            var game = failed.Where(f => f.EndsWith("(game)", StringComparison.Ordinal)).ToList();
            if (failed.Count > game.Count) p.Note("mods' prefabs that did not stand: " + string.Join(", ", failed.Except(game)));
            p.Check(game.Count == 0, "every one of the game's stands on the stage", string.Join(", ", game));
            p.Check(odd.Count == 0, "and measures to a sane size", string.Join(", ", odd));
        }

        /// <summary>Every raid tells when it comes and what it brings, each thing it brings an entry.</summary>
        private static IEnumerator EveryRaid(Probe p)
        {
            var raids = X.Catalog.Where(e => e.Kind == Kind.Raid).ToList();
            if (raids.Count == 0) p.Skip("there are no raids");
            var keys = new HashSet<string>(X.Catalog.Select(e => e.Key));
            var wrong = new List<string>();
            foreach (var raid in raids)
            {
                var told = Facts.For(raid);
                var brings = (raid.Source as RandomEvent)?.m_spawn?.Where(s => s?.m_prefab != null).Select(s => s.m_prefab.name).ToList() ?? new List<string>();
                // A boss's fight tells when it is on; a raid how long it lasts, and what it brings when it brings anything.
                if (BossFight(raid) != null)
                {
                    if (!Tells(told, "On")) wrong.Add($"{raid.Name} tells not when it is on");
                }
                else if (!Tells(told, "Lasts") || brings.Count > 0 && !told.Pairs.Any(pair => pair.Key.StartsWith("Brings ", StringComparison.Ordinal))) wrong.Add($"{raid.Name} tells too little");
                var missing = brings.Where(b => !keys.Contains(b)).ToList();
                if (missing.Count > 0) wrong.Add($"{raid.Name} brings {string.Join(", ", missing)}, not in the catalog");
            }
            p.Note($"{Numbers.Count(raids.Count)} raids");
            p.Check(wrong.Count == 0, "every raid tells what it brings, each an entry", string.Join("; ", wrong.Take(6)));
            yield break;
        }

        /// <summary>
        /// Every entry's details can be told, and every chip, link and line in them leads to an
        /// entry; for the game's own entries both must hold, for mods' each miss is noted.
        /// </summary>
        private static IEnumerator EveryDetail(Probe p)
        {
            var keys = new HashSet<string>(X.Catalog.Select(e => e.Key));
            var notShown = new List<string>();
            var dangling = new Dictionary<string, List<string>>();
            var slow = new List<(double Ms, string Name)>();
            var chips = 0;
            var watch = new Stopwatch();
            var unregistered = new HashSet<string>(StringComparer.Ordinal);

            // Entries' names told as text where a chip could go to them, by what they are told under.
            // Scry's own name is prose, and the values of a few labels are kinds the game names
            // like things (a material Wood, an item type Chest, a comfort group Fire).
            var byName = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var shownOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var each in X.Catalog)
            {
                var shown = each.DisplayName.Length > 0 ? each.DisplayName : each.Name;
                shownOf[each.Key] = shown;
                if (each.DisplayName.Length >= 3 && each.DisplayName != "Scry" && !byName.ContainsKey(each.DisplayName)) byName[each.DisplayName] = each;
            }
            var names = new HashSet<string>(byName.Keys, StringComparer.Ordinal);
            var kinds = new HashSet<string>(StringComparer.Ordinal) { "Material", "Type", "Comfort group", "Faction", "Skill" };
            var unlinked = new Dictionary<string, (int Count, string Example)>(StringComparer.Ordinal);
            var onPage = new HashSet<string>(StringComparer.Ordinal);
            void Unlinked(Entry entry, string label, string text, string linked)
            {
                if (label != null && kinds.Contains(label)) return;
                foreach (var name in NamesInText.Find(text, names))
                {
                    // One with a chip elsewhere on the page can be gone to (the biomes at its foot
                    // excuse nothing, as a line naming one should go there itself); several
                    // entries can share a name, so names are what is compared.
                    if (name == entry.DisplayName || onPage.Contains(name)) continue;
                    if (linked != null && shownOf.TryGetValue(linked, out var linkedName) && linkedName == name) continue;
                    // A line is told by its words before the name, a pair by its label.
                    var under = label ?? text.Substring(0, Math.Max(0, Math.Min(text.IndexOf(name, StringComparison.Ordinal), 40))).Trim();
                    var key = $"{under} -> {name}";
                    unlinked.TryGetValue(key, out var seen);
                    unlinked[key] = (seen.Count + 1, seen.Example ?? entry.Name);
                }
            }

            void Lead(Entry entry, string target)
            {
                if (string.IsNullOrEmpty(target) || target == "hand") return;
                chips++;
                if (keys.Contains(target)) return;
                // A prefab the scene never registers (a creature's own attack items, which only the
                // game's item list holds) has no entry, and the panel shows it as text; only a
                // prefab of the scene or a key of Scry's own that leads nowhere is a gap.
                var name = EntryKeys.Split(target, out var kind);
                if (kind == null && ZNetScene.instance?.GetPrefab(name) == null)
                {
                    unregistered.Add(target);
                    return;
                }
                var owner = entry.Origin == Origin.Vanilla ? "game" : "mods";
                if (!dangling.TryGetValue(owner, out var list)) dangling[owner] = list = new List<string>();
                if (list.Count < 12) list.Add($"{entry.Name} -> {target}");
                else list.Add(null);
            }

            yield return Budgeted(X.Catalog, entry =>
            {
                watch.Restart();
                var told = Facts.For(entry);
                var ms = watch.Elapsed.TotalMilliseconds;
                if (ms > 20) slow.Add((ms, entry.Name));
                if (told.Pairs.Any(pair => pair.Key == "Not shown")) notShown.Add($"{entry.Name} ({told.Pairs.First(pair => pair.Key == "Not shown").Value})");
                foreach (var row in told.Rows.Concat(told.UseRows))
                {
                    Lead(entry, row.TitleLink);
                    foreach (var item in row.Items) Lead(entry, item.Prefab);
                }
                foreach (var link in told.Links.Values) Lead(entry, link);
                foreach (var line in told.Where) Lead(entry, line.Prefab);

                onPage.Clear();
                void OnPage(string key)
                {
                    if (key != null && shownOf.TryGetValue(key, out var shown)) onPage.Add(shown);
                }
                foreach (var row in told.Rows.Concat(told.UseRows))
                {
                    OnPage(row.TitleLink);
                    foreach (var item in row.Items) OnPage(item.Prefab);
                }
                foreach (var link in told.Links.Values) OnPage(link);
                foreach (var line in told.Where) OnPage(line.Prefab);
                foreach (var pair in told.Pairs) Unlinked(entry, pair.Key, pair.Value, told.Links.TryGetValue(pair.Key, out var linked) ? linked : null);
                foreach (var line in told.Where) Unlinked(entry, null, line.Text, line.Prefab);
                foreach (var row in told.Rows.Concat(told.UseRows))
                {
                    Unlinked(entry, "a row's title", row.Title, row.TitleLink);
                    foreach (var item in row.Items) if (string.IsNullOrEmpty(item.Prefab)) Unlinked(entry, row.Title, item.Name, null);
                }
            }, 10);
            Facts.Forget();

            p.Note($"{Numbers.Count(X.Catalog.Count)} entries told, {Numbers.Count(chips)} chips, links and lines followed");
            if (unlinked.Count > 0)
            {
                p.Note($"{Numbers.Count(unlinked.Count)} kinds of name are told as text where a chip could go, the most told first: " +
                       string.Join("; ", unlinked.OrderByDescending(u => u.Value.Count).ThenBy(u => u.Key, StringComparer.Ordinal).Take(40).Select(u => $"{u.Key} ({Numbers.Count(u.Value.Count)}, as in {u.Value.Example})")));
            }
            if (unregistered.Count > 0) p.Note($"{Numbers.Count(unregistered.Count)} prefabs named are not among the scene's, so have no entry and show as text: " + string.Join(", ", unregistered.Take(10)));
            if (slow.Count > 0) p.Note("slowest to tell: " + string.Join(", ", slow.OrderByDescending(s => s.Ms).Take(6).Select(s => $"{s.Name} {Numbers.Amount(s.Ms, 0)} ms")));
            var gameNotShown = notShown.Where(n => X.Catalog.FirstOrDefault(e => n.StartsWith(e.Name + " ", StringComparison.Ordinal))?.Origin == Origin.Vanilla).ToList();
            if (notShown.Count > gameNotShown.Count) p.Note($"{Numbers.Count(notShown.Count - gameNotShown.Count)} mods' entries could not tell all their details: " + string.Join("; ", notShown.Except(gameNotShown).Take(6)));
            p.Check(gameNotShown.Count == 0, "every entry of the game's tells all its details", string.Join("; ", gameNotShown.Take(6)));
            if (dangling.TryGetValue("mods", out var mods)) p.Note($"{Numbers.Count(mods.Count)} chips in mods' entries lead nowhere: " + string.Join("; ", mods.Where(m => m != null)));
            var game = dangling.TryGetValue("game", out var g) ? g : new List<string>();
            p.Check(game.Count == 0, "every chip, link and line in the game's entries leads to an entry", $"{Numbers.Count(game.Count)}: " + string.Join("; ", game.Where(m => m != null)));
        }
    }
}
