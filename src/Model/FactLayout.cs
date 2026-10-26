using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What a page tells besides its labelled facts and rows: where it comes from, where it is found, its biomes, what plays it, what it is used for, and the mods hooking into it.</summary>
    internal enum FactBlock
    {
        Where,
        FoundIn,
        Biomes,
        Users,
        Uses,
        Hooks,
    }

    /// <summary>One thing a topic shows, by its place among the facts read: a labelled fact, a row, or a block; with the facts told under it as notes.</summary>
    internal sealed class FactBit
    {
        public int Pair = -1;
        public int Row = -1;
        public FactBlock? Block;

        /// <summary>A group of the entry's links shown in this topic rather than under Linked (<see cref="LinkWords.GivenBy"/> and the like); null for none.</summary>
        public string LinkGroup;
        public readonly List<int> Notes = new List<int>();
    }

    /// <summary>A topic of the page: its small heading (none for the first, the headline tiles), its tiles and what follows them.</summary>
    internal sealed class FactTopicPlan
    {
        /// <summary>What the topic is about, as the layout names it ("loot", "fight", "lives"), whether or not it has a heading.</summary>
        public string Id;
        public string Heading;
        public readonly List<int> Tiles = new List<int>();
        public readonly List<FactBit> Bits = new List<FactBit>();
    }

    /// <summary>
    /// How In the game is laid out for a kind: topics in a fixed order, each under a small
    /// heading; the headline numbers as tiles, a set of them chosen by what the thing is (a
    /// weapon's damage, armour's armour, food's food); what qualifies a row (its drops' stars, the
    /// world's rate) told under it as notes. A fact goes to a topic by its label, else by the
    /// part of the reader that told it, else to More at the end, so every fact read is shown
    /// once whatever reader told it. Within a topic its facts come in the order read, then
    /// its rows, then its blocks. A kind not laid out yet has no plan, and keeps its old page.
    /// </summary>
    internal static class FactLayout
    {
        private sealed class KindPlan
        {
            public (string Id, string Heading)[] Topics;
            /// <summary>Each topic's tiles: sets of labels, the first whose key fact is told taken (one with no key always).</summary>
            public readonly List<(string Topic, string Key, string[] Labels)> Tiles = new List<(string, string, string[])>();
            public readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly List<(Func<string, bool> Matches, string Topic)> Starts = new List<(Func<string, bool>, string)>();
            public readonly Dictionary<string, string> Parts = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Notes = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<FactBlock, string> Blocks = new Dictionary<FactBlock, string>();
            /// <summary>Topic orders for what a page tells, the first whose key fact or row is told taken over <see cref="Topics"/>' own.</summary>
            public readonly List<(string Key, string[] Ids)> Orders = new List<(string, string[])>();
            /// <summary>What leads a topic, in this order, ahead of the rest in the order placed: a fact's label, a row's title or a group of links.</summary>
            public readonly Dictionary<string, string[]> Leads = new Dictionary<string, string[]>(StringComparer.Ordinal);
            /// <summary>The topics whose rows stand first, ahead of what leads them: a piece's cost, titled by its station, before its tool.</summary>
            public readonly HashSet<string> RowsFirst = new HashSet<string>(StringComparer.Ordinal);
            /// <summary>Groups of the entry's links shown in a topic, not under Linked, by the topic.</summary>
            public readonly Dictionary<string, string> LinkGroups = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private const string More = "more";

        private static readonly Dictionary<Kind, KindPlan> Kinds = new Dictionary<Kind, KindPlan>
        {
            [Kind.Creature] = WithChain(Creature()),
            [Kind.Item] = WithChain(Item()),
            [Kind.Piece] = WithChain(Piece()),
            [Kind.Resource] = WithChain(Resource()),
            [Kind.Projectile] = WithChain(Projectile()),
            [Kind.Location] = WithChain(Location()),
            [Kind.Raid] = WithChain(Raid()),
            [Kind.Biome] = WithChain(Biome()),
            [Kind.StatusEffect] = WithChain(StatusEffect()),
            [Kind.Mod] = WithChain(Mod()),
            [Kind.Skill] = WithChain(Skill()),
            [Kind.Spawner] = WithChain(Spawner()),
            [Kind.Trader] = WithChain(Trader()),
        };

        /// <summary>A fish's plan, an item's with its own topics (<see cref="FishPage"/>), taken for an item whose page tells how deep it swims.</summary>
        private static readonly KindPlan FishPlan = WithChain(Fish());

        /// <summary>A kind's plan with the chains it is a step of right under its tiles, without a heading of their own (<see cref="ChainBook"/>).</summary>
        private static KindPlan WithChain(KindPlan plan)
        {
            var topics = new List<(string, string)>(plan.Topics);
            topics.Insert(1, ("chain", null));
            plan.Topics = topics.ToArray();
            for (var i = 0; i < plan.Orders.Count; i++)
            {
                var ids = new List<string>(plan.Orders[i].Ids);
                ids.Insert(1, "chain");
                plan.Orders[i] = (plan.Orders[i].Key, ids.ToArray());
            }
            Part(plan, "chain", "chain");
            return plan;
        }

        private static KindPlan Creature()
        {
            var plan = new KindPlan
            {
                Topics = new[]
                {
                    ("overview", (string)null), ("fight", "Fight"), ("loot", "Loot"), ("lives", "Where it lives"), ("taming", "Taming and breeding"),
                    ("riding", "Riding"), ("senses", "Senses and behaviour"), ("after", "After it falls"), (More, "More"), ("hooks", null),
                },
            };
            // A boss: how to summon it (under where it lives) and what its fall opens, before its loot.
            Order(plan, "Boss", "overview", "fight", "lives", "after", "loot", "senses");
            // A tameable one: its taming and riding first.
            Order(plan, "Tames in", "overview", "taming", "riding", "fight", "loot", "lives", "senses");
            Tile(plan, "overview", null, "Health", "Faction", "Moves", "Tameable", "Boss");
            Tile(plan, "senses", null, "Sees", "Hears", "Turns on you", "Fire");
            Label(plan, "fight", "Attacks", CombatWords.StarsTitle, "Boss fight", "Fights", "Damage it takes", "Trains");
            Label(plan, "senses", "Gives up chasing", "Flees", "Leaves alone", "When enemies are passive");
            Label(plan, "loot", "Drops");
            Label(plan, "taming", "Tames in", "Fed for", "Eats");
            Label(plan, "after", "Forsaken power", "On its boss stone");
            Label(plan, More, "Not shown");
            plan.Starts.Add((l => l.StartsWith("Seen dropping", StringComparison.Ordinal), "loot"));
            Part(plan, "fight", "attacks", "weak spots", "resistances", "what to bring");
            // What it deals and what stars add to it first, then what hurts it.
            Lead(plan, "fight", CombatWords.AttacksTitle, CombatWords.StarsTitle);
            Links(plan, "fight", LinkWords.Carries, LinkWords.StatusEffects);
            Part(plan, "senses", "behaviour");
            Part(plan, "taming", "breeding", "growing up");
            Part(plan, "riding", "riding");
            Part(plan, "after", "after it falls");
            Part(plan, "lives", "summoning");
            Part(plan, "loot", "world settings");
            plan.Notes["Drops with stars"] = "Drops";
            plan.Notes["In this world"] = "Drops";
            plan.Blocks[FactBlock.Where] = "lives";
            plan.Blocks[FactBlock.FoundIn] = "lives";
            plan.Blocks[FactBlock.Biomes] = "lives";
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Uses] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan Item()
        {
            var plan = new KindPlan
            {
                Topics = new[]
                {
                    ("overview", (string)null), ("does", "What it does"), ("hatching", "Hatching"), ("fight", "Fight"), ("wearing", "Wearing"), ("food", "Food"),
                    ("making", "Making"), ("from", "Where it comes from"), ("uses", "What it is used for"), ("mods", "Set by mods"), (More, "More"), ("hooks", null),
                },
            };
            Tile(plan, "overview", "Damage", "Damage", "Weight", "Quality", "Durability");
            Tile(plan, "overview", "Armour", "Armour", "Weight", "Quality", "Movement");
            Tile(plan, "overview", "Food", "Food", "Heals", "Lasts", "Weight");
            Tile(plan, "overview", null, "Type", "Weight", "Stacks to", "Worth");
            // The table by quality goes with what it is about, by the part that told it: a weapon's fight, armour's wearing.
            Label(plan, "fight", "Damage", CombatWords.WeaponAttacksTitle, "Secondary attack", "Block", "Block force", "Parry bonus", "Backstab", "Knockback", "Each attack costs", "Drawing costs",
                CombatWords.FullDraw, CombatWords.Reloads, "Summons at once", "Summons hit",
                "At full adrenaline", "On hit", ItemWords.DamageTaken(false), ItemWords.ResistsNothing(false));
            Label(plan, "wearing", "Armour", "Movement", "When worn", "Set bonus", "Other changes while worn", ItemWords.DamageTaken(true), ItemWords.ResistsNothing(true));
            Label(plan, "food", "Food", "Heals", "Lasts", "When used");
            Label(plan, "hatching", "Hatches into", "Hatches in", "Hatches when");
            // What a trophy, a key, a fish, a bait or a building tool is for.
            Label(plan, "does", "On its boss stone", "Opens", "Each use costs", "Each use wears", "Harvests within", "With a fish on", "Reeling in");
            Part(plan, "does", "builds", "fishing", "bait");
            Label(plan, "making", "Type", "Weight", "Quality", "Durability", "Repaired at", "Upgrades need", "Skill", "Tool tier", "Stacks to", "Worth", "Portals");
            Label(plan, More, "Not shown");
            plan.Starts.Add((l => l.EndsWith(" damage causes", StringComparison.Ordinal), "fight"));
            Part(plan, "fight", "item stats");
            Part(plan, "wearing", "gear", "resistances", "seen equipped");
            Part(plan, "making", "recipe", "made at stations");
            Part(plan, "mods", "mod settings");
            // A weapon's damage first, by attack and by quality, then what attacking costs; its blocking after.
            Lead(plan, "fight", CombatWords.WeaponAttacksTitle, ItemWords.ByQualityTitle, "Each attack costs", "Drawing costs", "Secondary attack");
            // Armour by quality first, then its set.
            Lead(plan, "wearing", ItemWords.ByQualityTitle, "Set bonus", LinkBook.SameSet);
            // A set's other pieces, led right under its bonus below.
            Links(plan, "wearing", LinkBook.SameSet);
            plan.Blocks[FactBlock.Where] = "from";
            plan.Blocks[FactBlock.FoundIn] = "from";
            plan.Blocks[FactBlock.Biomes] = "from";
            plan.Blocks[FactBlock.Uses] = "uses";
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        /// <summary>
        /// A fish: what it is as tiles, then where it lives (its water, where it is never placed,
        /// its biomes and sources), when it comes and leaps, how it finds a bait, its fight by
        /// stars and what a catch brings; the rest as an item's.
        /// </summary>
        private static KindPlan Fish()
        {
            var item = Item();
            var plan = new KindPlan
            {
                Topics = new[]
                {
                    ("overview", (string)null), ("lives", "Where it lives"), ("when", "When"), ("biting", "Biting"), ("fishfight", "The fight"), ("catch", "What a catch gives"),
                    ("does", "What it does"), ("making", "Making"), ("from", "Where it comes from"), ("uses", "What it is used for"), ("mods", "Set by mods"), (More, "More"), ("hooks", null),
                },
            };
            foreach (var pair in item.Labels) plan.Labels[pair.Key] = pair.Value;
            foreach (var pair in item.Parts) plan.Parts[pair.Key] = pair.Value;
            foreach (var pair in item.Blocks) plan.Blocks[pair.Key] = pair.Value;
            Tile(plan, "overview", null, FishWords.Swims, FishWords.GroupsOf, FishWords.Stars);
            Label(plan, "lives", FishWords.Water, FishWords.Never, FishWords.HowMany);
            Label(plan, "when", FishWords.Comes, FishWords.Leaps);
            Label(plan, "biting", FishWords.FindsFloat, FishWords.SwimsOff, FishWords.WrongBait);
            Label(plan, "fishfight", FishWords.FightTitle, "Reeling in", FishWords.Rests, FishWords.Line);
            Label(plan, "catch", FishWords.OnLand);
            Part(plan, "biting", "fishing");
            Lead(plan, "biting", FishWords.BitesOn);
            Part(plan, "catch", "fish catch");
            RowsFirst(plan, "catch");
            Lead(plan, "fishfight", FishWords.FightTitle, "Reeling in");
            plan.Blocks[FactBlock.Where] = "lives";
            plan.Blocks[FactBlock.FoundIn] = "lives";
            plan.Blocks[FactBlock.Biomes] = "lives";
            return plan;
        }

        private static KindPlan Piece()
        {
            var plan = new KindPlan
            {
                Topics = new[]
                {
                    ("overview", (string)null), ("does", "What it does"), ("building", "Building"), ("resting", "Resting"), ("standing", "Standing"),
                    ("from", "Where it comes from"), (More, "More"), ("hooks", null),
                },
            };
            Tile(plan, "overview", "Slots", "Slots", "Health", "Material");
            Tile(plan, "overview", "Building reach", "Building reach", "Health", "Material");
            Tile(plan, "overview", null, "Health", "Comfort", "Material", "Support");
            Label(plan, "building", "Build cost", "Built with", "Placed", "Upgrades", "Claiming it");
            // As the hammer's menu reads: the cost first, then the tool, then where it stands.
            RowsFirst(plan, "building");
            Lead(plan, "building", "Built with");
            // What it is for: a station's making, a machine's work, an area's, what it holds and opens with.
            Label(plan, "does", "Slots", "Opened with");
            Part(plan, "does", "station", "machines", "areas", "chest");
            // What feeds being rested: comfort and sleeping.
            Label(plan, "resting", "Comfort", "Comfort group", "Sleeping in it");
            Label(plan, More, "Not shown");
            Part(plan, "building", "piece", "placement", "built with", "build cost");
            Part(plan, "standing", "support", "weather", "resistances");
            plan.Blocks[FactBlock.Where] = "from";
            plan.Blocks[FactBlock.FoundIn] = "from";
            plan.Blocks[FactBlock.Biomes] = "from";
            plan.Blocks[FactBlock.Uses] = More;
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan Resource()
        {
            var plan = new KindPlan
            {
                Topics = new[] { ("overview", (string)null), ("gathering", "Gathering"), ("growing", "Growing"), ("lives", "Where it lives"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Health", "Needs tool tier");
            // A plant's growing, apart from what gathering it gives.
            Label(plan, "growing", "Grows in", "Needs", "Tolerates", GatherWords.GrowsInto(false), GatherWords.GrowsInto(true));
            Label(plan, More, "Not shown");
            Part(plan, "gathering", "resource");
            plan.Blocks[FactBlock.Where] = "lives";
            plan.Blocks[FactBlock.FoundIn] = "lives";
            plan.Blocks[FactBlock.Biomes] = "lives";
            plan.Blocks[FactBlock.Uses] = More;
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan Projectile()
        {
            var plan = new KindPlan
            {
                Topics = new[] { ("overview", (string)null), ("from", "Where it comes from"), ("hit", "Hit"), ("flight", "Flight"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Own damage", "Flies for");
            Label(plan, "flight", "Falls", "Slows", "Bounces", "Leaves");
            Label(plan, More, "Not shown");
            Part(plan, "hit", "projectile");
            plan.Blocks[FactBlock.Where] = "from";
            plan.Blocks[FactBlock.FoundIn] = "from";
            plan.Blocks[FactBlock.Biomes] = "from";
            plan.Blocks[FactBlock.Uses] = More;
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan Location()
        {
            var plan = new KindPlan
            {
                Topics = new[] { ("overview", (string)null), ("contents", "Contents"), ("layout", "Layout"), ("music", "Music"), ("placement", "Placement"), (More, "More"), ("hooks", null) },
            };
            Order(plan, "Built into", "overview", "layout");
            Tile(plan, "overview", null, "Biome", "Per world", "Size");
            Label(plan, "placement", "Role", "Not before");
            Label(plan, "layout", "Building", "Doorways", "Built into");
            Label(plan, "contents", "What it holds", "What its rooms hold", "Stars at its spawn points");
            Label(plan, "music", "Music");
            Label(plan, More, "Not shown");
            // What a dungeon's rooms hold, read with its example: "Its rooms hold", "Loot in its rooms" and the like.
            plan.Starts.Add((l => l.StartsWith("Its rooms", StringComparison.Ordinal) || l.Contains(" in its rooms"), "contents"));
            Part(plan, "placement", "placement");
            Part(plan, "layout", "room", "dungeon");
            Part(plan, "contents", "location");
            plan.Blocks[FactBlock.Biomes] = "placement";
            plan.Blocks[FactBlock.Where] = "contents";
            plan.Blocks[FactBlock.FoundIn] = "layout";
            plan.Blocks[FactBlock.Uses] = More;
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan Raid()
        {
            var plan = new KindPlan
            {
                Topics = new[] { ("overview", (string)null), ("comes", "What comes"), ("when", "When"), ("music", "Music"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Lasts", "Weather", "Comes for");
            Label(plan, "when", "On the table", "Rolled", "Also rolled", "Ends with", "When");
            Label(plan, "music", "Music");
            Label(plan, More, "Not shown");
            // What it brings is told creature by creature, each by its name.
            Part(plan, "comes", "raid");
            Lead(plan, "comes", RaidWords.BringsTitle);
            plan.Blocks[FactBlock.Biomes] = "when";
            plan.Blocks[FactBlock.Where] = More;
            plan.Blocks[FactBlock.FoundIn] = More;
            plan.Blocks[FactBlock.Uses] = More;
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan Biome()
        {
            var plan = new KindPlan
            {
                Topics = new[] { ("overview", (string)null), ("there", "What is there"), ("weather", "Weather"), ("music", "Music"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Puts on you", "Creatures", "Places");
            Label(plan, "weather", BiomeWords.WeathersTitle);
            Links(plan, "weather", LinkWords.StatusEffects);
            Label(plan, "music", BiomeWords.MusicTitle);
            Label(plan, More, "Not shown");
            Part(plan, "there", "biome");
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan StatusEffect()
        {
            var plan = new KindPlan
            {
                // What it does first, then how you get it.
                Topics = new[] { ("overview", (string)null), ("changes", "What it does"), ("given", "How you get it"), ("others", "With other effects"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Lasts");
            Links(plan, "given", LinkWords.GivenBy);
            Label(plan, "others", "While it lasts, cannot take");
            Label(plan, More, "Not shown");
            Part(plan, "changes", "status effect");
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan Mod()
        {
            var plan = new KindPlan
            {
                Topics = new[]
                {
                    ("overview", (string)null), ("adds", "What it adds"), ("hooksinto", "What it hooks into"), ("ties", "Ties to other mods"), ("about", "About"), (More, "More"), ("hooks", null),
                },
            };
            Tile(plan, "overview", null, "Version", "By");
            Label(plan, "about", "Website", "Id", "Folder");
            Label(plan, "hooksinto", "Hooks into");
            Label(plan, More, "Not shown");
            // Its ties, each a row counting the mods: "Needs (2)", "Works with, when there (1)".
            plan.Starts.Add((l => l.StartsWith("Will not run with", StringComparison.Ordinal) || l.StartsWith("Needs", StringComparison.Ordinal)
                || l.StartsWith("Needed by", StringComparison.Ordinal) || l.StartsWith("Works with", StringComparison.Ordinal), "ties"));
            Part(plan, "adds", "mod");
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        /// <summary>A trader's page: what it sells, the wares waiting for a boss among them, then the rest.</summary>
        private static KindPlan Trader()
        {
            var plan = new KindPlan
            {
                Topics = new[] { ("overview", (string)null), ("sells", "What it sells"), (More, "More"), ("hooks", null) },
            };
            plan.Starts.Add((l => l.StartsWith(StationWords.Sells(null), StringComparison.Ordinal), "sells"));
            Label(plan, More, "Not shown");
            plan.Blocks[FactBlock.Where] = More;
            plan.Blocks[FactBlock.FoundIn] = More;
            plan.Blocks[FactBlock.Biomes] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        /// <summary>
        /// A skill: what it changes at no skill, the character's level and the top, then how it
        /// rises and what a death takes, then what it works on (<see cref="SkillPageWords"/>).
        /// </summary>
        private static KindPlan Skill()
        {
            var plan = new KindPlan
            {
                Topics = new[]
                {
                    ("overview", (string)null), ("changes", SkillPageWords.ChangesHeading), ("rises", "How it rises"), ("works", "What it works on"), (More, "More"), ("hooks", null),
                },
            };
            Label(plan, "changes", SkillPageWords.ChangesTitle, SkillPageWords.ChangesHeading, "Digging the ground");
            Lead(plan, "changes", SkillPageWords.ChangesTitle);
            Label(plan, "rises", SkillPageWords.RisesLabel, SkillPageWords.OnDeathLabel, "In this world", SkillPageWords.AddsLevelsTitle, SkillPageWords.FasterTitle);
            Label(plan, "works", SkillPageWords.TrainedWithTitle, SkillPageWords.ToolsTitle, SkillPageWords.StationsTitle, SkillPageWords.CookingTitle, SkillPageWords.PickedTitle);
            Label(plan, More, "Not shown");
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan Spawner()
        {
            var plan = new KindPlan
            {
                Topics = new[] { ("overview", (string)null), ("spawns", "Spawns"), ("breaking", "Breaking it"), ("where", "Where it is"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Pace", "Keeps alive", "Health");
            Label(plan, More, "Not shown");
            Part(plan, "spawns", "spawner");
            Lead(plan, "spawns", SpawnWords.PoolTitle);
            Part(plan, "breaking", "resource");
            plan.Blocks[FactBlock.Where] = "where";
            plan.Blocks[FactBlock.FoundIn] = "where";
            plan.Blocks[FactBlock.Biomes] = "where";
            plan.Blocks[FactBlock.Uses] = More;
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static void Tile(KindPlan plan, string topic, string key, params string[] labels) => plan.Tiles.Add((topic, key, labels));

        private static void Label(KindPlan plan, string topic, params string[] labels)
        {
            foreach (var label in labels) plan.Labels[label] = topic;
        }

        private static void Part(KindPlan plan, string topic, params string[] parts)
        {
            foreach (var part in parts) plan.Parts[part] = topic;
        }

        /// <summary>A topic order for pages that tell a key fact or row: the topics named first, then the rest in their own order.</summary>
        private static void Order(KindPlan plan, string key, params string[] first)
        {
            var ids = new List<string>(first);
            foreach (var (id, _) in plan.Topics) if (!ids.Contains(id)) ids.Add(id);
            plan.Orders.Add((key, ids.ToArray()));
        }

        private static void Lead(KindPlan plan, string topic, params string[] first) => plan.Leads[topic] = first;

        private static void RowsFirst(KindPlan plan, string topic)
        {
            plan.RowsFirst.Add(topic);
            if (!plan.Leads.ContainsKey(topic)) plan.Leads[topic] = Array.Empty<string>();
        }

        private static void Links(KindPlan plan, string topic, params string[] groups)
        {
            foreach (var group in groups) plan.LinkGroups[group] = topic;
        }

        /// <summary>Whether a kind's page is laid out by topic yet.</summary>
        public static bool LaysOut(Kind kind) => Kinds.ContainsKey(kind);

        /// <summary>Whether a group of a kind's links shows in a topic of In the game, and so not under Linked.</summary>
        public static bool Places(Kind kind, string group) => Kinds.TryGetValue(kind, out var plan) && plan.LinkGroups.ContainsKey(group);

        /// <summary>
        /// Which of a Linked row's chips still show there, by their place in it: none of a group
        /// a topic places; of the rest, those In the game does not link already, so each link
        /// shows once on the page.
        /// </summary>
        /// <param name="kind">The kind of entry.</param>
        /// <param name="group">The row's group.</param>
        /// <param name="targets">What each chip goes to, in the row's order.</param>
        /// <param name="shown">What In the game links.</param>
        public static List<int> LeftInLinked(Kind kind, string group, IReadOnlyList<string> targets, ICollection<string> shown)
        {
            var left = new List<int>();
            if (Places(kind, group)) return left;
            for (var i = 0; i < targets.Count; i++) if (!shown.Contains(targets[i])) left.Add(i);
            return left;
        }

        /// <summary>
        /// The page's topics in their order, those with nothing left out; null for a kind not
        /// laid out yet.
        /// </summary>
        /// <param name="kind">The kind of entry.</param>
        /// <param name="pairs">The labelled facts read, each with the reader part that told it ("" for none).</param>
        /// <param name="rows">The rows read, by title, each with its part.</param>
        /// <param name="blocks">The blocks the page has.</param>
        /// <param name="linkGroups">The groups of links the entry has, by name; those a topic shows go there.</param>
        public static List<FactTopicPlan> Plan(Kind kind, IReadOnlyList<(string Label, string Part)> pairs, IReadOnlyList<(string Title, string Part)> rows, IReadOnlyCollection<FactBlock> blocks,
            IReadOnlyCollection<string> linkGroups = null)
        {
            if (!Kinds.TryGetValue(kind, out var plan)) return null;
            if (kind == Kind.Item && pairs.Any(p => p.Label == FishWords.Swims)) plan = FishPlan;
            var topics = new Dictionary<string, FactTopicPlan>(StringComparer.Ordinal);
            foreach (var (id, heading) in plan.Topics) topics[id] = new FactTopicPlan { Id = id, Heading = heading };

            string TopicOf(string label, string part)
            {
                if (plan.Labels.TryGetValue(label, out var byLabel)) return byLabel;
                foreach (var (matches, topic) in plan.Starts) if (matches(label)) return topic;
                return part != null && plan.Parts.TryGetValue(part, out var byPart) ? byPart : More;
            }

            // Each topic's tiles, from the first set whose key fact is told, in the set's order
            // whatever order they were read in.
            var tiled = new HashSet<int>();
            var chosen = new HashSet<string>(StringComparer.Ordinal);
            bool Told(string label)
            {
                for (var i = 0; i < pairs.Count; i++) if (pairs[i].Label == label) return true;
                return false;
            }
            foreach (var (topic, key, labels) in plan.Tiles)
            {
                if (chosen.Contains(topic) || (key != null && !Told(key))) continue;
                chosen.Add(topic);
                foreach (var label in labels)
                {
                    for (var i = 0; i < pairs.Count; i++)
                    {
                        if (pairs[i].Label != label || !tiled.Add(i)) continue;
                        topics[topic].Tiles.Add(i);
                    }
                }
            }

            var anchors = new Dictionary<string, FactBit>(StringComparer.Ordinal);
            var notes = new List<int>();
            for (var i = 0; i < pairs.Count; i++)
            {
                var (label, part) = pairs[i];
                // A pair handed in with no label is left out of the page (its part hinges on a thing not met).
                if (label == null || tiled.Contains(i)) continue;
                if (plan.Notes.ContainsKey(label))
                {
                    notes.Add(i);
                    continue;
                }
                var bit = new FactBit { Pair = i };
                topics[TopicOf(label, part)].Bits.Add(bit);
                if (!anchors.ContainsKey(label)) anchors[label] = bit;
            }
            for (var i = 0; i < rows.Count; i++)
            {
                var (title, part) = rows[i];
                if (title == null) continue;
                var bit = new FactBit { Row = i };
                topics[TopicOf(title, part)].Bits.Add(bit);
                anchors[title] = bit;
            }
            foreach (var block in blocks)
            {
                topics[plan.Blocks.TryGetValue(block, out var topic) ? topic : More].Bits.Add(new FactBit { Block = block });
            }
            foreach (var group in linkGroups ?? Array.Empty<string>())
            {
                if (plan.LinkGroups.TryGetValue(group, out var topic)) topics[topic].Bits.Add(new FactBit { LinkGroup = group });
            }

            // What leads a topic comes first, in the lead's order, its rows before that where they stand first; the rest keep theirs.
            foreach (var pair in plan.Leads)
            {
                var first = pair.Value;
                var rowsFirst = plan.RowsFirst.Contains(pair.Key);
                string NameOf(FactBit b) => b.LinkGroup ?? (b.Pair >= 0 ? pairs[b.Pair].Label : b.Row >= 0 ? rows[b.Row].Title : null);
                int Rank(FactBit b)
                {
                    if (rowsFirst && b.Row >= 0) return -1;
                    var at = Array.IndexOf(first, NameOf(b));
                    return at < 0 ? first.Length : at;
                }
                var bits = topics[pair.Key].Bits;
                var led = bits.Select((b, i) => (Bit: b, At: i)).OrderBy(x => Rank(x.Bit)).ThenBy(x => x.At).Select(x => x.Bit).ToList();
                bits.Clear();
                bits.AddRange(led);
            }

            // A note goes under what it qualifies; with nothing of that to stand under, it is a fact of its own there.
            foreach (var i in notes)
            {
                var anchor = plan.Notes[pairs[i].Label];
                if (anchors.TryGetValue(anchor, out var under)) under.Notes.Add(i);
                else topics[TopicOf(anchor, pairs[i].Part)].Bits.Add(new FactBit { Pair = i });
            }

            var order = new List<string>();
            foreach (var (id, _) in plan.Topics) order.Add(id);
            foreach (var (key, ids) in plan.Orders)
            {
                if (!Told(key) && !RowTold(key)) continue;
                order = new List<string>(ids);
                break;
            }
            bool RowTold(string title)
            {
                for (var i = 0; i < rows.Count; i++) if (rows[i].Title == title) return true;
                return false;
            }

            var laid = new List<FactTopicPlan>();
            foreach (var id in order)
            {
                var topic = topics[id];
                if (topic.Tiles.Count + topic.Bits.Count > 0) laid.Add(topic);
            }
            return laid;
        }
    }
}
