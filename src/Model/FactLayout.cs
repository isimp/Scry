using System;
using System.Collections.Generic;

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
        public readonly List<int> Notes = new List<int>();
    }

    /// <summary>A topic of the page: its small heading (none for the first, the headline tiles), its tiles and what follows them.</summary>
    internal sealed class FactTopicPlan
    {
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
        }

        private const string More = "more";

        private static readonly Dictionary<Kind, KindPlan> Kinds = new Dictionary<Kind, KindPlan>
        {
            [Kind.Creature] = Creature(),
            [Kind.Item] = Item(),
            [Kind.Piece] = Piece(),
            [Kind.Resource] = Resource(),
            [Kind.Projectile] = Projectile(),
            [Kind.Location] = Location(),
            [Kind.Raid] = Raid(),
            [Kind.Biome] = Biome(),
            [Kind.StatusEffect] = StatusEffect(),
        };

        private static KindPlan Creature()
        {
            var plan = new KindPlan
            {
                Topics = new[]
                {
                    ("overview", (string)null), ("fight", "Fight"), ("senses", "Senses and behaviour"), ("loot", "Loot"), ("lives", "Where it lives"),
                    ("taming", "Taming and breeding"), ("riding", "Riding"), ("after", "After it falls"), (More, "More"), ("hooks", null),
                },
            };
            Tile(plan, "overview", null, "Health", "Faction", "Moves", "Tameable", "Boss");
            Tile(plan, "senses", null, "Sees", "Hears", "Turns on you", "Fire");
            Label(plan, "fight", "Attacks", "Health with stars", "Damage with stars", "Its fight", "Fights", "Damage it takes");
            Label(plan, "senses", "Gives up chasing", "Flees", "Leaves alone", "With Passive enemies");
            Label(plan, "loot", "Drops");
            Label(plan, "taming", "Takes to tame", "Stays fed", "Eats");
            Label(plan, "after", "Forsaken power", "On its boss stone");
            Label(plan, More, "Not shown");
            plan.Starts.Add((l => l.StartsWith("Seen dropping", StringComparison.Ordinal), "loot"));
            Part(plan, "fight", "attacks", "weak spots", "resistances");
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
                    ("overview", (string)null), ("fight", "Fight"), ("wearing", "Wearing"), ("food", "Food"), ("hatching", "Hatching"), ("making", "Making"),
                    ("from", "Where it comes from"), ("uses", "What it is used for"), (More, "More"), ("hooks", null),
                },
            };
            Tile(plan, "overview", "Damage", "Damage", "Weight", "Quality", "Durability");
            Tile(plan, "overview", "Armour", "Armour", "Weight", "Quality", "Movement");
            Tile(plan, "overview", "Food", "Food", "Heals", "Lasts", "Weight");
            Tile(plan, "overview", null, "Type", "Weight", "Stacks to", "Worth");
            Label(plan, "fight", "Damage", "Per quality", "Secondary attack", "Block", "Block force", "Parry bonus", "Backstab", "Knockback", "Each attack costs", "Drawing costs",
                "At full adrenaline", "On hit", ItemWords.DamageTaken(false), ItemWords.ResistsNothing(false));
            Label(plan, "wearing", "Armour", "Movement", "When worn", "Set bonus", "Other changes while worn", ItemWords.DamageTaken(true), ItemWords.ResistsNothing(true));
            Label(plan, "food", "Food", "Heals", "Lasts", "When used");
            Label(plan, "hatching", "Hatches into", "Hatches in", "Hatches when");
            Label(plan, "making", "Type", "Weight", "Quality", "Durability", "Repaired at", "Upgrades need", "Skill", "Tool tier", "Stacks to", "Worth", "Portals");
            Label(plan, More, "Not shown");
            plan.Starts.Add((l => l.EndsWith(" damage causes", StringComparison.Ordinal), "fight"));
            Part(plan, "fight", "item stats");
            Part(plan, "wearing", "gear", "resistances");
            Part(plan, "making", "recipe", "made at stations");
            plan.Blocks[FactBlock.Where] = "from";
            plan.Blocks[FactBlock.FoundIn] = "from";
            plan.Blocks[FactBlock.Biomes] = "from";
            plan.Blocks[FactBlock.Uses] = "uses";
            plan.Blocks[FactBlock.Users] = More;
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan Piece()
        {
            var plan = new KindPlan
            {
                Topics = new[]
                {
                    ("overview", (string)null), ("building", "Building"), ("standing", "Standing"), ("comfort", "Comfort"), ("sleeping", "Sleeping"),
                    ("station", "As a station"), ("from", "Where it comes from"), (More, "More"), ("hooks", null),
                },
            };
            Tile(plan, "overview", null, "Health", "Comfort", "Material", "Support");
            Label(plan, "building", "Build cost", "Built with", "Placed", "Upgrades", "Claiming it");
            Label(plan, "comfort", "Comfort group");
            Label(plan, "sleeping", "Sleeping in it");
            Label(plan, More, "Not shown");
            Part(plan, "building", "piece", "placement", "built with", "build cost");
            Part(plan, "standing", "support", "weather", "resistances");
            Part(plan, "station", "station");
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
                Topics = new[] { ("overview", (string)null), ("gathering", "Gathering"), ("lives", "Where it lives"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Health", "Needs tool tier");
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
                Topics = new[] { ("overview", (string)null), ("hit", "Hit"), ("flight", "Flight"), ("from", "Where it comes from"), (More, "More"), ("hooks", null) },
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
                Topics = new[] { ("overview", (string)null), ("placement", "Placement"), ("layout", "Layout"), ("contents", "Contents"), ("music", "Music"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Biome", "Per world", "Size");
            Label(plan, "placement", "Is", "Not before");
            Label(plan, "layout", "Building", "Doorways", "Built into");
            Label(plan, "contents", "What it holds", "What its rooms hold", "Levels at its spawn points");
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
                Topics = new[] { ("overview", (string)null), ("when", "When"), ("comes", "What comes"), ("music", "Music"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Lasts", "Weather", "Comes for");
            Label(plan, "when", "On the table", "Rolled", "Also rolled", "Ends with", "On");
            Label(plan, "music", "Music");
            Label(plan, More, "Not shown");
            // What it brings is told creature by creature, each by its name.
            Part(plan, "comes", "raid");
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
                Topics = new[] { ("overview", (string)null), ("weather", "Weather"), ("music", "Music"), ("there", "What is there"), (More, "More"), ("hooks", null) },
            };
            Label(plan, More, "Not shown");
            // Each weather by its share of the time, the music by the time of day.
            plan.Starts.Add((l => l.EndsWith(" of the time", StringComparison.Ordinal), "weather"));
            plan.Starts.Add((l => l.StartsWith("Music", StringComparison.Ordinal), "music"));
            Part(plan, "there", "biome");
            plan.Blocks[FactBlock.Hooks] = "hooks";
            return plan;
        }

        private static KindPlan StatusEffect()
        {
            var plan = new KindPlan
            {
                Topics = new[] { ("overview", (string)null), ("changes", "Changes"), (More, "More"), ("hooks", null) },
            };
            Tile(plan, "overview", null, "Lasts");
            Label(plan, More, "Not shown");
            Part(plan, "changes", "status effect");
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

        /// <summary>Whether a kind's page is laid out by topic yet.</summary>
        public static bool LaysOut(Kind kind) => Kinds.ContainsKey(kind);

        /// <summary>
        /// The page's topics in their order, those with nothing left out; null for a kind not
        /// laid out yet.
        /// </summary>
        /// <param name="kind">The kind of entry.</param>
        /// <param name="pairs">The labelled facts read, each with the reader part that told it ("" for none).</param>
        /// <param name="rows">The rows read, by title, each with its part.</param>
        /// <param name="blocks">The blocks the page has.</param>
        public static List<FactTopicPlan> Plan(Kind kind, IReadOnlyList<(string Label, string Part)> pairs, IReadOnlyList<(string Title, string Part)> rows, IReadOnlyCollection<FactBlock> blocks)
        {
            if (!Kinds.TryGetValue(kind, out var plan)) return null;
            var topics = new Dictionary<string, FactTopicPlan>(StringComparer.Ordinal);
            foreach (var (id, heading) in plan.Topics) topics[id] = new FactTopicPlan { Heading = heading };

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
                if (tiled.Contains(i)) continue;
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
                var bit = new FactBit { Row = i };
                topics[TopicOf(title, part)].Bits.Add(bit);
                anchors[title] = bit;
            }
            foreach (var block in blocks)
            {
                topics[plan.Blocks.TryGetValue(block, out var topic) ? topic : More].Bits.Add(new FactBit { Block = block });
            }

            // A note goes under what it qualifies; with nothing of that to stand under, it is a fact of its own there.
            foreach (var i in notes)
            {
                var anchor = plan.Notes[pairs[i].Label];
                if (anchors.TryGetValue(anchor, out var under)) under.Notes.Add(i);
                else topics[TopicOf(anchor, pairs[i].Part)].Bits.Add(new FactBit { Pair = i });
            }

            var laid = new List<FactTopicPlan>();
            foreach (var (id, _) in plan.Topics)
            {
                var topic = topics[id];
                if (topic.Tiles.Count + topic.Bits.Count > 0) laid.Add(topic);
            }
            return laid;
        }
    }
}
