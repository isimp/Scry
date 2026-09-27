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

    /// <summary>What fires a projectile: its kind, the skill of the weapon, and whether only creatures carry that weapon.</summary>
    public struct Shooter
    {
        public Kind Kind;
        public string Skill;
        public bool CarriedByCreature;

        public Shooter(Kind kind, string skill, bool carriedByCreature)
        {
            Kind = kind;
            Skill = skill ?? "";
            CarriedByCreature = carriedByCreature;
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
            ["Helmet"] = new Group("Helmets", 3),
            ["Chest"] = new Group("Chest armour", 4),
            ["Legs"] = new Group("Leg armour", 5),
            ["Hands"] = new Group("Gloves", 6),
            ["Shoulder"] = new Group("Capes", 7),
            ["Utility"] = new Group("Belts and trinkets", 8),
            ["Trinket"] = new Group("Belts and trinkets", 8),
            ["Ammo"] = new Group("Ammo", 9),
            ["AmmoNonEquipable"] = new Group("Ammo", 9),
            ["Consumable"] = new Group("Food and meads", 10),
            ["Material"] = new Group("Materials", 11),
            ["Tool"] = new Group("Tools", 12),
            ["Torch"] = new Group("Tools", 12),
            ["Fish"] = new Group("Fish", 13),
            ["Trophy"] = new Group("Trophies", 14),
        };

        /// <summary>An item by its item type (<c>ItemDrop.ItemData.ItemType</c>, by name); an unknown or odd one is other.</summary>
        public static Group Item(string itemType)
        {
            return itemType != null && ItemTypes.TryGetValue(itemType, out var group) ? group : new Group("Other", 15);
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
        /// A piece by the build menu it is in: the hammer's by its tab, any other tool's (the
        /// cultivator, the serving tray, a mod's own, such as PlanBuild's with its many tabs) all
        /// in one group. The hammer's tabs first, then the tools.
        /// </summary>
        public static Group Piece(string tool, int toolOrder, bool mainTool, string tab, int tabOrder, int tabCount)
        {
            return mainTool ? new Group(tab, 1 + tabOrder) : new Group(tool, 1 + toolOrder * 1000);
        }

        /// <summary>A piece no build menu holds.</summary>
        public static Group InNoMenu => new Group("In no build menu", int.MaxValue);

        // ----- Effects and sounds by what they are for -----

        /// <summary>
        /// What an effect list is for, by words in its field's name, each purpose with the words
        /// that mark it, in the order a name is tried against them: "unsummon" is a death before it
        /// is a summoning, "shieldHit" a hit before anything else.
        /// </summary>
        private static readonly (string Name, int Order, string[] Words)[] PurposeWords =
        {
            ("Deaths and destruction", 3, new[] { "death", "destroy", "destruction", "break", "despawn", "drown", "unsummon", "remove", "gib" }),
            ("Hits and blocks", 2, new[] { "hit", "impact", "block", "parry", "crit", "backstab", "bullseye", "perfect", "stagger", "damage" }),
            ("Attacks and swings", 1, new[] { "attack", "trigger", "trail", "shoot", "reload", "charge", "hold", "punch", "warmup", "chain", "start" }),
            ("Spawning and summoning", 4, new[] { "spawn", "summon", "birth", "hatch", "wakeup", "initiate" }),
            ("Creature calls", 5, new[] { "idle", "alert", "taunt", "pet", "tamed", "soothe", "love", "pheromone", "greet", "talk", "goodbye", "speak", "noise", "sleep" }),
            ("Footsteps and movement", 6, new[] { "jump", "slide", "water", "dodge", "flying", "step", "leg", "moving", "walk", "tareffect", "lava" }),
            ("Weather", 10, new[] { "thunder", "lightning" }),
            ("Building, crafting and using", 7, new[] { "place", "build", "craft", "repair", "upgrade", "fuel", "add", "produce", "done", "open", "close", "lever", "switch", "toggle", "activate", "lock", "write", "eat", "consume", "pick", "load", "equip", "cook", "grow", "sell", "buy", "trade", "firework", "ping", "tap", "select", "move", "drop", "nibble", "connect", "sail", "arm", "enter", "leave", "tab", "group", "button", "inventory" }),
        };

        private static readonly Group AnimationSounds = new Group("Animation sounds", 8);
        private static readonly Group StatusEffects = new Group("Status effects", 9);
        private static readonly Group Interface = new Group("Interface", 11);
        private static readonly Group OtherPurpose = new Group("Other", 12);
        private static readonly Group NothingFound = new Group("Played by nothing found", 13);

        /// <summary>
        /// What an effect or sound is for: by the names of the effect lists it is in (a status
        /// effect's marked "se:", the interface's "ui:"), and whether it is a footstep or played by
        /// an animation's events. Used for several things, it goes under the first of them.
        /// </summary>
        public static Group Purpose(IEnumerable<string> fields, bool footstep, bool animation)
        {
            var best = footstep ? new Group("Footsteps and movement", 6) : animation ? AnimationSounds : NothingFound;
            foreach (var field in fields)
            {
                var purpose = PurposeOf(field);
                if (purpose.Order < best.Order) best = purpose;
            }
            return best;
        }

        private static Group PurposeOf(string field)
        {
            if (string.IsNullOrEmpty(field)) return OtherPurpose;
            if (field.StartsWith("se:", System.StringComparison.Ordinal)) return StatusEffects;
            if (field.StartsWith("ui:", System.StringComparison.Ordinal)) return Interface;
            var name = field.StartsWith("m_", System.StringComparison.Ordinal) ? field.Substring(2) : field;
            name = name.ToLowerInvariant();
            foreach (var (purpose, order, words) in PurposeWords)
            {
                foreach (var word in words)
                {
                    if (name.Contains(word)) return new Group(purpose, order);
                }
            }
            return OtherPurpose;
        }

        // ----- Projectiles by who fires them -----

        /// <summary>
        /// A projectile by what fires it: a player's weapon by its skill (bows and crossbows,
        /// staffs, anything else thrown), then a trap or turret, then a creature (by its attack or
        /// an item only creatures carry). Fired by a weapon and a creature, it goes with the weapon.
        /// </summary>
        public static Group Projectile(IEnumerable<Shooter> shooters)
        {
            var best = new Group("Other", 6);
            foreach (var shooter in shooters)
            {
                Group group;
                if (shooter.Kind == Kind.Creature || (shooter.Kind == Kind.Item && shooter.CarriedByCreature)) group = new Group("Creatures", 5);
                else if (shooter.Kind == Kind.Piece) group = new Group("Traps and turrets", 4);
                else if (shooter.Kind != Kind.Item) continue;
                else if (shooter.Skill == "Bows" || shooter.Skill == "Crossbows") group = new Group("Bows and crossbows", 1);
                else if (shooter.Skill == "ElementalMagic" || shooter.Skill == "BloodMagic") group = new Group("Staffs", 2);
                else group = new Group("Thrown", 3);
                if (group.Order < best.Order) best = group;
            }
            return best;
        }

        // ----- Other by role -----

        /// <summary>What something that is none of the other kinds is there for, by what it has.</summary>
        public static Group Role(PrefabTraits traits)
        {
            if (traits.HasSpawner || traits.IsAltar) return new Group("Spawners and altars", 1);
            if (traits.HasRagdoll) return new Group("Remains", 2);
            if (traits.HasContainer) return new Group("Chests and containers", 3);
            if (traits.IsUsable) return new Group("Things you can use", 4);
            var visible = traits.HasRenderer || traits.HasParticles || traits.HasLight;
            if (visible && traits.HasSolidCollider) return new Group("Scenery", 5);
            if (visible) return new Group("Decoration", 6);
            return new Group("Nothing to show", 7);
        }
    }
}
