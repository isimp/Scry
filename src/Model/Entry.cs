using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// One thing in the catalog. The game objects behind it are carried untyped, so the catalog
    /// and everything that searches it can be exercised without the game.
    /// </summary>
    public sealed class Entry
    {
        /// <summary>The prefab name, or the status effect's name.</summary>
        public string Name = "";

        /// <summary>The name the game shows for it, in the current language. Empty when it has none.</summary>
        public string DisplayName = "";

        public Kind Kind;
        public Origin Origin;

        /// <summary>Nothing to see or hear, such as an invisible controller object.</summary>
        public bool Empty;

        /// <summary>How many levels above the first have a look of their own (creature stars).</summary>
        public int ExtraLevels;

        /// <summary>Whether the piece has worn and broken looks.</summary>
        public bool HasWear;

        /// <summary>Names of the prefabs whose effect lists point at this one.</summary>
        public List<string> UsedBy = new List<string>();

        /// <summary>The prefab (a GameObject) or status effect behind this entry.</summary>
        public object Source;

        /// <summary>The icon the game has for it (a Sprite), if any.</summary>
        public object Icon;

        /// <summary>
        /// The key favourites are stored under. Status effects live in their own namespace, since
        /// a status effect and a prefab may share a name.
        /// </summary>
        public string Key => Kind == Kind.StatusEffect ? "se:" + Name : Name;

        public override string ToString() => Key;
    }
}
