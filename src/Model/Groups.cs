using System.Collections.Generic;

namespace Scry
{
    /// <summary>A group a kind's tab lists entries under, and where it comes among the others.</summary>
    public struct Group
    {
        public string Name;
        public int Order;

        public Group(string name, int order)
        {
            Name = name;
            Order = order;
        }
    }

    /// <summary>
    /// The groups each kind's tab lists its entries in, by what the game itself says of them:
    /// items by their item type, creatures by their faction, pieces by the build menu and tab
    /// they are in, effects and sounds by what plays them.
    /// </summary>
    public static class Groups
    {
        private static readonly Dictionary<string, Group> ItemTypes = new Dictionary<string, Group>
        {
            ["OneHandedWeapon"] = new Group("Weapons", 1),
            ["TwoHandedWeapon"] = new Group("Weapons", 1),
            ["TwoHandedWeaponLeft"] = new Group("Weapons", 1),
            ["Bow"] = new Group("Weapons", 1),
            ["Attach_Atgeir"] = new Group("Weapons", 1),
            ["Shield"] = new Group("Shields", 2),
            ["Helmet"] = new Group("Armour and capes", 3),
            ["Chest"] = new Group("Armour and capes", 3),
            ["Legs"] = new Group("Armour and capes", 3),
            ["Hands"] = new Group("Armour and capes", 3),
            ["Shoulder"] = new Group("Armour and capes", 3),
            ["Utility"] = new Group("Belts and trinkets", 4),
            ["Trinket"] = new Group("Belts and trinkets", 4),
            ["Ammo"] = new Group("Ammo", 5),
            ["AmmoNonEquipable"] = new Group("Ammo", 5),
            ["Consumable"] = new Group("Food and meads", 6),
            ["Material"] = new Group("Materials", 7),
            ["Tool"] = new Group("Tools", 8),
            ["Torch"] = new Group("Tools", 8),
            ["Fish"] = new Group("Fish", 9),
            ["Trophy"] = new Group("Trophies", 10),
        };

        /// <summary>An item by its item type (<c>ItemDrop.ItemData.ItemType</c>, by name); an unknown or odd one is other.</summary>
        public static Group Item(string itemType)
        {
            return itemType != null && ItemTypes.TryGetValue(itemType, out var group) ? group : new Group("Other", 11);
        }

        private static readonly Dictionary<string, Group> Factions = new Dictionary<string, Group>
        {
            ["AnimalsVeg"] = new Group("Animals", 1),
            ["ForestMonsters"] = new Group("Forest monsters", 2),
            ["Undead"] = new Group("Undead", 3),
            ["Demon"] = new Group("Demons", 4),
            ["MountainMonsters"] = new Group("Mountain monsters", 5),
            ["SeaMonsters"] = new Group("Sea monsters", 6),
            ["PlainsMonsters"] = new Group("Plains monsters", 7),
            ["MistlandsMonsters"] = new Group("Mistlands monsters", 8),
            ["DeepNorth"] = new Group("Deep North", 9),
            ["Dverger"] = new Group("Dvergr", 10),
            ["Players"] = new Group("Players and their summons", 12),
            ["PlayerSpawned"] = new Group("Players and their summons", 12),
            ["TrainingDummy"] = new Group("Training dummies", 13),
        };

        /// <summary>
        /// A creature by its faction (<c>Character.Faction</c>, by name); a boss among the bosses
        /// whatever its faction, and a faction a mod adds among the other factions.
        /// </summary>
        public static Group Creature(string faction, bool boss)
        {
            if (boss || faction == "Boss") return new Group("Bosses", 11);
            return faction != null && Factions.TryGetValue(faction, out var group) ? group : new Group("Other factions", 14);
        }

        /// <summary>
        /// A piece by the build menu it is in and the tab there: the hammer's by the tab alone,
        /// another tool's by the tool, and its tab too where it has several. By tool, then by tab.
        /// </summary>
        public static Group Piece(string tool, int toolOrder, bool mainTool, string tab, int tabOrder, int tabCount)
        {
            var name = mainTool ? tab : tabCount <= 1 ? tool : tool + ": " + tab;
            return new Group(name, 1 + toolOrder * 1000 + tabOrder);
        }

        /// <summary>A piece no build menu holds.</summary>
        public static Group InNoMenu => new Group("In no build menu", int.MaxValue);

        private static readonly (Kind Kind, string Name)[] Players =
        {
            (Kind.Creature, "Played by creatures"),
            (Kind.Item, "Played by items"),
            (Kind.Piece, "Played by pieces"),
            (Kind.Resource, "Played by resources"),
            (Kind.Projectile, "Played by projectiles"),
            (Kind.StatusEffect, "Played by status effects"),
        };

        /// <summary>
        /// An effect or sound by the kinds of what plays it, the first of creatures, items, pieces,
        /// resources, projectiles and status effects that does; then anything else; then nothing.
        /// </summary>
        public static Group ByUsers(IEnumerable<Kind> users)
        {
            var any = false;
            var best = int.MaxValue;
            foreach (var kind in users)
            {
                any = true;
                for (var i = 0; i < Players.Length; i++)
                {
                    if (Players[i].Kind == kind && i < best) best = i;
                }
            }
            if (best < Players.Length) return new Group(Players[best].Name, 1 + best);
            return any ? new Group("Played by other things", 1 + Players.Length) : new Group("Played by nothing listed", 2 + Players.Length);
        }
    }
}
