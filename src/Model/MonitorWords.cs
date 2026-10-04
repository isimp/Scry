using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>The resource monitor's figures in words: milliseconds and memory, and the lines they make.</summary>
    internal static class MonitorWords
    {
        /// <summary>Milliseconds: to two places under ten, to one above.</summary>
        public static string Ms(double ms) => Numbers.Fixed(ms, ms < 10 ? 2 : 1);

        /// <summary>Memory in bytes, kilobytes, megabytes or gigabytes, whichever reads best.</summary>
        public static string Bytes(long bytes)
        {
            bytes = Math.Max(0, bytes);
            if (bytes < 1024) return Numbers.Count(bytes) + " B";
            if (bytes < 1024 * 1024) return Numbers.Count(bytes / 1024) + " KB";
            if (bytes < 1024L * 1024 * 1024) return Numbers.Fixed(bytes / (1024.0 * 1024), 1) + " MB";
            return Numbers.Fixed(bytes / (1024.0 * 1024 * 1024), 1) + " GB";
        }

        /// <summary>The frame time now, on average and at the most, and Scry's share of the frames.</summary>
        public static string Frame(double last, double mean, double max, double share) =>
            $"{Ms(last)} ms a frame now, {Ms(mean)} on average, {Ms(max)} at the most; {Numbers.Fixed(share * 100, 1)}% of the frames";

        /// <summary>One part of Scry's work with the time it took.</summary>
        public static string Part(string name, double ms) => $"{name} {Ms(ms)}";

        /// <summary>The parts of Scry's work one after another (<see cref="Part"/>), or that none was measured.</summary>
        public static string Parts(IReadOnlyList<string> parts) => parts.Count > 0 ? string.Join(" · ", parts) : "nothing measured";

        /// <summary>What Scry allocates a second, and the memory cleanups in its work and in all over the last seconds.</summary>
        public static string Allocates(double bytesPerSecond, int cleanups, int allCleanups, double seconds) =>
            $"allocates {Bytes((long)bytesPerSecond)} a second; {Numbers.Count(cleanups)} memory cleanups in its work, {Numbers.Count(allCleanups)} in all in {Numbers.Amount(seconds, 0)} s";

        /// <summary>What Scry holds: entries, bundles, what is on the stage, and whether previews are in the world.</summary>
        public static string Holds(int entries, int bundles, int parts, int creatures, int grass, bool inWorld) =>
            $"holds {Numbers.Count(entries)} entries, {Numbers.Count(bundles)} bundles loaded; on the stage {Numbers.Count(parts)} parts, {Numbers.Count(creatures)} creatures, {Numbers.Count(grass)} grass; {(inWorld ? "previews in the world" : "nothing in the world")}";

        /// <summary>What Scry has made and keeps: textures, render textures and meshes, each with its memory.</summary>
        public static string Made(int textures, long textureBytes, int renders, long renderBytes, int meshes, long meshBytes) =>
            $"made {Numbers.Count(textures)} textures {Bytes(textureBytes)}, {Numbers.Count(renders)} render textures {Bytes(renderBytes)}, {Numbers.Count(meshes)} meshes {Bytes(meshBytes)}";

        /// <summary>The game's memory: managed and native.</summary>
        public static string Game(long managed, long native) => $"the game's managed memory {Bytes(managed)}, native {Bytes(native)}";
    }
}
