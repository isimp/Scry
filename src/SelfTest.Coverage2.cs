using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// More of the self-test's cases: the adjustments (stars, looks, wear, animation speed,
    /// loudness, and that a new selection starts afresh), playing and pausing clips and sounds,
    /// wearing an item, felling a tree, switching fast through many entries, the list's keys,
    /// filters and help, closing and opening the panel, what items, stations, smelters and beds
    /// tell, the catalog's own soundness and the search's speed; and once the locations are read,
    /// altars, traders, every location's music and a spread of rooms on the stage.
    /// </summary>
    internal static partial class SelfTest
    {
        // ----- Adjustments -----

        /// <summary>A creature shown with stars, and in each of its looks, still stands; its details tell what stars do.</summary>
        private static IEnumerator StarsAndLooks(Probe p)
        {
            var creature = Pick(Kind.Creature, "Greydwarf", "Boar");
            if (creature == null) p.Skip("there is no creature");
            Select(creature);
            yield return Until(() => CopyOf(creature) != null, 10);
            if (!p.Check(CopyOf(creature) != null, "a copy stands on the stage")) yield break;
            var mods = X.Modifiers;
            p.Note($"{creature.Name}: up to {mods.MaxLevel} levels, {mods.LookNames.Length} looks");
            if (mods.MaxLevel > 1)
            {
                var before = CopyOf(creature);
                mods.Level = mods.MaxLevel;
                yield return Until(() => CopyOf(creature) != null && CopyOf(creature) != before, 5);
                p.Check(CopyOf(creature) != null, $"it stands with {mods.MaxLevel - 1} stars");
                p.Check(Tells(Facts.For(creature), "Health with stars"), "its details tell what stars add");
                mods.Level = 1;
            }
            var looks = mods.LookNames.Length;
            for (var look = 0; look < Math.Min(looks, 4); look++)
            {
                mods.Look = look;
                yield return new Wait(0.2);
                p.Check(CopyOf(creature) != null, $"it stands in the look \"{mods.LookNames[look]}\"");
            }
        }

        /// <summary>A slower animation slows the copy's animator, and a piece stands in every state of wear.</summary>
        private static IEnumerator SpeedAndWear(Probe p)
        {
            var creature = Pick(Kind.Creature, "Boar", "Deer");
            if (creature == null) p.Skip("there is no creature");
            Select(creature);
            yield return Until(() => CopyOf(creature) != null, 10);
            X.Modifiers.AnimationSpeed = 0.5f;
            yield return null;
            yield return null;
            var animator = CopyOf(creature)?.GetComponentInChildren<Animator>();
            p.Check(animator != null && Mathf.Abs(animator.speed - 0.5f) < 0.01f, "half the speed slows its animator to half", animator != null ? $"{animator.speed}" : "no animator");
            X.Modifiers.AnimationSpeed = 1f;

            var wall = Pick(Kind.Piece, "wood_wall_half", "wood_wall", "stone_wall_1x1");
            if (wall == null) yield break;
            Select(wall);
            yield return Until(() => CopyOf(wall) != null, 10);
            if (!X.Modifiers.WearAvailable)
            {
                p.Note($"{wall.Name} shows no wear");
                yield break;
            }
            foreach (Wear wear in Enum.GetValues(typeof(Wear)))
            {
                X.Modifiers.Wear = wear;
                yield return new Wait(0.2);
                p.Check(CopyOf(wall) != null, $"{wall.Name} stands {wear.ToString().ToLowerInvariant()}");
            }
        }

        /// <summary>Loudness follows the adjustment, and a new selection starts every adjustment afresh.</summary>
        private static IEnumerator AdjustmentsStartAfresh(Probe p)
        {
            var boar = Pick(Kind.Creature, "Boar");
            var neck = Pick(Kind.Creature, "Neck", "Deer");
            if (boar == null || neck == null) p.Skip("there is no boar or neck");
            Select(boar);
            yield return null;
            X.Modifiers.Volume = 0.5f;
            X.Modifiers.Scale = 2f;
            yield return null;
            p.Check(Mathf.Abs(Loudness.Gain - 0.5f) < 0.01f, "half the loudness is what previews play at", $"{Loudness.Gain}");
            Select(neck);
            yield return null;
            p.Check(Mathf.Abs(X.Modifiers.Scale - 1f) < 0.001f && Mathf.Abs(X.Modifiers.Volume - 1f) < 0.001f, "selecting another starts at its own size and loudness", $"size {X.Modifiers.Scale}, loudness {X.Modifiers.Volume}");
            yield return null;
            p.Check(Mathf.Abs(Loudness.Gain - 1f) < 0.01f, "and previews play at the game's own loudness again");
        }

        // ----- Clips and sounds -----

        /// <summary>A creature's clip plays, pauses, seeks, loops and stops.</summary>
        private static IEnumerator ClipControls(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll", "Greydwarf");
            if (troll == null) p.Skip("there is no creature");
            Select(troll);
            yield return Until(() => CopyOf(troll) != null && Previews.Clips().Count > 0, 10);
            var clips = Previews.Clips();
            if (!p.Check(clips.Count > 0, "its clips are known", $"{clips.Count}")) yield break;
            var clip = clips.OrderByDescending(c => c.length).First();
            Previews.PlayClip(clip);
            yield return new Wait(0.2);
            p.Check(Previews.PlayingClip() == clip, $"{clip.name} plays");
            Previews.PauseClip(true);
            yield return null;
            p.Check(Previews.ClipPaused, "it pauses");
            Previews.SeekClip(clip.length / 2f);
            yield return null;
            p.Check(Previews.ClipPosition(out var time, out var length) && Mathf.Abs(time - length / 2f) < length * 0.15f + 0.05f, "it seeks to its middle", $"{time:0.00} of {length:0.00} s");
            Previews.PauseClip(false);
            var loops = Previews.LoopClips;
            Previews.ToggleLoopClips();
            p.Check(Previews.LoopClips != loops, "looping can be switched");
            Previews.ToggleLoopClips();
            Previews.StopClip();
            yield return null;
            p.Check(Previews.PlayingClip() == null, "and it stops");
        }

        /// <summary>A sound pauses, seeks and goes on.</summary>
        private static IEnumerator SoundControls(Probe p)
        {
            Entry sound = null;
            foreach (var entry in X.Catalog.Where(e => e.Kind == Kind.Sound && e.Origin == Origin.Vanilla && e.Source is GameObject).Take(400))
            {
                var clips = Previews.SoundVariants((GameObject)entry.Source);
                if (clips.Count > 0 && clips.All(c => c != null && c.length > 2f))
                {
                    sound = entry;
                    break;
                }
            }
            if (sound == null) p.Skip("no sound of the game's is longer than two seconds");
            p.Note(sound.Name);
            Select(sound);
            yield return null;
            Previews.PlaySound(sound);
            yield return Until(() => Previews.SoundPlaying && Previews.SoundPosition(out _, out _), 2);
            Previews.PauseSound(true);
            yield return null;
            p.Check(Previews.SoundPaused, "it pauses");
            Previews.SeekSound(1f);
            yield return null;
            p.Check(Previews.SoundPosition(out var time, out _) && Mathf.Abs(time - 1f) < 0.2f, "it seeks", $"{time:0.00} s");
            p.Note("plays through " + Previews.SoundSourceTold());
            Previews.PauseSound(false);
            yield return new Wait(0.3);
            p.Check(Previews.SoundPosition(out var later, out _) && later > time, "and goes on from there", $"{later:0.00} s");
            Previews.StopSound();
        }

        // ----- Wearing, felling, switching -----

        /// <summary>An item put on shows on the person, and on its own again once taken off.</summary>
        private static IEnumerator WearIt(Probe p)
        {
            var armour = X.Catalog.FirstOrDefault(e => e.Name == "ArmorBronzeChest" && e.Source is GameObject g && Gear.IsWearable(g))
                         ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Origin == Origin.Vanilla && e.Source is GameObject g && Gear.IsWearable(g));
            if (armour == null) p.Skip("there is nothing wearable");
            var was = Looks.OnPerson;
            Looks.OnPerson = true;
            Select(armour);
            yield return Until(() => CopyOf(armour) != null, 10);
            var worn = CopyOf(armour);
            var wornParts = Renderers(worn);
            p.Check(worn != null && worn.GetComponentInChildren<Animator>(true) != null, $"{armour.Name} is shown worn by a person", $"{wornParts} parts");
            Looks.OnPerson = false;
            Previews.Rebuild();
            yield return Until(() => CopyOf(armour) != null && CopyOf(armour) != worn, 10);
            var alone = CopyOf(armour);
            p.Check(alone != null && Renderers(alone) < wornParts, "and on its own once taken off", $"{Renderers(alone)} parts");
            Looks.OnPerson = was;
        }

        /// <summary>A tree felled on the stage plays what it leaves, which goes again in time.</summary>
        private static IEnumerator TreeFalls(Probe p)
        {
            var tree = Pick(Kind.Resource, "Beech1", "Birch1", "Oak1");
            if (tree == null) p.Skip("there is no tree");
            Select(tree);
            yield return Until(() => CopyOf(tree) != null, 10);
            var list = Previews.PrefabLists((GameObject)tree.Source).FirstOrDefault(pair => pair.Value != null && Falling.IsDestroyedList((GameObject)tree.Source, pair.Value));
            if (!p.Check(list.Value != null, "it has what it does when felled")) yield break;
            var thuds = Thud.Played;
            var felled = Time.unscaledTime;
            Previews.PlayEffectList(list.Key, list.Value);
            yield return Until(() => Stage.PlayedCount > 0, 2);
            p.Check(Stage.PlayedCount > 0, $"felling it plays {list.Key}", $"{Stage.PlayedCount} things");

            // Its log strikes the ground as the game's does, with its impact's sound (ImpactEffect).
            var log = ((GameObject)tree.Source).GetComponent<TreeBase>()?.m_logPrefab;
            var impact = log != null ? log.GetComponentInChildren<ImpactEffect>(true) : null;
            if (impact == null) p.Note("its log has no impact of its own");
            else
            {
                p.Note($"its log strikes with {string.Join(", ", impact.m_hitEffect.m_effectPrefabs.Where(e => e?.m_prefab != null).Select(e => e.m_prefab.name))}, from {impact.m_minVelocity:0.#} m/s");
                // The thump is the log toppling off its stump onto the ground, not its first touch.
                bool Toppled() => Thud.Contacts.Any(c => c.Struck && c.At - felled >= 0.5f);
                yield return Until(Toppled, 8);
                var contacts = string.Join(", ", Thud.Contacts.Where(c => c.At >= felled).Select(c => $"{c.Speed:0.#} m/s after {c.At - felled:0.0} s{(c.Struck ? ", heard" : "")}"));
                p.Note("its log touched: " + (contacts.Length > 0 ? contacts : "nothing"));
                p.Check(Thud.Played > thuds && Toppled(), "its log is heard striking the ground as it topples off its stump", $"{Thud.Played - thuds} strikes");
            }
            yield return Until(() => Stage.PlayedCount == 0, 20);
            p.Check(Stage.PlayedCount == 0, "and what it left goes again");
        }

        /// <summary>Selecting a different entry every frame, across every kind, leaves nothing broken and the last one shown.</summary>
        private static IEnumerator FastSwitching(Probe p)
        {
            var all = X.Catalog.Where(e => e.Kind != Kind.Location && e.Kind != Kind.Mod).OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
            var picks = Spread(all, 60);
            foreach (var entry in picks)
            {
                Select(entry);
                yield return null;
            }
            var last = picks.LastOrDefault(e => Stage.IsStaged(e) && e.Source is GameObject);
            if (last != null)
            {
                Select(last);
                yield return Until(() => CopyOf(last) != null, 5);
                p.Check(CopyOf(last) != null, $"after {picks.Count} in as many frames, the last shows");
            }
            else p.Check(X.Selected == picks.Last(), $"after {picks.Count} in as many frames, the last is selected");
        }

        // ----- The list -----

        /// <summary>The list as a part left it when it ran out of time: every origin, every kind, no search.</summary>
        private static void ResetList()
        {
            X.Origin = OriginFilter.All;
            X.KindFilter = null;
            X.SearchEverything("");
        }

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
            p.Check(X.Results.All(e => e.Origin == Origin.Mod), "Mods shows only what mods added", $"{X.Results.Count}");
            X.Origin = OriginFilter.All;

            X.SearchEverything("greydwarf");
            yield return null;
            var empty = Enum.GetValues(typeof(Kind)).Cast<Kind>().Where(k => X.CountOf(k) == 0).Select(k => (Kind?)k).FirstOrDefault();
            if (empty != null && X.CountAll > 0)
            {
                X.KindFilter = empty;
                yield return null;
                p.Check(X.ShowingEveryKind && X.Results.Count == X.CountAll, $"the {empty} tab, with nothing for \"greydwarf\", shows every kind's matches instead", $"{X.Results.Count} of {X.CountAll}");
                X.KindFilter = null;
            }
            X.SearchEverything("");
        }

        /// <summary>The search's help opens and draws, and closes.</summary>
        private static IEnumerator HelpOpens(Probe p)
        {
            var drawn = ScryPanel.HelpsDrawn;
            ScryPanel.ShowHelp(true);
            yield return Until(() => ScryPanel.HelpsDrawn > drawn, 3);
            p.Check(ScryPanel.HelpShown && ScryPanel.HelpsDrawn > drawn, "the search's help opens and draws");
            ScryPanel.ShowHelp(false);
            p.Check(!ScryPanel.HelpShown, "and closes");
        }

        /// <summary>Closing the panel takes the copy down and quiets it; opening it again brings the selection back.</summary>
        private static IEnumerator CloseAndOpen(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll", "Boar");
            if (troll == null) p.Skip("there is no creature");
            Select(troll);
            yield return Until(() => CopyOf(troll) != null, 10);
            Session.Hide();
            yield return null;
            p.Check(Stage.Subject == null && !Previews.SoundPlaying, "closed, nothing is left on the stage or playing");
            Session.Show(null);
            yield return Until(() => CopyOf(troll) != null, 10);
            p.Check(X.Selected == troll && CopyOf(troll) != null, "opened again, the selection is back on the stage");
        }

        // ----- What entries tell -----

        /// <summary>An item tells what it is used for and where it is made; a station what is made and built at it; a smelter what it turns into what; a bed its comfort.</summary>
        private static IEnumerator WhatThingsTell(Probe p)
        {
            var wood = Pick(Kind.Item, "Wood");
            if (wood != null) p.Check(Facts.For(wood).UseRows.Count > 0, "wood tells what it is used for", string.Join("; ", Facts.For(wood).UseRows.Select(r => r.Title).Take(4)));
            var sword = Pick(Kind.Item, "SwordIron");
            if (sword != null) p.Check(Facts.For(sword).Rows.Any(r => r.Title.StartsWith("Made at", StringComparison.Ordinal) && r.TitleLink != null), "the iron sword tells where it is made, going to the station");
            var bench = Pick(Kind.Piece, "piece_workbench");
            if (bench != null)
            {
                var told = Facts.For(bench);
                p.Check(told.Rows.Any(r => r.Title == "Made here" && r.Items.Count > 0), "the workbench tells what is made at it");
                p.Check(told.Rows.Any(r => r.Title == "Built near it" && r.Items.Count > 0), "and what is built near it");
            }
            var smelter = Pick(Kind.Piece, "smelter");
            if (smelter != null)
            {
                var told = Facts.For(smelter);
                p.Check(told.Rows.Count > 0 && Tells(told, "Burns"), "the smelter tells what it turns into what, and what it burns", Pairs(told));
            }
            var bed = Pick(Kind.Piece, "bed", "piece_bed02");
            if (bed != null) p.Check(Tells(Facts.For(bed), "Comfort"), "a bed tells its comfort", Pairs(Facts.For(bed)));
            yield break;
        }

        /// <summary>The catalog is sound: every key once, every entry named and grouped in its tab, the counts adding up.</summary>
        private static IEnumerator CatalogSound(Probe p)
        {
            var twice = X.Catalog.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            p.Check(twice.Count == 0, "every entry has a key of its own", string.Join(", ", twice.Take(8)));
            var nameless = X.Catalog.Where(e => string.IsNullOrEmpty(e.Name)).Count();
            p.Check(nameless == 0, "every entry has a name", $"{nameless}");
            var ungrouped = X.Catalog.Where(e => string.IsNullOrEmpty(e.Group)).Select(e => e.Name).ToList();
            p.Check(ungrouped.Count == 0, "every entry is in a group of its tab", string.Join(", ", ungrouped.Take(8)));
            X.SearchEverything("");
            yield return null;
            var sum = Enum.GetValues(typeof(Kind)).Cast<Kind>().Sum(k => X.CountOf(k));
            p.Check(sum == X.CountAll, "the tabs' counts add up to all of them", $"{sum} and {X.CountAll}");
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
            p.Note($"the slowest search took {slowest:0.0} ms (\"{slowestText}\")");
            p.Check(slowest < 50, "every search takes under 50 ms", $"{slowest:0.0} ms for \"{slowestText}\"");
        }

        // ----- Once every location is read -----

        /// <summary>Every altar's boss tells where it is summoned and with what; every trader tells what it sells.</summary>
        private static IEnumerator AltarsAndTraders(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var bosses = Knowledge.Summons().Select(s => s.Boss).Where(b => b != null).Distinct().ToList();
            var wrong = new List<string>();
            foreach (var boss in bosses)
            {
                var entry = X.Catalog.FirstOrDefault(e => e.Name == boss && e.Kind == Kind.Creature);
                if (entry == null) continue;
                if (!Facts.For(entry).Rows.Any(r => r.Title.StartsWith("Summoned at", StringComparison.Ordinal))) wrong.Add($"{boss} tells no altar");
            }
            p.Note($"{bosses.Count} bosses summoned at altars");
            p.Check(bosses.Count > 0 && wrong.Count == 0, "every boss tells where it is summoned and with what", string.Join("; ", wrong));
            var traders = X.Catalog.Where(e => e.Source is GameObject g && g.GetComponent<Trader>() != null).ToList();
            var quiet = traders.Where(t => !Facts.For(t).Rows.Any(r => r.Title.StartsWith("Sells", StringComparison.Ordinal))).Select(t => t.Name).ToList();
            p.Note($"{traders.Count} traders: {string.Join(", ", traders.Select(t => t.Name))}");
            p.Check(quiet.Count == 0, "every trader tells what it sells", string.Join(", ", quiet));
            yield break;
        }

        /// <summary>Every location and room that names music of its own plays it.</summary>
        private static IEnumerator EveryMusic(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var withMusic = X.Catalog.Where(e => PlaceOf(e)?.Contents?.Music.Count > 0).ToList();
            if (withMusic.Count == 0) p.Skip("no location or room plays music");
            var silent = new List<string>();
            foreach (var entry in withMusic)
            {
                var place = PlaceOf(entry);
                Select(entry);
                yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading, 15);
                var said = Previews.PlacesMusic(entry);
                yield return null;
                if (MusicPreview.PlayingFor != entry || !MusicPreview.Sounding) silent.Add($"{entry.Name} ({place.Contents.Music[0].Name}: {said})");
                Previews.StopSound();
            }
            p.Note($"{withMusic.Count} play music: " + string.Join(", ", withMusic.Take(12).Select(e => e.Name)));
            p.Check(silent.Count == 0, "each plays its music", string.Join(", ", silent));
        }

        /// <summary>A spread of rooms stands on the stage on its lowest doorway, opened on its top floor.</summary>
        private static IEnumerator RoomsOnStage(Probe p)
        {
            var rooms = X.Catalog.Where(e => PlaceOf(e)?.IsRoom == true && PlaceOf(e).Contents?.Room != null && !PlaceOf(e).Contents.Room.EndCap).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            if (rooms.Count == 0) p.Skip("no room is read");
            var wrong = new List<string>();
            foreach (var entry in Spread(rooms, 8))
            {
                var place = PlaceOf(entry);
                Select(entry);
                yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading && CopyOf(entry) != null, 15);
                if (CopyOf(entry) == null)
                {
                    wrong.Add($"{entry.Name} stands nowhere");
                    continue;
                }
                var floor = PlaceView.Ground(place.Contents, true);
                if (Mathf.Abs(Stage.Ground - floor) > 0.01f) wrong.Add($"{entry.Name} stands at {Stage.Ground:0.0}, its floor {floor:0.0}");
                if (!Stage.Cutting) wrong.Add($"{entry.Name} is not opened");
            }
            p.Check(wrong.Count == 0, "each stands on its floor, opened to look into", string.Join("; ", wrong));
        }

        // ----- The run itself, the roof, leftovers -----

        private const string ProgressPart = "the panel shows how far the self-test has got";

        /// <summary>While the self-test runs, the panel's strip names the part running and how far it has got.</summary>
        private static IEnumerator ProgressShown(Probe p)
        {
            if (!Session.IsOpen) Session.Show(null);
            var drawn = ScryPanel.TestNoticesDrawn;
            yield return Until(() => ScryPanel.TestNoticesDrawn > drawn, 3);
            p.Check(ScryPanel.TestNoticesDrawn > drawn, "the panel's strip shows the self-test running");
            var progress = Progress;
            p.Check(progress != null && progress.Contains(ProgressPart), "it names the part running", progress);
            p.Check(Fraction > 0f && Fraction < 1f, "and how far it has got", $"{Fraction:P0}");
        }

        /// <summary>Shift and the wheel move a room's cut; going away and back opens it afresh; a location keeps its roof until its chip takes it off.</summary>
        private static IEnumerator CutMoves(Probe p)
        {
            var rooms = X.Catalog.Where(e => PlaceOf(e)?.IsRoom == true).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            var room = rooms.FirstOrDefault(e => PlaceOf(e).Prefab.StartsWith("forestcrypt", StringComparison.OrdinalIgnoreCase)) ?? rooms.FirstOrDefault();
            var other = rooms.FirstOrDefault(e => e != room);
            if (room == null || other == null) p.Skip("there are not two dungeon rooms");
            Select(room);
            yield return Until(() => CopyOf(room) != null && Stage.HasFloors, 20);
            if (!p.Check(Stage.Cutting, $"{room.Name} opens cut", Stage.CutLabel)) yield break;
            var at = Stage.CutAt;
            Stage.CutBy(1f);
            p.Check(Mathf.Abs(Stage.CutAt - (at + 1f)) < 0.01f, "Shift and the wheel move the cut up", $"{at:0.0} to {Stage.CutAt:0.0} m");
            Stage.CutBy(-2f);
            p.Check(Mathf.Abs(Stage.CutAt - (at - 1f)) < 0.01f, "and down", $"{Stage.CutAt:0.0} m");
            Select(other);
            yield return Until(() => CopyOf(other) != null, 20);
            Select(room);
            yield return Until(() => CopyOf(room) != null, 20);
            p.Check(Stage.Cutting && Mathf.Abs(Stage.CutAt - at) < 0.01f, "going away and back opens it afresh on its top floor", $"{Stage.CutAt:0.0} m");

            var location = LocationNamed("WoodHouse1", "WoodHouse2", "Ruin1", "StoneHouse3");
            if (location == null) yield break;
            Select(location);
            yield return Until(() => CopyOf(location) != null, 20);
            p.Check(Stage.HasFloors && !Stage.Cutting, $"{location.Name} keeps its roof on", Stage.CutLabel);
            Stage.ToggleRoof();
            p.Check(Stage.Cutting, "until its chip takes it off", Stage.CutLabel);
            var height = (Stage.ModelBottom + Stage.ModelTop) / 2f;
            Stage.CutTo(height);
            p.Check(Mathf.Abs(Stage.CutAt - height) < 0.01f && Stage.CutLevel == PlaceView.LevelAt(Stage.FloorHeights, height), "the ruler sets the cut anywhere, over the floor below it", $"{Stage.CutAt:0.0} m, {Stage.CutLabel}");
            Stage.ToggleRoof();
            p.Check(!Stage.Cutting, "and the chip puts the roof back on", Stage.CutLabel);
        }

        /// <summary>After many other entries, the stage holds no more than it held before.</summary>
        private static IEnumerator NoLeftovers(Probe p)
        {
            var boar = Pick(Kind.Creature, "Boar", "Deer");
            if (boar == null) p.Skip("there is no creature");
            Select(boar);
            yield return Until(() => CopyOf(boar) != null, 10);
            yield return Until(() => Stage.PlayedCount == 0, 10);
            yield return null;
            var held = Stage.Held;
            var others = Spread(X.Catalog.Where(e => e.Kind != Kind.Location && e.Kind != Kind.Mod).OrderBy(e => e.Key, StringComparer.Ordinal).ToList(), 40);
            foreach (var entry in others)
            {
                Select(entry);
                yield return null;
                yield return null;
            }
            Select(boar);
            yield return Until(() => CopyOf(boar) != null, 10);
            yield return Until(() => Stage.Held <= held, 10);
            p.Check(Stage.Held == held, $"after {others.Count} others, the stage holds what it held before", $"{Stage.Held} things, {held} before");
        }
    }
}
