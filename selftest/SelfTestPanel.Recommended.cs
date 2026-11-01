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

        /// <summary>What is recommended, a topic box a group as the run's card has them, each with the chip that runs it; measured, then drawn.</summary>
        private static void RecommendedCard(Rect rect)
        {
            var groups = Groups(SelfTest.Recommended);
            var width = CardWidth(rect);
            Pass(false);
            var height = RecommendedBody(groups, U(14f), U(44f), width) - U(44f) + U(60f);
            var card = BeginCard(rect, ref _recommendedScroll, height, "Self-test · Recommended");
            Pass(true);
            RecommendedBody(groups, card.X, card.Y, width);
            if (EndCard(rect, out _)) ShowRecommended(false);
        }

        private static float RecommendedBody(List<Group> groups, float x, float y, float width)
        {
            if (groups.Count == 0)
            {
                if (_drawing) GUI.Label(new Rect(x, y, width, U(22f)), "Nothing to recommend now.", Skin.DimLabel);
                return y + U(22f);
            }
            foreach (var group in groups)
            {
                var scope = group.Scope;
                y = Topic(x, y, width, group.Title, scope.HasValue ? null : group.Button, scope.HasValue ? group.Button : null,
                    () =>
                    {
                        ShowRecommended(false);
                        Say("Scry: " + SelfTest.StartRecommended(scope.Value));
                    },
                    ty => GroupRows(group.Rows, x + U(10f), ty, width - U(20f)));
            }
            return y;
        }

        /// <summary>A group's rows, wrapped, at most <see cref="RowsShown"/> before the rest are counted.</summary>
        private static float GroupRows(List<string> rows, float x, float y, float width)
        {
            foreach (var row in rows.Take(RowsShown)) y = TwoColumns(x, y, width, row, "", Skin.DimWrap, Skin.DimWrap);
            if (rows.Count <= RowsShown) return y;
            var more = $"and {Numbers.Count(rows.Count - RowsShown)} more";
            if (_drawing) GUI.Label(new Rect(x, y, width, U(20f)), more, Skin.FaintLabel);
            return y + U(22f);
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
