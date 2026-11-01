using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Scry.ScryPanel;

namespace Scry
{
    /// <summary>
    /// What the self-test recommends running next, between runs (<see cref="SelfTestRecommended"/>):
    /// a strip in the blue of what is immune counting each, put away until what it says changes,
    /// and its card listing them, each group with a button that runs it: the plan, the untried rows
    /// near the character, the parts that failed last, and the checks not judged, which wait while
    /// the machine is still busy; then the untried rows nothing near frees, counted.
    /// </summary>
    internal static partial class SelfTestPanel
    {
        private static void RecommendedStrip(Rect rect)
        {
            var recommended = SelfTest.Recommended;
            if (recommended?.Line == null) return;
            switch (NoticeStrip(rect, Skin.Immune, recommended.Line, Skin.Label, false, "Recommended", _recommendedCard, true, crossTip: "Put this away until what it recommends changes"))
            {
                case StripClick.Chip:
                    ShowRecommended(!_recommendedCard);
                    CloseCards();
                    break;
                case StripClick.Cross:
                    RecommendedAway(recommended.Line);
                    break;
            }
        }

        private static Vector2 _recommendedScroll;

        /// <summary>A group's rows, at most this many before the rest are counted.</summary>
        private const int RowsShown = 6;

        private static void RecommendedCard(Rect rect)
        {
            var recommended = SelfTest.Recommended;
            var width = CardWidth(rect);
            var groups = Groups(recommended);
            var height = U(44f) + U(12f);
            foreach (var group in groups) height += U(34f) + Mathf.Min(group.Rows.Count, RowsShown + 1) * U(22f) + U(10f);
            var card = BeginCard(rect, ref _recommendedScroll, height, "Self-test · Recommended");
            var x = card.X;
            var y = card.Y;
            if (groups.Count == 0) GUI.Label(new Rect(x, y, width, U(22f)), "Nothing to recommend now.", Skin.DimLabel);
            foreach (var group in groups)
            {
                GUI.Label(new Rect(x, y + U(4f), width - U(140f), U(24f)), group.Title, Skin.Label);
                var chipW = Skin.Width(Skin.Chip, group.Button) + U(12f);
                var chip = new Rect(x + width - chipW, y + U(2f), chipW, U(24f));
                if (group.Scope.HasValue)
                {
                    if (GUI.Button(chip, group.Button, Skin.Fitted(Skin.ChipOn, chip)))
                    {
                        ShowRecommended(false);
                        Say("Scry: " + SelfTest.StartRecommended(group.Scope.Value));
                    }
                }
                else GUI.Label(chip, group.Button, Skin.FaintLabel);
                y += U(34f);
                foreach (var row in group.Rows.Take(RowsShown))
                {
                    GUI.Label(new Rect(x + U(14f), y, width - U(14f), U(22f)), row, Skin.DimLabel);
                    y += U(22f);
                }
                if (group.Rows.Count > RowsShown)
                {
                    GUI.Label(new Rect(x + U(14f), y, width - U(14f), U(22f)), $"and {Numbers.Count(group.Rows.Count - RowsShown)} more", Skin.FaintLabel);
                    y += U(22f);
                }
                y += U(10f);
            }
            if (EndCard(rect, out _)) ShowRecommended(false);
        }

        /// <summary>One group of the card: its title, its button's words and the run it starts (none for one that waits), and its rows.</summary>
        private sealed class Group
        {
            public string Title;
            public string Button;
            public RunScope? Scope;
            public List<string> Rows = new List<string>();
        }

        private static List<Group> Groups(SelfTestRecommended recommended)
        {
            var groups = new List<Group>();
            if (recommended == null) return groups;
            if (recommended.Planned.Count > 0)
                groups.Add(new Group { Title = "Planned for a change", Button = $"Run {Numbers.Count(recommended.Planned.Count)}", Scope = RunScope.Plan, Rows = recommended.Planned });
            if (recommended.Near.Count > 0)
                groups.Add(new Group { Title = "Near you now", Button = $"Try {Numbers.Count(recommended.Near.Select(n => n.Part).Distinct().Count())}", Scope = RunScope.Near, Rows = recommended.Near.Select(n => n.Row).ToList() });
            if (recommended.Failed.Count > 0)
                groups.Add(new Group { Title = "Failed last run", Button = $"Run {Numbers.Count(recommended.Failed.Count)}", Scope = RunScope.Failed, Rows = recommended.Failed });
            if (recommended.BusyChecks > 0)
            {
                groups.Add(new Group
                {
                    Title = $"Not judged, the machine was busy ({Numbers.Count(recommended.BusyChecks)})",
                    Button = recommended.BusyReady ? $"Run {Numbers.Count(recommended.BusyParts.Count)}" : $"Waits · {Numbers.Fixed(SelfTest.IdleSlowdown, 1)}× slower now",
                    Scope = recommended.BusyReady ? RunScope.Busy : (RunScope?)null,
                    Rows = recommended.BusyParts,
                });
            }
            if (recommended.UntriedElsewhere > 0)
                groups.Add(new Group { Title = $"Untried elsewhere ({Numbers.Count(recommended.UntriedElsewhere)})", Button = "Run all untried", Scope = RunScope.Untried, Rows = new List<string> { "each says where to stand in the last run's summary" } });
            return groups;
        }
    }
}
