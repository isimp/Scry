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

    /// <summary>What gives a status effect: its kind, and how, in the words of the field that names it ("consume", "set", "fire damage").</summary>
    public struct Giver
    {
        public Kind Kind;
        public string How;

        public Giver(Kind kind, string how)
        {
            Kind = kind;
            How = how ?? "";
        }
    }

    /// <summary>What fires a projectile: its kind, the skill of the weapon, and whether only creatures carry that weapon.</summary>
    public struct Shooter
    {
        public Kind Kind;
        public string Skill;
        public bool CarriedByCreature;

        /// <summary>For ammo, what it is ammo for ("$ammo_arrows"), which says more than its own skill.</summary>
        public string AmmoType;

        public Shooter(Kind kind, string skill, bool carriedByCreature, string ammoType = "")
        {
            Kind = kind;
            Skill = skill ?? "";
            CarriedByCreature = carriedByCreature;
            AmmoType = ammoType ?? "";
        }
    }

    /// <summary>
    /// The groups each kind's tab lists its entries in, by what the game itself says of them:
    /// items by their item type, creatures by their faction, pieces by the build menu and tab
    /// they are in, effects and sounds by what they are for (the names of the effect lists that
    /// play them), projectiles by what fires them, status effects by what gives them, and the
    /// rest by what they are there for.
    /// </summary>
    public static class Groups
    {
        private static readonly Dictionary<string, Group> ItemTypes = new Dictionary<string, Group>
        {
            ["Shield"] = new Group("Shields", 20),
            ["Helmet"] = new Group("Helmets", 30),
            ["Chest"] = new Group("Chest armour", 40),
            ["Legs"] = new Group("Leg armour", 50),
            ["Hands"] = new Group("Gloves", 60),
            ["Shoulder"] = new Group("Capes", 70),
            ["Utility"] = new Group("Belts and trinkets", 80),
            ["Trinket"] = new Group("Belts and trinkets", 80),
            ["Ammo"] = new Group("Ammo", 90),
            ["AmmoNonEquipable"] = new Group("Ammo", 90),
            ["Consumable"] = new Group("Food and meads", 100),
            ["Material"] = new Group("Materials", 110),
            ["Tool"] = new Group("Tools", 120),
            ["Torch"] = new Group("Tools", 120),
            ["Fish"] = new Group("Fish", 130),
            ["Trophy"] = new Group("Trophies", 140),
        };

        /// <summary>The game's item types for what is held to fight with, grouped by the skill it trains.</summary>
        private static readonly HashSet<string> WeaponTypes = new HashSet<string>
        {
            "OneHandedWeapon", "TwoHandedWeapon", "TwoHandedWeaponLeft", "Bow", "Attach_Atgeir",
        };

        /// <summary>Weapons by the skill they train (<c>Skills.SkillType</c>, by name): melee, then ranged, then magic, then pickaxes.</summary>
        private static readonly Dictionary<string, Group> WeaponSkills = new Dictionary<string, Group>
        {
            ["Swords"] = new Group("Swords", 1),
            ["Axes"] = new Group("Axes", 2),
            ["Clubs"] = new Group("Clubs", 3),
            ["Knives"] = new Group("Knives", 4),
            ["Spears"] = new Group("Spears", 5),
            ["Polearms"] = new Group("Polearms", 6),
            ["Unarmed"] = new Group("Fists", 7),
            ["Bows"] = new Group("Bows", 8),
            ["Crossbows"] = new Group("Crossbows", 9),
            ["ElementalMagic"] = new Group("Staffs", 10),
            ["BloodMagic"] = new Group("Staffs", 10),
            ["Pickaxes"] = new Group("Pickaxes", 11),
        };

        /// <summary>
        /// An item by its item type (<c>ItemDrop.ItemData.ItemType</c>, by name), a weapon by the skill
        /// it trains; an unknown or odd one is other.
        /// </summary>
        /// <param name="carriedByCreature">A creature carries it (its attacks and gear are items).</param>
        /// <param name="obtainable">A recipe makes it, or something drops, holds, sells or places it.</param>
        public static Group Item(string itemType, string skill = null, bool carriedByCreature = false, bool obtainable = true)
        {
            if (carriedByCreature && !obtainable) return CarriedByCreatures;
            if (itemType != null && WeaponTypes.Contains(itemType))
            {
                return skill != null && WeaponSkills.TryGetValue(skill, out var weapons) ? weapons : new Group("Other weapons", 12);
            }
            return itemType != null && ItemTypes.TryGetValue(itemType, out var group) ? group : new Group("Other", 150);
        }

        /// <summary>What only creatures have: their attacks and gear no player can get, after every other group.</summary>
        public static Group CarriedByCreatures => new Group("Carried by creatures", 160);

        /// <summary>One item's type, in the words of its group ("Capes" holds a "Cape").</summary>
        private static readonly Dictionary<string, string> ItemTypeNames = new Dictionary<string, string>
        {
            ["OneHandedWeapon"] = "One-handed weapon",
            ["TwoHandedWeapon"] = "Two-handed weapon",
            ["TwoHandedWeaponLeft"] = "Two-handed weapon",
            ["Bow"] = "Bow",
            ["Attach_Atgeir"] = "Weapon",
            ["Shield"] = "Shield",
            ["Helmet"] = "Helmet",
            ["Chest"] = "Chest armour",
            ["Legs"] = "Leg armour",
            ["Hands"] = "Gloves",
            ["Shoulder"] = "Cape",
            ["Utility"] = "Belt or trinket",
            ["Trinket"] = "Belt or trinket",
            ["Ammo"] = "Ammo",
            ["AmmoNonEquipable"] = "Ammo",
            ["Consumable"] = "Food or mead",
            ["Material"] = "Material",
            ["Tool"] = "Tool",
            ["Torch"] = "Torch",
            ["Fish"] = "Fish",
            ["Trophy"] = "Trophy",
        };

        /// <summary>An item's type as its facts tell it, in the words of the group it is listed under; an odd one by the game's name.</summary>
        public static string ItemTypeName(string itemType)
        {
            return itemType != null && ItemTypeNames.TryGetValue(itemType, out var name) ? name : itemType ?? "";
        }

        /// <summary>A faction as a creature's facts tell it: the name of its group, whatever the creature is.</summary>
        public static string FactionName(string faction)
        {
            if (faction == "Boss") return "Bosses";
            return faction != null && Factions.TryGetValue(faction, out var group) ? group.Name : faction ?? "";
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
        public static Group Piece(string tool, int toolOrder, bool mainTool, string tab, int tabOrder)
        {
            return mainTool ? new Group(tab, 1 + tabOrder) : new Group(tool, 1 + toolOrder * 1000);
        }

        /// <summary>A mod, by whether it adds to the game, only hooks into its drops or spawning, or neither.</summary>
        public static Group Mod(bool adds, bool hooks)
        {
            if (adds) return new Group("Adding to the game", 1);
            return hooks ? new Group("Hooking into what Scry tells", 2) : new Group("Neither, as far as Scry sees", 3);
        }

        /// <summary>A raid by what starts it: the raid roll or its own timer, a boss being fought, or something else.</summary>
        public static Group Raid(RaidRole role)
        {
            switch (role)
            {
                case RaidRole.Raid: return new Group("Raids", 1);
                case RaidRole.BossFight: return new Group("Boss fights", 2);
                default: return new Group("Started by something else", 3);
            }
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
            ("Weather and ambience", 10, new[] { "thunder", "lightning", "weather", "ambience" }),
            ("Building, crafting and using", 7, new[] { "place", "build", "craft", "repair", "upgrade", "fuel", "add", "produce", "done", "open", "close", "lever", "switch", "toggle", "activate", "lock", "write", "eat", "consume", "pick", "load", "equip", "cook", "grow", "sell", "buy", "trade", "firework", "ping", "tap", "select", "move", "drop", "nibble", "connect", "sail", "arm", "enter", "leave", "tab", "group", "button", "inventory" }),
        };

        private static readonly Group AnimationSounds = new Group("Animation sounds", 8);
        private static readonly Group StatusEffects = new Group("Status effects", 9);
        private static readonly Group Interface = new Group("Interface", 11);
        private static readonly Group OtherPurpose = new Group("Other", 12);
        private static readonly Group NothingFound = new Group("Played by nothing found", 14);

        /// <summary>An effect or sound nothing was found to play, but a location or dungeon room names, once they are read.</summary>
        public static readonly Group EffectsInLocations = new Group("In locations", 13);

        /// <summary>A projectile nothing was found to fire, but a location or dungeon room names, once they are read.</summary>
        public static readonly Group ProjectilesInLocations = new Group("In locations", 6);

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
            var best = new Group("Other", 7);
            foreach (var shooter in shooters)
            {
                Group group;
                if (shooter.Kind == Kind.Creature || (shooter.Kind == Kind.Item && shooter.CarriedByCreature)) group = new Group("Creatures", 5);
                else if (shooter.Kind == Kind.Piece) group = new Group("Traps and turrets", 4);
                else if (shooter.Kind != Kind.Item) continue;
                else if (shooter.AmmoType.Length > 0) group = Ammo(shooter.AmmoType);
                else if (shooter.Skill == "Bows" || shooter.Skill == "Crossbows") group = new Group("Bows and crossbows", 1);
                else if (shooter.Skill == "ElementalMagic" || shooter.Skill == "BloodMagic") group = new Group("Staffs", 2);
                else group = new Group("Thrown", 3);
                if (group.Order < best.Order) best = group;
            }
            return best;
        }

        // ----- Status effects by where they come from -----

        /// <summary>
        /// A status effect by what gives it and how, as the links noted it: a guardian power,
        /// food and meads, a set, worn equipment, damage of a kind, an attack or a creature, a
        /// piece, anything else; given by nothing Scry found (the game's own wet, cold and rested,
        /// given from its code, and any a mod gives the same way), it goes under that name. Given
        /// several ways, it goes under the first.
        /// </summary>
        public static Group StatusEffect(IEnumerable<Giver> givers)
        {
            var best = new Group("Given by nothing found", 9);
            foreach (var giver in givers)
            {
                var how = (giver.How ?? "").ToLowerInvariant();
                Group group;
                if (how.Contains("guardian")) group = new Group("Guardian powers", 1);
                else if (how.Contains("consume")) group = new Group("Food and meads", 2);
                else if (how.Contains("set")) group = new Group("Set bonuses", 3);
                else if (how.Contains("equip")) group = new Group("Worn equipment", 4);
                else if (how.Contains("damage")) group = new Group("From damage", 5);
                else if (giver.Kind == Kind.Creature || how.Contains("attack") || how.Contains("hit")) group = new Group("From attacks and creatures", 6);
                else if (giver.Kind == Kind.Piece) group = new Group("From pieces", 7);
                else group = new Group("Other", 8);
                if (group.Order < best.Order) best = group;
            }
            return best;
        }

        /// <summary>
        /// How something gives a status effect, as it is shown with the effect: in the words the
        /// item's own facts use ("When worn", "When used", "Set bonus", "On hit"), not the game's
        /// field names, which the groups above read.
        /// </summary>
        public static string GiverWords(string how)
        {
            switch ((how ?? "").Trim().ToLowerInvariant())
            {
                case "equip": return "when worn";
                case "consume": return "when used";
                case "set": return "set bonus";
                case "attack": return "on hit";
                default: return how ?? "";
            }
        }

        /// <summary>Ammo by what fires it: a turret's or a ballista's with the traps, arrows and bolts with the bows, anything else thrown.</summary>
        private static Group Ammo(string ammoType)
        {
            var type = ammoType.ToLowerInvariant();
            if (type.Contains("turret") || type.Contains("ballista") || type.Contains("catapult")) return new Group("Traps and turrets", 4);
            if (type.Contains("arrow") || type.Contains("bolt")) return new Group("Bows and crossbows", 1);
            return new Group("Thrown", 3);
        }

        /// <summary>
        /// Puts a projectile nothing else was found to fire in the group of the projectile that
        /// spawns it (a cluster bomb's splinters, a meteor's rocks), following chains of them.
        /// One with a group of its own keeps it.
        /// </summary>
        public static void FollowSpawners(IList<Entry> catalog, string spawnedBy, string other)
        {
            var projectiles = new Dictionary<string, Entry>();
            foreach (var entry in catalog)
            {
                if (entry.Kind == Kind.Projectile && !projectiles.ContainsKey(entry.Name)) projectiles[entry.Name] = entry;
            }
            for (var round = 0; round < 8; round++)
            {
                var changed = false;
                foreach (var entry in projectiles.Values)
                {
                    if (entry.Group != other) continue;
                    foreach (var link in entry.Links)
                    {
                        if (link.Group != spawnedBy || !projectiles.TryGetValue(link.Target, out var parent) || parent == entry || parent.Group == other) continue;
                        entry.Group = parent.Group;
                        entry.GroupOrder = parent.GroupOrder;
                        changed = true;
                        break;
                    }
                }
                if (!changed) return;
            }
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
