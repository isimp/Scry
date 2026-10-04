using System;

namespace Scry
{
    /// <summary>
    /// What a play button outside an effect list or a clip started, as the previews start it and
    /// the panel asks after it to keep the button lit: an effect played on you or in front of you,
    /// by its prefab, a log or an item let fall, a creature's ragdoll. Two made alike are one key.
    /// </summary>
    internal sealed class PlayKey : IEquatable<PlayKey>
    {
        private readonly string _button;
        private readonly string _prefab;

        private PlayKey(string button, string prefab)
        {
            _button = button;
            _prefab = prefab ?? "";
        }

        /// <summary>An effect played on you.</summary>
        public static PlayKey OnYou(string prefab) => new PlayKey("on you", prefab);

        /// <summary>An effect played in the world in front of you.</summary>
        public static PlayKey There(string prefab) => new PlayKey("there", prefab);

        /// <summary>The selection let fall, on the stage and in the world.</summary>
        public static readonly PlayKey LetFall = new PlayKey("let fall", null);

        /// <summary>The selection's ragdoll let fall, on the stage and in the world.</summary>
        public static readonly PlayKey Ragdoll = new PlayKey("ragdoll", null);

        public bool Equals(PlayKey other) => other != null && string.Equals(_button, other._button, StringComparison.Ordinal) && string.Equals(_prefab, other._prefab, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as PlayKey);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_button) * 31 + StringComparer.Ordinal.GetHashCode(_prefab);
    }
}
