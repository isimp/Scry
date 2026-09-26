using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a part of the game Scry relies on was found.</summary>
    public enum Found
    {
        /// <summary>There, as it was when this version of Scry was made.</summary>
        Present,

        /// <summary>There, but its code has changed since, so what Scry copies of it may be off.</summary>
        Changed,

        /// <summary>Gone or renamed, so the feature that needs it is off.</summary>
        Missing,
    }

    /// <summary>
    /// The parts of the game Scry reaches by name or copies the workings of, as found at start:
    /// each with the feature that needs it. Told in the log as one line when all is well, and a
    /// line for each part that is missing or has changed otherwise.
    /// </summary>
    public sealed class Checklist
    {
        private readonly List<(string Part, string Feature, Found Found)> _parts = new List<(string, string, Found)>();

        public void Add(string part, string feature, Found found) => _parts.Add((part, feature, found));

        public bool AnyTrouble => _parts.Any(p => p.Found != Found.Present);

        public List<string> Report()
        {
            var missing = _parts.Count(p => p.Found == Found.Missing);
            var changed = _parts.Count(p => p.Found == Found.Changed);
            var head = $"Scry checked {_parts.Count} parts of the game it relies on";
            if (missing == 0 && changed == 0) return new List<string> { head + ": all as expected." };

            var lines = new List<string> { $"{head}: {missing} missing, {changed} changed." };
            foreach (var part in _parts.Where(p => p.Found == Found.Missing)) lines.Add($"  {part.Part} is missing: {part.Feature} is off.");
            foreach (var part in _parts.Where(p => p.Found == Found.Changed)) lines.Add($"  {part.Part} changed since this version of Scry: {part.Feature} may preview slightly wrong.");
            return lines;
        }
    }
}
