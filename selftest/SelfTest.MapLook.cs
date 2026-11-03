using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for Show on map following the settings' Map section.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Each of Show on map's settings, stepped through its values on the self-test's own
        /// spots: the other pins at full, a third or hidden; the map's veil off, half or strong;
        /// the dots small, normal or large beside each other; the map left at the player's zoom
        /// with zooming out off; a place's squares left out with squares off. The strong veil and
        /// the large dots are pictured. Every setting is put back.
        /// </summary>
        private static IEnumerator ShowOnMapFollowsItsSettings(Probe p)
        {
            MapKept.SetAsideForTest(true);
            var player = Player.m_localPlayer;
            var map = Minimap.instance;
            if (player == null || map == null || Game.m_noMap || CompendiumMap.World == null) p.Skip("there is no character, or no map in this world");
            MapLookAtDefaults();
            KeepMapView(map);
            Session.Hide();
            var here = player.transform.position;
            var spots = Grid(here, 200, 40);
            var before = MapDim.BrightnessForTest(null);

            // The other pins, as each value sets them, back as the kind is hidden.
            var pinsWrong = new List<string>();
            foreach (OtherPinsShown shown in Enum.GetValues(typeof(OtherPinsShown)))
            {
                Settings.MapOtherPins = shown;
                yield return ShowSpots(spots);
                var lit = MapDim.BrightnessForTest(null);
                var expected = before.Pins * MapLook.PinsShare(shown);
                if (Mathf.Abs(lit.Pins - expected) > 0.01f || MapDim.Dimming != MapLook.Dims(shown)) pinsWrong.Add($"{shown}: {Numbers.Fixed(lit.Pins, 2)} for {Numbers.Fixed(expected, 2)}");
            }
            Settings.MapOtherPins = OtherPinsShown.Third;
            p.Check(pinsWrong.Count == 0, "your other pins stand at full, a third or hidden as set", string.Join("; ", pinsWrong));

            // The veil over the map, none where it is off.
            var veilWrong = new List<string>();
            foreach (MapDarkening darkening in Enum.GetValues(typeof(MapDarkening)))
            {
                Settings.MapDarken = darkening;
                yield return ShowSpots(spots);
                var veil = MapDim.VeilForTest();
                var alpha = MapLook.Veil(darkening);
                if (veil.Shown != alpha > 0f || veil.Shown && Mathf.Abs(veil.Alpha - alpha) > 0.01f) veilWrong.Add($"{darkening}: shown {veil.Shown}, {Numbers.Fixed(veil.Alpha, 2)}");
                if (darkening == MapDarkening.Strong) yield return Picture(p, "show on map, the map darkened strongly");
            }
            Settings.MapDarken = MapDarkening.Half;
            p.Check(veilWrong.Count == 0, "the map lies darkened not at all, by half or strongly as set", string.Join("; ", veilWrong));

            // The dots beside their normal size.
            var sizes = new Dictionary<DotSizeChoice, float>();
            foreach (DotSizeChoice size in Enum.GetValues(typeof(DotSizeChoice)))
            {
                Settings.MapDotSize = size;
                yield return ShowSpots(spots);
                sizes[size] = MapSpots.DotSizeForTest;
                if (size == DotSizeChoice.Large) yield return Picture(p, "show on map, large dots");
            }
            Settings.MapDotSize = DotSizeChoice.Normal;
            var normal = sizes[DotSizeChoice.Normal];
            p.Check(normal > 0f && sizes.All(pair => Mathf.Abs(pair.Value / normal - MapLook.DotScale(pair.Key)) < 0.02f), "the dots stand small, normal or large as set",
                string.Join(", ", sizes.Select(pair => $"{pair.Key} {Numbers.Fixed(pair.Value, 1)}")));

            // Zooming out off: a spot far off leaves the map at the player's zoom.
            Settings.MapZoomOut = false;
            MapSpots.Hide();
            map.LargeZoom = SpotsZoom;
            MapSpots.Show(MadeUpSpots, "Self-test spots", Skin.KindColor(Kind.Resource), new List<(int X, int Z)> { ((int)Mathf.Round(here.x), (int)Mathf.Round(here.z) + 3000) });
            yield return null;
            yield return null;
            p.Check(Mathf.Abs(map.LargeZoom - SpotsZoom) < 0.0001f, "with zooming out off, a spot 3 km off leaves the map at your own zoom", $"zoom {Numbers.Fixed(map.LargeZoom, 3)}");
            Settings.MapZoomOut = true;
            MapSpots.Hide();

            // Squares off: a place whose squares the server told shows none, its button counting none.
            var told = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && !e.IsRoom && MapSpots.SquareCount(e) > 0);
            if (told == null) p.Untried("a place's squares left out with squares off", "join a world whose server runs this Scry, with a place in ground you explored but never came near", "squares off");
            else
            {
                Settings.MapSquares = false;
                MapSpots.Show(told);
                yield return null;
                yield return null;
                p.Check(MapSpots.SquareCount(told) == 0 && MapSquares.SquaresShown == 0 && !(MapSpots.Strip ?? "").Contains(" in square"),
                    $"with squares off, {told.ShownName}'s page shows none of the server's squares", MapSpots.Strip);
                Settings.MapSquares = true;
            }
            ShowOnMapAsItWas();
        }

        /// <summary>The self-test's spots shown afresh, the large map open, until they stand.</summary>
        private static IEnumerator ShowSpots(List<(int X, int Z)> spots)
        {
            MapSpots.Hide();
            yield return null;
            MapSpots.Show(MadeUpSpots, "Self-test spots", Skin.KindColor(Kind.Resource), spots);
            yield return Until(() => MapSpots.DotsShown > 0, 10);
            yield return null;
        }
    }
}
