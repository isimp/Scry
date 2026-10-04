using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A feature of Scry as a player knows it, named as the notice of what is off names it. Every
    /// feature is made here, in this one list (split over files by what it is part of), so the
    /// startup check, a part of Scry that fails because the game changed, and the notice all name
    /// it one way, and one feature off for two reasons is named once. A family of features named
    /// by what they work on (a part of the details, a layer, a script) is made by its function,
    /// and two made alike are the same feature.
    /// </summary>
    internal sealed partial class Feature : IEquatable<Feature>
    {
        private Feature(string name)
        {
            Name = name;
        }

        /// <summary>The feature's name, as it reads in the middle of a sentence.</summary>
        public string Name { get; }

        /// <summary>A part of the details, by the word its reader goes by: "the resistances details".</summary>
        public static Feature Details(string part) => new Feature(FactWords.Part(part));

        /// <summary>Falling copies landing on a layer the game names.</summary>
        public static Feature FallingCopies(string layer) => new Feature($"falling copies landing on {layer}");

        /// <summary>The effects that keep a script of the game's on their copies.</summary>
        public static Feature KeptScript(string script) => new Feature($"effects keeping their {script}");

        /// <summary>The link from a kind of damage to the status effect it puts on.</summary>
        public static Feature DamageLink(string effect) => new Feature($"linking damage to {effect}");

        /// <summary>Every feature of the list, the families' aside.</summary>
        public static IReadOnlyList<Feature> All => _listed;

        /// <summary>
        /// The list's features as they are made. It has no initializer of its own: the list is made
        /// over several files, whose features may be made before a line here would run.
        /// </summary>
        private static List<Feature> _listed;

        /// <summary>A feature of the list, kept in <see cref="All"/>.</summary>
        private static Feature Listed(string name)
        {
            var feature = new Feature(name);
            (_listed ?? (_listed = new List<Feature>())).Add(feature);
            return feature;
        }

        /// <summary>What is off, each feature once: those a missing part turned off at start, then those a game change turned off since.</summary>
        public static List<Feature> Off(IEnumerable<Feature> missing, IEnumerable<Feature> changed) => missing.Concat(changed).Distinct().ToList();

        public bool Equals(Feature other) => other != null && string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as Feature);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

        public override string ToString() => Name;

        public static readonly Feature World = Listed("Scry's work in a world");
        public static readonly Feature Leaving = Listed("putting everything away on leaving a world");
        public static readonly Feature Catalog = Listed("the catalog");
        public static readonly Feature StartupCheck = Listed("the startup check");
        public static readonly Feature SelfTest = Listed("the self-test");
    }
}
