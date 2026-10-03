using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>A piece's facts and what an item is used for.</summary>
    internal sealed partial class Facts
    {
        // ----- Pieces -----

        private void Piece(Piece piece, WearNTear wear)
        {
            Description = CatalogBuilder.Localize(piece.m_description);
            if (piece.m_comfort > 0)
            {
                // SE_Rested.CalculateComfortLevel counts, within 10 m, only the best of each comfort
                // group, and a piece of the same name once.
                Add("Comfort", Numbers.Count(piece.m_comfort));
                Add("Comfort group", piece.m_comfortGroup != global::Piece.ComfortGroup.None
                    ? $"{Word(piece.m_comfortGroup)}: only the best of these within 10 m counts"
                    : "none: a second one within 10 m adds nothing");
                Hooked(HookedRule.Comfort);
            }
            // Furniture, where comfort is sought, says when it gives none.
            else if (piece.m_category == global::Piece.PieceCategory.Furniture) Add("Comfort", "none");
            if (wear != null)
            {
                Add("Health", Numbers.Amount(wear.m_health));
                Add("Material", Word(wear.m_materialType));
                Part("support", () => Support(wear));
                Part("weather", () =>
                {
                    Add("Rain", BuildWords.Rain(wear.m_noRoofWear));
                    Add("Ashlands", BuildWords.Ash(wear.m_ashDamageImmune, wear.m_ashDamageResist));
                    Add("Heavy snow", BuildWords.Snow(wear.m_snowDamageImmune));
                });
                Part("resistances", () => Resists(wear.m_damages));
                Hooked(HookedRule.Wear);
            }

            Part("placement", () => Placement(piece));

            // An upgrade of a station, which must stand near it and apart from its other upgrades.
            var extension = piece.GetComponent<StationExtension>();
            if (extension != null && extension.m_craftingStation != null)
            {
                var upgraded = extension.m_craftingStation.gameObject;
                var apart = piece.m_spaceRequirement > 0f ? $", {Numbers.Amount(piece.m_spaceRequirement)} m from its other upgrades" : "";
                Add("Upgrades", $"{AnyName(upgraded, upgraded.name)}, within {Numbers.Amount(extension.m_maxStationDistance)} m of it{apart}", upgraded.name);
            }

            // Bed.Interact: claiming and sleeping, each needing a roof and 80% cover (CheckExposure).
            if (piece.GetComponent<Bed>() != null)
            {
                Add("Claiming it", BuildWords.BedClaim(0.8f));
                Add("Sleeping in it", BuildWords.BedSleep(0.8f));
            }

            if (piece.m_resources != null && piece.m_resources.Length > 0)
            {
                var station = piece.m_craftingStation != null ? CatalogBuilder.Localize(piece.m_craftingStation.m_name) : "";
                var row = Requirements(station.Length > 0 ? "Built near " + station : "Build cost", piece.m_resources, false);
                row.TitleLink = piece.m_craftingStation != null ? piece.m_craftingStation.gameObject.name : null;
                Rows.Add(row);
                Hooked(HookedRule.Crafting);
            }
            // Free only where some tool builds it; one in no build menu costs nothing because it is never built.
            else if (Knowledge.Tools.ToolsOf(piece.gameObject.name).Count > 0) Add("Build cost", "free");
        }

        /// <summary>
        /// Where it may be placed (<see cref="BuildWords.Placement"/>); the biomes it keeps to, the
        /// pieces it keeps apart from and the one it must stand near, each going to it.
        /// </summary>
        private void Placement(Piece piece)
        {
            var rules = new PlacementRules
            {
                GroundOnly = piece.m_groundOnly || piece.m_groundPiece, CultivatedOnly = piece.m_cultivatedGroundOnly, DirtOnly = piece.m_vegetationGroundOnly,
                OnWater = piece.m_waterPiece, NotInWater = piece.m_noInWater, NotOnWood = piece.m_notOnWood, Level = piece.m_notOnTiltingSurface,
                CeilingOnly = piece.m_inCeilingOnly, NotOnFloor = piece.m_notOnFloor, TeleportArea = piece.m_onlyInTeleportArea, InDungeons = piece.m_allowedInDungeons,
                DeepSnowOnly = piece.m_requireDeepSnow,
            };
            var lines = BuildWords.Placement(rules);
            if (lines.Count > 0) Add("Placed", string.Join(", ", lines));

            if (piece.m_mustConnectTo != null)
            {
                var near = piece.m_mustConnectTo.gameObject;
                Add(BuildWords.Near(piece.m_connectRadius, piece.m_mustBeAboveConnected), AnyName(near, near.name), near.name);
            }
            var apart = BuildWords.KeptApart(piece.m_blockRadius);
            if (apart != null && piece.m_blockingPieces != null)
            {
                var row = new Row { Title = apart };
                foreach (var other in piece.m_blockingPieces.Where(b => b != null).Select(b => b.gameObject.name).Distinct()) row.Items.Add(Chip(other, ""));
                if (row.Items.Count > 0) Rows.Add(row);
            }
            if (piece.m_onlyInBiome != 0) BiomeRow("Placed only in", piece.m_onlyInBiome);
        }

        /// <summary>The build tools a piece is built with, each with the tab it is on there; one alone is a link to the tool.</summary>
        private void BuiltWith(string piece)
        {
            var tools = Knowledge.Tools.ToolsOf(piece);
            if (tools.Count == 0)
            {
                Add("Built with", "no tool: it is in no build menu");
                return;
            }
            if (tools.Count == 1)
            {
                Add("Built with", $"{tools[0].ToolName}, on its {tools[0].Tab} tab", tools[0].Tool);
                return;
            }
            var row = new Row { Title = "Built with" };
            foreach (var tool in tools) row.Items.Add(Chip(tool.Tool, tool.Tab));
            Rows.Add(row);
        }

        /// <summary>What a build tool builds, a row for each of its tabs, in its own order.</summary>
        private void Builds(string tool)
        {
            foreach (var (tab, pieces) in Knowledge.Tools.PiecesOf(tool))
            {
                var row = new Row { Title = $"Builds on its {tab} tab ({Numbers.Count(pieces.Count)})" };
                foreach (var piece in pieces) row.Items.Add(Chip(piece, ""));
                Rows.Add(row);
            }
        }

        /// <summary>
        /// What holds it up: its material's support, read from the game's own
        /// <c>WearNTear.GetMaterialProperties</c> so a mod that changes it shows, and whether it
        /// holds up other pieces at all.
        /// </summary>
        private void Support(WearNTear wear)
        {
            var args = new object[4];
            MaterialProperties.Invoke(wear, args);
            float max = (float)args[0], min = (float)args[1], sideways = (float)args[2], up = (float)args[3];
            Add("Support", BuildWords.Support(max, min, wear.m_noSupportWear));
            Add("Support lost", BuildWords.SupportLoss(sideways, up));
            if (!wear.m_supports) Add("Holds up others", "no");
        }

        private static System.Reflection.MethodInfo _materialProperties;

        private static System.Reflection.MethodInfo MaterialProperties => _materialProperties ?? (_materialProperties = typeof(WearNTear).GetMethod(
            "GetMaterialProperties", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            ?? throw new System.MissingMethodException(nameof(WearNTear), "GetMaterialProperties"));

        /// <summary>
        /// One of the game's own prefabs a mod made buildable, by adding a piece to it that it
        /// leaves switched off on the prefab (MoreVanillaBuildPrefabs does so): what it costs.
        /// </summary>
        private void MadeBuildable(Piece piece)
        {
            var by = BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(BuildPrefabsGuid, out var mod) && mod?.Metadata != null ? mod.Metadata.Name : "a mod";
            // The mod goes to its page where it has one.
            Add("Buildable", "through " + by, EntryOf(EntryKeys.For(Kind.Mod, by)) != null ? EntryKeys.For(Kind.Mod, by) : null);
            if (piece.m_resources == null || piece.m_resources.Length == 0) return;
            var station = piece.m_craftingStation != null ? CatalogBuilder.Localize(piece.m_craftingStation.m_name) : "";
            var row = Requirements(station.Length > 0 ? $"Built through {by} near {station}" : $"Built through {by} with", piece.m_resources, false);
            row.TitleLink = piece.m_craftingStation != null ? piece.m_craftingStation.gameObject.name : null;
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>MoreVanillaBuildPrefabs' plugin id, as it loads ("Loading [MoreVanillaBuildPrefabs 1.5.0] (Searica.Valheim.MoreVanillaBuildPrefabs)").</summary>
        private const string BuildPrefabsGuid = "Searica.Valheim.MoreVanillaBuildPrefabs";

        /// <summary>
        /// What something costs. Each ingredient also says how many more each upgrade needs, but
        /// only for items that can be upgraded: the game fills that number in everywhere, pieces
        /// and single-quality items included, where it means nothing. Upgrade kits are left out, as
        /// the game leaves them out (<see cref="UpgradeKits"/>).
        /// </summary>
        private static Row Requirements(string title, Piece.Requirement[] requirements, bool upgradable)
        {
            var row = new Row { Title = title };
            if (requirements == null) return row;
            foreach (var need in requirements)
            {
                if (need?.m_resItem == null || need.m_upgraderResource) continue;
                var amount = Numbers.Count(need.m_amount);
                if (upgradable && need.m_amountPerLevel > 0) amount += $", +{Numbers.Count(need.m_amountPerLevel)} per quality";
                row.Items.Add(new Ingredient
                {
                    Icon = Icon(need.m_resItem.gameObject), Name = ItemName(need.m_resItem.gameObject), Amount = amount, Prefab = need.m_resItem.gameObject.name,
                });
            }
            return row;
        }

        // ----- What it is used for -----

        /// <summary>What an item is used for, a row for each kind of use and place, each thing it goes into a chip.</summary>
        private void Uses(string item)
        {
            foreach (var group in Knowledge.UsesOf(item))
            {
                var row = new Row { Title = UseTitle(group), TitleLink = group.Place != null && group.Place != "hand" ? group.Place : null };
                if (group.Kind == UseKind.UpgradesPastTop) row.TitleLink = Knowledge.UpgradeStation;
                foreach (var (target, amount) in group.Targets)
                {
                    var prefab = GamePrefabs.Item(target);
                    // A recipe that takes none of it at first needs it only to upgrade what it makes.
                    var name = AnyName(prefab, target);
                    if (amount <= 0 && group.Kind == UseKind.Crafts) name += " (upgrades)";
                    var shown = amount > 0 ? Numbers.Count(amount) : "";
                    row.Items.Add(new Ingredient { Icon = AnyIcon(prefab), Name = name, Amount = shown, Prefab = target });
                }
                if (row.Items.Count > 0) UseRows.Add(row);
            }
            OfferedFor(item);
        }

        private static string UseTitle(UseGroup group)
        {
            var place = group.Place != null && group.Place != "hand" ? AnyName(GamePrefabs.Item(group.Place), group.Place) : null;
            switch (group.Kind)
            {
                case UseKind.Crafts: return place != null ? $"Used to make at {place}" : "Used to make by hand";
                case UseKind.UpgradesPastTop: return "Takes these past their top quality, at " + UpgradeStationName;
                case UseKind.Builds: return place != null ? $"Used to build near {place}" : "Used to build";
                case UseKind.TurnsInto: return place != null ? $"{place} turns it into" : "Turned into";
                case UseKind.Fuels: return "Burnt as fuel by";
                default: return "Eaten by";
            }
        }

        /// <summary>The upgrade station by the name the game shows, or said plainly when no prefab is one.</summary>
        private static string UpgradeStationName => Knowledge.UpgradeStationName ?? "an upgrade station";

        /// <summary>A prefab's name as the game shows it: an item's, a piece's or a creature's, else the prefab's own.</summary>
        private static string AnyName(GameObject prefab, string fallback)
        {
            if (prefab == null) return fallback;
            var token = prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_name
                        ?? prefab.GetComponent<Piece>().OrNull()?.m_name
                        ?? prefab.GetComponent<Character>().OrNull()?.m_name;
            var shown = CatalogBuilder.Localize(token);
            return shown.Length > 0 ? shown : prefab.name;
        }

        private static Sprite AnyIcon(GameObject prefab)
        {
            if (prefab == null) return null;
            var icons = prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_icons;
            if (icons != null && icons.Length > 0) return icons[0];
            var piece = prefab.GetComponent<Piece>();
            return piece != null ? piece.m_icon : null;
        }
    }
}
