using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// One thing an entry is linked to: under which heading, the key of the entry it goes to
    /// (a prefab name, or "se:" and a status effect's name), and what the link is about, such as
    /// the gait of a footstep or the clip that plays a sound.
    /// </summary>
    internal sealed class Link
    {
        public string Group;
        public string Target;
        public List<string> Notes = new List<string>();

        /// <summary>
        /// Whether this is the far end of a link noted from its target, under the heading the
        /// target's link is shown by here ("Given by" on a status effect, "Spawned by" on what a
        /// projectile leaves): it tells what leads to the entry, not what the entry has.
        /// </summary>
        public bool Back;
    }

    /// <summary>
    /// Links between entries, noted while the catalog is read and put on the entries at the end.
    /// Every link is noted from one end and shows on both, each end under its own heading ("Carries"
    /// on a troll, "Carried by" on its club). Links to what is not in the catalog, and from
    /// anything to itself, are left out; the same link noted again only adds what it is about.
    /// </summary>
    internal sealed class LinkBook
    {
        public const string SameSet = "Same set";
        public const string Variants = "Variants";
        public const string VariantOf = "Variant of";
        public const string Shoots = "Shoots";
        public const string ShotFrom = "Shot from";

        private readonly List<(string From, string Group, string To, string Note, bool Back)> _links = new List<(string, string, string, string, bool)>();

        /// <summary>
        /// Notes a link from one entry to another, with the heading each end shows it under. Without
        /// a heading an end does not show it, as when that end tells of it in its own way already.
        /// </summary>
        public void Add(string from, string group, string to, string backGroup, string note = null)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            if (group != null) _links.Add((from, group, to, note, false));
            if (backGroup != null) _links.Add((to, backGroup, from, note, true));
        }

        /// <summary>
        /// Links the pieces of each set to the rest of it, given each item's set name and shown
        /// name. The game keeps copies of some pieces under the same name and set for creatures to
        /// wear; one of them stands for the piece (the one that is made, else the shortest name)
        /// and the others are linked to it as its variants rather than listed as more pieces.
        /// </summary>
        public void AddSets(IEnumerable<(string Item, string Set, string Shown, bool Made)> items)
        {
            foreach (var set in items.Where(i => !string.IsNullOrEmpty(i.Set)).GroupBy(i => i.Set))
            {
                var pieces = new List<string>();
                foreach (var piece in set.GroupBy(i => string.IsNullOrEmpty(i.Shown) ? i.Item : i.Shown))
                {
                    var copies = piece.GroupBy(i => i.Item).Select(g => g.First()).ToList();
                    var stands = copies.OrderBy(i => i.Made ? 0 : 1).ThenBy(i => i.Item.Length).ThenBy(i => i.Item, StringComparer.Ordinal).First();
                    pieces.Add(stands.Item);
                    foreach (var copy in copies) if (copy.Item != stands.Item) Add(stands.Item, Variants, copy.Item, VariantOf);
                }
                foreach (var item in pieces)
                {
                    foreach (var other in pieces) _links.Add((item, SameSet, other, null, false));
                }
            }
        }

        /// <summary>Links weapons and the ammo they shoot, which share the game's ammo type.</summary>
        public void AddAmmo(IEnumerable<(string Item, string AmmoType, bool IsAmmo)> items)
        {
            foreach (var type in items.Where(i => !string.IsNullOrEmpty(i.AmmoType)).GroupBy(i => i.AmmoType))
            {
                foreach (var weapon in type.Where(i => !i.IsAmmo))
                {
                    foreach (var ammo in type.Where(i => i.IsAmmo)) Add(weapon.Item, Shoots, ammo.Item, ShotFrom);
                }
            }
        }

        /// <summary>
        /// Links each crafting station to its upgrades as the game counts them: every piece whose own
        /// station goes by the station's name (<c>StationExtension.FindExtensions</c> compares the
        /// names), so a piece naming another prefab of the station, as a mod's copy of it, upgrades
        /// it too. A piece naming a station no prefab of the catalog goes by, or one of no name, is
        /// linked to the station it names.
        /// </summary>
        public void AddUpgrades(IEnumerable<(string Upgrade, string Station, string StationName)> upgrades, IEnumerable<(string Station, string Name)> stations)
        {
            // A station of no name is matched by none, as no upgrade looks for it by name.
            var byName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var (station, name) in stations)
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (!byName.TryGetValue(name, out var named)) byName[name] = named = new List<string>();
                named.Add(station);
            }
            foreach (var (upgrade, station, name) in upgrades)
            {
                if (byName.TryGetValue(name ?? "", out var named)) foreach (var each in named) Add(each, LinkWords.Upgrades, upgrade, LinkWords.UpgradeOf);
                else Add(station, LinkWords.Upgrades, upgrade, LinkWords.UpgradeOf);
            }
        }

        /// <summary>
        /// Puts the links on the entries at both of their ends. A copy of a set piece is left out
        /// of a heading where the piece it copies is listed already, so the piece shows once.
        /// </summary>
        public void Apply(IEnumerable<Entry> catalog)
        {
            foreach (var _ in ApplyInSteps(catalog, int.MaxValue)) { }
        }

        /// <summary>
        /// The same, a few links at a time: each step puts on up to <paramref name="perStep"/> of
        /// the links noted, and says how many are on so far, so the work can be spread over frames.
        /// </summary>
        public IEnumerable<int> ApplyInSteps(IEnumerable<Entry> catalog, int perStep)
        {
            var byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in catalog) if (!byKey.ContainsKey(entry.Key)) byKey[entry.Key] = entry;

            var copyOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (from, group, to, _, _) in _links) if (group == VariantOf) copyOf[from] = to;
            var listed = new HashSet<(string, string, string)>(_links.Select(l => (l.From, l.Group, l.To)));

            // Each entry's links by heading and target, so a link noted again is found at once
            // rather than looked for among all of the entry's (a creature's footsteps, a set's pieces).
            var index = new Dictionary<(Entry, string, string), Link>();
            var indexed = new HashSet<Entry>();
            yield return 0;

            var done = 0;
            foreach (var (from, group, to, note, back) in _links)
            {
                if (++done % perStep == 0) yield return done;
                if (from == to || !byKey.TryGetValue(from, out var entry) || !byKey.ContainsKey(to)) continue;
                if (group != Variants && group != VariantOf && copyOf.TryGetValue(to, out var piece) && listed.Contains((from, group, piece))) continue;

                if (indexed.Add(entry))
                {
                    foreach (var had in entry.Links) if (!index.ContainsKey((entry, had.Group, had.Target))) index[(entry, had.Group, had.Target)] = had;
                }
                if (!index.TryGetValue((entry, group, to), out var link))
                {
                    link = new Link { Group = group, Target = to, Back = back };
                    entry.Links.Add(link);
                    index[(entry, group, to)] = link;
                }
                if (!string.IsNullOrEmpty(note) && !link.Notes.Contains(note)) link.Notes.Add(note);
            }
        }
    }
}
