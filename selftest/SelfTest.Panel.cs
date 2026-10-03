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
            var drawn = ScryPanel.TestNoticesDrawn;
            yield return Until(() => ScryPanel.TestNoticesDrawn > drawn, 3);
            p.Check(ScryPanel.TestNoticesDrawn > drawn, "the panel's strip shows the self-test running");
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

            var drawn = ScryPanel.ViewMenusDrawn;
            ScryPanel.OpenViewMenu(shown, under);
            yield return Until(() => ScryPanel.ViewMenusDrawn > drawn, 3);
            p.Check(ScryPanel.ViewMenuOpen && ScryPanel.ViewMenusDrawn > drawn, "the View box opens and draws");
            ScryPanel.CloseBoxes();

            foreach (var size in new[] { false, true })
            {
                drawn = ScryPanel.SlidersDrawn;
                ScryPanel.OpenSlider(shown, size, under);
                yield return Until(() => ScryPanel.SlidersDrawn > drawn, 3);
                p.Check(ScryPanel.SliderOpen && ScryPanel.SlidersDrawn > drawn, size ? "the size slider opens and draws" : "the volume slider opens and draws");
            }

            ScryPanel.OpenViewMenu(shown, under);
            Select(sword);
            yield return Until(() => !ScryPanel.ViewMenuOpen && !ScryPanel.SliderOpen, 3);
            p.Check(!ScryPanel.ViewMenuOpen && !ScryPanel.SliderOpen, "another selection closes them");
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

        /// <summary>
        /// What Scry costs idling with the panel closed, as between looks: its own work each frame
        /// and what it allocates over five seconds, and what it keeps, the textures, render
        /// textures and meshes it made, beside the game's whole managed memory for scale.
        /// </summary>
        private static IEnumerator IdleCost(Probe p)
        {
            var wasOpen = Session.IsOpen;
            Session.Hide();
            yield return null;
            yield return null;
            var first = Timing.Measuring?.Frames ?? 0;
            var bytes = Timing.ScryBytes;
            var frames = Time.frameCount;
            yield return new Wait(5.0);
            var idle = Timing.Measuring?.Since(first);
            var count = Math.Max(1, Time.frameCount - frames);
            var allocated = Timing.ScryBytes - bytes;
            p.Note(idle != null
                ? $"idling {Numbers.Count(idle.Frames)} frames: Scry's own work {Numbers.Fixed(idle.Mean, 3)} ms a frame on average, {Numbers.Fixed(idle.Percentile(0.95), 3)} ms at the 95th percentile, {Numbers.Fixed(idle.Max, 3)} ms at the most; it allocated about {Numbers.Count(allocated / 1024)} KB, {Numbers.Count(allocated / count)} bytes a frame"
                : "frames were not measured");
            p.Note("it keeps: " + KeptTold());
            if (idle != null) p.Check(idle.Mean < 0.25, "idling, Scry's own work is a quarter of a millisecond a frame at the most on average", $"{Numbers.Fixed(idle.Mean, 3)} ms");
            p.Check(allocated / count < 1024, "idling, Scry allocates under a kilobyte a frame", $"{Numbers.Count(allocated / count)} bytes");
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
            var was = Plugin.ShowMonitor;
            Plugin.ShowMonitor = true;
            var drawn = ScryPanel.MonitorsDrawn;
            var from = Time.unscaledTime;
            yield return Until(() => Time.unscaledTime - from > 1.5f, 3);
            p.Check(ScryPanel.MonitorsDrawn > drawn, "it draws over the screen", $"{Numbers.Count(ScryPanel.MonitorsDrawn - drawn)} times");
            p.Check(Monitor.Window.Count > 0 && Monitor.Window.Mean > 0, "it measures Scry's frames", $"{Numbers.Count(Monitor.Window.Count)} frames, {Numbers.Fixed(Monitor.Window.Mean, 3)} ms on average");
            p.Check(Monitor.Lines.Any(l => l.StartsWith("holds ", StringComparison.Ordinal) && l.Contains($"{Numbers.Count(X.Catalog.Count)} entries")), "it tells what Scry holds", string.Join(" | ", Monitor.Lines));
            p.Note(string.Join(" | ", Monitor.Lines));
            Plugin.ShowMonitor = was;
        }
    }
}
