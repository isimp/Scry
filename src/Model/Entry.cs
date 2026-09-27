using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// One thing in the catalog. The game objects behind it are carried untyped, so the catalog
    /// and everything that searches it can be exercised without the game.
    /// </summary>
    public sealed class Entry
    {
        private string _name = "";
        private string _displayName = "";

        // Worked out once per name, since the search reads them for every entry on every keystroke.
        private string _nameUpper;
        private string _displayNameUpper;
        private string _statusEffectKey;

        /// <summary>The prefab name, or the status effect's name.</summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                _nameUpper = null;
                _statusEffectKey = null;
            }
        }

        /// <summary>The name the game shows for it, in the current language. Empty when it has none.</summary>
        public string DisplayName
        {
            get => _displayName;
            set
            {
                _displayName = value;
                _displayNameUpper = null;
            }
        }

        /// <summary>The prefab name in capitals, to match typed words against whatever their case.</summary>
        internal string NameUpper => _nameUpper ?? (_nameUpper = _name?.ToUpperInvariant());

        /// <summary>The shown name in capitals, to match typed words against whatever their case.</summary>
        internal string DisplayNameUpper => _displayNameUpper ?? (_displayNameUpper = _displayName?.ToUpperInvariant());

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

        /// <summary>What leaves this behind (a ragdoll's creature, a log's tree), when it is such a leftover.</summary>
        public List<string> LeftBy = new List<string>();

        /// <summary>The leftovers this leaves behind: its ragdoll, its log and stump, its debris.</summary>
        public List<string> LeavesBehind = new List<string>();

        /// <summary>What else it is linked to, under headings: what it carries, its footsteps, its set, its ammo.</summary>
        public List<Link> Links = new List<Link>();

        /// <summary>The links under each heading, in the order the headings first came in.</summary>
        public List<KeyValuePair<string, List<Link>>> LinkGroups()
        {
            var groups = new List<KeyValuePair<string, List<Link>>>();
            foreach (var link in Links)
            {
                var group = groups.FindIndex(g => g.Key == link.Group);
                if (group < 0) groups.Add(new KeyValuePair<string, List<Link>>(link.Group, new List<Link> { link }));
                else groups[group].Value.Add(link);
            }
            return groups;
        }

        /// <summary>Registered with the scene, so the game's own spawn command knows it.</summary>
        public bool Registered;

        /// <summary>The type names of the components on the prefab, each once.</summary>
        public string[] Components = new string[0];

        /// <summary>The biomes it spawns or grows in, as the game names them.</summary>
        public string[] Biomes = new string[0];

        /// <summary>The mod that added it, when that could be told. Empty otherwise.</summary>
        public string ModName = "";

        /// <summary>
        /// The looks it can be shown in, when the game switches between several by script: a
        /// creature with or without its gear, a fire lit or not, a plant growing or grown.
        /// </summary>
        public string[] Looks = new string[0];

        /// <summary>The look it is shown in at first.</summary>
        public int DefaultLook;

        /// <summary>The crafting stations it is made or built at, with the level each needs.</summary>
        public StationUse[] Stations = new StationUse[0];

        /// <summary>The prefab (a GameObject) or status effect behind this entry.</summary>
        public object Source;

        /// <summary>The icon the game has for it (a Sprite), if any.</summary>
        public object Icon;

        /// <summary>
        /// The key favourites are stored under. Status effects live in their own namespace, since
        /// a status effect and a prefab may share a name.
        /// </summary>
        public string Key => Kind == Kind.StatusEffect ? _statusEffectKey ?? (_statusEffectKey = "se:" + Name) : Name;

        public override string ToString() => Key;
    }

    /// <summary>A crafting station something is made at, by its prefab name and shown name, and the level it needs.</summary>
    public struct StationUse
    {
        public string Name;
        public string Shown;
        public int Level;

        public StationUse(string name, string shown, int level)
        {
            Name = name ?? "";
            Shown = shown ?? "";
            Level = level;
        }

    }
}
