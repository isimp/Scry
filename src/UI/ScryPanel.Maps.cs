using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The maps kept of a dungeon location's dungeons in the world played, under its stage's
    /// buttons (<see cref="DungeonWalks"/>): a line each with a room walked saying how far, with
    /// <i>Open map</i>, which shows it on the stage wherever the player is, and <i>Forget</i>; then,
    /// where the compendium's floor opens them, a line for each of the server's records it sent,
    /// anyone's walks on this server (<see cref="GroupMapsLink"/>), with <i>Open map</i>. No world
    /// position is shown.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static readonly List<(string Id, DungeonMap Map, int Walked, int Rooms)> MapsShown = new List<(string, DungeonMap, int, int)>();
        private static readonly List<(string Id, DungeonMap Map)> GroupShown = new List<(string, DungeonMap)>();

        /// <summary>How many of the server's lines were last drawn, for the self-test.</summary>
        [Diagnostic]
        public static int GroupLinesDrawn { get; private set; }

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
            GroupLinesDrawn = 0;
            if (!(entry.Source is PlaceSource place) || place.IsRoom || place.Contents?.Dungeon?.Algorithm != "Dungeon") return y;
            MapsShown.Clear();
            // A walk with no room walked shows nothing; /scry forget maps still clears it.
            var example = ExampleLayouts.Of(entry);
            MapsShown.AddRange(DungeonWalks.Maps.WalkedOf(CompendiumMap.World, place.Prefab, map => example ? ExampleLayouts.Count(map) : DungeonMaps.Walked(map, _ => null)));
            GroupShown.Clear();
            if (ExampleLayouts.Of(entry) && ExampleLayouts.GroupOpen) foreach (var map in GroupMapsLink.Group.OfLocation(CompendiumMap.World, place.Prefab)) GroupShown.Add(map);
            if (MapsShown.Count == 0 && GroupShown.Count == 0) return y;
            var rowH = U(26f);
            y += U(6f);
            for (var i = 0; i < MapsShown.Count; i++)
            {
                var (id, map, walked, rooms) = MapsShown[i];
                var said = DungeonMapWords.Yours(i, MapsShown.Count, walked, rooms, map.FirstDay, map.LastDay);
                if (i == 0) FirstMapLineForTest = said;
                var forgetW = Skin.Width(Skin.Button, DungeonMapWords.Forget) + U(12f);
                var openW = Skin.Width(Skin.Button, DungeonMapWords.Open) + U(12f);
                var forget = new Rect(width - forgetW, y, forgetW, rowH);
                var open = new Rect(forget.x - U(6f) - openW, y, openW, rowH);
                var line = new Rect(0f, y, Mathf.Max(0f, open.x - U(8f)), rowH);
                GUI.Label(line, said, Skin.Fitted(Skin.DimLabel, line));
                DaysTip(line, "map:days:" + id, DungeonMapWords.DaysTip(true, map.FirstDay, map.LastDay));
                var shown = ExampleLayouts.ShownMap == id;
                if (open.Contains(Event.current.mousePosition)) AskTip("map:open:" + id, DungeonMapWords.OpenTip);
                if (GUI.Button(open, DungeonMapWords.Open, shown ? Skin.On : Skin.Button) && !shown) OpenMap(id);
                if (forget.Contains(Event.current.mousePosition)) AskTip("map:forget:" + id, DungeonMapWords.ForgetTip);
                if (GUI.Button(forget, DungeonMapWords.Forget, Skin.Button)) DungeonWalks.ForgetMap(id);
                y += rowH + U(4f);
                MapLinesDrawn++;
            }
            var floor = CompendiumWords.FloorName(_explorer?.Compendium?.Floor ?? 0);
            for (var i = 0; i < GroupShown.Count; i++)
            {
                var (id, map) = GroupShown[i];
                var (walked, rooms) = ExampleLayouts.Count(map);
                // The rooms of traces alone are rooms only, as the way between rooms runs through no end cap or divider.
                var said = DungeonMapWords.Server(i, GroupShown.Count, walked, rooms, floor, map.FirstDay, map.LastDay, map.Traced.Count);
                var openW = Skin.Width(Skin.Button, DungeonMapWords.Open) + U(12f);
                var open = new Rect(width - openW, y, openW, rowH);
                var label = new Rect(0f, y, Mathf.Max(0f, open.x - U(8f)), rowH);
                GUI.Label(label, said, Skin.Fitted(Skin.DimLabel, label));
                DaysTip(label, "map:server-days:" + id, DungeonMapWords.ServerTip(map.Tally, map.FirstDay, map.LastDay));
                var shown = ExampleLayouts.ShownMap == id;
                if (open.Contains(Event.current.mousePosition)) AskTip("map:group:" + id, DungeonMapWords.GroupOpenTip);
                if (GUI.Button(open, DungeonMapWords.Open, shown ? Skin.On : Skin.Button) && !shown) OpenMap(id);
                y += rowH + U(4f);
                GroupLinesDrawn++;
            }
            CountDrawn(PanelPart.MapLine);
            return y;
        }

        /// <summary>The days a walk's line spans in words, on its hover; none where it has none.</summary>
        private static void DaysTip(Rect line, string key, string tip)
        {
            if (tip != null && line.Contains(Event.current.mousePosition)) AskTip(key, tip);
        }

        /// <summary>The first line of the character's walks as last drawn, for the self-test.</summary>
        [Diagnostic]
        public static string FirstMapLineForTest { get; private set; }

        /// <summary>Shows a kept map on the stage, its inside.</summary>
        private static void OpenMap(string id)
        {
            ExampleLayouts.OpenMap(id);
            Stage.Inside = true;
        }
    }
}
