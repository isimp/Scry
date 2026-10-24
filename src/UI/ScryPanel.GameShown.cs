using System;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the game shows the moment a thing is known comes with it (<see cref="Explorer.LeavesOut(string, bool)"/>),
    /// but only while the game lists the recipe it shows it by (<see cref="Explorer.RecipeListed"/>);
    /// and what was counted over every character's play stands faint under the strict rules
    /// beside the character's own counts.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>Whether the game shows a row now: a game-shown row whose recipe the game lists for the character.</summary>
        private static bool GameShows(Explorer explorer, Facts.Row row) => row.GameShown && explorer.RecipeListed(row.Recipe);

        /// <summary>Whether the game shows a row's item now: by the item's own recipe where it names one, else the row's.</summary>
        private static bool GameShows(Explorer explorer, Facts.Row row, Facts.Ingredient item) => row.GameShown && explorer.RecipeListed(item.Recipe ?? row.Recipe);

        /// <summary>Whether the game shows a pair now: a game-shown pair whose recipe the game lists for the character.</summary>
        private static bool GameShows(Explorer explorer, Facts facts, string key) =>
            facts.GameShown.Contains(key) && explorer.RecipeListed(facts.GameShownBy.TryGetValue(key, out var recipe) ? recipe : null);

        /// <summary>A row or line counted over every character's play, drawn faint under the strict rules, its hover saying it is not counted for this character.</summary>
        private static float AllPlayFaint(Rect at, Func<float> draw)
        {
            var was = GUI.color;
            GUI.color = Skin.Alpha(was, 0.55f);
            var bottom = draw();
            GUI.color = was;
            if (new Rect(at.x, at.y, at.width, bottom - at.y).Contains(Event.current.mousePosition)) AskTip("all-play", SeenWords.AllPlayTip);
            CountDrawn(PanelPart.AllPlayFaint);
            return bottom;
        }
    }
}
