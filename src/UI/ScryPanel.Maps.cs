using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The dungeons of a dungeon location walked in the world played, under its stage's buttons,
    /// a line each (<see cref="WalkLines"/>): where it lies from where you stand, or that it lies
    /// outside what you know, its place kept hidden; how far you walked it (<see cref="DungeonWalks"/>)
    /// and, where the compendium opens the server's records, how far anyone on this server did
    /// (<see cref="GroupMapsLink"/>); its days and what was left inside on hover. Each has
    /// <i>On the map</i> where you know where it lies, which opens Valheim's large map there
    /// (<c>Minimap.ShowPointOnMap</c>), <i>Show it</i>, which shows it on the stage wherever you
    /// are, and <i>Forget</i> for a walk of yours. Those you know stand nearest first, the rest
    /// after; past four the rest fold behind one chip.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The lines last built, with their words and hover, and what they were built for: rebuilt as any of it changes, and each second for rooms read since.</summary>
        private static readonly List<WalkLine> WalkLinesBuilt = new List<WalkLine>();
        private static readonly List<(string Said, string Tip)> WalkLineWords = new List<(string, string)>();
        private static (Entry Entry, string World, int Mine, int Group, int Ground, bool Open, int X, int Z) _walkLinesOf;
        private static float _walkLinesAt = float.NegativeInfinity;
        private static string _walkHeading;

        /// <summary>How often the lines are built again while nothing else changed, for the rooms of the example read meanwhile, in seconds.</summary>
        private const float WalkLinesEvery = 1f;

        /// <summary>How many server's lines were last drawn, for the self-test: lines with a walk on this server.</summary>
        [Diagnostic]
        public static int GroupLinesDrawn { get; private set; }

        /// <summary>How many map lines were last drawn, for the self-test: lines with a walk of yours.</summary>
        [Diagnostic]
        public static int MapLinesDrawn { get; private set; }

        /// <summary>How many lines last drawn told a dungeon walked whole, with the map's ticked mark, for the self-test.</summary>
        [Diagnostic]
        public static int WholeLinesDrawn { get; private set; }

        /// <summary>The lines last built, in the order drawn, for the self-test.</summary>
        [Diagnostic]
        public static IReadOnlyList<WalkLine> WalkLinesForTest => WalkLinesBuilt;

        /// <summary>The heading over the lines as last drawn, for the self-test; none where none was.</summary>
        [Diagnostic]
        public static string WalkHeadingForTest { get; private set; }

        /// <summary>Opens a kept map as its button does, for the self-test.</summary>
        [Diagnostic]
        public static void OpenMapForTest(string id) => OpenMap(id);

        /// <summary>Opens Valheim's large map where a line's dungeon lies, as its <i>On the map</i> does, for the self-test.</summary>
        [Diagnostic]
        public static void ShowOnTheMapForTest(string id)
        {
            foreach (var line in WalkLinesBuilt)
            {
                if (line.Id == id) ShowOnTheMap(line.Entrance);
            }
        }

        /// <summary>The lines of a dungeon location, under the stage's buttons; where it is none, or none of its dungeons is walked, nothing.</summary>
        private static float MapLines(Entry entry, float width, float y)
        {
            MapLinesDrawn = 0;
            GroupLinesDrawn = 0;
            WholeLinesDrawn = 0;
            WalkHeadingForTest = null;
            if (!(entry.Source is PlaceSource place) || place.IsRoom || place.Contents?.Dungeon?.Algorithm != "Dungeon") return y;
            BuildWalkLines(entry, place);
            if (WalkLinesBuilt.Count == 0) return y;
            var e = Event.current;
            y += U(6f);
            GUI.Label(new Rect(0f, y, width, U(20f)), _walkHeading, Skin.DimLabel);
            WalkHeadingForTest = _walkHeading;
            y += U(24f);
            var rowH = U(26f);
            var key = "walks:" + place.Prefab;
            var shown = ShownOf(key, WalkLinesBuilt.Count, WalkLines.FirstShown);
            var mapW = Skin.Width(Skin.Button, DungeonMapWords.OnTheMap) + U(12f);
            var showW = Mathf.Max(Skin.Width(Skin.Button, DungeonMapWords.Open), Skin.Width(Skin.On, DungeonMapWords.Shown)) + U(12f);
            var forgetW = Skin.Width(Skin.Button, DungeonMapWords.Forget) + U(12f);
            for (var i = 0; i < shown; i++)
            {
                var line = WalkLinesBuilt[i];
                var (said, tip) = WalkLineWords[i];
                // From the right: Forget for a walk of yours, Show it, On the map where you know where it lies.
                var x = width;
                if (line.Yours.HasValue)
                {
                    x -= forgetW;
                    var forget = new Rect(x, y, forgetW, rowH);
                    if (forget.Contains(e.mousePosition)) AskTip("map:forget:" + line.Id, DungeonMapWords.ForgetTip);
                    if (GUI.Button(forget, DungeonMapWords.Forget, Skin.Button)) DungeonWalks.ForgetMap(line.Id);
                    x -= U(6f);
                }
                x -= showW;
                var show = new Rect(x, y, showW, rowH);
                var onStage = ExampleLayouts.ShownMap == line.Id;
                if (show.Contains(e.mousePosition)) AskTip("map:open:" + line.Id, line.Yours.HasValue ? line.Server.HasValue ? DungeonMapWords.BothOpenTip : DungeonMapWords.OpenTip : DungeonMapWords.GroupOpenTip);
                if (GUI.Button(show, onStage ? DungeonMapWords.Shown : DungeonMapWords.Open, onStage ? Skin.On : Skin.Button) && !onStage) OpenMap(line.Id);
                if (line.Found)
                {
                    x -= U(6f) + mapW;
                    var onMap = new Rect(x, y, mapW, rowH);
                    if (onMap.Contains(e.mousePosition)) AskTip("map:on-the-map:" + line.Id, DungeonMapWords.OnTheMapTip);
                    if (GUI.Button(onMap, DungeonMapWords.OnTheMap, Skin.Button)) ShowOnTheMap(line.Entrance);
                }

                // The dot: a walk of yours, one on this server alone, or one lying outside what you know;
                // one walked whole the map's ticked mark, solid where you walked in it, else hollow.
                if (line.WalkedWhole && MarkPicture(line.Yours.HasValue ? MarkLook.Whole : MarkLook.ServerWhole).OrNull()?.texture is Texture whole)
                {
                    var markSize = U(13f);
                    GUI.DrawTexture(new Rect(0f, y + (rowH - markSize) / 2f, markSize, markSize), whole);
                    WholeLinesDrawn++;
                }
                else
                {
                    var size = U(8f);
                    var dot = line.Yours.HasValue ? Skin.Accent : line.Found ? Skin.Alpha(Skin.Accent, 0.45f) : Skin.Alpha(Skin.Faint, 0.45f);
                    Skin.Icon(new Rect(U(2f), y + (rowH - size) / 2f, size, size), Skin.Circle, dot);
                }
                var text = new Rect(U(16f), y, Mathf.Max(0f, x - U(24f)), rowH);
                GUI.Label(text, said, Skin.Fitted(Skin.DimLabel, text));
                if (tip != null && text.Contains(e.mousePosition)) AskTip("map:line:" + line.Id, tip);
                if (i == 0)
                {
                    FirstMapLineForTest = said;
                    FirstMapTipForTest = tip;
                }
                y += rowH + U(4f);
                if (line.Yours.HasValue) MapLinesDrawn++;
                if (line.Server.HasValue) GroupLinesDrawn++;
            }
            var flow = new ChipFlow(0f, width, y, U(26f), U(5f), U(5f));
            MoreChip(key, WalkLinesBuilt.Count, WalkLines.FirstShown, width, ref flow);
            if (flow.InRow) y = flow.Below + U(4f);
            CountDrawn(PanelPart.MapLine);
            return y;
        }

        /// <summary>
        /// The location's lines, built again where what they show changed: yours, the server's records
        /// where the compendium opens them, which dungeons lie in ground you know, or where you stand,
        /// to ten metres; and each second, for rooms of the example read meanwhile.
        /// </summary>
        private static void BuildWalkLines(Entry entry, PlaceSource place)
        {
            var world = CompendiumMap.World;
            var example = ExampleLayouts.Of(entry);
            var open = example && ExampleLayouts.GroupOpen;
            var at = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;
            var of = (entry, world, DungeonWalks.Maps.Stamp, GroupMapsLink.Group.Stamp, DungeonsInGround.Stamp, open, Mathf.RoundToInt(at.x / 10f), Mathf.RoundToInt(at.z / 10f));
            if (of == _walkLinesOf && Time.unscaledTime - _walkLinesAt < WalkLinesEvery) return;
            _walkLinesOf = of;
            _walkLinesAt = Time.unscaledTime;
            var yours = DungeonWalks.Maps.WalkedOf(world, place.Prefab, map => example ? ExampleLayouts.Count(map) : RoomKinds.Walked(map)).ToList();
            var server = new List<(string, DungeonMap, int, int)>();
            // The server's records of your own walks count with them whatever the compendium shows,
            // as how far you and others walked a dungeon tells nothing of its rooms; the rest only
            // where the compendium opens them.
            if (!open && yours.Count > 0) GroupMapsLink.Ask(place.Prefab);
            if (open || yours.Count > 0)
            {
                foreach (var (id, map) in GroupMapsLink.Group.OfLocation(world, place.Prefab))
                {
                    if (!open && !yours.Exists(y => y.Id == id)) continue;
                    var (walked, rooms) = ExampleLayouts.Count(map);
                    server.Add((id, map, walked, rooms));
                }
            }
            // The server's records carry no place: one lies where the ground you know holds it.
            var lines = WalkLines.Of(yours, server, id => DungeonsInGround.MapOf(id) is DungeonMap held ? held.Entrance : (Vec3?)null, new Vec3(at.x, at.y, at.z),
                (mine, theirs) => DungeonMaps.WalkedTogether(mine, theirs, RoomKinds.Of));
            WalkLinesBuilt.Clear();
            WalkLinesBuilt.AddRange(lines);
            WalkLineWords.Clear();
            foreach (var line in lines) WalkLineWords.Add((DungeonMapWords.Line(line), DungeonMapWords.LineTip(line)));
            _walkHeading = DungeonMapWords.Heading(lines.Count);
        }

        /// <summary>
        /// Opens Valheim's large map where a dungeon lies, the panel put away as its close button
        /// does so the map is seen: the game's own way to show a point on it (<c>Minimap.ShowPointOnMap</c>).
        /// </summary>
        private static void ShowOnTheMap(Vec3 at)
        {
            var map = Minimap.instance;
            if (map == null) return;
            AskClose();
            map.ShowPointOnMap(new Vector3(at.X, at.Y, at.Z));
        }

        /// <summary>A dungeon mark's picture by its look, in the panel's colours, as the large map and a dungeon's page show it (<see cref="MarkPictures"/>).</summary>
        internal static Sprite MarkPicture(MarkLook look) => MarkPictures.Of(look, Skin.Accent, Skin.OnAccent, Skin.Faint, Skin.Shade(Skin.Faint, 0.55f));

        /// <summary>A dungeon mark's picture by its glyph and look, in the panel's colours, as the map shows it (<see cref="MarkPictures"/>).</summary>
        internal static Sprite MarkPicture(MarkGlyph glyph, MarkLook look) => MarkPictures.Of(glyph, look, Skin.Accent, Skin.OnAccent, Skin.Faint, Skin.Shade(Skin.Faint, 0.55f));

        /// <summary>Draws the marks' pictures ahead in the panel's colours, one made a frame (<see cref="MarkPictures.DrawAhead"/>).</summary>
        internal static void DrawMarkPicturesAhead() => MarkPictures.DrawAhead(Skin.Accent, Skin.OnAccent, Skin.Faint, Skin.Shade(Skin.Faint, 0.55f));

        /// <summary>The first line as last drawn, and its hover, for the self-test.</summary>
        [Diagnostic]
        public static string FirstMapLineForTest { get; private set; }

        [Diagnostic]
        public static string FirstMapTipForTest { get; private set; }

        /// <summary>Shows a kept map on the stage, its inside.</summary>
        private static void OpenMap(string id)
        {
            ExampleLayouts.OpenMap(id);
            Stage.Inside = true;
        }
    }
}
