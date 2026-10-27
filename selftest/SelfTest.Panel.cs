using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for the panel itself: it opens and draws, shows the test's
    /// progress, draws both views, opens its View box and sliders, closes and opens again; and
    /// what Scry costs and keeps, idling and in the resource monitor.
    /// </summary>
    internal static partial class SelfTest
    {
        // ----- The panel -----

        private static IEnumerator PanelDraws(Probe p)
        {
            Session.Show(null);
            var from = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= from + 5, 5);
            p.Check(Session.IsOpen, "the panel is open");
            p.Check(ScryPanel.Repaints >= from + 5, "it draws, frame after frame", $"{Numbers.Count(ScryPanel.Repaints - from)} repaints");

            // The stage is drawn only in the full view; the view it was in is put back afterwards.
            KeepView();
            if (ScryPanel.Compact)
            {
                ScryPanel.Compact = false;
                p.Note("switched to the full view, which draws the stage");
                from = ScryPanel.Repaints;
                yield return Until(() => ScryPanel.Repaints >= from + 2, 5);
            }
        }

        private const string ProgressPart = "the panel shows how far the self-test has got";

        /// <summary>While the self-test runs, the panel's strip names the part running and how far it has got.</summary>
        private static IEnumerator ProgressShown(Probe p)
        {
            if (!Session.IsOpen) Session.Show(null);
            var drawn = ScryPanel.Drawn(PanelPart.TestNotice);
            yield return Until(() => ScryPanel.Drawn(PanelPart.TestNotice) > drawn, 3);
            p.Check(ScryPanel.Drawn(PanelPart.TestNotice) > drawn, "the panel's strip shows the self-test running");
            var progress = Progress;
            p.Check(progress != null && progress.Contains(ProgressPart), "it names the part running", progress);
            p.Check(Fraction > 0f && Fraction < 1f, "and how far it has got", Numbers.Percent(Fraction));
        }

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

        /// <summary>The stage's View box and the header's volume and size sliders open and draw, and another selection closes them.</summary>
        private static IEnumerator BoxesOpen(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll", "Greydwarf");
            var sword = Pick(Kind.Item, "SwordIron", "AxeBronze");
            if (troll == null || sword == null) p.Skip("there is no creature or no item");
            Select(troll);
            yield return null;
            var shown = X.Selected;
            var under = new Vector2(Screen.width / 2f, Screen.height / 2f);

            var drawn = ScryPanel.Drawn(PanelPart.ViewMenu);
            ScryPanel.OpenViewMenu(shown, under);
            yield return Until(() => ScryPanel.Drawn(PanelPart.ViewMenu) > drawn, 3);
            p.Check(ScryPanel.ViewMenuOpen && ScryPanel.Drawn(PanelPart.ViewMenu) > drawn, "the View box opens and draws");
            ScryPanel.CloseBoxes();

            foreach (var size in new[] { false, true })
            {
                drawn = ScryPanel.Drawn(PanelPart.Slider);
                ScryPanel.OpenSlider(shown, size, under);
                yield return Until(() => ScryPanel.Drawn(PanelPart.Slider) > drawn, 3);
                p.Check(ScryPanel.SliderOpen && ScryPanel.Drawn(PanelPart.Slider) > drawn, size ? "the size slider opens and draws" : "the volume slider opens and draws");
            }

            ScryPanel.OpenViewMenu(shown, under);
            Select(sword);
            yield return Until(() => !ScryPanel.ViewMenuOpen && !ScryPanel.SliderOpen, 3);
            p.Check(!ScryPanel.ViewMenuOpen && !ScryPanel.SliderOpen, "another selection closes them");
        }

        /// <summary>
        /// The line under a page's title names its sections, the animations with their count, and
        /// lights the first at the top; going to a section lights it, the last one too, where the
        /// page scrolls no further than its end. Only open sections are gone to, so the folds
        /// a player keeps are left as they were. A topic of In the game is gone to the same way.
        /// </summary>
        private static IEnumerator SectionLineGoes(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll", "Greydwarf");
            if (troll == null) p.Skip("there is no creature");
            Select(troll);
            var drawn = ScryPanel.Drawn(PanelPart.SectionLine);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SectionLine) > drawn + 1, 5);
            var links = ScryPanel.SectionLinks().ToList();
            p.Check(links.Count >= 2 && ScryPanel.Drawn(PanelPart.SectionLine) > drawn, "a creature's page draws a line of its sections", string.Join(", ", links.Select(l => l.Link)));
            p.Check(links.Any(l => l.Key == "animations" && l.Link.StartsWith("Animations ", StringComparison.Ordinal)), "the line counts the animations");
            p.Check(links.Count > 0 && ScryPanel.SectionLit == links[0].Key, "at the page's top its first section is lit", ScryPanel.SectionLit ?? "none lit");

            var open = links.Where(l => ScryPanel.SectionOpen(l.Key)).Select(l => l.Key).ToList();
            if (open.Count < 2)
            {
                p.Note("fewer than two of its sections are open: going to them is not tried");
                yield break;
            }
            foreach (var key in new[] { open[open.Count / 2], open[open.Count - 1], open[0] })
            {
                ScryPanel.GoToSection(key);
                yield return Until(() => ScryPanel.SectionLit == key, 2);
                p.Check(ScryPanel.SectionLit == key, $"going to {key} lights it", ScryPanel.SectionLit ?? "none lit");
            }
            if (ScryPanel.SectionOpen("facts"))
            {
                yield return GoTo("fight");
                p.Check(ScryPanel.StandsAt("fight"), "going to its fight, a topic of In the game, brings it to the top");
            }
        }

        /// <summary>
        /// The page's header in the full view and the compact one, for a creature, an effect, an
        /// item and a camp: the name with copy and star at its row's end; the toolbar under it,
        /// the actions joined from the left and the slider icons joined at its first line's end;
        /// the line of sections under it with fold all at its end; none over another. Pictured.
        /// </summary>
        private static IEnumerator HeaderLaidOut(Probe p)
        {
            var picks = new[]
            {
                Pick(Kind.Creature, "Greydwarf", "Troll"),
                X.Catalog.FirstOrDefault(e => e.Kind == Kind.Effect && e.Origin == Origin.Vanilla),
                Pick(Kind.Item, "HelmetBronze", "HelmetLeather", "ArmorLeatherChest"),
                Pick(Kind.Location, "Greydwarf_camp1", "Greydwarf_camp2"),
            }.Where(e => e != null).ToList();
            if (picks.Count == 0) p.Skip("there is nothing to look at");
            KeepView();
            var wasCompact = ScryPanel.Compact;
            var wrong = new List<string>();
            var looked = 0;
            foreach (var compact in new[] { false, true })
            {
                ScryPanel.Compact = compact;
                foreach (var entry in picks)
                {
                    Select(entry);
                    var drawn = ScryPanel.Drawn(PanelPart.Toolbar);
                    yield return Until(() => ScryPanel.Drawn(PanelPart.Toolbar) > drawn + 2 && ScryPanel.ToolbarPartsForTest.Any(q => q.Part == "line"), 5);
                    var name = $"{entry.Name} {(compact ? "compact" : "full")}";
                    looked++;
                    wrong.AddRange(HeaderWrong(name));
                    yield return Picture(p, $"header {name}");
                }
            }
            // With the stage dragged to its tallest, the stage gives way and the header stays whole in the panel.
            var tallest = new List<string>();
            var wasScale = ScryPanel.StageScale;
            ScryPanel.StageScale = 2.4f;
            foreach (var compact in new[] { false, true })
            {
                ScryPanel.Compact = compact;
                Select(picks[0]);
                var drawn = ScryPanel.Drawn(PanelPart.Toolbar);
                yield return Until(() => ScryPanel.Drawn(PanelPart.Toolbar) > drawn + 2, 5);
                if (!ScryPanel.HeaderInsideForTest) tallest.Add(compact ? "compact" : "full");
                yield return Picture(p, $"header {picks[0].Name} {(compact ? "compact" : "full")} tallest stage");
            }
            ScryPanel.StageScale = wasScale;
            ScryPanel.Compact = wasCompact;
            p.Check(tallest.Count == 0, "with the stage dragged to its tallest, the page's header stays whole inside the panel", "cut off in " + string.Join(", ", tallest));
            p.Check(looked > 0 && wrong.Count == 0, "each page's header stands the same in both views: the name with copy and star at its end, the actions joined from the left under it and the slider icons at that line's end, the line of sections with fold all at its end, none over another", string.Join("; ", wrong.Take(6)));
        }

        /// <summary>The parts of the compact view's header, as it notes where each was drawn.</summary>
        private static readonly string[] CompactHeaderParts = { "nav", "search", "buttons", "pill", "origin" };

        /// <summary>
        /// The compact view's header in three rows however many kinds the list holds: the list
        /// button and the way back after the name where the title row has room, the search with
        /// its buttons under it, then the kind picked as a pill and where the list is taken from as
        /// a chip at the row's end, none over another; the pill's box holds All and every kind
        /// with something and picks one, closing; the chip's box picks where the list is taken
        /// from. Pictured, each box open.
        /// </summary>
        private static IEnumerator CompactHeader(Probe p)
        {
            KeepView();
            var was = (X.KindFilter, X.Origin, X.LearnedOnly);
            ScryPanel.Compact = true;
            X.LearnedOnly = false;
            X.KindFilter = null;
            X.Origin = OriginFilter.All;
            var from = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= from + 3, 5);
            var parts = ScryPanel.CompactHeaderForTest.ToList();
            Rect? Of(string part) => parts.Any(q => q.Part == part) ? parts.First(q => q.Part == part).At : (Rect?)null;
            var drawnAll = CompactHeaderParts.All(part => Of(part) != null);
            var nav = Of("nav") ?? default;
            var search = Of("search") ?? default;
            var pill = Of("pill") ?? default;
            var origin = Of("origin") ?? default;
            if (!p.Check(drawnAll, "the compact header draws the way back, the search, its buttons, the kind pill and the origin chip", string.Join(", ", parts.Select(q => q.Part))))
            {
                ScryPanel.Compact = false;
                yield break;
            }
            var width = ScryPanel.PanelDrawnForTest.width;
            if (Previews.OutLines > 0) p.Note("Clear world stands in the title row: the way back may start the search row");
            else p.Check(nav.yMax <= search.y, "the list button and the way back stand in the title row, over the search", $"way back {Said(nav)}, search {Said(search)}");
            p.Check(pill.y >= search.yMax && pill.y - search.yMax < search.height && Mathf.Abs(origin.y - pill.y) < 0.5f && origin.xMax <= width + 0.5f && origin.x > pill.xMax,
                "the kind pill stands the row under the search, the origin chip at its end", $"pill {Said(pill)}, origin {Said(origin)}, panel {Numbers.Fixed(width, 0)} wide");
            var over = new List<string>();
            for (var i = 0; i < parts.Count; i++)
                for (var j = i + 1; j < parts.Count; j++)
                    if (parts[i].At.Overlaps(parts[j].At)) over.Add($"{parts[i].Part} and {parts[j].Part}");
            p.Check(over.Count == 0 && parts.All(q => q.At.x >= 0f && q.At.xMax <= width + 0.5f), "none of the compact header's parts stands over another or past the panel", string.Join("; ", over));

            // The way back follows the name as drawn, joined; every title button as tall as the
            // view button; the eye beside it while the compendium is on.
            var name = Of("name");
            var view = Of("view");
            var eye = Of("eye");
            if (name is Rect drawnName && view is Rect viewButton)
            {
                if (nav.yMax <= search.y)
                    p.Check(nav.x > drawnName.xMax && nav.x - drawnName.xMax < viewButton.height, "the way back follows the name as drawn, a gap after it", $"name ends at {Numbers.Fixed(drawnName.xMax, 0)}, the group starts at {Numbers.Fixed(nav.x, 0)}");
                p.Check(Mathf.Abs(nav.height - viewButton.height) < 0.5f && (!(eye is Rect drawnEye) || Mathf.Abs(drawnEye.height - viewButton.height) < 0.5f),
                    "every button of the title row is as tall as the view button", $"group {Numbers.Fixed(nav.height, 0)}, view {Numbers.Fixed(viewButton.height, 0)}, eye {(eye is Rect seen ? Numbers.Fixed(seen.height, 0) : "none")}");
                if (X.CompendiumSet)
                    p.Check(eye is Rect shownEye && shownEye.xMax <= viewButton.x && viewButton.x - shownEye.xMax < viewButton.height, "with the compendium on, the eye stands beside the view button", eye is Rect where ? Said(where) : "no eye");
                else p.Check(eye == null, "with the compendium off, the title row holds no eye", Said(eye ?? default));
            }
            else p.Check(false, "the title row's name and view button are drawn", string.Join(", ", parts.Select(q => q.Part)));
            yield return Picture(p, "compact header");
            if (X.CompendiumSet)
            {
                X.Peeking = true;
                var peekFrom = ScryPanel.Repaints;
                yield return Until(() => ScryPanel.Repaints >= peekFrom + 3, 5);
                yield return Picture(p, "compact header peeking");
                X.Peeking = false;
            }

            var drawn = ScryPanel.Drawn(PanelPart.KindMenu);
            ScryPanel.OpenHeaderMenuForTest(ScryPanel.HeaderMenu.Kinds);
            yield return Until(() => ScryPanel.Drawn(PanelPart.KindMenu) > drawn + 2, 5);
            var (box, choices) = ScryPanel.HeaderMenuForTest;
            var panel = ScryPanel.PanelDrawnForTest;
            var kinds = 1 + System.Enum.GetValues(typeof(Kind)).Cast<Kind>().Count(k => X.CountOf(k) > 0);
            p.Check(choices == kinds && ScryPanel.TabsDrawnForTest.Count == choices, "the kind pill's box holds All and every kind the list holds something of, as tabs", $"{Numbers.Count(choices)} in the box, {Numbers.Count(kinds)} with something");
            p.Check(box.x >= panel.x - 0.5f && box.xMax <= panel.xMax + 0.5f && box.y >= panel.y + pill.yMax && box.yMax <= panel.yMax, "the box stands under the pill, inside the panel", $"box {Said(box)}, panel {Said(panel)}");
            yield return Picture(p, "compact header kind box");
            ScryPanel.ChooseHeaderMenuForTest((int)Kind.Creature);
            from = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= from + 3, 5);
            p.Check(X.KindFilter == Kind.Creature && ScryPanel.HeaderMenuOpen == ScryPanel.HeaderMenu.None && X.Results.Count > 0 && X.Results.All(e => e.Kind == Kind.Creature),
                "a kind picked in the box shows that kind alone, the box closed", $"{X.KindFilter}, {ScryPanel.HeaderMenuOpen}, {Numbers.Count(X.Results.Count)} listed");

            drawn = ScryPanel.Drawn(PanelPart.OriginMenu);
            ScryPanel.OpenHeaderMenuForTest(ScryPanel.HeaderMenu.Origin);
            yield return Until(() => ScryPanel.Drawn(PanelPart.OriginMenu) > drawn + 2, 5);
            (box, choices) = ScryPanel.HeaderMenuForTest;
            p.Check(choices == 3 && box.xMax <= panel.xMax + 0.5f, "the origin chip's box holds all, the game's own and mods'", $"{Numbers.Count(choices)} choices, box {Said(box)}");
            yield return Picture(p, "compact header origin box");
            ScryPanel.ChooseHeaderMenuForTest((int)OriginFilter.Vanilla);
            from = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= from + 3, 5);
            p.Check(X.Origin == OriginFilter.Vanilla && ScryPanel.HeaderMenuOpen == ScryPanel.HeaderMenu.None && X.Results.All(e => e.Origin == Origin.Vanilla),
                "the game's own picked in the box lists only those, the box closed", $"{X.Origin}, {Numbers.Count(X.Results.Count)} listed");
            (X.KindFilter, X.Origin, X.LearnedOnly) = was;
        }

        /// <summary>What is wrong with where the page's header was last drawn (<see cref="HeaderLaidOut"/>).</summary>
        private static List<string> HeaderWrong(string name)
        {
            var wrong = new List<string>();
            var parts = ScryPanel.ToolbarPartsForTest.ToList();
            Rect? Of(string part) => parts.Any(q => q.Part == part) ? parts.First(q => q.Part == part).At : (Rect?)null;
            if (!(Of("name") is Rect title) || !(Of("copy") is Rect copy) || !(Of("star") is Rect star))
            {
                wrong.Add($"{name}: {string.Join(", ", parts.Select(q => q.Part))} drawn");
                return wrong;
            }
            var right = star.xMax;
            if (copy.xMax > star.x || Mathf.Abs(copy.y - star.y) > 0.5f || title.xMax > copy.x) wrong.Add($"{name}: the name at {Said(title)}, copy at {Said(copy)}, star at {Said(star)}");

            var actions = parts.Where(q => q.Part == "action").Select(q => q.At).ToList();
            var icons = parts.Where(q => q.Part == "icon").Select(q => q.At).ToList();
            if (icons.Count == 0) wrong.Add($"{name}: no slider icon");
            else
            {
                if (Mathf.Abs(icons.Max(i => i.xMax) - right) > 1f || icons.Any(i => Mathf.Abs(i.y - icons[0].y) > 0.5f) || icons[0].y < star.yMax)
                    wrong.Add($"{name}: the icons not one group at the toolbar's end, the first at {Said(icons[0])}");
                for (var i = 1; i < icons.Count; i++) if (icons[i].x - icons[i - 1].xMax > 1.5f) wrong.Add($"{name}: icon {Numbers.Count(i + 1)} apart at {Said(icons[i])}");
            }
            if (actions.Count > 0)
            {
                if (Mathf.Abs(actions[0].x) > 0.5f || (icons.Count > 0 && Mathf.Abs(actions[0].y - icons[0].y) > 0.5f)) wrong.Add($"{name}: the first action at {Said(actions[0])}");
                for (var i = 1; i < actions.Count; i++)
                {
                    var sameLine = Mathf.Abs(actions[i].y - actions[i - 1].y) < 0.5f;
                    if (sameLine ? actions[i].x - actions[i - 1].xMax > 1.5f : Mathf.Abs(actions[i].x) > 0.5f) wrong.Add($"{name}: action {Numbers.Count(i + 1)} at {Said(actions[i])}");
                }
            }
            var toolbarBottom = actions.Concat(icons).Select(r => r.yMax).DefaultIfEmpty(star.yMax).Max();
            if (!(Of("line") is Rect line) || !(Of("fold") is Rect fold)) wrong.Add($"{name}: no line of sections or no fold all");
            else if (Mathf.Abs(fold.xMax - right) > 1f || Mathf.Abs(fold.y - line.y) > 0.5f || line.y < toolbarBottom) wrong.Add($"{name}: fold all at {Said(fold)}, the line at {Said(line)}");

            for (var i = 0; i < parts.Count; i++)
            {
                for (var j = i + 1; j < parts.Count; j++)
                {
                    var overlap = Rect.MinMaxRect(Mathf.Max(parts[i].At.xMin, parts[j].At.xMin), Mathf.Max(parts[i].At.yMin, parts[j].At.yMin), Mathf.Min(parts[i].At.xMax, parts[j].At.xMax), Mathf.Min(parts[i].At.yMax, parts[j].At.yMax));
                    if (overlap.width > 0.5f && overlap.height > 0.5f) wrong.Add($"{name}: {parts[i].Part} over {parts[j].Part}");
                }
            }
            return wrong;
        }

        /// <summary>An example of the search's help, clicked, goes into the search and the help gives way to what it finds.</summary>
        private static IEnumerator HelpExamplesTried(Probe p)
        {
            ScryPanel.ShowHelp(true);
            yield return null;
            var example = SearchHelp.Lines.First(l => l.Tries && l.Example == SearchHelp.Term("is", "boss")).Example;
            ScryPanel.TryExample(X, example);
            yield return null;
            p.Check(X.Text == example && !ScryPanel.HelpShown && X.Results.Count > 0, "a help example clicked goes into the search, and the help gives way", $"{X.Text}: {Numbers.Count(X.Results.Count)} results");
            X.SearchEverything("");
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

        // ----- What Scry costs -----

        /// <summary>The parts' own allocation by group, from the first word of each part's name: the panel's (panel, side, facts, controls, list, clips, the stage's texture), the render's (render), the rest the update's.</summary>
        private static (long Panel, long Render, long Update) OwnByGroup(IReadOnlyList<(string Part, long Bytes, double Ms)> parts, OtherThreads others)
        {
            long panel = 0, render = 0, update = 0;
            foreach (var (part, counted, ms) in parts)
            {
                // Each part gives up what other threads allocated while it ran, as the waits saw
                // them do: told by the memory in use, the longest part would carry most of it.
                var bytes = Timing.CountsPerThread ? counted : others.Left(counted, ms);
                var space = part.IndexOf(' ');
                var word = space > 0 ? part.Substring(0, space) : part;
                if (word == "render") render += bytes;
                else if (word == "panel" || word == "side" || word == "facts" || word == "controls" || word == "list" || word == "clips" || part == "stage texture") panel += bytes;
                else update += bytes;
            }
            return (panel, render, update);
        }

        /// <summary>
        /// What Scry's own work costs while the panel stands open and nothing is touched: a
        /// creature's page on the stage, then a dungeon's example with its plan box, each held four
        /// seconds; each notes its time a frame and what it allocated a frame, by part, the parts
        /// allocating most first, as frequent allocation brings the game's memory cleanups. Notes
        /// only, to find what to make allocate less.
        /// </summary>
        private static IEnumerator OpenCost(Probe p)
        {
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var creature = Pick(Kind.Creature, "Troll", "Greydwarf");
            var dungeon = LocationNamed("MountainCave02", "Crypt2");
            var piece = Pick(Kind.Piece, "woodwall", "piece_workbench");
            if (creature == null && dungeon == null) p.Skip("neither a troll nor a dungeon is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            // A piece stands still: with the spin off, the stage is filmed only now and then.
            var spinWas = Stage.Spin;
            foreach (var (entry, what) in new[] { (creature, "a creature's page"), (dungeon, "a dungeon's example with its plan box"), (piece, "a piece's page, the spin off") })
            {
                if (entry == null) continue;
                if (entry == piece) Stage.Spin = false;
                Select(entry);
                yield return Until(() => CopyOf(entry) != null && (entry.Kind != Kind.Location || Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0), 60);
                // A moment for what follows a selection (its page told, its warm-up) to settle; the
                // locations read or clips probed in the background would be counted: waited for a
                // minute, the page passed over where they go on, so the numbers stay comparable.
                yield return new Wait(1);
                yield return Until(() => !Locations.Busy && !TriggerProbe.Busy, 60);
                var busy = Locations.Busy ? "the locations were being read" : TriggerProbe.Busy ? "clips were being probed" : null;
                if (busy != null)
                {
                    p.Note($"{what}: not measured, as {busy}");
                    continue;
                }
                var first = Timing.Measuring?.Frames ?? 0;
                var bytes = Timing.ScryBytes;
                var frames = Time.frameCount;
                var filmed = Stage.Filmed;
                var repainted = Plugin.Repaints;
                Timing.ForgetBytesByPart();
                var others = new OtherThreads(() => GC.GetTotalMemory(false), () => GC.CollectionCount(0), () => System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                var until = Time.unscaledTime + 4f;
                while (Time.unscaledTime < until)
                {
                    if (!Timing.CountsPerThread && Timing.Measuring != null) others.Wait(Timing.Measuring.Last);
                    yield return null;
                }
                var held = Timing.Measuring?.Since(first);
                var count = Math.Max(1, Time.frameCount - frames);
                var filmsMade = Stage.Filmed - filmed;
                var films = Math.Max(1, filmsMade);
                var repaints = Math.Max(1, Plugin.Repaints - repainted);
                var allocated = Timing.ScryBytes - bytes;
                var own = Timing.CountsPerThread ? allocated : Math.Max(0, allocated - others.Bytes);
                // What the parts allocated of their own, each group in its own unit: the panel's parts
                // a repaint, as the panel draws once per event; the render's a film; the rest a frame.
                var (panelBytes, renderBytes, updateBytes) = OwnByGroup(Timing.OwnBytesByPartCopy(), others);
                p.Note(held != null
                    ? $"{what} ({entry.Name}), {Numbers.Count(held.Frames)} frames: Scry's own work {Numbers.Fixed(held.Mean, 2)} ms a frame on average, {Numbers.Fixed(held.Percentile(0.95), 2)} ms at the 95th percentile; the frame totals say {Numbers.Count((int)(own / count))} bytes a frame, other threads' left out"
                    : $"{what}: frames were not measured");
                p.Note($"{what}, by part, each part's own allocation: " + Timing.BytesByPartTold(30, own: true));
                p.Note($"{what}, by part, inner parts included: " + Timing.BytesByPartTold(30));
                p.Note($"{what}: the stage was filmed in {Numbers.Count(filmsMade)} of {Numbers.Count(count)} frames, {(Stage.LiveForTest ? "alive" : "still")} at the last look; the panel repainted {Numbers.Count(repaints)} times");
                p.Note($"{what}: the panel's parts allocated {Numbers.Count((int)(panelBytes / repaints / 1024))} KB a repaint, the render's {Numbers.Count((int)(renderBytes / films / 1024))} KB a film, the rest {Numbers.Count((int)(updateBytes / count / 1024))} KB a frame, their own shares summed, other threads' taken off for the time each ran");
                // The bounds a quarter over the first clean baseline (50 KB a repaint, 56 a film, 2 a frame at most), so a run says when it grows.
                p.Check(panelBytes / repaints < 64 * 1024 && renderBytes / films < 72 * 1024 && updateBytes / count < 24 * 1024,
                    $"{what}: Scry's own work allocates within bounds standing open: under 64 KB a repaint, 72 KB a film and 24 KB a frame",
                    $"{Numbers.Count((int)(panelBytes / repaints / 1024))} KB a repaint, {Numbers.Count((int)(renderBytes / films / 1024))} KB a film, {Numbers.Count((int)(updateBytes / count / 1024))} KB a frame");
                if (entry == piece) p.Check(filmsMade <= count / 3 + 2, "a still page is filmed in a third of its frames at most", $"{Numbers.Count(filmsMade)} of {Numbers.Count(count)}");
            }
            Stage.Spin = spinWas;
            Stage.Inside = wasInside;
            p.Check(true, "the panel stood open on each and was measured");
        }

        /// <summary>
        /// What Scry costs idling with the panel closed, as between looks: its own work each frame
        /// and what it allocates over five seconds, and what it keeps, the textures, render
        /// textures and meshes it made, beside the game's whole managed memory for scale. Where
        /// the runtime keeps no count of what a thread allocates, as the game's does not, what
        /// Scry allocates is told by the memory in use, which grows by what other threads allocate
        /// while Scry works too; as long a wait each frame as Scry's work took the frame before,
        /// doing nothing, tells how much of that is theirs (<see cref="OtherThreads"/>), and it is
        /// left out of what is checked.
        /// </summary>
        private static IEnumerator IdleCost(Probe p)
        {
            var wasOpen = Session.IsOpen;
            Session.Hide();
            // Places kept by another build of Scry, or with other mods, are read again in the
            // background on entering the world: idling is measured once that is done.
            var waited = Time.unscaledTime;
            yield return Until(() => !Locations.Busy && !Locations.Saving && !FloorRecords.Busy, 300);
            if (Time.unscaledTime - waited > 1f) p.Note($"waited {Numbers.Amount(Time.unscaledTime - waited, 0)} s for the places being read again in the background");
            yield return null;
            yield return null;
            var first = Timing.Measuring?.Frames ?? 0;
            var bytes = Timing.ScryBytes;
            var frames = Time.frameCount;
            Timing.ForgetBytesByPart();
            var others = new OtherThreads(() => GC.GetTotalMemory(false), () => GC.CollectionCount(0), () => System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            var until = Time.unscaledTime + 5f;
            while (Time.unscaledTime < until)
            {
                if (!Timing.CountsPerThread && Timing.Measuring != null) others.Wait(Timing.Measuring.Last);
                yield return null;
            }
            var idle = Timing.Measuring?.Since(first);
            var count = Math.Max(1, Time.frameCount - frames);
            var allocated = Timing.ScryBytes - bytes;
            var own = Timing.CountsPerThread ? allocated : Math.Max(0, allocated - others.Bytes);
            var counted = Timing.CountsPerThread
                ? "counted for Scry's thread alone"
                : $"told by the memory in use, every thread's, in 4 KB steps, as the runtime keeps no count of a thread's own; as long a wait each frame doing nothing, {Numbers.Fixed(others.Ms, 1)} ms in all, saw it grow {Numbers.Count(others.Bytes / 1024)} KB, what other threads allocated meanwhile, which leaves Scry's own about {Numbers.Count(own / 1024)} KB, {Numbers.Count(own / count)} bytes a frame";
            p.Note(idle != null
                ? $"idling {Numbers.Count(idle.Frames)} frames: Scry's own work {Numbers.Fixed(idle.Mean, 3)} ms a frame on average, {Numbers.Fixed(idle.Percentile(0.95), 3)} ms at the 95th percentile, {Numbers.Fixed(idle.Max, 3)} ms at the most; it allocated about {Numbers.Count(allocated / 1024)} KB, {Numbers.Count(allocated / count)} bytes a frame, {counted}"
                : "frames were not measured");
            // By part, an outer one with the inner ones in it, to tell which allocates.
            p.Note((Timing.CountsPerThread ? "by part: " : "by part, other threads' allocations while each ran in it too: ") + Timing.BytesByPartTold(20));
            // Standing still, the compendium's sight looks only at what can move.
            p.Note($"the compendium's sight keeps {Numbers.Count(CompendiumSight.ThingCount)} things to look at, {Numbers.Count(CompendiumSight.ThingsResting)} of them resting as the character and the eye stood still");
            // A frame above a millisecond while idling is one to account for: every part of it.
            if (idle != null && idle.Max >= 1.0 && idle.PartsOfSlowest(1)[0] is string slowest) p.Note("its slowest idle frame: " + slowest);
            p.Note("it keeps: " + KeptTold());
            if (idle != null) p.Check(idle.Mean < 0.25, "idling, Scry's own work is a quarter of a millisecond a frame at the most on average", $"{Numbers.Fixed(idle.Mean, 3)} ms");
            p.Check(own / count < 1024, "idling, Scry allocates under a kilobyte a frame", Timing.CountsPerThread ? $"{Numbers.Count(own / count)} bytes" : $"{Numbers.Count(own / count)} bytes, other threads' share left out");
            if (wasOpen) Session.Show(null);
        }

        /// <summary>The textures, render textures and meshes Scry made, by kind with their memory, and the game's managed and native memory for scale.</summary>
        private static string KeptTold()
        {
            long Size(UnityEngine.Object thing) => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(thing);
            // The panel's own small shapes are left out: a few dozen kilobytes, unnamed.
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>().Where(t => t != null && t.name.StartsWith("Scry", StringComparison.Ordinal)).ToList();
            var renders = Resources.FindObjectsOfTypeAll<RenderTexture>().Where(t => t != null && t.name.StartsWith("Scry", StringComparison.Ordinal)).ToList();
            var meshes = Resources.FindObjectsOfTypeAll<Mesh>().Where(m => m != null && m.name.StartsWith("Scry", StringComparison.Ordinal)).ToList();
            var byName = textures.GroupBy(t => t.name).OrderByDescending(g => g.Sum(Size)).Take(6).Select(g => $"{g.Key} x{Numbers.Count(g.Count())} {Numbers.Count(g.Sum(Size) / 1024)} KB");
            var mb = 1024.0 * 1024.0;
            return $"{Numbers.Count(textures.Count)} textures, {Numbers.Fixed(textures.Sum(Size) / mb, 1)} MB ({string.Join(", ", byName)}); {Numbers.Count(renders.Count)} render textures, {Numbers.Fixed(renders.Sum(Size) / mb, 1)} MB; {Numbers.Count(meshes.Count)} meshes, {Numbers.Fixed(meshes.Sum(Size) / mb, 1)} MB; "
                   + $"{Numbers.Count(X.Catalog.Count)} entries; the game's managed memory in use {Numbers.Amount(GC.GetTotalMemory(false) / mb, 0)} MB, its native memory {Numbers.Amount(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / mb, 0)} MB";
        }

        /// <summary>
        /// The resource monitor, switched on: it draws over the screen, measures Scry's frames,
        /// and tells what Scry holds, its catalog among it; its lines are noted.
        /// </summary>
        private static IEnumerator ResourceMonitor(Probe p)
        {
            var was = Settings.ShowMonitor;
            Settings.ShowMonitor = true;
            var drawn = ScryPanel.Drawn(PanelPart.Monitor);
            var from = Time.unscaledTime;
            yield return Until(() => Time.unscaledTime - from > 1.5f, 3);
            p.Check(ScryPanel.Drawn(PanelPart.Monitor) > drawn, "it draws over the screen", $"{Numbers.Count(ScryPanel.Drawn(PanelPart.Monitor) - drawn)} times");
            p.Check(Monitor.Window.Count > 0 && Monitor.Window.Mean > 0, "it measures Scry's frames", $"{Numbers.Count(Monitor.Window.Count)} frames, {Numbers.Fixed(Monitor.Window.Mean, 3)} ms on average");
            p.Check(Monitor.Lines.Any(l => l.StartsWith("holds ", StringComparison.Ordinal) && l.Contains($"{Numbers.Count(X.Catalog.Count)} entries")), "it tells what Scry holds", string.Join(" | ", Monitor.Lines));
            p.Check(Monitor.Lines.Any(l => l.StartsWith("kept for next time: ", StringComparison.Ordinal) && l.Contains($"the floors of {Numbers.Count(FloorRecords.Count)} kinds")), "and what it keeps for the next session", string.Join(" | ", Monitor.Lines));
            p.Note(string.Join(" | ", Monitor.Lines));
            Settings.ShowMonitor = was;
        }
    }
}
