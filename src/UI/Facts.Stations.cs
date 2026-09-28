using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Scry
{
    internal sealed partial class Facts
    {
        // ----- Stations, producers, traders and altars -----

        /// <summary>A chip for a prefab by name, with the name and icon the game shows.</summary>
        private static Ingredient Chip(string name, string amount)
        {
            var prefab = Looks.Prefab(name);
            return new Ingredient { Icon = AnyIcon(prefab), Name = AnyName(prefab, name), Amount = amount ?? "", Prefab = name };
        }

        private static Ingredient Chip(ItemDrop item, string amount) => Chip(item.gameObject.name, amount);

        /// <summary>Where an item is made other than by a recipe: each station that turns something into it, and from what.</summary>
        private void MadeIn(GameObject item)
        {
            foreach (var making in Knowledge.MadeOf(item.name))
            {
                var row = new Row { Title = MakerBook.ItemTitle(AnyName(Looks.Prefab(making.Station), making.Station), making), TitleLink = making.Station };
                foreach (var (input, amount) in making.Inputs) row.Items.Add(Chip(input, amount.ToString(CultureInfo.InvariantCulture)));
                Rows.Add(row);
            }
        }

        /// <summary>
        /// What a station, producer, trader, fire or altar does: what it makes and from what, what
        /// it burns and holds, how long it takes and what it needs, what is made at it, what it
        /// sells and which boss it summons.
        /// </summary>
        private void Station(GameObject prefab)
        {
            foreach (var making in Knowledge.MadeAt(prefab.name))
            {
                var row = new Row { Title = MakerBook.StationTitle(AnyName(Looks.Prefab(making.Output), making.Output), making), TitleLink = making.Output };
                foreach (var (input, amount) in making.Inputs) row.Items.Add(Chip(input, amount.ToString(CultureInfo.InvariantCulture)));
                Rows.Add(row);
            }

            var smelter = prefab.GetComponent<Smelter>();
            if (smelter != null)
            {
                var fuel = smelter.m_fuelItem;
                if (fuel != null) Add("Burns", $"{ItemName(fuel.gameObject)}, {smelter.m_fuelPerProduct} for each", fuel.gameObject.name);
                Add("Holds", fuel != null ? $"{smelter.m_maxOre} to process, {smelter.m_maxFuel} fuel" : $"{smelter.m_maxOre} to process");
                Add("Each takes", Naming.Duration(smelter.m_secPerProduct));
                if (smelter.m_requiresRoof) Add("Needs", "a roof");
            }

            var cooking = prefab.GetComponent<CookingStation>();
            if (cooking != null)
            {
                if (cooking.m_slots != null && cooking.m_slots.Length > 0) Add("Cooks", $"{cooking.m_slots.Length} at a time");
                var times = cooking.m_conversion?.Where(c => c?.m_from != null && c.m_to != null).Select(c => c.m_cookTime).ToList();
                if (times != null && times.Count > 0)
                {
                    var least = times.Min();
                    var most = times.Max();
                    Add("Each takes", Naming.DurationRange(least, most));
                }
                if (cooking.m_canOvercookItems && cooking.m_overCookedItem != null)
                {
                    Add("Left too long", ItemName(cooking.m_overCookedItem.gameObject), cooking.m_overCookedItem.gameObject.name);
                }
                if (cooking.m_useFuel && cooking.m_fuelItem != null)
                {
                    Add("Burns", $"{ItemName(cooking.m_fuelItem.gameObject)}, one every {Naming.Duration(cooking.m_secPerFuel)}", cooking.m_fuelItem.gameObject.name);
                    Add("Holds", $"{cooking.m_maxFuel} fuel");
                }
                if (cooking.m_requireFire) Add("Needs", "a fire under it");
            }

            var fermenter = prefab.GetComponent<Fermenter>();
            if (fermenter != null)
            {
                Add("Each takes", Naming.Duration(fermenter.m_fermentationDuration));
                // Fermenter.UpdateCover restarts the batch without a roof or with less than 70% cover.
                Add("Needs", "a roof, and cover on most sides");
            }

            var incinerator = prefab.GetComponent<Incinerator>();
            if (incinerator != null && incinerator.m_defaultResult != null && incinerator.m_defaultCost > 0)
            {
                Add("Anything else", $"becomes {ItemName(incinerator.m_defaultResult.gameObject)}, one for every {incinerator.m_defaultCost}", incinerator.m_defaultResult.gameObject.name);
            }

            var hive = prefab.GetComponent<Beehive>();
            if (hive != null && hive.m_honeyItem != null)
            {
                Add("Makes", $"{ItemName(hive.m_honeyItem.gameObject)}, one every {Naming.Duration(hive.m_secPerUnit)}, holding up to {hive.m_maxHoney}", hive.m_honeyItem.gameObject.name);
                if (hive.m_biome != 0) Add("Works in", Knowledge.BiomeNames(hive.m_biome));
                // Beehive.HaveFreeSpace: it makes nothing while more of the sky around it is covered.
                if (hive.m_maxCover > 0f) Add("Needs", $"open sky, less than {Mathf.RoundToInt(hive.m_maxCover * 100f)}% covered");
            }

            var tap = prefab.GetComponent<SapCollector>();
            if (tap != null && tap.m_spawnItem != null)
            {
                Add("Makes", $"{ItemName(tap.m_spawnItem.gameObject)}, one every {Naming.Duration(tap.m_secPerUnit)}, holding up to {tap.m_maxLevel}", tap.m_spawnItem.gameObject.name);
                // SapCollector.UpdateTick: it makes only while on its root, and no more than the root has left.
                var root = tap.m_mustConnectTo != null ? tap.m_mustConnectTo.gameObject : null;
                if (root != null) Add("Needs", $"to be built on {AnyName(root, root.name)}, and takes only the sap it has left", root.name);
            }

            var fire = prefab.GetComponent<Fireplace>();
            if (fire != null && fire.m_fuelItem != null && !fire.m_infiniteFuel)
            {
                Add("Burns", $"{ItemName(fire.m_fuelItem.gameObject)}, one every {Naming.Duration(fire.m_secPerFuel)}", fire.m_fuelItem.gameObject.name);
                Add("Holds", $"{Number(fire.m_maxFuel)} fuel");
            }

            var shield = prefab.GetComponent<ShieldGenerator>();
            if (shield != null && shield.m_fuelItems != null && shield.m_fuelItems.Count > 0)
            {
                var row = new Row { Title = shield.m_fuelItems.Count > 1 ? "Burns any one of these" : "Burns" };
                foreach (var each in shield.m_fuelItems.Where(f => f != null)) row.Items.Add(Chip(each, ""));
                if (row.Items.Count > 0) Rows.Add(row);
                Add("Holds", $"{shield.m_maxFuel} fuel");
            }

            var craft = prefab.GetComponent<CraftingStation>();
            if (craft != null) MadeHere(prefab.name);
            // An upgrader takes what can be upgraded past its top quality, with the upgrade kits
            // its recipe names alone (InventoryGui.UpdateRecipeList, Player.HaveRequirements).
            if (craft != null && craft.m_upgrader) Add("Upgrade station", "takes items past their top quality, with the upgrade kits their recipes name");

            var trader = prefab.GetComponent<Trader>();
            if (trader != null) Sells(trader);

            var bowl = prefab.GetComponent<OfferingBowl>();
            if (bowl != null && bowl.m_bossPrefab != null && bowl.m_bossItem != null)
            {
                var row = new Row { Title = $"Summons {AnyName(bowl.m_bossPrefab, bowl.m_bossPrefab.name)} with", TitleLink = bowl.m_bossPrefab.name };
                row.Items.Add(Chip(bowl.m_bossItem, Mathf.Max(1, bowl.m_bossItems).ToString(CultureInfo.InvariantCulture)));
                Rows.Add(row);
            }
        }

        /// <summary>What is made at a crafting station, and what only upgrades and repairs there (<c>Recipe.m_repairStation</c>).</summary>
        private void MadeHere(string station)
        {
            var db = ObjectDB.instance;
            if (db == null) return;
            var made = new Row { Title = "Made here" };
            var upgraded = new Row { Title = "Upgraded and repaired here" };
            var seen = new HashSet<string>();
            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || !recipe.m_enabled || recipe.m_item == null) continue;
                var name = recipe.m_item.gameObject.name;
                if (recipe.m_craftingStation != null && recipe.m_craftingStation.gameObject.name == station)
                {
                    if (seen.Add(name)) made.Items.Add(Chip(recipe.m_item, recipe.m_amount > 1 ? recipe.m_amount.ToString(CultureInfo.InvariantCulture) : ""));
                }
                else if (recipe.m_craftingStation == null && recipe.m_repairStation != null && recipe.m_repairStation.gameObject.name == station)
                {
                    if (seen.Add(name)) upgraded.Items.Add(Chip(recipe.m_item, ""));
                }
            }
            if (made.Items.Count > 0) Rows.Add(made);
            if (upgraded.Items.Count > 0) Rows.Add(upgraded);

            // The pieces built near it, as each piece says "Built near" it.
            var built = new Row { Title = "Built near it" };
            var prefabs = ZNetScene.instance != null ? ZNetScene.instance.m_prefabs : null;
            if (prefabs != null)
            {
                foreach (var prefab in prefabs)
                {
                    var piece = prefab != null ? prefab.GetComponent<global::Piece>() : null;
                    if (piece == null || !piece.enabled || !piece.m_enabled || piece.m_craftingStation == null || piece.m_craftingStation.gameObject.name != station) continue;
                    if (seen.Add(prefab.name)) built.Items.Add(Chip(prefab.name, ""));
                }
            }
            if (built.Items.Count > 0) Rows.Add(built);
        }

        /// <summary>What a trader sells and for how much, a row for each world key its wares wait for.</summary>
        private void Sells(Trader trader)
        {
            if (trader.m_items == null) return;
            var rows = new List<(string Key, Row Row)>();
            foreach (var trade in trader.m_items)
            {
                if (trade?.m_prefab == null) continue;
                var key = trade.m_requiredGlobalKey ?? "";
                var at = rows.FindIndex(r => r.Key == key);
                if (at < 0)
                {
                    var title = key.Length == 0 ? "Sells" : "Sells " + SpawnWords.Once(key, Knowledge.BossOf);
                    rows.Add((key, new Row { Title = title, TitleLink = Knowledge.BossPrefabOf(key) }));
                    at = rows.Count - 1;
                }
                var price = trade.m_stack > 1 ? $"{trade.m_stack} for {trade.m_price} coins" : $"{trade.m_price} coins";
                rows[at].Row.Items.Add(Chip(trade.m_prefab, price));
            }
            foreach (var (_, row) in rows) Rows.Add(row);
        }

        /// <summary>A boss's altars: where each stands and what is offered at it.</summary>
        private void SummonedBy(GameObject boss)
        {
            foreach (var summon in Knowledge.Summons().Where(s => s.Boss == boss.name && s.Item != null))
            {
                var row = new Row { Title = $"Summoned at {summon.Place} with", TitleLink = summon.PlacePrefab };
                row.Items.Add(Chip(summon.Item, summon.Count.ToString(CultureInfo.InvariantCulture)));
                if (!Rows.Any(r => r.Title == row.Title && r.Items.Count == 1 && r.Items[0].Prefab == summon.Item)) Rows.Add(row);
            }
        }

        /// <summary>The bosses an item is offered at an altar to summon.</summary>
        private void OfferedFor(string item)
        {
            var row = new Row { Title = "Offered at an altar to summon" };
            foreach (var summon in Knowledge.Summons().Where(s => s.Item == item && s.Boss != null))
            {
                if (row.Items.Any(i => i.Prefab == summon.Boss)) continue;
                row.Items.Add(Chip(summon.Boss, summon.Count.ToString(CultureInfo.InvariantCulture)));
            }
            if (row.Items.Count > 0) UseRows.Add(row);
        }
    }
}
