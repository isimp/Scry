using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for what an entry's details tell: creatures, pieces and tools, the
    /// deaths seen, gear, breeding, baits, keys and powers, building rules, machines, what a
    /// defeat lets come, biome pages, unsure marks, resistances, the standard rows, spawners,
    /// loot odds, what things are used for, and the order content lists come in.
    /// </summary>
    internal static partial class SelfTest
    {
        private static IEnumerator CreatureFacts(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll");
            if (troll == null) p.Skip("there is no troll");
            var told = Facts.For(troll);
            var head = Value(told, "Hit on the head");
            if (p.Check(head != null, "the troll tells its head as a weak spot", Pairs(told))) p.Check(head.Contains("pierce"), "a hit there is told by its own resistances", head);

            // Counted over the game's own creatures, a few a frame, as each creature's details take a while.
            var creatures = X.Catalog.Where(e => e.Kind == Kind.Creature && e.Origin == Origin.Vanilla && e.Source is GameObject).ToList();
            int alert = 0, chase = 0, weak = 0, n = 0;
            foreach (var creature in creatures)
            {
                var facts = Facts.For(creature);
                if (Tells(facts, "Turns on you")) alert++;
                if (Tells(facts, "Gives up chasing")) chase++;
                if (facts.Pairs.Any(pair => pair.Key.StartsWith("Hit on the ", StringComparison.Ordinal))) weak++;
                if (++n % 3 == 0) yield return null;
            }
            p.Note($"of {Numbers.Count(creatures.Count)} of the game's creatures, {Numbers.Count(alert)} tell when they turn on you, {Numbers.Count(chase)} how far they chase, {Numbers.Count(weak)} a weak spot");
            p.Check(alert + chase > 0, "some tell when they turn on you or how far they chase");
            p.Check(weak > 0, "some tell a weak spot");
        }

        private static IEnumerator PieceFacts(Probe p)
        {
            var bench = Pick(Kind.Piece, "piece_workbench");
            if (bench == null) p.Skip("there is no workbench");
            var told = Facts.For(bench);
            foreach (var row in new[] { "Support", "Support lost", "Rain" }) p.Check(Tells(told, row), $"the workbench tells its {row.ToLowerInvariant()}", Pairs(told));
            p.Note($"support {Value(told, "Support")}; lost {Value(told, "Support lost")}; rain {Value(told, "Rain")}");
            var stone = Pick(Kind.Piece, "stone_wall_1x1", "stone_wall_2x1", "stone_wall_4x2");
            if (stone != null)
            {
                var wall = Facts.For(stone);
                p.Note($"{stone.Name}: support {Value(wall, "Support")}; rain {Value(wall, "Rain")}");
            }
            yield break;
        }

        /// <summary>
        /// The workbench says the hammer builds it and on which tab, the hammer lists it there,
        /// and every tool, mods' too, lists what it builds.
        /// </summary>
        private static IEnumerator ToolsAndPieces(Probe p)
        {
            var bench = Pick(Kind.Piece, "piece_workbench");
            var hammer = Pick(Kind.Item, "Hammer");
            if (bench == null || hammer == null) p.Skip("there is no workbench or hammer");
            var built = Value(Facts.For(bench), "Built with") ?? "";
            p.Check(built.StartsWith("Hammer", StringComparison.Ordinal) || built.Contains(hammer.DisplayName), "the workbench says the hammer builds it", built);
            var builds = Facts.For(hammer).Rows.Where(r => r.Title.StartsWith("Builds on its", StringComparison.Ordinal)).ToList();
            p.Check(builds.Any(r => r.Items.Any(i => i.Prefab == "piece_workbench")), "the hammer lists the workbench among what it builds", string.Join("; ", builds.Select(r => r.Title)));

            var tools = X.Catalog.Where(e => e.Kind == Kind.Item && Knowledge.Tools.IsTool(e.Name)).ToList();
            p.Note($"{Numbers.Count(tools.Count)} build tools: " + string.Join(", ", tools.Select(t => $"{t.Name} ({Numbers.Count(Knowledge.Tools.PiecesOf(t.Name).Sum(tab => tab.Pieces.Count))} pieces{(t.Origin == Origin.Vanilla ? "" : ", " + t.ModName)})")));
            var empty = tools.Where(t => !Facts.For(t).Rows.Any(r => r.Title.StartsWith("Builds on its", StringComparison.Ordinal))).Select(t => t.Name).ToList();
            p.Check(empty.Count == 0, "every tool lists what it builds", string.Join(", ", empty));
            var pieces = X.Catalog.Where(e => e.Kind == Kind.Piece).ToList();
            var withTool = pieces.Count(e => Knowledge.Tools.ToolsOf(e.Name).Count > 0);
            p.Note($"{Numbers.Count(withTool)} of {Numbers.Count(pieces.Count)} pieces are built with a tool; the rest are in no build menu");
            yield break;
        }

        /// <summary>
        /// The hooks watching the loot of deaths are in place, and what was seen so far is told:
        /// a creature seen dying shows its row, and each item it dropped names it. Nothing is
        /// killed for the test, so what it checks is what earlier play left.
        /// </summary>
        private static IEnumerator DropsWatched(Probe p)
        {
            foreach (var (type, name) in new[] { (typeof(CharacterDrop), "OnDeath"), (typeof(Ragdoll), "SpawnLoot"), (typeof(Ragdoll), "Setup"), (typeof(ItemDrop), "Awake") })
            {
                var method = HarmonyLib.AccessTools.DeclaredMethod(type, name);
                p.Check(method != null && HarmonyLib.Harmony.GetPatchInfo(method)?.Owners.Contains(Plugin.Guid) == true, $"{type.Name}.{name} is watched");
            }
            var creatures = X.Catalog.Where(e => e.Kind == Kind.Creature && DropWatch.Seen.Kills(e.Name) > 0).ToList();
            p.Note($"{Numbers.Count(creatures.Count)} kinds of creature seen dying in play, {Numbers.Count(creatures.Sum(c => DropWatch.Seen.Kills(c.Name)))} kills: " + string.Join(", ", creatures.Take(10).Select(c => $"{c.Name} {Numbers.Count(DropWatch.Seen.Kills(c.Name))}")));
            var seen = creatures.FirstOrDefault(c => DropWatch.Seen.Of(c.Name).Count > 0);
            if (seen == null)
            {
                p.Note("nothing seen dropping yet; kill something and run the test again to see it told");
                yield break;
            }
            var told = Facts.For(seen);
            p.Check(told.Rows.Any(r => r.Title == SeenWords.Title(DropWatch.Seen.Kills(seen.Name))), $"{seen.Name} shows what it was seen to drop");
            var item = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == DropWatch.Seen.Of(seen.Name)[0].Item);
            if (item != null) p.Check(Facts.For(item).Where.Any(l => l.Prefab == seen.Name && l.Text.StartsWith("Seen dropped by", StringComparison.Ordinal)), $"{item.Name} names {seen.Name} as seen dropping it");
        }

        /// <summary>
        /// For each kind of gear fact, the first item that has it shows it: armour's resistances
        /// while worn, a shield's while blocking, what gear changes while worn (heat, stamina,
        /// eitr, adrenaline), what full adrenaline gives, and a weapon's second attack.
        /// </summary>
        private static IEnumerator GearFacts(Probe p)
        {
            var items = X.Catalog.Where(e => e.Kind == Kind.Item && e.Source is GameObject).Select(e => (Entry: e, Shared: ((GameObject)e.Source).GetComponent<ItemDrop>()?.m_itemData?.m_shared))
                .Where(i => i.Shared != null).OrderBy(i => i.Entry.Name, StringComparer.Ordinal).ToList();
            bool Worn(ItemDrop.ItemData.ItemType t) => t == ItemDrop.ItemData.ItemType.Chest || t == ItemDrop.ItemData.ItemType.Legs || t == ItemDrop.ItemData.ItemType.Helmet || t == ItemDrop.ItemData.ItemType.Shoulder;
            var cases = new (string What, Func<ItemDrop.ItemData.SharedData, bool> Has, string Label)[]
            {
                ("armour resisting while worn", s => Worn(s.m_itemType) && s.m_damageModifiers.Any(m => m.m_modifier != HitData.DamageModifier.Normal), "Damage it takes while worn"),
                ("a shield resisting while blocking", s => s.m_itemType == ItemDrop.ItemData.ItemType.Shield && s.m_damageModifiers.Any(m => m.m_modifier != HitData.DamageModifier.Normal), "Damage it takes while blocking"),
                ("gear against heat", s => Math.Abs(s.m_heatResistanceModifier) >= 0.005f, "Heat resistance"),
                ("gear changing run stamina", s => Math.Abs(s.m_runStaminaModifier) >= 0.005f, "Run stamina"),
                ("gear changing eitr regeneration", s => Math.Abs(s.m_eitrRegenModifier) >= 0.005f, "Eitr regeneration"),
                ("gear adding adrenaline", s => s.m_maxAdrenaline >= 0.5f, "Most adrenaline"),
                ("gear giving something at full adrenaline", s => s.m_fullAdrenalineSE != null, "At full adrenaline"),
                ("a weapon with a second attack", s => s.m_secondaryAttack != null && !string.IsNullOrEmpty(s.m_secondaryAttack.m_attackAnimation) && s.m_attack != null
                    && (s.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon || s.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon), "Secondary attack"),
            };
            foreach (var (what, has, label) in cases)
            {
                var found = items.Where(i => has(i.Shared)).ToList();
                if (found.Count == 0)
                {
                    p.Note($"no {what} in this game");
                    continue;
                }
                var (entry, _) = found[0];
                var gearFacts = Facts.For(entry);
                var value = Value(gearFacts, label) ?? (gearFacts.Rows.Any(r => r.Title == label && r.Cells != null) ? string.Join(", ", gearFacts.Rows.First(r => r.Title == label).Cells.Where(c => c.Tone != Tone.Plain).Select(c => c.Type + " " + c.Value)) : null);
                p.Check(value != null, $"{entry.Name}, {what} ({Numbers.Count(found.Count)} such), tells it under {label}", value ?? "not told");
                if (value != null) p.Note($"{entry.Name}: {label} {value}");
            }
            yield break;
        }

        /// <summary>
        /// Every creature that breeds tells how, and its young names it as where it comes from;
        /// every young one tells what it grows into; an egg tells what hatches from it and when;
        /// a creature with a saddle tells what it is ridden with and its stamina then.
        /// </summary>
        private static IEnumerator BreedingFacts(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            var breeders = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Procreation>() != null && Of(e).GetComponent<Tameable>() != null).ToList();
            p.Note($"{Numbers.Count(breeders.Count)} creatures breed: {string.Join(", ", breeders.Take(10).Select(e => e.Name))}");
            var untold = new List<string>();
            var unborn = new List<string>();
            foreach (var breeder in breeders)
            {
                var told = Facts.For(breeder);
                if (Value(told, "Breeds when") == null || Value(told, "Love") == null || !told.Rows.Any(r => r.Title.StartsWith("Has young", StringComparison.Ordinal))) untold.Add(breeder.Name);
                var young = Of(breeder).GetComponent<Procreation>().m_offspring;
                if (young == null) continue;
                var lines = Knowledge.WhereLines(young.name).Concat(Knowledge.SourceLines(young.name));
                if (!lines.Any(l => l.Prefab == breeder.Name && l.Text.StartsWith("Born to", StringComparison.Ordinal))) unborn.Add(young.name);
            }
            p.Check(untold.Count == 0, "every one tells how it breeds and its young", string.Join(", ", untold.Take(5)));
            p.Check(unborn.Count == 0, "every one's young names it as where it comes from", string.Join(", ", unborn.Take(5)));
            if (breeders.Count > 0) p.Note($"{breeders[0].Name}: {Pairs(Facts.For(breeders[0]))}");

            var growing = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Growup>() != null).ToList();
            var ungrown = growing.Where(e => Value(Facts.For(e), "Grows up in") == null || !Facts.For(e).Rows.Any(r => r.Title.StartsWith("Grows into", StringComparison.Ordinal))).Select(e => e.Name).ToList();
            p.Check(ungrown.Count == 0, $"every young one ({Numbers.Count(growing.Count)}) tells what it grows into and when", string.Join(", ", ungrown.Take(5)));

            var egg = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && Of(e)?.GetComponent<EggGrow>()?.m_grownPrefab != null);
            if (egg == null) p.Note("no egg hatches in this game");
            else
            {
                var hatch = Of(egg).GetComponent<EggGrow>().m_grownPrefab.name;
                p.Check(Value(Facts.For(egg), "Hatches into") != null && Knowledge.WhereLines(hatch).Any(l => l.Prefab == egg.Name), $"{egg.Name} tells what hatches from it, and {hatch} that it hatches from it", Value(Facts.For(egg), "Hatches when") ?? "not told");
            }

            var ridden = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Tameable>()?.m_saddle != null);
            if (ridden == null) p.Note("no creature is ridden in this game");
            else p.Check(Value(Facts.For(ridden), "Stamina when ridden") != null, $"{ridden.Name} tells its stamina when ridden", Pairs(Facts.For(ridden)));
            yield break;
        }

        /// <summary>
        /// A fish names the baits it bites on and a bait the fish it catches; a locked door its
        /// key and the key the door; a boss its Forsaken power and its trophy the power it gives
        /// on its boss stone.
        /// </summary>
        private static IEnumerator BaitsKeysPowers(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            var fish = X.Catalog.Where(e => Of(e)?.GetComponent<Fish>()?.m_baits?.Any(b => b?.m_bait != null) == true).ToList();
            p.Note($"{Numbers.Count(fish.Count)} fish with baits");
            if (fish.Count > 0)
            {
                var one = fish[0];
                p.Check(Facts.For(one).Rows.Any(r => r.Title.StartsWith("Bites on", StringComparison.Ordinal)), $"{one.Name} names its baits");
                var bait = Of(one).GetComponent<Fish>().m_baits.First(b => b?.m_bait != null).m_bait.gameObject.name;
                var baitEntry = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == bait);
                if (baitEntry != null) p.Check(Facts.For(baitEntry).Rows.Any(r => r.Title.StartsWith("Catches", StringComparison.Ordinal) && r.Items.Any(i => i.Prefab == one.Name)), $"{bait} names {one.Name} among what it catches");
            }

            var doors = X.Catalog.Where(e => Of(e)?.GetComponent<Door>()?.m_keyItem != null).ToList();
            p.Note($"{Numbers.Count(doors.Count)} locked doors: {string.Join(", ", doors.Take(6).Select(e => e.Name))}");
            if (doors.Count > 0)
            {
                var door = doors[0];
                var key = Of(door).GetComponent<Door>().m_keyItem.gameObject.name;
                p.Check(Value(Facts.For(door), "Opened with") != null, $"{door.Name} names its key");
                var keyEntry = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == key);
                if (keyEntry != null) p.Check(Facts.For(keyEntry).Rows.Any(r => r.Title == "Opens" && r.Items.Any(i => i.Prefab == door.Name)), $"{key} names {door.Name} among what it opens");
            }

            var bosses = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<CharacterDrop>()?.m_drops?.Any(d => d?.m_prefab != null && Knowledge.PowerOf(d.m_prefab.name).Power != null) == true).ToList();
            p.Note($"{Numbers.Count(bosses.Count)} creatures drop a trophy with a Forsaken power: {string.Join(", ", bosses.Take(8).Select(e => e.Name))}");
            var unpowered = bosses.Where(b => Value(Facts.For(b), "Forsaken power") == null).Select(b => b.Name).ToList();
            p.Check(bosses.Count > 0 && unpowered.Count == 0, "every one names its power", string.Join(", ", unpowered));
            var trophy = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && Knowledge.PowerOf(e.Name).Power != null);
            if (trophy != null) p.Check(Value(Facts.For(trophy), "On its boss stone") != null, $"{trophy.Name} names the power it gives on its boss stone");
            yield break;
        }

        /// <summary>For each building rule, the first piece having it tells it.</summary>
        private static IEnumerator BuildingRules(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            var pieces = X.Catalog.Where(e => Of(e) != null && Of(e).GetComponent<Piece>() != null).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            var cases = new (string What, Func<GameObject, bool> Has, string Label)[]
            {
                ("a piece with placement rules", g => g.GetComponent<Piece>() is Piece piece && (piece.m_groundOnly || piece.m_cultivatedGroundOnly || piece.m_noInWater || piece.m_notOnWood || piece.m_onlyInBiome != 0), "Placed"),
                ("a station", g => g.GetComponent<CraftingStation>() != null, "Building reach"),
                ("a station's upgrade", g => g.GetComponent<StationExtension>()?.m_craftingStation != null, "Upgrades"),
                ("a bed", g => g.GetComponent<Bed>() != null, "Sleeping in it"),
                ("a warm piece", g => g.GetComponentsInChildren<EffectArea>(true).Any(a => (a.m_type & EffectArea.Type.Heat) != 0), "Warmth"),
                ("a base piece", g => g.GetComponentsInChildren<EffectArea>(true).Any(a => (a.m_type & EffectArea.Type.PlayerBase) != 0), "A base"),
            };
            foreach (var (what, has, label) in cases)
            {
                var found = pieces.Where(e => has(Of(e))).ToList();
                if (found.Count == 0)
                {
                    p.Note($"no {what} in this game");
                    continue;
                }
                var value = Value(Facts.For(found[0]), label);
                p.Check(value != null, $"{found[0].Name}, {what} ({Numbers.Count(found.Count)} such), tells it under {label}", value ?? "not told");
                if (value != null) p.Note($"{found[0].Name}: {label} {value}");
            }
            yield break;
        }

        /// <summary>Each kind of machine, the first of it in the catalog, tells what it does.</summary>
        private static IEnumerator MachineFacts(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            var all = X.Catalog.Where(e => Of(e) != null).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            var cases = new (string What, Func<GameObject, bool> Has, string Label)[]
            {
                ("a ballista", g => g.GetComponent<Turret>() != null, "Shoots"),
                ("a trap", g => g.GetComponent<Trap>() != null, "Springs on"),
                ("a ship", g => g.GetComponent<Ship>() != null, "Ashlands seas"),
                ("a cart", g => g.GetComponent<Vagon>() != null, "Weighs"),
                ("a catapult", g => g.GetComponent<Catapult>() != null, "Loads"),
            };
            foreach (var (what, has, label) in cases)
            {
                var found = all.Where(e => has(Of(e))).ToList();
                if (found.Count == 0)
                {
                    p.Note($"no {what} in this game");
                    continue;
                }
                var told = Facts.For(found[0]);
                var value = Value(told, label);
                p.Check(value != null, $"{found[0].Name}, {what} ({Numbers.Count(found.Count)} such), tells it under {label}", value ?? "not told");
                p.Note($"{found[0].Name}: {Pairs(told)}");
            }
            yield break;
        }

        /// <summary>
        /// Every creature whose defeat sets a world key that something waits for tells it, and
        /// every raid the game's own list waits on that key for is among its raids.
        /// </summary>
        private static IEnumerator AfterDefeat(Probe p)
        {
            var keyed = X.Catalog.Where(e => e.Kind == Kind.Creature && e.Source is GameObject g && g.GetComponent<Character>() is Character c && !string.IsNullOrEmpty(c.m_defeatSetGlobalKey))
                .Select(e => (Entry: e, Key: ((GameObject)e.Source).GetComponent<Character>().m_defeatSetGlobalKey)).ToList();
            p.Note($"{Numbers.Count(keyed.Count)} creatures set a world key when they fall: {string.Join(", ", keyed.Take(12).Select(k => $"{k.Entry.Name} ({k.Key})"))}");
            var silent = keyed.Where(k => Knowledge.Unlocks.Any(k.Key) && !Facts.For(k.Entry).Rows.Any(r => r.Title.StartsWith("After it falls", StringComparison.Ordinal))).Select(k => k.Entry.Name).ToList();
            p.Check(silent.Count == 0, "each one something waits for tells what follows", string.Join(", ", silent.Take(5)));
            foreach (var (entry, key) in keyed.Take(3))
            {
                p.Note($"{entry.Name}: " + string.Join("; ", Facts.For(entry).Rows.Where(r => r.Title.StartsWith("After it falls", StringComparison.Ordinal)).Select(r => $"{r.Title}: {string.Join(", ", r.Items.Take(6).Select(i => i.Name))}")));
            }

            var byPlayer = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.PlayerEvents);
            var missed = new List<string>();
            if (!byPlayer && RandEventSystem.instance != null)
            {
                foreach (var raid in RandEventSystem.instance.m_events)
                {
                    // A raid the game has switched off never comes, so nothing follows it.
                    if (raid?.m_requiredGlobalKeys == null || !raid.m_enabled || raid.m_spawn == null) continue;
                    foreach (var key in raid.m_requiredGlobalKeys)
                    {
                        if (!Knowledge.Unlocks.Of(key, Unlock.RaidStarts).Contains(EntryKeys.For(Kind.Raid, raid.m_name))) missed.Add($"{raid.m_name} ({key})");
                    }
                }
            }
            p.Check(missed.Count == 0, "every raid waiting on a key is told under it", string.Join(", ", missed.Take(5)));
            yield break;
        }

        private static IEnumerator BiomePages(Probe p)
        {
            var biomes = X.Catalog.Where(e => e.Kind == Kind.Biome).ToList();
            p.Note($"{Numbers.Count(biomes.Count)} biomes: {string.Join(", ", biomes.Select(b => b.DisplayName))}");
            var bare = biomes.Where(b => !Facts.For(b).Pairs.Any(pair => pair.Key.EndsWith("of the time", StringComparison.Ordinal))).Select(b => b.Name).ToList();
            p.Check(biomes.Count > 0 && bare.Count == 0, "each tells its weathers", string.Join(", ", bare));
            var empty = biomes.Where(b => b.Name != "Ocean" && !Facts.For(b).Rows.Any(r => r.Title.StartsWith("Lives here", StringComparison.Ordinal))).Select(b => b.Name).ToList();
            p.Note(empty.Count == 0 ? "each but the ocean tells what lives there" : $"no creature told living in {string.Join(", ", empty)}");
            // What spans most biomes, and what of other kinds claims a biome at all, kind by kind, for a look at what is mixed in.
            var wide = X.Catalog.Where(e => e.Kind != Kind.Biome && e.Biomes.Length >= 6).ToList();
            foreach (var kind in wide.GroupBy(e => e.Kind)) p.Note($"{Kinds.Label(kind.Key).ToLowerInvariant()} in 6 or more biomes ({Numbers.Count(kind.Count())}): {string.Join(", ", kind.Take(12).Select(e => $"{e.Name} ({Numbers.Count(e.Biomes.Length)}{(e.ModName.Length > 0 ? ", " + e.ModName : "")})"))}");
            var others = X.Catalog.Where(e => e.Biomes.Length > 0 && e.Kind != Kind.Biome && e.Kind != Kind.Creature && e.Kind != Kind.Resource && e.Kind != Kind.Location && e.Kind != Kind.Raid).ToList();
            foreach (var kind in others.GroupBy(e => e.Kind)) p.Note($"{Kinds.Label(kind.Key).ToLowerInvariant()} with biomes ({Numbers.Count(kind.Count())}): {string.Join(", ", kind.Take(12).Select(e => $"{e.Name} ({string.Join("/", e.Biomes)})"))}");
            var named = X.Catalog.Where(e => e.Kind != Kind.Biome).SelectMany(e => e.Biomes).Distinct().ToList();
            var pageless = named.Where(b => !X.Catalog.Any(e => e.Key == EntryKeys.For(Kind.Biome, b))).ToList();
            p.Check(pageless.Count == 0, "every biome an entry names has a page", string.Join(", ", pageless));
            if (biomes.Count > 0) p.Note($"{biomes[0].Name}: {Pairs(Facts.For(biomes[0]))}");

            var withMusic = biomes.FirstOrDefault(b => b.Source is BiomeSource s && BiomeWords.Music(s.Morning, s.Evening, s.Day, s.Night).Count > 0);
            if (withMusic == null)
            {
                p.Note("no biome has music of its own");
                yield break;
            }
            var said = Previews.PlacesMusic(withMusic);
            p.Check(said != null && said.StartsWith("Playing", StringComparison.Ordinal) && MusicPreview.PlayingFor == withMusic, $"{withMusic.Name}'s music plays", said);
            yield return null;
            said = Previews.PlacesMusic(withMusic);
            p.Check(MusicPreview.PlayingFor != withMusic, $"{withMusic.Name}'s music stops when asked again", said);
        }

        /// <summary>
        /// A creature's health and drops are sure, a mods' note on them is not; a mod's things
        /// matched by clues are told apart from those its registry names; a line where Scry found
        /// nothing is marked; every mark has its reason.
        /// </summary>
        private static IEnumerator UnsureMarks(Probe p)
        {
            var creature = Pick(Kind.Creature, "Greydwarf", "Boar") ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature);
            if (creature == null) p.Skip("there is no creature");
            var told = Facts.For(creature);
            p.Check(!told.Unsure.ContainsKey("Health") && told.Pairs.Any(pair => pair.Key == "Health"), $"{creature.Name}'s health is told as sure");
            if (ModHooks.Mods(HookedRule.Drops).Count > 0)
            {
                p.Check(ModHookWords.Line(told.Hooks) != null && HookNote(told, HookedRule.Drops) != null, $"{creature.Name} tells in one line after the rest that mods hook into its drops", ModHookWords.Line(told.Hooks) ?? "no line");
            }
            p.Check(told.Unsure.Values.All(why => !string.IsNullOrEmpty(why)), "every mark says why");

            var named = X.Catalog.Where(e => e.ModName.Length > 0 && e.Kind != Kind.Mod).ToList();
            var guessed = named.Where(e => !UnsureWords.IsSureClue(e.ModClue)).ToList();
            p.Note($"{Numbers.Count(named.Count)} entries named for their mod: {Numbers.Count(named.Count - guessed.Count)} by the mod's own word, {Numbers.Count(guessed.Count)} by clues ({string.Join(", ", guessed.GroupBy(e => e.ModClue).Select(g => $"{g.Key}: {Numbers.Count(g.Count())}"))})");
            if (guessed.Count > 0)
            {
                var one = guessed[0];
                var page = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Mod && e.Name == one.ModName);
                if (page != null) p.Check(Facts.For(page).Rows.Any(r => r.Unsure != null && r.Items.Any(i => i.Prefab == one.Key)), $"{one.Name} is under its mod's clue-matched row, marked");
            }

            var nowhere = X.Catalog.FirstOrDefault(e => (e.Kind == Kind.Item || e.Kind == Kind.Creature) && Facts.For(e).Where.Any(l => l.Unsure == UnsureWords.NothingFound || l.Unsure == UnsureWords.NowhereFound));
            p.Note(nowhere == null ? "no item or creature without a source" : $"{nowhere.Name} says where Scry found nothing, marked");
            yield break;
        }

        /// <summary>
        /// Every creature, every piece that can be damaged and every rock or tree tells its
        /// resistances as the same grid of ten damage types; one drawn on the page counts.
        /// </summary>
        private static IEnumerator ResistanceGrids(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            bool HasGrid(Entry e) => Facts.For(e).Rows.Any(r => r.Cells != null && r.Cells.Count == ResistWords.Types.Length);
            var creatures = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Character>() != null && !(Of(e).GetComponent<Character>() is Player)).ToList();
            var gridless = new List<string>();
            yield return Budgeted(creatures, e => { if (!HasGrid(e)) gridless.Add(e.Name); }, 8);
            p.Check(creatures.Count > 0 && gridless.Count == 0, $"every creature ({Numbers.Count(creatures.Count)}) has the grid", string.Join(", ", gridless.Take(5)));
            var pieces = Spread(X.Catalog.Where(e => e.Kind == Kind.Piece && Of(e)?.GetComponent<WearNTear>() != null && Of(e).GetComponent<Piece>()?.enabled == true).OrderBy(e => e.Name, StringComparer.Ordinal).ToList(), 40);
            var pieceless = pieces.Where(e => !HasGrid(e)).Select(e => e.Name).ToList();
            p.Check(pieceless.Count == 0, $"a spread of {Numbers.Count(pieces.Count)} pieces each have it", string.Join(", ", pieceless.Take(5)));
            var rocks = Spread(X.Catalog.Where(e => e.Kind == Kind.Resource && (Of(e)?.GetComponent<Destructible>() != null || Of(e)?.GetComponent<MineRock5>() != null)).OrderBy(e => e.Name, StringComparer.Ordinal).ToList(), 20);
            p.Note($"{Numbers.Count(rocks.Count(HasGrid))} of a spread of {Numbers.Count(rocks.Count)} rocks and trees have it");

            var shown = creatures.FirstOrDefault(e => e.Name == "Troll") ?? creatures.FirstOrDefault();
            if (shown == null) yield break;
            var cells = Facts.For(shown).Rows.First(r => r.Cells != null).Cells;
            p.Note($"{shown.Name}: {string.Join(", ", cells.Select(c => $"{c.Type} {c.Value}"))}");
            Select(shown);
            var drawn = ScryPanel.GridsDrawn;
            yield return Until(() => ScryPanel.GridsDrawn > drawn, 3);
            p.Check(ScryPanel.GridsDrawn > drawn, $"{shown.Name}'s page draws its grid");
        }

        /// <summary>
        /// The rows a player looks for show on every page of a type, with "none" or "no" where
        /// that is the answer: a creature's attacks, weak spots, taming and drops; gear's quality,
        /// portals and wear; armour's armour, movement and set; a weapon's block and second
        /// attack; a buildable piece's cost.
        /// </summary>
        private static IEnumerator StandardRows(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            // A label may name alternatives, "Build cost|Built near", and one ending in a space or
            // colon is the start of labels ("Hit on the "): any one of them will do.
            bool Has(Facts told, string labels) => labels.Split('|').Any(label =>
                told.Pairs.Any(pair => pair.Key == label || ((label.EndsWith(" ", StringComparison.Ordinal) || label.EndsWith(":", StringComparison.Ordinal)) && pair.Key.StartsWith(label, StringComparison.Ordinal)))
                || told.Rows.Any(r => r.Title.StartsWith(label, StringComparison.Ordinal)));
            // Each kind's entries told a few a frame, so the check makes no long frame of its own.
            var checks = new List<(string What, Entry Entry, string[] Labels)>();
            var groups = new List<(string What, int Count, string[] Labels)>();
            void Every(string what, IEnumerable<Entry> entries, params string[] labels)
            {
                var list = entries.ToList();
                groups.Add((what, list.Count, labels));
                foreach (var entry in list) checks.Add((what, entry, labels));
            }

            var creatures = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Character>() is Character c && !(c is Player));
            Every("creature", creatures, "Health", "Attacks|Attack: ", "Weak spots|Hit on the ", "Tameable", "Drops");
            var shared = X.Catalog.Where(e => e.Kind == Kind.Item && Of(e)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared != null)
                .Select(e => (Entry: e, Type: Of(e).GetComponent<ItemDrop>().m_itemData.m_shared.m_itemType)).ToList();
            bool Is(ItemDrop.ItemData.ItemType t, params ItemDrop.ItemData.ItemType[] types) => types.Contains(t);
            Every("item", shared.Select(s => s.Entry), "Type", "Weight", "Portals");
            Every("piece of armour", shared.Where(s => Is(s.Type, ItemDrop.ItemData.ItemType.Helmet, ItemDrop.ItemData.ItemType.Chest, ItemDrop.ItemData.ItemType.Legs, ItemDrop.ItemData.ItemType.Shoulder)).Select(s => s.Entry),
                "Quality", "Durability", "Armour", "Movement", "Set bonus");
            Every("weapon", shared.Where(s => Is(s.Type, ItemDrop.ItemData.ItemType.OneHandedWeapon, ItemDrop.ItemData.ItemType.TwoHandedWeapon, ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft, ItemDrop.ItemData.ItemType.Bow)).Select(s => s.Entry),
                "Quality", "Durability", "Block", "Secondary attack");
            var built = X.Catalog.Where(e => e.Kind == Kind.Piece && Of(e)?.GetComponent<Piece>()?.enabled == true && Knowledge.Tools.ToolsOf(e.Name).Count > 0);
            Every("buildable piece", built, "Build cost|Built near", "Built with");

            var missing = new Dictionary<string, List<string>>();
            yield return Budgeted(checks, check =>
            {
                var told = Facts.For(check.Entry);
                foreach (var label in check.Labels)
                {
                    if (Has(told, label)) continue;
                    if (!missing.TryGetValue(check.What, out var list)) missing[check.What] = list = new List<string>();
                    list.Add($"{check.Entry.Name} {label}");
                }
            }, 8);
            foreach (var (what, count, labels) in groups)
            {
                var gaps = missing.TryGetValue(what, out var list) ? list : new List<string>();
                p.Check(gaps.Count == 0, $"every {what} ({Numbers.Count(count)}) shows {string.Join(", ", labels)}", string.Join("; ", gaps.Take(6)));
            }
        }

        private static IEnumerator SpawnerFacts(Probe p)
        {
            var nest = X.Catalog.FirstOrDefault(e => e.Name == "Spawner_GreydwarfNest")
                       ?? X.Catalog.FirstOrDefault(e => e.Source is GameObject prefab && prefab.GetComponentInChildren<SpawnArea>(true) != null);
            if (nest == null) p.Skip("there is no spawner");
            p.Note($"{nest.Name} ({nest.DisplayName})");
            var told = Facts.For(nest);
            foreach (var row in new[] { "Works", "Pace", "Keeps alive", "Puts them" }) p.Check(Tells(told, row), $"it tells {row.ToLowerInvariant()}", Pairs(told));
            p.Check(told.Pairs.Any(pair => pair.Key.StartsWith("Spawns ", StringComparison.Ordinal)), "it tells what it spawns and how often", Pairs(told));

            var area = ((GameObject)nest.Source).GetComponentInChildren<SpawnArea>(true);
            var first = area?.m_prefabs?.FirstOrDefault(d => d?.m_prefab != null)?.m_prefab;
            var creature = first != null ? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e.Name == first.name) : null;
            if (creature == null) yield break;
            var line = Facts.For(creature).Where.FirstOrDefault(s => s.Text.StartsWith("Comes from", StringComparison.Ordinal) && s.Prefab == nest.Name);
            if (p.Check(line.Text != null, $"{creature.Name} says it comes from it")) p.Check(line.Text.Contains("of the spawns") || line.Text.Contains("every spawn"), "with its share of the spawns", line.Text);
        }

        private static IEnumerator LootOdds(Probe p)
        {
            var items = X.Catalog.Where(e => e.Kind == Kind.Item && e.Origin == Origin.Vanilla).ToList();
            var withOdds = items.Where(e => Knowledge.SourceLines(e.Name).Any(s => s.Text.Contains("% a roll"))).ToList();
            p.Note($"{Numbers.Count(withOdds.Count)} of {Numbers.Count(items.Count)} of the game's items tell their share of a roll somewhere");
            p.Check(withOdds.Count > 0, "items tell their share of a roll in the tables that give them");
            var amber = items.FirstOrDefault(e => e.Name == "Amber");
            if (amber != null)
            {
                var lines = Knowledge.SourceLines("Amber").Select(s => s.Text).ToList();
                p.Check(lines.Any(t => t.Contains("% a roll")), "amber tells its odds in the chests that hold it", string.Join(" | ", lines.Take(3)));
            }
            yield break;
        }

        /// <summary>An item tells what it is used for and where it is made; a station what is made and built at it; a smelter what it turns into what; a bed its comfort.</summary>
        private static IEnumerator WhatThingsTell(Probe p)
        {
            var wood = Pick(Kind.Item, "Wood");
            if (wood != null) p.Check(Facts.For(wood).UseRows.Count > 0, "wood tells what it is used for", string.Join("; ", Facts.For(wood).UseRows.Select(r => r.Title).Take(4)));
            var sword = Pick(Kind.Item, "SwordIron");
            if (sword != null) p.Check(Facts.For(sword).Rows.Any(r => r.Title.StartsWith("Made at", StringComparison.Ordinal) && r.TitleLink != null), "the iron sword tells where it is made, going to the station");
            var bench = Pick(Kind.Piece, "piece_workbench");
            if (bench != null)
            {
                var told = Facts.For(bench);
                p.Check(told.Rows.Any(r => r.Title == "Made here" && r.Items.Count > 0), "the workbench tells what is made at it");
                p.Check(told.Rows.Any(r => r.Title == "Built near it" && r.Items.Count > 0), "and what is built near it");
            }
            var smelter = Pick(Kind.Piece, "smelter");
            if (smelter != null)
            {
                var told = Facts.For(smelter);
                p.Check(told.Rows.Count > 0 && Tells(told, "Burns"), "the smelter tells what it turns into what, and what it burns", Pairs(told));
            }
            var bed = Pick(Kind.Piece, "bed", "piece_bed02");
            if (bed != null) p.Check(Tells(Facts.For(bed), "Comfort"), "a bed tells its comfort", Pairs(Facts.For(bed)));
            yield break;
        }

        /// <summary>
        /// Content lists in their order, once every location is read: each location's parts told
        /// once, in a row by what each is, building pieces apart; creatures' drops rarest first;
        /// each biome's creatures toughest first; where things come from surest first. Examples
        /// are noted.
        /// </summary>
        private static IEnumerator ContentOrders(Probe p)
        {
            var titles = PlaceParts.Roles.Select(r => PlaceParts.Title(r, false)).ToList();
            var places = X.Catalog.Where(e => PlaceOf(e) is PlaceSource place && !place.IsRoom && place.Contents != null && place.Contents.Dungeon == null).ToList();
            if (places.Count == 0) p.Skip("no location has been read");
            var miscounted = new List<string>();
            var built = 0;
            Entry shown = null;
            foreach (var place in places)
            {
                var facts = Facts.For(place);
                var rows = facts.Rows.Where(r => titles.Contains(r.Title)).ToList();
                var told = rows.Sum(r => r.Items.Count);
                if (told != PlaceOf(place).Contents.Parts.Count) miscounted.Add($"{place.Name} {Numbers.Count(told)} of {Numbers.Count(PlaceOf(place).Contents.Parts.Count)}");
                if (rows.Any(r => r.Title == PlaceParts.Title(PartRole.Built, false)))
                {
                    built++;
                    if (shown == null || rows.Count > Facts.For(shown).Rows.Count(r => titles.Contains(r.Title))) shown = place;
                }
            }
            p.Check(miscounted.Count == 0, "every location tells each of its parts once, in a row by what it is", miscounted.Count > 0 ? string.Join(", ", miscounted.Take(10)) : $"{Numbers.Count(places.Count)} locations");
            p.Note($"{Numbers.Count(built)} of {Numbers.Count(places.Count)} locations have building pieces apart");
            if (shown != null)
            {
                p.Note($"{shown.Name}: " + string.Join("; ", Facts.For(shown).Rows.Where(r => titles.Contains(r.Title) || r.Title == "Its spawn points place")
                    .Select(r => $"{r.Title} ({Numbers.Count(r.Items.Count)}): {string.Join(", ", r.Items.Take(6).Select(i => i.Prefab))}")));
            }
            yield return null;

            // Creatures' drops, rarest first: each chip's chance no more than the next's.
            var unsorted = new List<string>();
            foreach (var creature in X.Catalog.Where(e => e.Kind == Kind.Creature && (e.Source as GameObject)?.GetComponent<CharacterDrop>() != null))
            {
                var drops = ((GameObject)creature.Source).GetComponent<CharacterDrop>().m_drops.Where(d => d?.m_prefab != null).ToList();
                var row = Facts.For(creature).Rows.FirstOrDefault(r => r.Title == "Drops");
                if (row == null) continue;
                var chances = row.Items.Select(i => drops.Where(d => d.m_prefab.name == i.Prefab).Select(d => d.m_chance).DefaultIfEmpty(1f).Min()).ToList();
                for (var i = 1; i < chances.Count; i++)
                {
                    if (chances[i] < chances[i - 1]) { unsorted.Add(creature.Name); break; }
                }
            }
            p.Check(unsorted.Count == 0, "creatures' drops come rarest first", unsorted.Count > 0 ? string.Join(", ", unsorted.Take(10)) : "");
            var greydwarf = Pick(Kind.Creature, "Greydwarf");
            var greyDrops = greydwarf != null ? Facts.For(greydwarf).Rows.FirstOrDefault(r => r.Title == "Drops") : null;
            if (greyDrops != null) p.Note($"{greydwarf.Name} drops: {string.Join(", ", greyDrops.Items.Select(i => $"{i.Prefab} {i.Amount}"))}");
            yield return null;

            // Each biome's creatures, toughest first: bosses, then the most health.
            var weaker = new List<string>();
            foreach (var biome in X.Catalog.Where(e => e.Kind == Kind.Biome))
            {
                var row = Facts.For(biome).Rows.FirstOrDefault(r => r.Title.StartsWith("Lives here", StringComparison.Ordinal));
                if (row == null) continue;
                var foes = row.Items.Select(i => Looks.Prefab(i.Prefab)?.GetComponent<Character>()).Where(c => c != null).Select(c => (c.m_boss ? 1 : 0, c.m_health)).ToList();
                for (var i = 1; i < foes.Count; i++)
                {
                    if (foes[i].Item1 > foes[i - 1].Item1 || (foes[i].Item1 == foes[i - 1].Item1 && foes[i].Item2 > foes[i - 1].Item2)) { weaker.Add(biome.Name); break; }
                }
                if (biome.Name == "Meadows" || biome.Name == "BlackForest") p.Note($"{biome.Name} lives: {string.Join(", ", row.Items.Take(6).Select(i => i.Prefab))}");
            }
            p.Check(weaker.Count == 0, "each biome's creatures come toughest first", string.Join(", ", weaker));
            yield return null;

            // Where things come from, the surest first.
            var doubtful = new List<string>();
            foreach (var item in X.Catalog.Where(e => e.Kind == Kind.Item))
            {
                var lines = Facts.For(item).Where.Where(l => l.Unsure == null).ToList();
                for (var i = 1; i < lines.Count; i++)
                {
                    if (lines[i].Chance > lines[i - 1].Chance + 1e-9) { doubtful.Add(item.Name); break; }
                }
            }
            p.Check(doubtful.Count == 0, "where things come from is told surest first", doubtful.Count > 0 ? string.Join(", ", doubtful.Take(10)) : "");
            var coal = Pick(Kind.Item, "Coal");
            if (coal != null) p.Note($"{coal.Name} comes from: {string.Join(" | ", Facts.For(coal).Where.Take(5).Select(l => l.Text))}");
        }
    }
}
