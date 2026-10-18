using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The maps kept of a dungeon location's dungeons in the world played, under its stage's
    /// buttons (<see cref="DungeonWalks"/>): a line each saying how far it was walked, with
    /// <i>Open map</i>, which shows it on the stage wherever the player is, and <i>Forget</i>.
    /// No world position is shown.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static readonly List<(string Id, DungeonMap Map)> MapsShown = new List<(string, DungeonMap)>();

        /// <summary>How many map lines were last drawn, for the self-test.</summary>
        [Diagnostic]
        public static int MapLinesDrawn { get; private set; }

        /// <summary>Opens a kept map as its button does, for the self-test.</summary>
        [Diagnostic]
        public static void OpenMapForTest(string id) => OpenMap(id);

        /// <summary>The map lines of a dungeon location, under the stage's buttons; where it is none, or none is kept, nothing.</summary>
        private static float MapLines(Entry entry, float width, float y)
        {
            MapLinesDrawn = 0;
            if (!(entry.Source is PlaceSource place) || place.IsRoom || place.Contents?.Dungeon?.Algorithm != "Dungeon") return y;
            MapsShown.Clear();
            foreach (var map in DungeonWalks.Maps.OfLocation(CompendiumMap.World, place.Prefab)) MapsShown.Add(map);
            if (MapsShown.Count == 0) return y;
            var rowH = U(26f);
            y += U(6f);
            for (var i = 0; i < MapsShown.Count; i++)
            {
                var (id, map) = MapsShown[i];
                var (walked, rooms) = ExampleLayouts.Of(entry) ? ExampleLayouts.Count(map) : DungeonMaps.Walked(map, _ => null);
                var said = DungeonMapWords.Yours(i, MapsShown.Count, walked, rooms);
                var forgetW = Skin.Width(Skin.Button, DungeonMapWords.Forget) + U(12f);
                var openW = Skin.Width(Skin.Button, DungeonMapWords.Open) + U(12f);
                var forget = new Rect(width - forgetW, y, forgetW, rowH);
                var open = new Rect(forget.x - U(6f) - openW, y, openW, rowH);
                GUI.Label(new Rect(0f, y, Mathf.Max(0f, open.x - U(8f)), rowH), said, Skin.Fitted(Skin.DimLabel, new Rect(0f, y, Mathf.Max(0f, open.x - U(8f)), rowH)));
                var shown = ExampleLayouts.ShownMap == id;
                if (open.Contains(Event.current.mousePosition)) AskTip("map:open:" + id, DungeonMapWords.OpenTip);
                if (GUI.Button(open, DungeonMapWords.Open, shown ? Skin.On : Skin.Button) && !shown) OpenMap(id);
                if (forget.Contains(Event.current.mousePosition)) AskTip("map:forget:" + id, DungeonMapWords.ForgetTip);
                if (GUI.Button(forget, DungeonMapWords.Forget, Skin.Button)) DungeonWalks.ForgetMap(id);
                y += rowH + U(4f);
                MapLinesDrawn++;
            }
            CountDrawn(PanelPart.MapLine);
            return y;
        }

        /// <summary>Shows a kept map on the stage, its inside.</summary>
        private static void OpenMap(string id)
        {
            ExampleLayouts.OpenMap(id);
            Stage.Inside = true;
        }
    }
}
