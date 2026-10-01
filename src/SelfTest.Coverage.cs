using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's wider cases: the search's every kind of term, favourites, recent and
    /// history, the tabs' counts and groups, the stage's controls and adjustments, sounds' variants,
    /// looping effects, clearing the world, the panel's views and the /scry command; and sweeps
    /// over the whole catalog: every entry's details, every chip and link they hold, a spread of
    /// every kind on the stage, every raid, and once the locations are read, every dungeon and
    /// camp laid out, a spread of locations on the stage, and every dungeon heading its group.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>Does something to each item, yielding a frame whenever this frame's share is used up, so a sweep over thousands does not stall the game.</summary>
        private static IEnumerator Budgeted<T>(IReadOnlyList<T> items, Action<T> each, double ms = 8)
        {
            var watch = Stopwatch.StartNew();
            foreach (var item in items)
            {
                each(item);
                if (watch.Elapsed.TotalMilliseconds < ms) continue;
                yield return null;
                watch.Restart();
            }
        }

        /// <summary>Up to so many of a list, spread evenly over it rather than its first few.</summary>
        private static List<T> Spread<T>(IReadOnlyList<T> all, int count)
        {
            if (all.Count <= count) return all.ToList();
            var step = (double)all.Count / count;
            return Enumerable.Range(0, count).Select(i => all[(int)(i * step)]).ToList();
        }

        // ----- The search -----

        /// <summary>Each kind of term the search's help names finds what it says it finds.</summary>
        private static IEnumerator SearchTerms(Probe p)
        {
            List<Entry> Find(string text)
            {
                X.SearchEverything(text);
                return X.Results.ToList();
            }

            var troll = Find("troll");
            p.Check(troll.Any(e => e.Name == "Troll"), "\"troll\" finds the troll", $"{troll.Count} results");
            var noRagdoll = Find("troll -ragdoll");
            p.Check(noRagdoll.Count > 0 && noRagdoll.All(e => e.Name.IndexOf("ragdoll", StringComparison.OrdinalIgnoreCase) < 0), "a minus leaves out what matches it", $"{noRagdoll.Count} results");
            var lights = Find("has:light");
            p.Check(lights.Count > 0 && lights.All(e => e.Components.Any(c => c.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0)), "\"has:light\" finds what has a light", $"{lights.Count} results");
            var swamp = Find("biome:swamp");
            p.Check(swamp.Count > 0 && swamp.All(e => e.Biomes.Any(b => b.IndexOf("swamp", StringComparison.OrdinalIgnoreCase) >= 0)), "\"biome:swamp\" finds what lives in the swamp", $"{swamp.Count} results");
            var played = Find("playedby:troll");
            p.Note("\"playedby:troll\": " + string.Join(", ", played.GroupBy(e => e.Kind).Select(g => $"{g.Count()} {Kinds.Label(g.Key).ToLowerInvariant()}")));
            p.Check(played.Count > 0 && played.All(e => e.UsedBy.Any(u => u.IndexOf("troll", StringComparison.OrdinalIgnoreCase) >= 0))
                    && played.Any(e => e.Kind == Kind.Sound) && played.Any(e => e.Kind == Kind.Effect),
                    "\"playedby:troll\" finds what the troll's effect lists play, its sounds and effects among them", $"{played.Count} results");
            var forge = Find("station:forge");
            p.Check(forge.Count > 0 && forge.All(e => e.Stations.Any(s => s.Name.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0 || s.Shown.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0)), "\"station:forge\" finds what is made at a forge", $"{forge.Count} results");
            var hand = Find("station:hand");
            p.Check(hand.Count > 0, "\"station:hand\" finds what needs no station", $"{hand.Count} results");
            var creatures = Find("-has:ragdoll kind:c");
            p.Check(creatures.Count > 0 && creatures.All(e => e.Kind == Kind.Creature), "terms combine and shorten (\"-has:ragdoll kind:c\")", $"{creatures.Count} results");
            var mod = X.Catalog.Where(e => e.Origin == Origin.Mod && e.ModName.Length > 0 && e.Kind != Kind.Mod).Select(e => e.ModName).FirstOrDefault();
            if (mod != null)
            {
                var ofMod = Find("mod:" + mod.Replace(" ", "").ToLowerInvariant());
                p.Check(ofMod.Count > 0 && ofMod.All(e => e.ModName == mod), $"\"mod:\" finds only what {mod} added", $"{ofMod.Count} results");
            }
            else p.Note("no mod adds anything, so mod: is not tried");
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
                if (X.Results.Count != count) broken.Add($"{kind} lists {X.Results.Count} of {count}");
                var seen = new HashSet<string>();
                string last = null;
                foreach (var entry in X.Results)
                {
                    var group = entry.Group ?? "";
                    if (group == last) continue;
                    if (!seen.Add(group)) broken.Add($"{kind}: \"{group}\" comes twice");
                    last = group;
                }
                p.Note($"{Kinds.Label(kind)}: {count} in {seen.Count} groups");
            }
            X.KindFilter = null;
            p.Check(broken.Count == 0, "every tab lists all it counts, each group in one run", string.Join("; ", broken.Take(6)));
        }

        // ----- The stage -----

        /// <summary>Every lighting, backdrop and view the stage offers draws, and the person and the spin come and go.</summary>
        private static IEnumerator StageControls(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll", "Boar");
            if (troll == null) p.Skip("there is no creature");
            Select(troll);
            yield return Until(() => CopyOf(troll) != null, 10);
            if (!p.Check(CopyOf(troll) != null, "a copy stands on the stage")) yield break;

            var lighting = Stage.LightingIndex;
            var backdrop = Stage.BackdropIndex;
            var person = Stage.ShowPerson;
            var spin = Stage.Spin;
            var pitch = Stage.Pitch;
            var yaw = Stage.Yaw;
            try
            {
                for (var i = 0; i < Stage.LightingNames.Length; i++)
                {
                    Stage.LightingIndex = i;
                    yield return null;
                }
                p.Check(Stage.LightingIndex == Stage.LightingNames.Length - 1, "every lighting can be set", string.Join(", ", Stage.LightingNames));
                for (var i = 0; i < Stage.BackdropNames.Length; i++)
                {
                    Stage.BackdropIndex = i;
                    yield return null;
                }
                p.Check(Stage.BackdropIndex == Stage.BackdropNames.Length - 1, "every backdrop can be set", string.Join(", ", Stage.BackdropNames));
                Stage.ShowPerson = !person;
                yield return null;
                Stage.ShowPerson = person;
                Stage.Spin = !spin;
                yield return null;
                Stage.Spin = spin;
                Stage.View("Top");
                yield return null;
                p.Check(Stage.Pitch > 60f, "Top looks down on it", $"{Stage.Pitch:0} degrees");
                Stage.View("Side");
                yield return null;
                p.Check(Mathf.Abs(Mathf.DeltaAngle(Stage.Yaw, 90f)) < 1f, "Side looks at it from the side", $"{Stage.Yaw:0} degrees");
                Stage.View("Front");
                yield return null;
                Stage.View("Fit");
                yield return null;
                p.Check(CopyOf(troll) != null, "the copy stays through it all");
            }
            finally
            {
                Stage.LightingIndex = lighting;
                Stage.BackdropIndex = backdrop;
                Stage.ShowPerson = person;
                Stage.Spin = spin;
                Stage.Pitch = pitch;
                Stage.Yaw = yaw;
            }
        }

        /// <summary>Making the model bigger makes its copy bigger, and putting it back puts it back.</summary>
        private static IEnumerator SizeAdjusts(Probe p)
        {
            var boar = Pick(Kind.Creature, "Boar", "Deer");
            if (boar == null) p.Skip("there is no boar");
            Select(boar);
            yield return Until(() => CopyOf(boar) != null, 10);
            var copy = CopyOf(boar);
            if (!p.Check(copy != null, "a copy stands on the stage")) yield break;
            var before = Stage.SubjectSize.magnitude;
            var scale = X.Modifiers.Scale;
            X.Modifiers.Scale = scale * 2f;
            yield return null;
            yield return null;
            var bigger = Stage.SubjectSize.magnitude;
            p.Check(bigger > before * 1.8f, "twice the size makes it about twice as big", $"{before:0.0} m, then {bigger:0.0} m");
            X.Modifiers.Scale = scale;
            yield return null;
            yield return null;
            p.Check(Mathf.Abs(Stage.SubjectSize.magnitude - before) < before * 0.05f + 0.01f, "and back again", $"{Stage.SubjectSize.magnitude:0.0} m");
        }

        // ----- Sounds and effects -----

        /// <summary>A sound with several variants plays the one chosen.</summary>
        private static IEnumerator SoundVariant(Probe p)
        {
            Entry sound = null;
            List<AudioClip> variants = null;
            foreach (var entry in X.Catalog.Where(e => e.Kind == Kind.Sound && e.Origin == Origin.Vanilla && e.Name.StartsWith("sfx_", StringComparison.Ordinal)).Take(200))
            {
                var clips = entry.Source is GameObject prefab ? Previews.SoundVariants(prefab) : null;
                if (clips == null || clips.Count < 2) continue;
                sound = entry;
                variants = clips;
                break;
            }
            if (sound == null) p.Skip("no sound of the game's has two variants");
            p.Note($"{sound.Name}: {variants.Count} variants");
            Select(sound);
            yield return null;
            var chosen = variants[variants.Count - 1];
            Previews.PlaySound(sound, chosen);
            yield return Until(() => Previews.SoundPlaying, 2);
            p.Check(Previews.SoundPlaying, "it plays");
            yield return new Wait(0.3);
            p.Check(Previews.SoundClipNow() == chosen, "the variant chosen is the one heard", Previews.SoundClipNow()?.name ?? "none");
            Previews.StopSound();
            p.Check(!Previews.SoundPlaying, "and it stops");
        }

        /// <summary>An effect on you plays on while looping is on, and stops when asked.</summary>
        private static IEnumerator EffectLoops(Probe p)
        {
            var effect = Pick(Kind.Effect, "vfx_HitSparks", "vfx_Place_workbench");
            if (effect == null) p.Skip("there is no effect");
            // On the stage: the full view, not the world.
            if (ScryPanel.Compact) ScryPanel.Compact = false;
            if (Previews.InWorld) Previews.ToggleWorld();
            var shows = Stage.Shows;
            var made = Stage.CopiesMade;
            Select(effect);
            yield return null;
            var loops = Previews.LoopEffects;
            Previews.LoopEffects = true;
            yield return Until(() => CopyOf(effect) != null, 5);
            var first = CopyOf(effect);
            yield return Until(() => CopyOf(effect) != null && CopyOf(effect) != first, 8);
            var again = first != null && CopyOf(effect) != null && CopyOf(effect) != first;
            // What still plays, when it has not played out: the stage waits for every particle and sound.
            var still = again || first == null ? "" : string.Join(", ",
                first.GetComponentsInChildren<ParticleSystem>(false).Where(s => s.IsAlive(false)).Select(s => $"{s.name} (loops {s.main.loop}, lasts {s.main.duration:0.#} s)")
                    .Concat(first.GetComponentsInChildren<AudioSource>(false).Where(s => s.isPlaying).Select(s => $"{s.name} sound (loops {s.loop})")));
            // Any second copy the stage made is the effect played again, however short it lives.
            again |= Stage.CopiesMade - made >= 2;
            var twins = X.Catalog.Count(e => e.Key == effect.Key);
            var why = first == null
                ? $"{twins} entries of its key; the stage showed {(Stage.Showing != null ? Stage.Showing.Name : "nothing")}{(Stage.IsStaged(effect) ? "" : ", having nothing of it to show")}, asked {Stage.Shows - shows} times, a copy made {Stage.CopiesMade - made} times, none seen; on you {Looks.OnPerson}, in the world {Previews.InWorld}, compact {ScryPanel.Compact}, panel {(Session.IsOpen ? "open" : "closed")}"
                : still.Length > 0 ? "still playing: " + still : null;
            p.Check(again, "with Repeat on, the stage plays it again once it has played out", why);
            Previews.LoopEffects = false;
            var last = CopyOf(effect);
            yield return Until(() => Stage.Finished, 8);
            yield return new Wait(1.0);
            p.Check(CopyOf(effect) == last, "with Repeat off, it is not played again");
            var key = "on you:" + effect.Name;
            Previews.PlayEffect(effect, onYou: true);
            yield return null;
            p.Check(Previews.Playing.IsPlaying(key), "played on you, it plays");
            Previews.Stop(key);
            yield return null;
            p.Check(!Previews.Playing.IsPlaying(key), "and stops when asked");
            Previews.LoopEffects = loops;
        }

        /// <summary>Clear world takes away what stands in the world, pinned or not.</summary>
        private static IEnumerator ClearWorld(Probe p)
        {
            yield return Until(() => !Previews.AnythingInWorld, 8);
            if (Previews.AnythingInWorld) p.Skip("something of yours is already in the world, and is left alone");
            var sword = Pick(Kind.Item, "SwordIron", "AxeBronze");
            if (sword == null) p.Skip("there is no sword or axe");
            Select(sword);
            yield return Until(() => CopyOf(sword) != null, 10);
            Previews.ToggleWorld();
            yield return Until(() => Previews.AnythingInWorld, 3);
            Previews.Pin();
            yield return null;
            p.Check(Previews.PinnedCount > 0 && Previews.OutLines > 0, "a copy stands pinned in the world", $"{Previews.PinnedCount} pinned, {Previews.OutLines} lines out");
            Previews.ClearWorld();
            yield return null;
            p.Check(!Previews.AnythingInWorld && Previews.PinnedCount == 0 && Previews.OutLines == 0, "Clear world takes all of it away");
        }

        // ----- The panel -----

        /// <summary>Both views draw, and /scry with words searches for them.</summary>
        private static IEnumerator PanelViews(Probe p)
        {
            var compact = ScryPanel.Compact;
            ScryPanel.Compact = !compact;
            var from = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= from + 3, 3);
            p.Check(ScryPanel.Repaints >= from + 3, compact ? "the full view draws" : "the compact view draws");
            ScryPanel.Compact = compact;
            from = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= from + 3, 3);
            p.Check(ScryPanel.Repaints >= from + 3, "and the view it was in again");

            Session.Show("troll");
            yield return null;
            p.Check(X.Text == "troll" && X.Results.Any(e => e.Name == "Troll"), "/scry with words searches for them", X.Text);
            X.SearchEverything("");
        }

        // ----- Sweeps over the catalog -----

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
                    // One with a chip elsewhere on the page, its biomes' among them, can be gone
                    // to; several entries can share a name, so names are what is compared.
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
                foreach (var biome in entry.Biomes) OnPage(EntryKeys.For(Kind.Biome, biome));
                foreach (var pair in told.Pairs) Unlinked(entry, pair.Key, pair.Value, told.Links.TryGetValue(pair.Key, out var linked) ? linked : null);
                foreach (var line in told.Where) Unlinked(entry, null, line.Text, line.Prefab);
                foreach (var row in told.Rows.Concat(told.UseRows))
                {
                    Unlinked(entry, "a row's title", row.Title, row.TitleLink);
                    foreach (var item in row.Items) if (string.IsNullOrEmpty(item.Prefab)) Unlinked(entry, row.Title, item.Name, null);
                }
            }, 10);
            Facts.Forget();

            p.Note($"{X.Catalog.Count} entries told, {chips} chips, links and lines followed");
            if (unlinked.Count > 0)
            {
                p.Note($"{unlinked.Count} kinds of name are told as text where a chip could go, the most told first: " +
                       string.Join("; ", unlinked.OrderByDescending(u => u.Value.Count).ThenBy(u => u.Key, StringComparer.Ordinal).Take(40).Select(u => $"{u.Key} ({u.Value.Count}, as in {u.Value.Example})")));
            }
            if (unregistered.Count > 0) p.Note($"{unregistered.Count} prefabs named are not among the scene's, so have no entry and show as text: " + string.Join(", ", unregistered.Take(10)));
            if (slow.Count > 0) p.Note("slowest to tell: " + string.Join(", ", slow.OrderByDescending(s => s.Ms).Take(6).Select(s => $"{s.Name} {s.Ms:0} ms")));
            var gameNotShown = notShown.Where(n => X.Catalog.FirstOrDefault(e => n.StartsWith(e.Name + " ", StringComparison.Ordinal))?.Origin == Origin.Vanilla).ToList();
            if (notShown.Count > gameNotShown.Count) p.Note($"{notShown.Count - gameNotShown.Count} mods' entries could not tell all their details: " + string.Join("; ", notShown.Except(gameNotShown).Take(6)));
            p.Check(gameNotShown.Count == 0, "every entry of the game's tells all its details", string.Join("; ", gameNotShown.Take(6)));
            if (dangling.TryGetValue("mods", out var mods)) p.Note($"{mods.Count} chips in mods' entries lead nowhere: " + string.Join("; ", mods.Where(m => m != null)));
            var game = dangling.TryGetValue("game", out var g) ? g : new List<string>();
            p.Check(game.Count == 0, "every chip, link and line in the game's entries leads to an entry", $"{game.Count}: " + string.Join("; ", game.Where(m => m != null)));
        }

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
            p.Note($"{shown} copies stood on the stage");
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
            p.Note($"{raids.Count} raids");
            p.Check(wrong.Count == 0, "every raid tells what it brings, each an entry", string.Join("; ", wrong.Take(6)));
            yield break;
        }

        // ----- Once every location is read -----

        /// <summary>Every dungeon and camp read lays out an example, a dungeon from its entrance and with no two rooms in each other.</summary>
        private static IEnumerator EveryDungeonLaysOut(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var rooms = X.Catalog.Select(PlaceOf).Where(s => s != null && s.IsRoom && s.Contents?.Room != null).ToList();
            var dungeons = X.Catalog.Where(e => PlaceOf(e)?.Contents?.Dungeon != null && !PlaceOf(e).IsRoom).ToList();
            if (dungeons.Count == 0) p.Skip("no location read builds a dungeon or camp");
            var wrong = new List<string>();
            var told = new List<string>();
            foreach (var entry in dungeons)
            {
                var place = PlaceOf(entry);
                var plan = place.Contents.Dungeon;
                var shapes = rooms.Where(r => ((int)r.Room.m_theme & plan.Themes) != 0).Select(r => r.Contents.Room).ToList();
                var rules = place.Rules.FirstOrDefault();
                var radius = rules != null ? Mathf.Max(rules.m_exteriorRadius, rules.m_interiorRadius) : 0f;
                var turned = rules == null || rules.m_randomRotation || rules.m_slopeRotation;
                var counts = new List<int>();
                for (var seed = 1; seed <= 3; seed++)
                {
                    var dice = new Dice(seed * 7919);
                    var example = DungeonLayout.Build(plan, shapes, DungeonLayout.Site(plan, radius, turned, dice), dice);
                    counts.Add(example.Rooms.Count);
                    if (example.Rooms.Count == 0) wrong.Add($"{entry.Name} laid out nothing");
                    else if (plan.Algorithm == "Dungeon" && !example.Rooms[0].Room.Entrance) wrong.Add($"{entry.Name} does not start at an entrance");
                    if (plan.Algorithm != "Dungeon") continue;
                    var solid = example.Rooms.Where(r => !r.Room.EndCap && !r.Room.Divider).ToList();
                    for (var i = 0; i < solid.Count; i++)
                    {
                        for (var j = i + 1; j < solid.Count; j++)
                        {
                            var a = solid[i];
                            var b = solid[j];
                            var shrunkA = new Vec3(a.Room.Size.X - 0.1f, a.Room.Size.Y - 0.1f, a.Room.Size.Z - 0.1f);
                            var shrunkB = new Vec3(b.Room.Size.X - 0.1f, b.Room.Size.Y - 0.1f, b.Room.Size.Z - 0.1f);
                            if (Boxes.Overlap(a.Position, a.Rotation, shrunkA, b.Position, b.Rotation, shrunkB)) wrong.Add($"{entry.Name}: {a.Room.Name} in {b.Room.Name}");
                        }
                    }
                }
                told.Add($"{entry.Name} {string.Join("/", counts)}");
            }
            p.Note($"{dungeons.Count} dungeons and camps, rooms in three examples each: " + string.Join(", ", told));
            p.Check(wrong.Count == 0, "every one lays out, dungeons from an entrance with no room in another", string.Join("; ", wrong.Distinct().Take(8)));
            yield break;
        }

        /// <summary>A spread of locations stands on the stage, framed to a sane size on its own ground.</summary>
        private static IEnumerator LocationsOnStage(Probe p)
        {
            var all = X.Catalog.Where(e => PlaceOf(e) != null && !PlaceOf(e).IsRoom).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            var wrong = new List<string>();
            var sizes = new List<string>();
            foreach (var entry in Spread(all, 10))
            {
                var place = PlaceOf(entry);
                Select(entry);
                yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading && CopyOf(entry) != null, 20);
                if (PlaceAssets.State(place) == PlaceLoad.Failed)
                {
                    wrong.Add($"{entry.Name} could not be loaded");
                    continue;
                }
                if (CopyOf(entry) == null)
                {
                    wrong.Add($"{entry.Name} stands nowhere");
                    continue;
                }
                yield return null;
                var size = Stage.SubjectSize;
                sizes.Add($"{entry.Name} {Mathf.Max(size.x, size.z):0} m");
                if (Mathf.Max(size.x, size.y, size.z) > 300f) wrong.Add($"{entry.Name} frames {size}");
                // A location stands on its own ground; a dungeon's example, shown inside, on its lowest floor.
                var inside = Stage.HasInside && Stage.Inside && Stage.FloorHeights.Count > 0;
                var ground = inside ? Stage.FloorHeights[Stage.FloorHeights.Count - 1] : 0f;
                if (float.IsNaN(Stage.Ground) || Mathf.Abs(Stage.Ground - ground) > 0.01f) wrong.Add($"{entry.Name} stands at {Stage.Ground:0.0} m{(inside ? $", its example's lowest floor at {ground:0.0} m" : "")}");
            }
            p.Note(string.Join(", ", sizes));
            p.Check(wrong.Count == 0, "each loads, is framed to a sane size and stands on its own ground", string.Join("; ", wrong));
        }

        /// <summary>
        /// The locations with the most parts are made over several frames, none of them long:
        /// what each took, and its slowest frame with the part that took longest in it.
        /// </summary>
        private static IEnumerator LargestLocations(Probe p)
        {
            int PartsOf(Entry e) => PlaceOf(e)?.Contents?.Parts.Sum(part => part.Count) ?? 0;
            var largest = X.Catalog.Where(e => PlaceOf(e) != null && !PlaceOf(e).IsRoom && PartsOf(e) > 0).OrderByDescending(PartsOf).Take(3).ToList();
            if (largest.Count == 0) p.Skip("no location's parts are read");

            var wrong = new List<string>();
            foreach (var entry in largest)
            {
                var place = PlaceOf(entry);
                var from = Frames.Frames;
                Select(entry);
                // The whole frame too: what Unity does of the copy on its own is no work of Scry's.
                var whole = 0f;
                var until = Time.unscaledTime + 30f;
                while ((PlaceAssets.State(place) == PlaceLoad.Loading || CopyOf(entry) == null) && Time.unscaledTime < until)
                {
                    yield return null;
                    whole = Mathf.Max(whole, Time.unscaledDeltaTime * 1000f);
                }
                if (CopyOf(entry) == null)
                {
                    wrong.Add($"{entry.Name} stands nowhere");
                    continue;
                }
                var frames = Frames.Since(from);
                p.Note($"{entry.Name}, {PartsOf(entry)} pieces, {Stage.LastBuildParts} parts, made over {Stage.LastBuildFrames} frames, the slowest whole frame {whole:0} ms: {frames.Line(Budget)}{Slowest(frames, 1)}");
                if (frames.Max >= 250) wrong.Add($"{entry.Name} took {frames.Max:0} ms in a frame");
            }
            p.Check(wrong.Count == 0, "each is made over several frames, none of them a quarter of a second", string.Join("; ", wrong));
        }

        /// <summary>Every dungeon or camp read heads a group of its own, tagged, with every room under it indented.</summary>
        private static IEnumerator EveryDungeonHeadsItsGroup(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var places = X.Catalog.Where(e => PlaceOf(e) != null).ToList();
            var wrong = new List<string>();
            foreach (var entry in places.Where(e => !PlaceOf(e).IsRoom && PlaceOf(e).Contents?.Dungeon != null))
            {
                if (!entry.Group.Contains(" · ") || entry.GroupRank != 0 || entry.Indent || string.IsNullOrEmpty(entry.Tag)) wrong.Add($"{entry.Name} is listed as \"{entry.Group}\", tagged \"{entry.Tag}\"");
            }
            foreach (var group in places.Where(e => e.Indent).GroupBy(e => e.Group))
            {
                if (!places.Any(e => e.Group == group.Key && !e.Indent && !PlaceOf(e).IsRoom)) wrong.Add($"\"{group.Key}\" has rooms but no location heading it");
            }
            p.Note($"{places.Count(e => e.Indent)} rooms under {places.Where(e => e.Indent).Select(e => e.Group).Distinct().Count()} dungeons and camps");
            p.Check(wrong.Count == 0, "every dungeon and camp heads its group, its rooms under it", string.Join("; ", wrong.Take(6)));
            yield break;
        }
    }
}
