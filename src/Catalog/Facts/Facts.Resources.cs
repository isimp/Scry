using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>A resource's facts: what it takes to gather, what it gives, and what it comes from and becomes.</summary>
    internal sealed partial class Facts
    {
        // ----- Resources -----

        /// <summary>
        /// What is chopped, mined, broken, picked or grown: how much it takes and with what tool,
        /// what it resists, and what it gives, as the game's own scripts hold it. A prefab can be
        /// several of these (a stump breaks and drops wood), so each part adds its own.
        /// </summary>
        private void Resource(GameObject prefab)
        {
            // Where it comes from and what it becomes: a tree falls as its log and leaves a stump,
            // a log splits into halves, a vein's shell breaks into the vein (TreeBase.SpawnLog,
            // TreeLog.Destroy, Destructible.Destroy).
            Leads("Comes from", Knowledge.TurnedFrom(prefab.name).Select(p => (p, 1)));
            int Gives() => Rows.Count(r => r.Cells == null && r.Items.Count > 0);
            var giving = Gives();
            var broken = prefab.GetComponent<TreeBase>() != null || prefab.GetComponent<TreeLog>() != null || prefab.GetComponent<MineRock>() != null
                         || prefab.GetComponent<MineRock5>() != null || prefab.GetComponent<Destructible>() != null;

            var tree = prefab.GetComponent<TreeBase>();
            if (tree != null)
            {
                Hits(tree.m_health, false, tree.m_minToolTier, tree.m_damageModifiers);
                Drops(tree.m_dropWhenDestroyed, GatherWords.WhenFelled);
                Leads("Felled, falls as", new[] { (tree.m_logPrefab, 1) });
                Leads("Leaves", new[] { (tree.m_stubPrefab, 1) });
            }

            var log = prefab.GetComponent<TreeLog>();
            if (log != null)
            {
                Hits(log.m_health, false, log.m_minToolTier, log.m_damages);
                Drops(log.m_dropWhenDestroyed, null);
                Leads("Splits into", new[] { (log.m_subLogPrefab, log.m_subLogPoints?.Count(p => p != null) ?? 0) });
            }

            // A rock or ore vein is mined a piece at a time; each piece has this much health.
            var rock = prefab.GetComponent<MineRock>();
            if (rock != null)
            {
                Hits(rock.m_health, true, rock.m_minToolTier, rock.m_damageModifiers);
                Drops(rock.m_dropItems, GatherWords.EachPiece);
            }
            var vein = prefab.GetComponent<MineRock5>();
            if (vein != null)
            {
                Hits(vein.m_health, true, vein.m_minToolTier, vein.m_damageModifiers);
                Drops(vein.m_dropItems, GatherWords.EachPiece);
            }

            var breaks = prefab.GetComponent<Destructible>();
            var inside = breaks != null ? Knowledge.MinedInside(breaks.m_spawnWhenDestroyed) : null;
            if (inside != null)
            {
                // A shell that, struck once, turns into what is mined (a silver vein, a copper
                // deposit): what that takes and gives is what the vein takes and gives.
                Add("Breaks into", GatherWords.BreaksInto(AnyName(inside, inside.name)), inside.name);
                var innerRock = inside.GetComponent<MineRock>();
                var innerVein = inside.GetComponent<MineRock5>();
                if (innerVein != null)
                {
                    Hits(innerVein.m_health, true, Math.Max(breaks.m_minToolTier, innerVein.m_minToolTier), innerVein.m_damageModifiers);
                    Drops(innerVein.m_dropItems, GatherWords.EachPiece);
                }
                else if (innerRock != null)
                {
                    Hits(innerRock.m_health, true, Math.Max(breaks.m_minToolTier, innerRock.m_minToolTier), innerRock.m_damageModifiers);
                    Drops(innerRock.m_dropItems, GatherWords.EachPiece);
                }
            }
            else if (breaks != null && tree == null && log == null)
            {
                Hits(breaks.m_health, false, breaks.m_minToolTier, breaks.m_damages);
                var dropping = prefab.GetComponent<DropOnDestroyed>();
                if (dropping != null) Drops(dropping.m_dropWhenDestroyed, GatherWords.WhenBroken);
            }
            // What breaks and gives nothing says so.
            if (broken && Gives() == giving) Add("Gives", GatherWords.NothingWhenBroken);

            var pickable = prefab.GetComponent<Pickable>();
            if (pickable != null && pickable.m_itemPrefab != null)
            {
                var picked = new Row { Title = GatherWords.Picked(oneOf: false) };
                picked.Items.Add(new Ingredient
                {
                    Icon = Icon(pickable.m_itemPrefab), Name = ItemName(pickable.m_itemPrefab),
                    Amount = Numbers.Count(pickable.m_amount), Prefab = pickable.m_itemPrefab.name,
                });
                Rows.Add(picked);
                _drops = true;
                if (pickable.m_respawnTimeMinutes > 0f) Add("Grows back in", Numbers.Duration(pickable.m_respawnTimeMinutes * 60f));
                var day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1200L;
                var yields = Yield.PerDay(pickable.m_amount, pickable.m_respawnTimeMinutes, day);
                if (yields != null) Add("Gives", GatherWords.PerDay(yields, day));
                Drops(pickable.m_extraDrops, GatherWords.AlsoDrops);
                if (pickable.m_respawnTimeMinutes > 0f) Hooked(HookedRule.Growth);
            }

            var found = prefab.GetComponent<PickableItem>();
            if (found != null)
            {
                var row = new Row { Title = GatherWords.Picked(oneOf: found.m_randomItemPrefabs != null && found.m_randomItemPrefabs.Length > 1) };
                if (found.m_randomItemPrefabs != null && found.m_randomItemPrefabs.Length > 0)
                {
                    foreach (var random in found.m_randomItemPrefabs)
                    {
                        if (random.m_itemPrefab == null) continue;
                        var item = random.m_itemPrefab.gameObject;
                        row.Items.Add(new Ingredient { Icon = Icon(item), Name = ItemName(item), Amount = Numbers.CountRange(random.m_stackMin, random.m_stackMax), Prefab = item.name });
                    }
                }
                else if (found.m_itemPrefab != null)
                {
                    var item = found.m_itemPrefab.gameObject;
                    row.Items.Add(new Ingredient { Icon = Icon(item), Name = ItemName(item), Amount = Numbers.Count(Math.Max(1, found.m_stack)), Prefab = item.name });
                }
                if (row.Items.Count > 0)
                {
                    Rows.Add(row);
                    _drops = true;
                }
            }

            var plant = prefab.GetComponent<Plant>();
            if (plant != null)
            {
                Add("Takes to grow", Numbers.DurationRange(plant.m_growTime, Math.Max(plant.m_growTime, plant.m_growTimeMax)));
                if (plant.m_biome != 0) BiomeRow("Grows in", plant.m_biome);
                if (plant.m_needCultivatedGround) Add("Needs", GatherWords.CultivatedGround);
                Hooked(HookedRule.Growth);
                Add("Tolerates", GatherWords.Tolerates(plant.m_tolerateHeat, plant.m_tolerateCold));
                if (plant.m_grownPrefabs != null && plant.m_grownPrefabs.Length > 0)
                {
                    var grows = new Row { Title = GatherWords.GrowsInto(oneOf: plant.m_grownPrefabs.Length > 1) };
                    foreach (var grown in plant.m_grownPrefabs.Where(g => g != null).GroupBy(g => g.name).Select(g => g.First()))
                    {
                        grows.Items.Add(new Ingredient { Icon = AnyIcon(grown), Name = AnyName(grown, grown.name), Amount = "", Prefab = grown.name });
                    }
                    if (grows.Items.Count > 0) Rows.Add(grows);
                }
            }
        }

        /// <summary>A row of what a resource leads to or comes from, each part going to it; nothing when there is none.</summary>
        private void Leads(string title, IEnumerable<(GameObject Prefab, int Count)> parts)
        {
            var row = new Row { Title = title };
            foreach (var (part, count) in parts)
            {
                if (part == null || count <= 0) continue;
                row.Items.Add(new Ingredient { Icon = AnyIcon(part), Name = AnyName(part, part.name), Amount = count > 1 ? Numbers.Count(count) : "", Prefab = part.name });
            }
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>How much it takes to break, with what tool, and what it resists.</summary>
        private void Hits(float health, bool perPiece, int toolTier, HitData.DamageModifiers resists)
        {
            if (health > 0f) Add("Health", GatherWords.Health(health, perPiece));
            Add("Needs tool tier", GatherWords.ToolTier(toolTier));
            Resists(resists);

            // What breaks it, told once, by its outer part where it has an inside.
            if (Rows.Any(r => r.Title == GatherWords.BrokenWithTitle)) return;
            var cells = Cells(resists);
            var immune = SearchFight.Taking(cells, Tone.Immune);
            var taken = cells.Select(c => c.Type.ToLowerInvariant()).Where(t => !immune.Contains(t));
            var row = new Row { Title = GatherWords.BrokenWithTitle };
            foreach (var tool in GatherWords.BreaksIt(toolTier, taken, Tools())) row.Items.Add(Chip(tool, ""));
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>The weapons, pickaxes and torches players hit with and can get, each with its tool tier and the damage types it deals; read once a world.</summary>
        private static List<(string Name, int Tier, string[] Deals)> Tools()
        {
            if (_tools != null) return _tools;
            _tools = new List<(string, int, string[])>();
            foreach (var entry in WorldCatalog.Current?.All ?? Enumerable.Empty<Entry>())
            {
                if (entry.Kind != Kind.Item || !(entry.Source is GameObject prefab)) continue;
                var item = prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData;
                var shared = item?.m_shared;
                // Only what a player swings hits: a weapon, a pickaxe or a torch in hand (ItemData.IsWeapon); a trophy's
                // damage figure is never dealt. One no inventory can show is a creature's attack, and one nothing gives a
                // player (the game's cheat sword) is no tool either.
                if (shared?.m_attack == null || !item.IsWeapon() || shared.m_icons == null || shared.m_icons.Length == 0 || !Grouping.Obtainable(entry)) continue;
                _tools.Add((prefab.name, shared.m_toolTier, SearchFight.Dealt(DamageFigures(shared.m_damages))));
            }
            return _tools;
        }

        private static List<(string Name, int Tier, string[] Deals)> _tools;

        /// <summary>
        /// A drop table as a row: its title says how often and how many times, each chip how many
        /// and its share. A chest's is told as what it holds.
        /// </summary>
        private void Drops(DropTable table, string lead, bool holds = false)
        {
            if (table?.m_drops == null) return;
            var info = Knowledge.InfoOf(table);
            if (DropWords.IsEmpty(info)) return;
            var items = new List<(DropInfo Drop, GameObject Item)>();
            foreach (var drop in table.m_drops) if (drop.m_item != null) items.Add((info.Drops[items.Count], drop.m_item));

            // The rarest first: within one table, the least weight.
            var title = holds ? DropWords.HoldsTitle(info) : DropWords.Title(info);
            title = DropWords.Led(lead, title);
            var row = new Row { Title = title };
            // What only this gives first, then what a trader pays for, then the rest, each the least likely first.
            var here = new[] { _entry?.Name ?? "" };
            foreach (var (each, item, notable) in ContentOrder.LootFirst(items.Select(i => (i.Drop, i.Item, Notable: Notable(i.Item.name, here))), i => i.Drop.Weight, i => i.Notable.Only, i => i.Notable.Worth))
            {
                row.Items.Add(Marked(new Ingredient { Icon = Icon(item), Name = ItemName(item), Amount = DropWords.Amount(info, each), Prefab = item.name }, notable));
            }
            Rows.Add(row);
            _drops = true;
        }

        /// <summary>The game's damage modifier as the model's degree, whose values it keeps (<see cref="Degree"/>).</summary>
        private static Degree DegreeOf(HitData.DamageModifier modifier) => (Degree)(int)modifier;
    }
}
