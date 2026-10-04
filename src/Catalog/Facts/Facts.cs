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
        static Facts()
        {
            WorldCaches.Register(nameof(Facts), Forget);

            // What reads the world says when an entry's details change (Learned).
            Learned.Changed += entry =>
            {
                if (entry == null) Forget();
                else Forget(entry);
            };
        }

        /// <summary>A row of items with amounts, such as a recipe or a creature's drops.</summary>
        public sealed class Row
        {
            public string Title = "";

            /// <summary>The prefab the title names, such as the crafting station, so it can be gone to.</summary>
            public string TitleLink;

            /// <summary>Why Scry is not sure of the row, or null when it is (<see cref="UnsureWords"/>).</summary>
            public string Unsure;

            /// <summary>A grid in place of chips: every damage type and the share of it taken (<see cref="ResistWords"/>).</summary>
            public List<ResistCell> Cells;
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

        public bool IsEmpty => Description.Length == 0 && Pairs.Count == 0 && Rows.Count == 0 && Where.Count == 0 && UseRows.Count == 0 && Hooks.Count == 0;

        /// <summary>Values that name something in the catalog, by their label: a prefab name, or "se:" and a status effect's.</summary>
        public readonly Dictionary<string, string> Links = new Dictionary<string, string>();

        private void Add(string label, string value, string link = null)
        {
            if (string.IsNullOrEmpty(value)) return;
            Pairs.Add(new KeyValuePair<string, string>(label, value));
            if (link != null) Links[label] = link;
        }

        /// <summary>The lines a kind's words give (<see cref="FactPair"/>), in their order, each with its link and why Scry is not sure of it.</summary>
        private void AddAll(IEnumerable<FactPair> pairs)
        {
            foreach (var pair in pairs)
            {
                if (pair.Unsure != null) AddUnsure(pair.Label, pair.Value, pair.Unsure, pair.Link);
                else Add(pair.Label, pair.Value, pair.Link);
            }
        }

        /// <summary>A row of biomes, each going to its page; all of them told in words.</summary>
        private void BiomeRow(string title, Heightmap.Biome biomes)
        {
            var keys = Knowledge.BiomeKeys(biomes);
            if (keys.Length == 0) return;
            if (keys.Length >= Knowledge.EveryBiomeKey.Length)
            {
                Add(title, "every biome");
                return;
            }
            var row = new Row { Title = title };
            foreach (var key in keys)
            {
                var page = EntryOf(EntryKeys.For(Kind.Biome, key));
                row.Items.Add(page != null ? EntryChip(page) : new Ingredient { Name = Knowledge.BiomeName(key), Amount = "" });
            }
            Rows.Add(row);
        }

        /// <summary>The entry of a key, or null when the catalog has none.</summary>
        private static Entry EntryOf(string key) => WorldCatalog.Find(key);

        /// <summary>Why Scry is not sure of a pair, by its label (<see cref="UnsureWords"/>).</summary>
        public readonly Dictionary<string, string> Unsure = new Dictionary<string, string>();

        /// <summary>A pair Scry is not sure of, with why.</summary>
        private void AddUnsure(string label, string value, string why, string link = null)
        {
            if (string.IsNullOrEmpty(value)) return;
            Add(label, value, link);
            Unsure[label] = why;
        }

        private static readonly Dictionary<Entry, Facts> Cache = new Dictionary<Entry, Facts>();

        public static Facts For(Entry entry)
        {
            if (entry == null) return new Facts();
            // A creature's or item's facts tell what was seen dropping in play, which grows as you play.
            if (Cache.TryGetValue(entry, out var known) && (known._seenVersion == DropWatch.Version || (entry.Kind != Kind.Creature && entry.Kind != Kind.Item && entry.Kind != Kind.Mod))) return known;

            var facts = new Facts { _seenVersion = DropWatch.Version };
            if (entry.Source is StatusEffect effect)
            {
                facts.Part("status effect", () => facts.StatusEffect(effect));
            }
            else if (entry.Source is RandomEvent raid)
            {
                facts.Part("raid", () => facts.Raid(raid));
            }
            else if (entry.Source is BiomeSource biome)
            {
                facts._entry = entry;
                facts.Part("biome", () => facts.Biome(biome));
            }
            else if (entry.Source is ModSource mod)
            {
                facts._entry = entry;
                facts.Part("mod", () => facts.Mod(mod));
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
                    // The surest first; among equally sure ones, what stands in the biome players
                    // reach first: where it lives or grows by its own biomes, what gives it by the giver's.
                    var own = ContentOrder.Earliest(Knowledge.Biomes(entry.Name));
                    var lines = Knowledge.WhereLines(entry.Name).Select(l => (Line: l, Biome: own))
                        .Concat(Knowledge.SourceLines(entry.Name).Select(l => (Line: l, Biome: l.Prefab != null ? ContentOrder.Earliest(Knowledge.Biomes(l.Prefab)) : int.MaxValue)));
                    facts.Where.AddRange(ContentOrder.SurestFirst(lines, l => l.Line.Chance, l => l.Biome).Select(l => l.Line));
                    foreach (var (creature, drop, kills) in DropWatch.Seen.Sources(entry.Name))
                    {
                        facts.Where.Add(new Source(SeenWords.Line(AnyName(GamePrefabs.Item(creature), creature), drop, kills), creature, UnsureWords.Seen));
                    }

                    // An item nothing makes, drops, sells or spawns here comes from somewhere Scry cannot
                    // see, unless the locations, once read, show where it is found. Something that
                    // spawns it (an Asksvin its egg) is told under LINKED.
                    if (entry.Kind == Kind.Item && facts.Where.Count == 0 && entry.FoundIn.Length == 0 && !facts.Rows.Any(r => r.Title.StartsWith("Made", StringComparison.Ordinal))
                        && !entry.Links.Any(l => l.Group == Relations.SpawnedBy))
                    {
                        facts.Where.Add(new Source("Nothing loaded makes, drops or sells it. It may come from a location, a dungeon, an event or a mod.", null, UnsureWords.NothingFound));
                    }

                    // A creature nothing spawns where Scry can see, the same.
                    if (entry.Kind == Kind.Creature && facts.Where.Count == 0 && entry.FoundIn.Length == 0 && !Knowledge.IsPlacedByWorld(entry.Name)
                        && !entry.Links.Any(l => l.Group == Relations.SpawnedBy) && !Knowledge.Summons().Any(s => s.Boss == entry.Name) && !(prefab.GetComponent<Character>() is Player))
                    {
                        facts.Where.Add(new Source("Nothing loaded spawns it where Scry can see. It may come from a location, an event, another creature or a mod.", null, UnsureWords.NowhereFound));
                    }
                });
                facts.Part("uses", () => facts.Uses(entry.Name));
            }
            facts.TellMissing();

            Cache[entry] = facts;
            return facts;
        }

        /// <summary>What had been seen dropping in play when these facts were told (<see cref="DropWatch.Version"/>).</summary>
        private int _seenVersion;

        /// <summary>The parts of these facts that could not be read, told at their end.</summary>
        private readonly List<string> _missing = new List<string>();

        /// <summary>
        /// Reads one part of the facts on its own. One that fails, on a mod's odd prefab or because
        /// an update changed what it reads, is told once in the log and named at the end of the
        /// facts, and the other parts still show.
        /// </summary>
        private void Part(string part, Action read)
        {
            if (!Guard.Run(Feature.Details(part), FactWords.Part(part), read, Settings.LogPreviews ? FactWords.PartTiming(part) : null) && !_missing.Contains(part)) _missing.Add(part);
        }

        private void TellMissing()
        {
            if (_missing.Count == 0) return;
            Add("Not shown", FactWords.NotShown(_missing));
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
            var shared = drop.OrNull()?.m_itemData?.m_shared;
            if (shared != null) Part("item", () => Item(prefab, shared));

            var character = prefab.GetComponent<Character>();
            if (character != null) Part("creature", () => Creature(prefab, character));

            // The tools that build it and the tab it is on in each, told for every piece, one
            // whose Piece sits on a part of it too; and what a tool builds.
            if (_entry != null && _entry.Kind == Kind.Piece) Part("built with", () => BuiltWith(prefab.name));
            if (Knowledge.Tools.IsTool(prefab.name)) Part("builds", () => Builds(prefab.name));

            var piece = prefab.GetComponent<Piece>();
            if (piece != null && piece.enabled) Part("piece", () => Piece(piece, prefab.GetComponent<WearNTear>()));

            Part("resource", () => Resource(prefab));
            if (Knowledge.GivesLoot(prefab)) Hooked(HookedRule.Loot);
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

            // What a fish bites on, and what a bait catches.
            var fish = prefab.GetComponent<Fish>();
            if (fish != null && fish.m_baits != null) Part("fishing", () => ChanceRow("Bites on, when it reaches the hook", fish.m_baits.Where(b => b?.m_bait != null).Select(b => (b.m_bait.gameObject.name, b.m_chance))));
            Part("bait", () => ChanceRow("Catches, when one reaches the hook", Knowledge.Catches(prefab.name)));

            // What a door is opened with, and what a key opens.
            var door = prefab.GetComponent<Door>();
            if (door != null && door.m_keyItem != null) Add("Opened with", BuildWords.OpenedWith(ItemName(door.m_keyItem.gameObject), door.m_consumeKey), door.m_keyItem.gameObject.name);
            var opens = Knowledge.Opens(prefab.name);
            if (opens.Count > 0)
            {
                var row = new Row { Title = "Opens" };
                foreach (var each in opens) row.Items.Add(Chip(each, ""));
                Rows.Add(row);
            }

            // The Forsaken power a trophy gives on its boss stone.
            var (power, stone) = Knowledge.PowerOf(prefab.name);
            var powerEffect = power != null && ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(power.GetStableHashCode()) : null;
            if (powerEffect != null) Add("On its boss stone", ItemWords.Gives(EffectName(powerEffect)), EntryKeys.For(Kind.StatusEffect, power));

            Part("machines", () => Machines(prefab));

            // What its areas do: warmth, a base, no monsters and the rest (EffectArea).
            Part("areas", () =>
            {
                foreach (var area in prefab.GetComponentsInChildren<EffectArea>(true))
                {
                    var sphere = area.GetComponent<SphereCollider>();
                    var scale = area.transform.lossyScale;
                    var radius = sphere != null ? sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) : 0f;
                    foreach (var (label, text) in AreaWords.Lines((int)area.m_type, radius)) Add(label, text);
                    var given = !string.IsNullOrEmpty(area.m_statusEffect) && ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(area.m_statusEffect.GetStableHashCode()) : null;
                    if (given != null) Add("Gives those in it", EffectName(given), EntryKeys.For(Kind.StatusEffect, given.name));
                }
            });

            // How much it holds: a chest's own container, or a cart's or ship's on a part of it.
            var storage = container != null ? container : prefab.GetComponentInChildren<Container>(true);
            if (storage != null && character == null)
            {
                Add("Slots", ContainerWords.Slots(storage.m_width, storage.m_height));
                Hooked(HookedRule.Storage);
            }

            // The prefab's own numbers are shown; a world that changes them says by how much.
            Part("world settings", () =>
            {
                var game = Game.instance;
                var enemy = character != null && !(character is Player);
                var note = WorldWords.Note(Game.m_worldLevel, game != null ? game.m_worldLevelEnemyHPMultiplier : 1f, Game.m_resourceRate, enemy, _drops);
                Add("In this world", Naming.Capital(note));
            });
        }

        /// <summary>The entry told of, for what the list already says of it (its group).</summary>
        private Entry _entry;

        /// <summary>Whether any drops were told, which a world's resource rate scales.</summary>
        private bool _drops;

        // ----- Helpers -----

        /// <summary>A row of prefabs each with its chance, as a share.</summary>
        private void ChanceRow(string title, IEnumerable<(string Prefab, float Chance)> prefabs)
        {
            var row = new Row { Title = title };
            foreach (var (name, chance) in prefabs) row.Items.Add(Chip(name, DropWords.Share(chance)));
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>
        /// The rules mods hook into on this page, with the mods, told in one soft line after
        /// everything else, who and what on hover (<see cref="ModHookWords.Line"/>).
        /// </summary>
        public readonly List<(HookedRule Rule, IReadOnlyList<string> Mods)> Hooks = new List<(HookedRule, IReadOnlyList<string>)>();

        /// <summary>The note naming the mods that hook into a rule told here, when any do (<see cref="ModHooks"/>).</summary>
        private void Hooked(HookedRule rule)
        {
            var mods = ModHooks.Mods(rule);
            if (mods.Count > 0 && !Hooks.Exists(h => h.Rule == rule)) Hooks.Add((rule, mods));
        }

        private static string ItemName(GameObject item)
        {
            var shared = item != null ? item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared : null;
            var name = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
            return name.Length > 0 ? name : item != null ? item.name : "";
        }

        private static Sprite Icon(GameObject item)
        {
            var icons = item != null ? item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_icons : null;
            return icons != null && icons.Length > 0 ? icons[0] : null;
        }

        private static string EffectName(StatusEffect effect)
        {
            var name = CatalogBuilder.Localize(effect.m_name);
            return name.Length > 0 ? name : effect.name;
        }

    }
}
