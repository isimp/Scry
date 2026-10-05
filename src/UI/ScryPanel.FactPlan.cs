using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>In the game laid out by topic (<see cref="FactLayout"/>): the plan for the page shown, its topics' headings, tiles and notes.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>The facts the plan was made for and the blocks they had; made again only when either changes.</summary>
        private static Facts _planFacts;

        private static int _planBlocks = -1;
        private static List<FactTopicPlan> _plan;

        private static readonly FactBlock[] EveryBlock = (FactBlock[])Enum.GetValues(typeof(FactBlock));
        private static readonly List<(string, string)> PlanPairs = new List<(string, string)>();
        private static readonly List<(string, string)> PlanRows = new List<(string, string)>();
        private static readonly List<FactBlock> PlanBlocks = new List<FactBlock>();

        /// <summary>The page's topics for an entry, or null for a kind not laid out by topic yet.</summary>
        private static List<FactTopicPlan> PlanOf(Explorer explorer, Entry entry, Facts facts, bool places)
        {
            if (!FactLayout.LaysOut(entry.Kind)) return null;
            PlanBlocks.Clear();
            if (facts.Where.Count > 0) PlanBlocks.Add(FactBlock.Where);
            if (places) PlanBlocks.Add(FactBlock.FoundIn);
            if (entry.Biomes.Length > 0 && entry.Kind != Kind.Biome) PlanBlocks.Add(FactBlock.Biomes);
            if (Users(explorer, entry).Count > 0) PlanBlocks.Add(FactBlock.Users);
            if (facts.UseRows.Count > 0) PlanBlocks.Add(FactBlock.Uses);
            if (ModHookWords.Line(facts.Hooks) != null) PlanBlocks.Add(FactBlock.Hooks);
            var blocks = 0;
            foreach (var block in PlanBlocks) blocks |= 1 << (int)block;
            if (ReferenceEquals(facts, _planFacts) && blocks == _planBlocks) return _plan;

            PlanPairs.Clear();
            for (var i = 0; i < facts.Pairs.Count; i++) PlanPairs.Add((facts.Pairs[i].Key, facts.PairParts[i]));
            PlanRows.Clear();
            foreach (var row in facts.Rows) PlanRows.Add((row.Title, row.Part ?? ""));
            _plan = FactLayout.Plan(entry.Kind, PlanPairs, PlanRows, PlanBlocks);
            _planFacts = facts;
            _planBlocks = blocks;
            return _plan;
        }

        /// <summary>Lets go of the plan and the facts it was made for, for another world.</summary>
        private static void ForgetFactPlan()
        {
            _planFacts = null;
            _plan = null;
            _planBlocks = -1;
        }

        /// <summary>A topic's small heading and a faint rule after it.</summary>
        private static float TopicHeading(string heading, float width, float y)
        {
            y += U(8f);
            var textW = Skin.Width(Skin.DimLabel, heading);
            GUI.Label(new Rect(0f, y, textW + U(4f), U(20f)), heading, Skin.DimLabel);
            Skin.Fill(new Rect(textW + U(10f), y + U(10f), Mathf.Max(0f, width - textW - U(10f)), U(1f)), Skin.Outline);
            return y + U(26f);
        }

        /// <summary>
        /// A topic's headline numbers as tiles, a small label over each value, up to four in a
        /// row and fewer where the side is narrow, each row as tall as its tallest value.
        /// </summary>
        private static float Tiles(Facts facts, List<int> tiles, float width, float y)
        {
            y += U(2f);
            var gap = U(6f);
            var columns = Mathf.Clamp(Mathf.FloorToInt((width + gap) / (U(110f) + gap)), 1, Mathf.Min(4, tiles.Count));
            var tileW = (width - gap * (columns - 1)) / columns;
            var valueW = tileW - U(16f);
            for (var first = 0; first < tiles.Count; first += columns)
            {
                var last = Mathf.Min(tiles.Count, first + columns);
                var valueH = U(20f);
                for (var i = first; i < last; i++) valueH = Mathf.Max(valueH, Skin.Height(Skin.Wrap, facts.Pairs[tiles[i]].Value, valueW));
                var tileH = U(26f) + valueH + U(6f);
                for (var i = first; i < last; i++)
                {
                    var pair = facts.Pairs[tiles[i]];
                    var at = new Rect((i - first) * (tileW + gap), y, tileW, tileH);
                    if (OutOfSight(at)) continue;
                    // A tile Scry is not sure of is marked, softer, and says why on hover.
                    var unsure = facts.Unsure.TryGetValue(pair.Key, out var why);
                    Skin.Box(at, Skin.Raised);
                    GUI.Label(new Rect(at.x + U(8f), at.y + U(4f), valueW, U(20f)), unsure ? UnsureWords.Marked(pair.Key) : pair.Key, Skin.FaintLabel);
                    GUI.Label(new Rect(at.x + U(8f), at.y + U(24f), valueW, valueH), pair.Value, unsure ? Skin.DimWrap : Skin.Wrap);
                    if (unsure && at.Contains(Event.current.mousePosition)) AskTip("unsure:" + pair.Key, why);
                }
                y += tileH + gap;
            }
            CountDrawn(PanelPart.FactTiles);
            return y + U(2f);
        }

        /// <summary>A fact that qualifies what stands above it, told under it in a soft line.</summary>
        private static float FactNote(Facts facts, KeyValuePair<string, string> pair, float width, float y)
        {
            var unsure = facts.Unsure.TryGetValue(pair.Key, out var why);
            var text = FactWords.Note(unsure ? UnsureWords.Marked(pair.Key) : pair.Key, pair.Value);
            var height = Skin.Height(Skin.DimWrap, text, width);
            var line = new Rect(0f, y, width, height);
            GUI.Label(line, text, Skin.DimWrap);
            if (unsure && line.Contains(Event.current.mousePosition)) AskTip("unsure:" + pair.Key, why);
            return y + height + U(6f);
        }
    }
}
