using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The resource monitor: while its setting is on (Diagnostics, or <c>/scry monitor</c>), a
    /// small box in the screen's corner, also with the panel closed, tells what Scry costs and
    /// holds: its own time each frame now, on average and at the most over the last ten seconds,
    /// with a little graph of it and its parts; what it allocates a second and the memory
    /// cleanups that ran in its work; and what it holds, its catalog, the bundles it has loaded,
    /// what stands on its stage and in the world, and the textures, render textures and meshes
    /// it made (<see cref="Kept"/>), beside the game's memory for scale. Its figures are made
    /// again four times a second, what it holds once a second, and its own drawing is a part of
    /// its own, "monitor", so it shows what it costs too. For finding faults, not for play.
    /// </summary>
    internal static class Monitor
    {
        /// <summary>About ten seconds of frames.</summary>
        private const int Frames = 600;

        public static readonly MonitorWindow Window = new MonitorWindow(Frames);

        public static bool On => Plugin.ShowMonitor;

        private static float _figuresAt = -10f;
        private static float _holdsAt = -10f;
        private static int _gameCleanupsFrom = -1;
        private static float _gameCleanupsSince;

        /// <summary>The lines it shows, made again a few times a second, and its graph.</summary>
        public static readonly List<string> Lines = new List<string>();
        public static double[] Graph = new double[0];
        public static double GraphTop = 1;
        private static string _holds = "", _made = "", _game = "";

        /// <summary>A frame of Scry's work is done: its time and the frame's, what it allocated, and its parts.</summary>
        public static void Frame(FrameTimes frame, double frameMs)
        {
            Window.Add(frame.Total, frameMs, frame.Bytes, frame.Cleanups);
            frame.ForEachPart((name, ms) => Window.AddPart(name, ms));
            Window.EndParts();
        }

        /// <summary>Makes its lines again when they are due.</summary>
        public static void Refresh()
        {
            var now = Time.unscaledTime;
            if (now - _holdsAt >= 1f)
            {
                _holdsAt = now;
                Holds();
            }
            if (now - _figuresAt < 0.25f) return;
            _figuresAt = now;

            var cleanups = GC.CollectionCount(0);
            if (_gameCleanupsFrom < 0 || now - _gameCleanupsSince > 10f)
            {
                _gameCleanupsFrom = cleanups;
                _gameCleanupsSince = now;
            }

            Lines.Clear();
            Lines.Add($"{MonitorWords.Ms(Window.Last)} ms a frame now, {MonitorWords.Ms(Window.Mean)} on average, {MonitorWords.Ms(Window.Max)} at the most; {Window.Share * 100:0.0}% of the frames");
            var outer = new List<string>();
            var inner = new List<string>();
            foreach (var (name, ms) in Window.Parts(20))
            {
                if (name.IndexOf(' ') < 0) outer.Add($"{name} {MonitorWords.Ms(ms)}");
                else if (inner.Count < 4) inner.Add($"{name} {MonitorWords.Ms(ms)}");
            }
            Lines.Add(outer.Count > 0 ? string.Join(" · ", outer) : "nothing measured");
            if (inner.Count > 0) Lines.Add(string.Join(" · ", inner));
            Lines.Add($"allocates {MonitorWords.Bytes((long)Window.BytesPerSecond)} a second; {Window.Cleanups} memory cleanups in its work, {cleanups - _gameCleanupsFrom} in all in {now - _gameCleanupsSince:0} s");
            Lines.Add(_holds);
            Lines.Add(_made);
            Lines.Add(_game);

            Graph = Window.Columns(60);
            GraphTop = 1;
            foreach (var value in Graph) GraphTop = Math.Max(GraphTop, value);
        }

        /// <summary>What Scry holds, counted once a second.</summary>
        private static void Holds()
        {
            var catalog = Session.Explorer?.Catalog.Count ?? 0;
            var bundles = (PlaceAssets.Held != null ? 1 : 0) + ExampleLayouts.HeldCount;
            _holds = $"holds {catalog} entries, {bundles} bundles loaded; on the stage {Stage.PartsOnStage} parts, {Stage.CreaturesMade} creatures, {Stage.GrassCount} grass; {(Previews.AnythingInWorld ? "previews in the world" : "nothing in the world")}";
            var kept = Kept.Tally();
            _made = $"made {kept.Textures} textures {MonitorWords.Bytes(kept.TextureBytes)}, {kept.Renders} render textures {MonitorWords.Bytes(kept.RenderBytes)}, {kept.Meshes} meshes {MonitorWords.Bytes(kept.MeshBytes)}";
            _game = $"the game's managed memory {MonitorWords.Bytes(GC.GetTotalMemory(false))}, native {MonitorWords.Bytes(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong())}";
        }
    }
}
