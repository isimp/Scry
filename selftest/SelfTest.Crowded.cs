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
            _crowdedBefore = (Settings.FarMarks, Settings.KindsStrip, map.LargeZoom, MapPanned(map), ZInput.pointerPosition);
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
            yield return Until(() => MapSpots.TouchForTest > 0f, 5);
            map.LargeZoom = Mathf.Clamp(0.06f * 70f * MarkStacks.Touching / MapSpots.TouchForTest, SeenOnMap.NearestZoom, SeenOnMap.FarthestZoom);
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
            MapSpots.ShowAlone(tin);
            yield return Until(() => MapSpots.FadedDotsForTest == 1, 3);
            p.Check(MapSpots.FadedDotsForTest == 1 && MapSpots.ShownAlone == tin, "tin shown alone, the copper's mark fades", $"{Numbers.Count(MapSpots.FadedDotsForTest)} faded");
            yield return Picture(p, "a crowded patch with tin shown alone");
            MapSpots.ShowAlone(tin);
            yield return Until(() => MapSpots.FadedDotsForTest == 0, 3);
            p.Check(MapSpots.FadedDotsForTest == 0 && MapSpots.ShownAlone == null, "clicked again, every kind shows again");

            // A merged mark pointed at names its kind and how many; clicked, the map zooms in on it.
            if (MapSpots.LoneDotForTest() is Vector2 merged)
            {
                PointerTo(merged);
                yield return Until(() => (MapSpots.Pointed?.Words ?? "").Contains("click to zoom in"), 3);
                var said = MapSpots.Pointed?.Words ?? "";
                p.Check(said.Contains(" here\n") && said.Contains("click to zoom in"), "a merged mark pointed at names its kind and how many, and that a click zooms in", said);
                yield return Picture(p, "a crowded patch, a merged mark pointed at");
                var zoom = map.LargeZoom;
                MapSpots.PressedForTest(merged);
                map.OnMapLeftClick();
                yield return null;
                p.Check(map.LargeZoom < zoom && map.m_mode == Minimap.MapMode.Large, "clicked, the map zooms in on the merged mark", $"{Numbers.Amount(zoom, 3)} to {Numbers.Amount(map.LargeZoom, 3)}");
            }
            else p.Check(false, "a merged mark stands on the screen to point at");

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

        /// <summary>The settings, the map's zoom and pan, the pointer and what was kept put back, should the part have stopped before it did.</summary>
        private static void CrowdedAsItWas()
        {
            MapSpots.ShowAlone(null);
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
