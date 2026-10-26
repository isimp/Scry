using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>A fish's own page (<see cref="FishPage"/>): where it lives, when it comes, how it bites and fights, and what a catch brings.</summary>
    internal sealed partial class Facts
    {
        /// <summary>What a catch shows under the strict rules: how it finds a float, how it fights.</summary>
        private static readonly HashSet<string> KeptUntilCaught = new HashSet<string> { FishWords.FindsFloat, FishWords.Rests };

        /// <summary>
        /// A fish by its own figures (<c>Fish</c>), its size and weight by quality, the rod's float
        /// (<see cref="SkillReading"/>), the prep table's recipe for it and the world spawns placing
        /// it (<see cref="Knowledge.FishSpawns"/>); its fight as a table by stars. Under the strict
        /// rules how it finds a float, rests and fights wait for one caught.
        /// </summary>
        private void Fish(GameObject prefab, Fish fish)
        {
            var shared = prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
            var inputs = SkillReading.Inputs(Skills.SkillType.Fishing);
            var (raw, perQuality) = RawFrom(prefab.name);
            var traits = new FishTraits
            {
                MinDepth = fish.m_minDepth, MaxDepth = fish.m_maxDepth, SwimRange = fish.m_swimRange,
                WaypointMin = fish.m_wpDurationMin, WaypointMax = fish.m_wpDurationMax, HookChance = fish.m_baseHookChance,
                StaminaUse = fish.m_staminaUse, EscapeStaminaUse = fish.m_escapeStaminaUse,
                EscapeMin = fish.m_escapeMin, EscapeMax = fish.m_escapeMax, EscapePerLevel = fish.m_escapeMaxPerLevel,
                RestMin = fish.m_escapeWaitMin, RestMax = fish.m_escapeWaitMax,
                JumpChance = fish.m_jumpChance, JumpMaxLevel = fish.m_jumpMaxLevel,
                ScaleByQuality = shared?.m_scaleByQuality ?? 0f, ScaleWeightByQuality = shared?.m_scaleWeightByQuality ?? 0f, Weight = shared?.m_weight ?? 0f,
                FloatRange = inputs.FloatRange, LineBreakPast = inputs.LineBreakPast, LineMost = inputs.LineMost,
                RawAmount = raw, RawPerQuality = perQuality,
            };
            var page = FishPage.Of(traits, Knowledge.FishSpawns(prefab.name));
            var caught = EntryKeys.Did(Deed.Caught);
            foreach (var (label, value) in page.Lines)
            {
                Add(label, value);
                if (KeptUntilCaught.Contains(label)) Proofs[label] = caught;
            }
            var (columns, lines) = page.Fight;
            var table = new Row { Title = FishWords.FightTitle, Columns = columns };
            foreach (var cells in lines)
            {
                table.LineProofs[table.Lines.Count] = caught;
                table.Lines.Add((cells, null));
            }
            Rows.Add(table);
        }

        /// <summary>What a catch brings besides the fish (<c>Fish.m_extraDrops</c>, given by <c>FishingFloat.Catch</c>), each kept back under the strict rules until one was caught.</summary>
        private void Catch(Fish fish)
        {
            var rows = Rows.Count;
            Drops(fish.m_extraDrops, GatherWords.AlsoDrops);
            var caught = new[] { EntryKeys.Did(Deed.Caught) };
            for (var r = rows; r < Rows.Count; r++)
            {
                var items = Rows[r].Items;
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    item.ShownBy = caught;
                    items[i] = item;
                }
            }
        }

        /// <summary>The raw fish the prep table makes of one (<c>Recipe.GetAmount</c>): the recipe taking any one of several, this fish among them; none for no such recipe.</summary>
        private static (int Amount, float PerQuality) RawFrom(string fish)
        {
            var recipes = ObjectDB.instance != null ? ObjectDB.instance.m_recipes : null;
            if (recipes == null) return (0, 0f);
            foreach (var recipe in recipes)
            {
                if (recipe == null || !recipe.m_enabled || !recipe.m_requireOnlyOneIngredient || recipe.m_resources == null) continue;
                if (recipe.m_resources.Any(r => r?.m_resItem != null && r.m_resItem.gameObject.name == fish)) return (recipe.m_amount, recipe.m_qualityResultAmountMultiplier);
            }
            return (0, 0f);
        }
    }
}
