using System.Collections.Generic;
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
            var prefab = GamePrefabs.Item(name);
            return new Ingredient { Icon = AnyIcon(prefab), Name = AnyName(prefab, name), Amount = amount ?? "", Prefab = name };
        }

        private static Ingredient Chip(ItemDrop item, string amount) => Chip(item.gameObject.name, amount);

        /// <summary>Where an item is made other than by a recipe: each station that turns something into it, and from what.</summary>
        private void MadeIn(GameObject item)
        {
            foreach (var making in Knowledge.MadeOf(item.name))
            {
                var title = ModWords.AddedBy(MakerBook.ItemTitle(AnyName(GamePrefabs.Item(making.Station), making.Station), making), Knowledge.ConversionMod(making), Knowledge.ModName(item.name));
                var row = new Row { Title = title, TitleLink = making.Station };
                foreach (var (input, amount) in making.Inputs) row.Items.Add(Chip(input, Numbers.Count(amount)));
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
                var row = new Row { Title = MakerBook.StationTitle(AnyName(GamePrefabs.Item(making.Output), making.Output), making), TitleLink = making.Output };
                foreach (var (input, amount) in making.Inputs) row.Items.Add(Chip(input, Numbers.Count(amount)));
                Rows.Add(row);
            }

            var smelter = prefab.GetComponent<Smelter>();
            if (smelter != null)
            {
                var fuel = smelter.m_fuelItem;
                if (fuel != null) Add("Burns", StationWords.BurnsForEach(ItemName(fuel.gameObject), smelter.m_fuelPerProduct), fuel.gameObject.name);
                Add("Holds", StationWords.Holds(smelter.m_maxOre, fuel != null ? smelter.m_maxFuel : (int?)null));
                // A windmill paces it by its wind (Windmill.GetPowerOutput), blocked around its hub (Windmill.CheckCover).
                var windmill = smelter.m_windmill != null ? smelter.m_windmill : prefab.GetComponentInChildren<Windmill>(true);
                if (windmill != null)
                {
                    Add("Each takes", StationWords.InFullWind(smelter.m_secPerProduct));
                    Add("Runs on", StationWords.RunsOnWind(windmill.m_minWindSpeed));
                    Add("Blocked by", StationWords.WindBlockedBy);
                    var blades = StationWords.BladesHurt(windmill.m_minAOEPropellerSpeed, windmill.m_propellerRotationSpeed);
                    if (blades != null && windmill.m_propellerAOE != null) Add("Its blades", blades);
                    Add("While away", StationWords.CatchesUp(StationWords.SmelterCatchUp));
                }
                else Add("Each takes", Numbers.Duration(smelter.m_secPerProduct));
                // The game shows neither how long each takes nor what it burns: under the strict rules they wait for one seen finished.
                Proofs["Each takes"] = Proofs["Burns"] = EntryKeys.Did(Deed.Finished);
                if (smelter.m_requiresRoof) Add("Needs", StationWords.Roof);
                Hooked(HookedRule.Smelting);
            }

            var cooking = prefab.GetComponent<CookingStation>();
            if (cooking != null)
            {
                if (cooking.m_slots != null && cooking.m_slots.Length > 0) Add("Cooks", StationWords.AtATime(cooking.m_slots.Length));
                var times = cooking.m_conversion?.Where(c => c?.m_from != null && c.m_to != null).Select(c => c.m_cookTime).ToList();
                if (times != null && times.Count > 0)
                {
                    var least = times.Min();
                    var most = times.Max();
                    Add("Each takes", Numbers.DurationRange(least, most));
                    Proofs["Each takes"] = EntryKeys.Did(Deed.Finished);
                }
                if (cooking.m_canOvercookItems && cooking.m_overCookedItem != null)
                {
                    Add("Left too long", ItemName(cooking.m_overCookedItem.gameObject), cooking.m_overCookedItem.gameObject.name);
                }
                if (cooking.m_useFuel && cooking.m_fuelItem != null)
                {
                    Add("Burns", StationWords.BurnsOneEvery(ItemName(cooking.m_fuelItem.gameObject), cooking.m_secPerFuel), cooking.m_fuelItem.gameObject.name);
                    Add("Holds", StationWords.Fuel(cooking.m_maxFuel));
                    Proofs["Burns"] = EntryKeys.Did(Deed.Finished);
                }
                if (cooking.m_requireFire) Add("Needs", StationWords.FireUnder);
                // Food taken off it may come with more, likelier by the skill it trains (CookingStation.OnInteract).
                var gui = InventoryGui.instance;
                if (cooking.m_skill != Skills.SkillType.None && cooking.m_canGiveBonusYield && gui != null && gui.m_craftBonusChance > 0f)
                {
                    TrainsSkill(cooking.m_skill);
                    Add("Bonus when taken off", SkillWords.BonusSpan(gui.m_craftBonusAmount, gui.m_craftBonusChance));
                    YoursAt("Bonus when taken off", cooking.m_skill, level => SkillWords.Bonus(gui.m_craftBonusAmount, SkillMath.BonusChance(level, gui.m_craftBonusChance)));
                }
                Hooked(HookedRule.Cooking);
            }

            var fermenter = prefab.GetComponent<Fermenter>();
            if (fermenter != null)
            {
                Add("Each takes", Numbers.Duration(fermenter.m_fermentationDuration));
                Proofs["Each takes"] = EntryKeys.Did(Deed.Finished);
                // Fermenter.UpdateCover restarts the batch without a roof or with less than 70% cover.
                Add("Needs", StationWords.Sheltered);
                Hooked(HookedRule.Fermenting);
            }

            var incinerator = prefab.GetComponent<Incinerator>();
            if (incinerator != null && incinerator.m_defaultResult != null && incinerator.m_defaultCost > 0)
            {
                Add("Anything else", StationWords.Incinerates(ItemName(incinerator.m_defaultResult.gameObject), incinerator.m_defaultCost), incinerator.m_defaultResult.gameObject.name);
            }

            var hive = prefab.GetComponent<Beehive>();
            if (hive != null && hive.m_honeyItem != null)
            {
                // What it makes, and apart from that its pace, which shows under the strict rules once collected from.
                Add("Makes", ItemName(hive.m_honeyItem.gameObject), hive.m_honeyItem.gameObject.name);
                Add("Pace", SourceWords.Pace(hive.m_secPerUnit, hive.m_maxHoney));
                Proofs["Pace"] = EntryKeys.Did(Deed.Collected);
                if (hive.m_biome != 0) BiomeRow("Works in", hive.m_biome);
                // Beehive.HaveFreeSpace: it makes nothing while more of the sky around it is covered.
                if (hive.m_maxCover > 0f) Add("Needs", StationWords.OpenSky(hive.m_maxCover));
                Hooked(HookedRule.Producing);
            }

            var tap = prefab.GetComponent<SapCollector>();
            if (tap != null && tap.m_spawnItem != null)
            {
                Add("Makes", ItemName(tap.m_spawnItem.gameObject), tap.m_spawnItem.gameObject.name);
                Add("Pace", SourceWords.Pace(tap.m_secPerUnit, tap.m_maxLevel));
                Proofs["Pace"] = EntryKeys.Did(Deed.Collected);
                // SapCollector.UpdateTick: it makes only while on its root, and no more than the root has left.
                var root = tap.m_mustConnectTo != null ? tap.m_mustConnectTo.gameObject : null;
                if (root != null) Add("Needs", StationWords.BuiltOn(AnyName(root, root.name)), root.name);
                Hooked(HookedRule.Producing);
            }

            var fire = prefab.GetComponent<Fireplace>();
            if (fire != null && fire.m_fuelItem != null && !fire.m_infiniteFuel)
            {
                Add("Burns", StationWords.BurnsOneEvery(ItemName(fire.m_fuelItem.gameObject), fire.m_secPerFuel), fire.m_fuelItem.gameObject.name);
                Add("Holds", StationWords.Fuel(fire.m_maxFuel));
                Hooked(HookedRule.Burning);
            }

            var shield = prefab.GetComponent<ShieldGenerator>();
            if (shield != null && shield.m_fuelItems != null && shield.m_fuelItems.Count > 0)
            {
                var row = new Row { Title = StationWords.BurnsAnyOf(shield.m_fuelItems.Count > 1) };
                foreach (var each in shield.m_fuelItems.Where(f => f != null)) row.Items.Add(Chip(each, ""));
                if (row.Items.Count > 0) Rows.Add(row);
                Add("Holds", StationWords.Fuel(shield.m_maxFuel));
            }

            // A hit on an archery target raises the skill of what was shot, more the nearer its middle (ArcheryTarget).
            var target = prefab.GetComponentInChildren<ArcheryTarget>(true);
            if (target != null && target.m_raiseSkillMultiplier > 0f) Add("Trains", SkillWords.TargetTrains(target.m_raiseSkillMultiplier));

            var craft = prefab.GetComponent<CraftingStation>();
            if (craft != null)
            {
                Add("Building reach", BuildWords.Range(craft.m_rangeBuild, craft.m_extraRangePerLevel));
                var needs = StationWords.CraftingNeeds(craft.m_craftRequireRoof, craft.m_craftRequireFire);
                if (needs != null) Add("Crafting needs", needs);
                // Making here trains its skill: faster, and more of what stacks likelier (InventoryGui.UpdateRecipe, InventoryGui.DoCrafting).
                var gui = InventoryGui.instance;
                if (craft.m_craftingSkill != Skills.SkillType.None && gui != null)
                {
                    TrainsSkill(craft.m_craftingSkill);
                    var time = gui.m_craftDuration;
                    var saved = gui.m_craftDurationSkillMaxDecrease;
                    Add("Making takes", SkillWords.Span(Numbers.Duration(time), Numbers.Duration(time * SkillMath.CraftTimeShare(SkillMath.Top, saved))));
                    YoursAt("Making takes", craft.m_craftingSkill, level => Numbers.Duration(time * SkillMath.CraftTimeShare(level, saved)));
                    if (gui.m_craftBonusChance > 0f)
                    {
                        Add("Bonus on what stacks", SkillWords.BonusSpan(gui.m_craftBonusAmount, gui.m_craftBonusChance));
                        YoursAt("Bonus on what stacks", craft.m_craftingSkill, level => SkillWords.Bonus(gui.m_craftBonusAmount, SkillMath.BonusChance(level, gui.m_craftBonusChance)));
                    }
                }
                MadeHere(prefab.name);
            }
            // An upgrader takes what can be upgraded past its top quality, with the upgrade kits
            // its recipe names alone (InventoryGui.UpdateRecipeList, Player.HaveRequirements).
            if (craft != null && craft.m_upgrader) Add("Upgrade station", StationWords.Upgrader);

            var trader = prefab.GetComponent<Trader>();
            if (trader != null)
            {
                Sells(trader);
                Hooked(HookedRule.Trading);
            }

            var bowl = prefab.GetComponent<OfferingBowl>();
            if (bowl != null && bowl.m_bossPrefab != null && bowl.m_bossItem != null)
            {
                var row = new Row { Title = StationWords.Summons(AnyName(bowl.m_bossPrefab, bowl.m_bossPrefab.name)), TitleLink = bowl.m_bossPrefab.name };
                row.Items.Add(Chip(bowl.m_bossItem, Numbers.Count(Mathf.Max(1, bowl.m_bossItems))));
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
                    if (seen.Add(name)) made.Items.Add(Chip(recipe.m_item, recipe.m_amount > 1 ? Numbers.Count(recipe.m_amount) : ""));
                }
                else if (recipe.m_craftingStation == null && recipe.m_repairStation != null && recipe.m_repairStation.gameObject.name == station)
                {
                    if (seen.Add(name)) upgraded.Items.Add(Chip(recipe.m_item, ""));
                }
            }
            if (made.Items.Count > 0) Rows.Add(made);
            if (upgraded.Items.Count > 0) Rows.Add(upgraded);

            // The pieces built near it, as each piece says "Built near" it: found in the catalog,
            // which knows each piece's station already, rather than in every prefab of the game.
            var built = new Row { Title = "Built near it" };
            var catalog = WorldCatalog.Current?.All;
            if (catalog != null)
            {
                foreach (var entry in catalog)
                {
                    if (entry.Kind != Kind.Piece || entry.Stations == null || !entry.Stations.Any(s => s.Name == station)) continue;
                    var piece = (entry.Source as GameObject).OrNull()?.GetComponent<global::Piece>();
                    if (piece == null || !piece.m_enabled) continue;
                    if (seen.Add(entry.Name)) built.Items.Add(Chip(entry.Name, ""));
                }
            }
            if (built.Items.Count > 0) Rows.Add(built);
        }

        /// <summary>
        /// What a trader sells and for how much, a row for each world key its wares wait for
        /// (<see cref="TraderStore"/>): its own list, what mods add as its store opens, and the
        /// game's upgrades sold as keys of the character, each with the game's icon and tooltip and
        /// no page, told bought once the character has it (so told again as its keys change).
        /// </summary>
        private void Sells(Trader trader)
        {
            _keyed = true;
            var (rows, icons) = TraderStore.Rows(trader);
            foreach (var (key, wares) in rows)
            {
                var title = StationWords.Sells(key.Length == 0 ? null : SpawnWords.Once(key, Knowledge.BossOf));
                var row = new Row { Title = title, TitleLink = Knowledge.BossPrefabOf(key), WaitsFor = key.Length > 0 ? key : null };
                foreach (var ware in wares)
                {
                    var amount = TraderWares.Amount(ware);
                    row.Items.Add(ware.Prefab != null ? Chip(ware.Prefab, amount)
                        : new Ingredient { Icon = icons.TryGetValue(ware, out var icon) ? icon : null, Name = ware.Name, Amount = amount, MarkTip = ware.Tip });
                }
                Rows.Add(row);
            }
        }

        /// <summary>A boss's altars: where each stands and what is offered at it.</summary>
        private void SummonedBy(GameObject boss)
        {
            foreach (var summon in Knowledge.Summons().Where(s => s.Boss == boss.name && s.Item != null))
            {
                var row = new Row { Title = StationWords.SummonedAt(summon.Place, summon.OnStands), TitleLink = summon.PlacePrefab };
                row.Items.Add(Chip(summon.Item, Numbers.Count(summon.Count)));
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
                row.Items.Add(Chip(summon.Boss, Numbers.Count(summon.Count)));
            }
            if (row.Items.Count > 0) UseRows.Add(row);
        }
    }
}
