using System;
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
        private string _key;

        /// <summary>The prefab name, or the status effect's name.</summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                _nameUpper = null;
                _key = null;
                NameOrder = -1;
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
                NameOrder = -1;
            }
        }

        /// <summary>
        /// Where the name comes among the catalog's names, for sorting results by name without
        /// comparing the names each time; -1 until the catalog's names are put in order.
        /// </summary>
        public int NameOrder = -1;

        /// <summary>The prefab name in capitals, to match typed words against whatever their case.</summary>
        internal string NameUpper => _nameUpper ?? (_nameUpper = _name?.ToUpperInvariant());

        /// <summary>The shown name in capitals, to match typed words against whatever their case.</summary>
        internal string DisplayNameUpper => _displayNameUpper ?? (_displayNameUpper = _displayName?.ToUpperInvariant());

        private Kind _kind;

        public Kind Kind
        {
            get => _kind;
            set
            {
                _kind = value;
                _key = null;
            }
        }

        /// <summary>
        /// The group a kind's tab lists it under (a resource by how it is gathered), and where
        /// that group comes among the others; none has an empty name and comes first.
        /// </summary>
        public string Group = "";
        public int GroupOrder;

        /// <summary>For an effect or sound, the effect lists that play it, by field name (a status effect's marked "se:", the interface's "ui:").</summary>
        public string[] PlayedIn = new string[0];

        /// <summary>Whether it took the kind of what leaves it behind when paired with it (a stump listed with its trees).</summary>
        public bool KindFromOwners;
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

        /// <summary>The locations and kinds of dungeon room it is found in, once they are read (<see cref="Places"/>).</summary>
        public string[] FoundIn = new string[0];

        /// <summary>The mod that added it, when that could be told. Empty otherwise.</summary>
        public string ModName = "";

        /// <summary>The clue its mod was named by; a clue that is not the mod's own word makes it Scry's best guess (<see cref="UnsureWords"/>).</summary>
        public string ModClue = "";

        /// <summary>Its place within its group, lower first (a dungeon's location before its rooms); 0 for most.</summary>
        public int GroupRank;

        /// <summary>A word or two beside its name in the list, in place of its prefab name (a room's "entrance room"); null for none.</summary>
        public string Tag;

        /// <summary>Whether it is listed indented, under the first of its group (a room under its dungeon).</summary>
        public bool Indent;

        private string[] _looks = new string[0];
        private int _defaultLook;
        private Func<(string[] Names, int Default)> _readLooks;

        /// <summary>
        /// The looks it can be shown in, when the game switches between several by script: a
        /// creature with or without its gear, a fire lit or not, a plant growing or grown.
        /// </summary>
        public string[] Looks
        {
            get
            {
                ReadLooks();
                return _looks;
            }
            set
            {
                _readLooks = null;
                _looks = value ?? new string[0];
            }
        }

        /// <summary>The look it is shown in at first.</summary>
        public int DefaultLook
        {
            get
            {
                ReadLooks();
                return _defaultLook;
            }
            set
            {
                ReadLooks();
                _defaultLook = value;
            }
        }

        /// <summary>
        /// Leaves the looks to be worked out when they are first asked for, which is when the
        /// entry is selected: reading them off a prefab takes a while, and a catalog of thousands
        /// only ever needs a few. Looks that cannot be worked out are none.
        /// </summary>
        public void LooksFrom(Func<(string[] Names, int Default)> read)
        {
            _readLooks = read;
        }

        private void ReadLooks()
        {
            var read = _readLooks;
            if (read == null) return;
            _readLooks = null;

            // One whose looks cannot be read offers none.
            if (Steps.Run(read, out var looks, null) == null)
            {
                _looks = looks.Names ?? new string[0];
                _defaultLook = looks.Default;
            }
            else
            {
                _looks = new string[0];
                _defaultLook = 0;
            }
        }

        /// <summary>The crafting stations it is made or built at, with the level each needs.</summary>
        public StationUse[] Stations = new StationUse[0];

        /// <summary>The prefab (a GameObject) or status effect behind this entry.</summary>
        public object Source;

        /// <summary>The icon the game has for it (a Sprite), if any.</summary>
        public object Icon;

        /// <summary>
        /// The key favourites, recent and links use (<see cref="EntryKeys"/>). What is no prefab
        /// lives in a namespace of its own, since it may share a name with a prefab.
        /// </summary>
        public string Key => _key ?? (_key = EntryKeys.For(Kind, Name));

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
