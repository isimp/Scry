using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for a crowded map: a kind's dots merged with their count, plain far out, and one kind shown alone from the kinds strip.</summary>
    internal static partial class SelfTest
    {
        /// <summary>The settings, zoom, pan and pointer before the part, put back after it.</summary>
        private static (FarMarks Far, bool Strip, float Zoom, Vector3 Panned, Vector2 Pointer)? _crowdedBefore;

        /// <summary>
        /// Six copper deposits 12 m apart and three tin 12 m apart 120 m off, on ground away from any
        /// pin, with Far out on Plain, merged by kind and the kinds strip on: zoomed out until a dot
        /// spans some 70 m, each kind stands as one plain mark with its count; pointed at, a merged
        /// mark names its kind and how many; clicked, the map zooms in on it; at the nearest zoom
        /// every dot stands apart in its full look; the kinds strip stands, and tin shown alone
        /// fades the copper, shown again with a second click. Pictured far out, pointed at, near and
        /// with tin alone.
        /// </summary>
        private static IEnumerator CrowdedMapTold(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            var player = Player.m_localPlayer;
            var map = Minimap.instance;
            const string copper = "rock4_copper", tin = "MineRock_Tin";
            if (book == null || player == null || map == null || Game.m_noMap || X.Find(copper) == null || X.Find(tin) == null)
            {
                p.Skip("there is no character, no map, or no copper or tin in this game");
                yield break;
            }
            KeepWhatIsKept(book);
            NoteMapAsItWas(map);
            Settings.FarMarks = FarMarks.PlainMerged;
            Settings.KindsStrip = true;
            yield return MapCodeCompiled(p);
            var clear = ClearGround(map, player.transform.position);
            if (clear == null) p.Note("no explored ground lies away from the map's pins; the marks stand by you");
            var at = clear ?? new Vector2(player.transform.position.x, player.transform.position.z);
            var (cx, cz) = ((int)at.x, (int)at.y);
            var copperSpots = new List<(int X, int Z)>();
            for (var i = 0; i < 6; i++) copperSpots.Add((cx + i % 3 * 12, cz + i / 3 * 12));
            var tinSpots = new List<(int X, int Z)> { (cx + 120, cz), (cx + 132, cz), (cx + 144, cz) };
            MapSpots.SpotsInsteadForTest(k => k == copper ? copperSpots : k == tin ? tinSpots : new List<(int X, int Z)>());
            ScryPanel.TakeAllOffMap();
            ScryPanel.KeepOnMap(copper, true);
            ScryPanel.KeepOnMap(tin, true);
            Session.Hide();
            MapSpots.Hide();
            if (Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(new Vector2(2f, 2f));

            // Far out, a dot spanning some 70 m: each kind one plain mark with its count.
            map.SetMapMode(Minimap.MapMode.Large);
            map.LargeZoom = 0.06f;
            CentreMapOn(map, new Vector2(cx + 70, cz));
            // The zoom settled over a few frames, each from the touch the map's own zoom then gives,
            // as a touch read before the map draws at a new zoom is the zoom before it.
            for (var round = 0; round < 3; round++)
            {
                yield return null;
                yield return null;
                if (MapSpots.TouchForTest > 0f) map.LargeZoom = Mathf.Clamp(map.LargeZoom * 70f * MarkStacks.Touching / MapSpots.TouchForTest, SeenOnMap.NearestZoom, SeenOnMap.FarthestZoom);
            }
            yield return Until(() => MapSpots.DotsShown == 2 && MapSpots.MergedShownForTest == 2 && MapSpots.PlainForTest, 5);
            p.Note($"zoomed out to {Numbers.Amount(map.LargeZoom, 3)}, a dot spanning {Numbers.Amount(MapSpots.TouchForTest / MarkStacks.Touching, 0)} m");
            p.Check(MapSpots.DotsShown == 2 && MapSpots.MergedShownForTest == 2, "far out, each kind's dots that would cover each other stand as one mark with how many",
                $"{Numbers.Count(MapSpots.DotsShown)} marks, {Numbers.Count(MapSpots.MergedShownForTest)} counts");
            p.Check(MapSpots.PlainForTest, "far out, the marks stand in their plain look");
            yield return Picture(p, "a crowded patch far out, merged and plain");

            // The kinds strip, and tin shown alone, as a click on its chip does.
            var strips = ScryPanel.Drawn(PanelPart.MapKindsStrip);
            yield return Until(() => ScryPanel.Drawn(PanelPart.MapKindsStrip) > strips + 1, 3);
            p.Check(ScryPanel.Drawn(PanelPart.MapKindsStrip) > strips && MapSpots.KindsStanding().Count == 2, "the kinds strip lists the two kinds kept", Numbers.Count(MapSpots.KindsStanding().Count));
            // Folded by its arrow, the list stands as its title alone and is kept so; then as it was.
            var foldedBefore = ScryPanel.KindsFoldedForTest;
            ScryPanel.FoldKindsForTest(true);
            yield return null;
            yield return Picture(p, "the kinds list folded to its title");
            p.Check(ScryPanel.KindsFoldedForTest && ScryPanel.RememberedForTest().KindsFolded == true, "folded by its arrow, the list stands as its title and is kept folded with the panel's place");
            ScryPanel.FoldKindsForTest(foldedBefore);
            yield return null;
            MapSpots.ShowAlone(tin);
            yield return Until(() => MapSpots.FadedDotsForTest == 1, 3);
            p.Check(MapSpots.FadedDotsForTest == 1 && MapSpots.FadedCountsForTest == 1 && MapSpots.PickedOverForTest == 1 && MapSpots.IsPicked(tin), "tin picked alone, the copper's mark and its count fade, the tin standing over the game's pins", $"{Numbers.Count(MapSpots.FadedDotsForTest)} marks, {Numbers.Count(MapSpots.FadedCountsForTest)} counts faded");
            yield return Picture(p, "a crowded patch with tin shown alone");
            // Copper picked too: both stand out; tin let go: copper alone; copper let go: all alike again.
            MapSpots.ShowAlone(copper);
            yield return Until(() => MapSpots.FadedDotsForTest == 0, 3);
            p.Check(MapSpots.FadedDotsForTest == 0 && MapSpots.PickedCount == 2, "copper picked too, both stand out", $"{Numbers.Count(MapSpots.FadedDotsForTest)} faded, {Numbers.Count(MapSpots.PickedCount)} picked");
            MapSpots.ShowAlone(tin);
            yield return Until(() => MapSpots.FadedDotsForTest == 1, 3);
            p.Check(MapSpots.FadedDotsForTest == 1 && MapSpots.IsPicked(copper) && !MapSpots.IsPicked(tin), "tin let go, it fades and copper stands out alone");
            MapSpots.ShowAlone(copper);
            yield return Until(() => MapSpots.FadedDotsForTest == 0, 3);
            p.Check(MapSpots.FadedDotsForTest == 0 && MapSpots.PickedCount == 0, "the last let go, every kind shows again");

            yield return MergedMarkTold(p, map);

            // At the nearest zoom, 12 m apart, every dot stands apart in its full look.
            map.LargeZoom = SeenOnMap.NearestZoom;
            CentreMapOn(map, new Vector2(cx + 70, cz));
            yield return Until(() => MapSpots.DotsShown == 9 && !MapSpots.PlainForTest, 5);
            if (MapSpots.TouchForTest < 12f)
                p.Check(MapSpots.DotsShown == 9 && MapSpots.MergedShownForTest == 0 && !MapSpots.PlainForTest, "near, every dot stands apart in its full look",
                    $"{Numbers.Count(MapSpots.DotsShown)} dots, {Numbers.Count(MapSpots.MergedShownForTest)} counts, plain {MapSpots.PlainForTest}");
            else p.Note($"at the nearest zoom dots still touch within {Numbers.Amount(MapSpots.TouchForTest, 1)} m, so dots 12 m apart stay merged");
            yield return Picture(p, "a crowded patch near, every dot apart");
            CrowdedAsItWas();
        }

        /// <summary>A merged mark pointed at: its words, its other dots and its count over them; the wheel kept from the map on the kinds list; its menu zooming in on it.</summary>
        private static IEnumerator MergedMarkTold(Probe p, Minimap map)
        {
            // A merged mark pointed at names its kind and how many; clicked, the map zooms in on it.
            if (MapSpots.LoneDotForTest() is Vector2 merged)
            {
                PointerTo(merged);
                yield return Until(() => (MapSpots.Pointed?.Words ?? "").Contains("right-click: more"), 3);
                var said = MapSpots.Pointed?.Words ?? "";
                p.Check(said.Contains(" here\n") && said.Contains("right-click: more"), "a merged mark pointed at names its kind and how many, and what a click does", said);
                // Every other dot it stands for shows at its own spot, ringed in the accent.
                var count = said.StartsWith("6 ", System.StringComparison.Ordinal) ? 5 : 2;
                yield return Until(() => MapSpots.MembersShownForTest == count, 3);
                p.Check(MapSpots.MembersShownForTest == count, "pointed at, the merged mark's other dots show where each lies", $"{Numbers.Count(MapSpots.MembersShownForTest)} of {Numbers.Count(count)}");
                yield return Until(() => MapSpots.PointedCountOnTopForTest, 3);
                p.Check(MapSpots.PointedCountOnTopForTest, "pointed at, the merged mark's count stands over the dots it brings up");
                p.Check(!WheelBlock.Blocks(), "with the pointer on the map, the wheel is the map's");
                yield return Picture(p, "a crowded patch, a merged mark pointed at");
                // On the kinds list the wheel is the list's: the map under it hears none.
                PointerTo(MapSpots.KindsListMiddleForTest);
                yield return null;
                yield return null;
                p.Check(WheelBlock.Blocks(), "with the pointer on the kinds list, the map hears no wheel");
                p.Check(MapSpots.PointedCountsForTest == 0, "the pointer gone from the merged mark, its count stands over the rest no more", Numbers.Count(MapSpots.PointedCountsForTest));
                PointerTo(merged);
                yield return Until(() => (MapSpots.Pointed?.Words ?? "").Contains("right-click: more"), 3);
                var zoom = map.LargeZoom;
                RightClick(map);
                yield return Until(() => MapSpots.MenuOpen, 3);
                var zoomLine = MapSpots.MenuWordsForTest.IndexOf(SpotWords.MenuWord(MapMenuChoice.ZoomIn, 2));
                if (zoomLine >= 0) MapSpots.PickMenuForTest(zoomLine);
                else MapSpots.CloseMenu();
                yield return null;
                p.Check(map.LargeZoom < zoom && map.m_mode == Minimap.MapMode.Large, "its menu zooms the map in on the merged mark", $"{Numbers.Amount(zoom, 3)} to {Numbers.Amount(map.LargeZoom, 3)}");
            }
            else p.Check(false, "a merged mark stands on the screen to point at");
        }

        /// <summary>The settings, the map's zoom and pan and the pointer noted, for <see cref="CrowdedAsItWas"/> to put back.</summary>
        private static void NoteMapAsItWas(Minimap map) => _crowdedBefore = (Settings.FarMarks, Settings.KindsStrip, map.LargeZoom, MapPanned(map), ZInput.pointerPosition);

        /// <summary>The settings, the map's zoom and pan, the pointer and what was kept put back, should the part have stopped before it did.</summary>
        private static void CrowdedAsItWas()
        {
            MapSpots.ShowAlone((string)null);
            if (_crowdedBefore is (FarMarks far, bool strip, float zoom, Vector3 panned, Vector2 pointer))
            {
                Settings.FarMarks = far;
                Settings.KindsStrip = strip;
                if (Minimap.instance != null)
                {
                    Minimap.instance.LargeZoom = zoom;
                    MapPanned(Minimap.instance) = panned;
                }
                if (Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(pointer);
            }
            _crowdedBefore = null;
            KeptOnMapAsItWas();
        }
    }
}
