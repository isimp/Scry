using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What an entry is in the game, in words: an item's stats and recipe, a creature's health,
    /// resistances and drops, a piece's cost and comfort, a resource's drops, a chest's contents,
    /// what a status effect changes, and where things spawn, grow or come from. Read from the
    /// prefab, so mods' changes show; a world whose settings change them says so.
    /// </summary>
    internal sealed partial class Facts
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Facts() => WorldCaches.Register(nameof(Facts), Forget);

        /// <summary>A row of items with amounts, such as a recipe or a creature's drops.</summary>
        public sealed class Row
        {
            public string Title = "";

            /// <summary>The prefab the title names, such as the crafting station, so it can be gone to.</summary>
            public string TitleLink;
            public readonly List<Ingredient> Items = new List<Ingredient>();
        }

        public struct Ingredient
        {
            public Sprite Icon;
            public string Name;
            public string Amount;

            /// <summary>The prefab, so the panel can jump to it.</summary>
            public string Prefab;
        }

        /// <summary>The text the game describes it with, if any.</summary>
        public string Description = "";

        /// <summary>Named values, shown as a two-column table.</summary>
        public readonly List<KeyValuePair<string, string>> Pairs = new List<KeyValuePair<string, string>>();

        public readonly List<Row> Rows = new List<Row>();

        /// <summary>Where it lives, comes from, or what gives it, under <see cref="WhereTitle"/>.</summary>
        public readonly List<Source> Where = new List<Source>();
        public string WhereTitle = "Where it comes from";

        /// <summary>What it is used for, a row for each kind of use and place.</summary>
        public readonly List<Row> UseRows = new List<Row>();

        public bool IsEmpty => Description.Length == 0 && Pairs.Count == 0 && Rows.Count == 0 && Where.Count == 0 && UseRows.Count == 0;

        /// <summary>Values that name something in the catalog, by their label: a prefab name, or "se:" and a status effect's.</summary>
        public readonly Dictionary<string, string> Links = new Dictionary<string, string>();

        private void Add(string label, string value, string link = null)
        {
            if (string.IsNullOrEmpty(value)) return;
            Pairs.Add(new KeyValuePair<string, string>(label, value));
            if (link != null) Links[label] = link;
        }

        private static readonly Dictionary<Entry, Facts> Cache = new Dictionary<Entry, Facts>();

        public static Facts For(Entry entry)
        {
            if (entry == null) return new Facts();
            if (Cache.TryGetValue(entry, out var known)) return known;

            var facts = new Facts();
            if (entry.Source is StatusEffect effect)
            {
                facts.Part("status effect", () => facts.StatusEffect(effect));
            }
            else if (entry.Source is RandomEvent raid)
            {
                facts.Part("raid", () => facts.Raid(raid));
            }
            else if (entry.Source is PlaceSource place)
            {
                facts._entry = entry;
                facts.Place(entry, place);
            }
            else if (entry.Source is GameObject prefab)
            {
                // The game spawns creatures up to two stars; some mods go higher, and show it.
                facts._stars = Math.Max(2, entry.ExtraLevels);
                facts._entry = entry;
                facts.Prefab(prefab);
                if (entry.Kind == Kind.Creature) facts.WhereTitle = "Where it lives";
                facts.Part("where it comes from", () =>
                {
                    facts.Where.AddRange(Knowledge.WhereLines(entry.Name));
                    facts.Where.AddRange(Knowledge.SourceLines(entry.Name));

                    // An item nothing makes, drops, sells or spawns here comes from somewhere Scry cannot
                    // see, unless the locations, once read, show where it is found. Something that
                    // spawns it (an Asksvin its egg) is told under LINKED.
                    if (entry.Kind == Kind.Item && facts.Where.Count == 0 && entry.FoundIn.Length == 0 && !facts.Rows.Any(r => r.Title.StartsWith("Made"))
                        && !entry.Links.Any(l => l.Group == Relations.SpawnedBy))
                    {
                        facts.Where.Add(new Source("Nothing loaded makes, drops or sells it. It may come from a location, a dungeon, an event or a mod.", null));
                    }
                });
                facts.Part("uses", () => facts.Uses(entry.Name));
            }
            facts.TellMissing();

            Cache[entry] = facts;
            return facts;
        }

        /// <summary>The parts of these facts that could not be read, told at their end.</summary>
        private readonly List<string> _missing = new List<string>();

        /// <summary>
        /// Reads one part of the facts on its own. One that fails, on a mod's odd prefab or because
        /// an update changed what it reads, is told once in the log and named at the end of the
        /// facts, and the other parts still show.
        /// </summary>
        private void Part(string part, Action read)
        {
            var started = Timing.Start();
            try
            {
                read();
            }
            catch (Exception ex)
            {
                Faults.Tell("the " + part + " details", ex);
                if (!_missing.Contains(part)) _missing.Add(part);
            }
            finally
            {
                if (Plugin.LogPreviews) Timing.Add("facts " + part, started);
            }
        }

        private void TellMissing()
        {
            if (_missing.Count == 0) return;
            Add("Not shown", $"the {string.Join(", ", _missing)} details could not be read (the log says why)");
        }

        /// <summary>Forgets everything read, for a new world.</summary>
        public static void Forget()
        {
            Cache.Clear();
        }

        /// <summary>Forgets what was told of one entry, which knows more now (a location, once read).</summary>
        public static void Forget(Entry entry)
        {
            if (entry != null) Cache.Remove(entry);
        }

        private void Prefab(GameObject prefab)
        {
            var drop = prefab.GetComponent<ItemDrop>();
            var shared = drop?.m_itemData?.m_shared;
            if (shared != null) Part("item", () => Item(prefab, shared));

            var character = prefab.GetComponent<Character>();
            if (character != null) Part("creature", () => Creature(prefab, character));

            // The build menu tab it is under, named as the list's group names it (not the game's
            // category enum). Told for every piece, one whose Piece sits on a part of it too.
            if (_entry != null && _entry.Kind == Kind.Piece) Add("Build menu", _entry.Group == Groups.InNoMenu.Name ? "none" : _entry.Group);

            var piece = prefab.GetComponent<Piece>();
            if (piece != null && piece.enabled) Part("piece", () => Piece(piece, prefab.GetComponent<WearNTear>()));

            Part("resource", () => Resource(prefab));
            if (piece != null && !piece.enabled) Part("build cost", () => MadeBuildable(piece));
            Part("station", () => Station(prefab));

            // A spawner finds its network view among its parents (SpawnArea.Awake), so it may sit
            // on a part of the prefab; the first found is told.
            var spawner = prefab.GetComponentInChildren<SpawnArea>(true);
            if (spawner != null) Part("spawner", () => Spawner(spawner));

            var projectile = prefab.GetComponent<Projectile>();
            if (projectile != null) Part("projectile", () => Flight(projectile));

            // What a chest is filled with when the game first opens it (Container.AddDefaultItems);
            // one players build has nothing.
            var container = prefab.GetComponent<Container>();
            if (container != null) Part("chest", () => Drops(container.m_defaultItems, null, holds: true));

            // The prefab's own numbers are shown; a world that changes them says by how much.
            Part("world settings", () =>
            {
                var game = Game.instance;
                var enemy = character != null && !(character is Player);
                var note = WorldWords.Note(Game.m_worldLevel, game != null ? game.m_worldLevelEnemyHPMultiplier : 1f, Game.m_resourceRate, enemy, _drops);
                if (note != null) Add("In this world", char.ToUpperInvariant(note[0]) + note.Substring(1));
            });
        }

        /// <summary>The entry told of, for what the list already says of it (its group).</summary>
        private Entry _entry;

        /// <summary>Whether any drops were told, which a world's resource rate scales.</summary>
        private bool _drops;

        // ----- Helpers -----

        private static string ItemName(GameObject item)
        {
            var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            var name = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
            return name.Length > 0 ? name : item != null ? item.name : "";
        }

        private static Sprite Icon(GameObject item)
        {
            var icons = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons : null;
            return icons != null && icons.Length > 0 ? icons[0] : null;
        }

        /// <summary>A value as text, or null for a choice the game has no name for (a mod's own numbered one).</summary>
        private static string Shown(object value)
        {
            if (value is float f) return Number(f);
            if (value is bool b) return b ? "yes" : "no";
            if (value is Enum e) return Word(e)?.ToLowerInvariant();
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A choice by its name, "OneHandedWeapon" as "One handed weapon". Null when the value has
        /// no name, which is how a mod's own categories and factions show up, as bare numbers.
        /// Flags that combine several names are joined.
        /// </summary>
        private static string Word(Enum value)
        {
            var text = value.ToString();
            if (text.Length > 0 && (char.IsDigit(text[0]) || text[0] == '-')) return null;
            return string.Join(", ", text.Split(new[] { ", " }, StringSplitOptions.None).Select(Naming.FieldLabel));
        }

        /// <summary>The status effect each kind of damage puts on what it hits, as <c>Character</c> adds them.</summary>
        private static readonly (string Damage, string Effect)[] DamageEffects =
        {
            ("fire", "Burning"), ("frost", "Frost"), ("lightning", "Lightning"), ("poison", "Poison"), ("spirit", "Spirit"),
        };

        private static string EffectName(StatusEffect effect)
        {
            var name = CatalogBuilder.Localize(effect.m_name);
            return name.Length > 0 ? name : effect.name;
        }

        private static string Number(float value)
        {
            return Naming.Number(value);
        }

        private static string Percent(float value)
        {
            var percent = Mathf.RoundToInt(value * 100f);
            return (percent > 0 ? "+" : "") + percent + "%";
        }

        private static string Minutes(float seconds)
        {
            return Naming.Duration(seconds);
        }
    }
}
