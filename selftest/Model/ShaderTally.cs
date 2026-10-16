using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// For the self-test: the shaders a dungeon example's rooms are drawn with, each with how many
    /// of their parts the dimming reached (<see cref="Dim"/> darkens a tint and a glow) and how many
    /// it left as they were, and for those it left, the colours the shader takes instead. The
    /// shaders leaving the most parts as they were come first.
    /// </summary>
    internal sealed class ShaderTally
    {
        private sealed class Counted
        {
            public int Dimmed;
            public int Left;
            public readonly List<string> Colours = new List<string>();
        }

        private readonly Dictionary<string, Counted> _shaders = new Dictionary<string, Counted>();

        /// <summary>How many parts the dimming left as they were, of every shader.</summary>
        public int Left => _shaders.Values.Sum(c => c.Left);

        /// <summary>Counts a part drawn with a shader, whether the dimming reached it, and the colours its shader takes.</summary>
        public void Add(string shader, bool dimmed, IEnumerable<string> colours)
        {
            if (!_shaders.TryGetValue(shader, out var counted)) _shaders[shader] = counted = new Counted();
            if (dimmed)
            {
                counted.Dimmed++;
                return;
            }
            counted.Left++;
            foreach (var colour in colours) if (!counted.Colours.Contains(colour)) counted.Colours.Add(colour);
        }

        /// <summary>A line for each shader, those leaving the most parts as they were first, then by name.</summary>
        public List<string> Lines() => _shaders
            .OrderByDescending(s => s.Value.Left)
            .ThenBy(s => s.Key, System.StringComparer.Ordinal)
            .Select(s => Line(s.Key, s.Value))
            .ToList();

        private static string Line(string shader, Counted counted)
        {
            var parts = counted.Dimmed + counted.Left;
            var line = $"{shader}: {Numbers.Count(parts)} {(parts == 1 ? "part" : "parts")}, ";
            if (counted.Left == 0) return line + "all dimmed";
            line += counted.Dimmed == 0 ? "none dimmed" : $"{Numbers.Count(counted.Dimmed)} dimmed, {Numbers.Count(counted.Left)} left as they were";
            return line + (counted.Colours.Count > 0 ? ", its colours " + string.Join(", ", counted.Colours) : ", no colours");
        }
    }
}
