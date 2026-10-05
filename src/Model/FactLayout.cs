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
    /// heading; the headline numbers as tiles; what qualifies a row (its drops' stars, the
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
            public readonly List<(string Label, string Topic)> Tiles = new List<(string, string)>();
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
            foreach (var tile in new[] { "Health", "Faction", "Moves", "Tameable", "Boss" }) plan.Tiles.Add((tile, "overview"));
            foreach (var tile in new[] { "Sees", "Hears", "Turns on you", "Fire" }) plan.Tiles.Add((tile, "senses"));
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

            // Tiles in the plan's order, whatever order they were read in.
            foreach (var (label, topic) in plan.Tiles)
            {
                for (var i = 0; i < pairs.Count; i++) if (pairs[i].Label == label) topics[topic].Tiles.Add(i);
            }

            var anchors = new Dictionary<string, FactBit>(StringComparer.Ordinal);
            var notes = new List<int>();
            for (var i = 0; i < pairs.Count; i++)
            {
                var (label, part) = pairs[i];
                if (plan.Tiles.Exists(t => t.Label == label)) continue;
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
