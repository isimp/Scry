using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a lasting file set aside held.</summary>
    internal enum SetAsideKind
    {
        /// <summary>The character's book.</summary>
        Compendium,

        /// <summary>The maps of the dungeons the character walked.</summary>
        Walks,

        /// <summary>The world's dungeon records, kept by the game running it.</summary>
        Records,
    }

    /// <summary>
    /// The lasting files that could not be read and were set aside (<see cref="KeptFile"/>), each
    /// told by a notice in turn, the first first, until put away.
    /// </summary>
    internal sealed class SetAsideFiles
    {
        private readonly List<(SetAsideKind Kind, string Path)> _told = new List<(SetAsideKind, string)>();

        /// <summary>How many are still to be told or put away.</summary>
        public int Count => _told.Count;

        /// <summary>The one the notice tells; none.</summary>
        public (SetAsideKind Kind, string Path)? First => _told.Count > 0 ? _told[0] : ((SetAsideKind, string)?)null;

        /// <summary>A file set aside, where it went; nothing for none.</summary>
        public void Tell(SetAsideKind kind, string path)
        {
            if (path != null) _told.Add((kind, path));
        }

        /// <summary>Puts the notice of the first away, the next told after it.</summary>
        public void PutAway()
        {
            if (_told.Count > 0) _told.RemoveAt(0);
        }
    }
}
