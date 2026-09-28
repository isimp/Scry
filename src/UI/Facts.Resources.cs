using System;
using System.Collections.Generic;
using System.Globalization;
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

            var tree = prefab.GetComponent<TreeBase>();
            if (tree != null)
            {
                Hits(tree.m_health, false, tree.m_minToolTier, tree.m_damageModifiers);
                Drops(tree.m_dropWhenDestroyed, "When felled, ");
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
                Drops(rock.m_dropItems, "Each piece ");
            }
            var vein = prefab.GetComponent<MineRock5>();
            if (vein != null)
            {
                Hits(vein.m_health, true, vein.m_minToolTier, vein.m_damageModifiers);
                Drops(vein.m_dropItems, "Each piece ");
            }

            var breaks = prefab.GetComponent<Destructible>();
            var inside = breaks != null ? Knowledge.MinedInside(breaks.m_spawnWhenDestroyed) : null;
            if (inside != null)
            {
                // A shell that, struck once, turns into what is mined (a silver vein, a copper
                // deposit): what that takes and gives is what the vein takes and gives.
                Add("Breaks into", AnyName(inside, inside.name) + ", mined a piece at a time", inside.name);
                var innerRock = inside.GetComponent<MineRock>();
                var innerVein = inside.GetComponent<MineRock5>();
                if (innerVein != null)
                {
                    Hits(innerVein.m_health, true, Math.Max(breaks.m_minToolTier, innerVein.m_minToolTier), innerVein.m_damageModifiers);
                    Drops(innerVein.m_dropItems, "Each piece ");
                }
                else if (innerRock != null)
                {
                    Hits(innerRock.m_health, true, Math.Max(breaks.m_minToolTier, innerRock.m_minToolTier), innerRock.m_damageModifiers);
                    Drops(innerRock.m_dropItems, "Each piece ");
                }
            }
            else if (breaks != null && tree == null && log == null)
            {
                Hits(breaks.m_health, false, breaks.m_minToolTier, breaks.m_damages);
                var dropping = prefab.GetComponent<DropOnDestroyed>();
                if (dropping != null) Drops(dropping.m_dropWhenDestroyed, "When broken, ");
            }

            var pickable = prefab.GetComponent<Pickable>();
            if (pickable != null && pickable.m_itemPrefab != null)
            {
                var picked = new Row { Title = "Picked" };
                picked.Items.Add(new Ingredient
                {
                    Icon = Icon(pickable.m_itemPrefab), Name = ItemName(pickable.m_itemPrefab),
                    Amount = pickable.m_amount.ToString(CultureInfo.InvariantCulture), Prefab = pickable.m_itemPrefab.name,
                });
                Rows.Add(picked);
                _drops = true;
                if (pickable.m_respawnTimeMinutes > 0f) Add("Grows back in", Minutes(pickable.m_respawnTimeMinutes * 60f));
                var day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1200L;
                var yields = Yield.PerDay(pickable.m_amount, pickable.m_respawnTimeMinutes, day);
                if (yields != null) Add("Gives", yields + $" (a day is {Minutes(day)})");
                Drops(pickable.m_extraDrops, "Also ");
            }

            var found = prefab.GetComponent<PickableItem>();
            if (found != null)
            {
                var row = new Row { Title = found.m_randomItemPrefabs != null && found.m_randomItemPrefabs.Length > 1 ? "Picked, one of" : "Picked" };
                if (found.m_randomItemPrefabs != null && found.m_randomItemPrefabs.Length > 0)
                {
                    foreach (var random in found.m_randomItemPrefabs)
                    {
                        if (random.m_itemPrefab == null) continue;
                        var item = random.m_itemPrefab.gameObject;
                        row.Items.Add(new Ingredient { Icon = Icon(item), Name = ItemName(item), Amount = DropWords.Range(random.m_stackMin, random.m_stackMax), Prefab = item.name });
                    }
                }
                else if (found.m_itemPrefab != null)
                {
                    var item = found.m_itemPrefab.gameObject;
                    row.Items.Add(new Ingredient { Icon = Icon(item), Name = ItemName(item), Amount = Math.Max(1, found.m_stack).ToString(CultureInfo.InvariantCulture), Prefab = item.name });
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
                Add("Takes to grow", Naming.DurationRange(plant.m_growTime, Math.Max(plant.m_growTime, plant.m_growTimeMax)));
                if (plant.m_biome != 0) Add("Grows in", Knowledge.BiomeNames(plant.m_biome));
                if (plant.m_needCultivatedGround) Add("Needs", "cultivated ground");
                var tolerates = new List<string>();
                if (plant.m_tolerateHeat) tolerates.Add("heat");
                if (plant.m_tolerateCold) tolerates.Add("cold");
                if (tolerates.Count > 0) Add("Tolerates", string.Join(", ", tolerates));
                if (plant.m_grownPrefabs != null && plant.m_grownPrefabs.Length > 0)
                {
                    var grows = new Row { Title = plant.m_grownPrefabs.Length > 1 ? "Grows into one of" : "Grows into" };
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
                row.Items.Add(new Ingredient { Icon = AnyIcon(part), Name = AnyName(part, part.name), Amount = count > 1 ? count.ToString(CultureInfo.InvariantCulture) : "", Prefab = part.name });
            }
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>How much it takes to break, with what tool, and what it resists.</summary>
        private void Hits(float health, bool perPiece, int toolTier, HitData.DamageModifiers resists)
        {
            if (health > 0f) Add("Health", Number(health) + (perPiece ? " a piece" : ""));
            if (toolTier > 0) Add("Needs tool tier", toolTier.ToString(CultureInfo.InvariantCulture));
            Resists(resists);
        }

        /// <summary>
        /// A drop table as a row: its title says how often and how many times, each chip how many
        /// and its share. A chest's is told as what it holds.
        /// </summary>
        private void Drops(DropTable table, string lead, bool holds = false)
        {
            if (table?.m_drops == null) return;
            var info = new DropTableInfo { Min = table.m_dropMin, Max = table.m_dropMax, Chance = table.m_dropChance, OneOfEach = table.m_oneOfEach };
            var items = new List<GameObject>();
            foreach (var drop in table.m_drops)
            {
                if (drop.m_item == null) continue;
                info.Drops.Add(new DropInfo(drop.m_item.name, drop.m_stackMin, drop.m_stackMax, drop.m_weight));
                items.Add(drop.m_item);
            }
            if (DropWords.IsEmpty(info)) return;

            var title = holds ? DropWords.HoldsTitle(info) : DropWords.Title(info);
            if (lead != null) title = lead + char.ToLowerInvariant(title[0]) + title.Substring(1);
            var row = new Row { Title = title };
            for (var i = 0; i < items.Count; i++)
            {
                row.Items.Add(new Ingredient { Icon = Icon(items[i]), Name = ItemName(items[i]), Amount = DropWords.Amount(info, info.Drops[i]), Prefab = items[i].name });
            }
            Rows.Add(row);
            _drops = true;
        }

        /// <summary>The degrees of a damage modifier, from the most harm taken to the least.</summary>
        private static readonly HitData.DamageModifier[] Degrees =
        {
            HitData.DamageModifier.VeryWeak, HitData.DamageModifier.Weak, HitData.DamageModifier.SlightlyWeak, HitData.DamageModifier.SlightlyResistant,
            HitData.DamageModifier.Resistant, HitData.DamageModifier.VeryResistant, HitData.DamageModifier.Immune, HitData.DamageModifier.Ignore,
        };

        /// <summary>How a damage modifier reads as a label, e.g. "Weak to".</summary>
        private static string ModifierWords(HitData.DamageModifier modifier)
        {
            switch (modifier)
            {
                case HitData.DamageModifier.VeryWeak: return "Very weak to";
                case HitData.DamageModifier.Weak: return "Weak to";
                case HitData.DamageModifier.SlightlyWeak: return "Slightly weak to";
                case HitData.DamageModifier.SlightlyResistant: return "Slightly resists";
                case HitData.DamageModifier.Resistant: return "Resists";
                case HitData.DamageModifier.VeryResistant: return "Strongly resists";
                case HitData.DamageModifier.Immune: return "Immune to";
                case HitData.DamageModifier.Ignore: return "Unaffected by";
                default: return Naming.FieldLabel(modifier.ToString());
            }
        }
    }
}
