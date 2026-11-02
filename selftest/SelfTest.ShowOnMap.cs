using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's look at Show on map (<see cref="MapSpots"/>): which pages show which kind,
    /// a thousand spots around the character shown as the button shows a kind, all coming to stand
    /// within a few seconds and holding no frame long, under the game's pins, smaller zoomed out,
    /// the strip naming them at the map's top left; Hide and closing the map taking them away; and
    /// a page's own spots shown where the character saw any.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>The made-up kind the thousand spots are shown as.</summary>
        private const string MadeUpSpots = "scry self-test spots";

        /// <summary>The large map's zoom and mode before the part, put back after it.</summary>
        private static float? _zoomBefore;
        private static Minimap.MapMode? _modeBefore;

        /// <summary>The zoom the thousand spots are shown at: the game's own first zoom, a window some 2.5 km tall, holding them all.</summary>
        private const float SpotsZoom = 0.1f;

        private static IEnumerator ShowOnMapDots(Probe p)
        {
            var player = Player.m_localPlayer;
            var map = Minimap.instance;
            if (player == null || map == null || Game.m_noMap || CompendiumMap.World == null) p.Skip("there is no character, or no map in this world");
            PagesShowingSpots(p);

            // A thousand spots on a grid around the character, 25 metres apart, so each dot shows apart at the first zoom, one where it stands.
            const int many = 1000;
            var here = player.transform.position;
            var spots = Grid(here, many, 25);
            _zoomBefore = map.LargeZoom;
            _modeBefore = map.m_mode;
            map.LargeZoom = SpotsZoom;
            Session.Hide();
            var first = Timing.Measuring?.Frames ?? 0;
            Timing.ForgetBytesByPart();
            MapSpots.Show(MadeUpSpots, "Self-test spots", Skin.KindColor(Kind.Resource), spots);
            var opened = Time.unscaledTime;
            yield return Until(() => MapSpots.DotsShown >= many, 20);
            var took = Time.unscaledTime - opened;
            // A few frames more, standing, for what placing them costs a frame once all are made.
            yield return new Wait(1);
            var frames = System.Math.Max(1, (Timing.Measuring?.Frames ?? 0) - first);
            var mostMs = Timing.MostMs("spots");
            var ownMs = Timing.OwnBytesByPartCopy().Where(part => part.Part == "spots").Sum(part => part.Ms);
            p.Check(map.m_mode == Minimap.MapMode.Large, "the button opens the large map");
            p.Check(MapSpots.DotsShown >= many, $"all {Numbers.Count(many)} spots in view come to stand as dots", $"{Numbers.Count(MapSpots.DotsShown)} after {Numbers.Amount(took, 1)} s");
            p.Note($"{Numbers.Count(MapSpots.DotsShown)} dots stood after {Numbers.Amount(took, 1)} s; they took {Numbers.Fixed(ownMs / frames, 2)} ms a frame on average, {Numbers.Amount(mostMs, 1)} ms at the most");
            p.Within(mostMs, 16, "making and placing them holds no frame a sixtieth of a second", $"{Numbers.Amount(mostMs, 1)} ms at the most");
            p.Check(MapSpots.Strip == "Self-test spots · 1,000 you saw · nearest beside you", "the strip names them, how many, and the nearest", MapSpots.Strip);
            p.Check(MapSpots.DotsUnderPinsForTest, "the dots stand first on the pins' layer, under every pin");
            yield return null;
            var strip = MapSpots.StripRectForTest;
            var shown = MapSpots.MapOnScreen();
            p.Check(shown is Rect area && area.Contains(strip.min) && area.Contains(strip.max) && strip.x - area.x < area.width / 4f && strip.y - area.y < area.height / 4f,
                "the strip stands inside the map, at its top left",
                $"strip at {Numbers.Point(strip.x, strip.y, 0)}, map at {(shown is Rect at ? Numbers.Point(at.x, at.y, 0) + " to " + Numbers.Point(at.xMax, at.yMax, 0) : "none")}");
            yield return Picture(p, "show on map, a thousand dots around you");

            // Zoomed out the dots are smaller; shown on a grid 200 metres apart, kilometres across, so they stand clear of the pins round the character.
            var near = MapSpots.DotSizeForTest;
            map.LargeZoom = SeenOnMap.FarthestZoom;
            MapSpots.Show(MadeUpSpots, "Self-test spots", Skin.KindColor(Kind.Resource), Grid(here, many, 200));
            // The dots shown before stand until the next placing: wait for those placed at this zoom.
            yield return Until(() => MapSpots.DotSizeForTest < near && MapSpots.DotsShown > many / 2, 10);
            yield return null;
            var far = MapSpots.DotSizeForTest;
            p.Check(far > 0f && far < near, "zoomed out, the dots are drawn smaller", $"{Numbers.Amount(near, 1)} near, {Numbers.Amount(far, 1)} zoomed out");
            yield return Picture(p, "show on map, a thousand dots zoomed out");
            map.LargeZoom = SpotsZoom;

            // Hide takes them away and leaves the map open.
            MapSpots.Hide();
            yield return null;
            p.Check(MapSpots.DotsShown == 0 && MapSpots.Strip == null && map.m_mode == Minimap.MapMode.Large, "Hide takes the dots and the strip away, the map staying open");

            // Shown again and the map closed: gone, so the next time it opens it shows as usual.
            MapSpots.Show(MadeUpSpots, "Self-test spots", Skin.KindColor(Kind.Resource), spots);
            yield return Until(() => MapSpots.DotsShown > 0, 10);
            map.SetMapMode(Minimap.MapMode.Small);
            yield return null;
            yield return null;
            p.Check(MapSpots.KindShown == null, "closing the map takes them away");
            map.SetMapMode(Minimap.MapMode.Large);
            yield return null;
            yield return null;
            p.Check(MapSpots.DotsShown == 0 && MapSpots.Strip == null, "and the map opened again shows none");
            map.SetMapMode(Minimap.MapMode.Small);
            yield return null;

            // A page's own spots, as its button shows them.
            var copper = X.Find("rock4_copper");
            var kind = MapSpots.KindOf(copper);
            var count = kind != null ? SeenSpotsKeeper.CountOf(kind) : 0;
            if (copper == null || count == 0)
            {
                p.Untried("a page's own spots shown on the map", "see a copper deposit, then run the test again", "deposit");
            }
            else
            {
                MapSpots.Show(copper);
                yield return Until(() => MapSpots.DotsShown > 0, 10);
                p.Check(MapSpots.KindShown == "rock4_copper" && (MapSpots.Strip ?? "").StartsWith(copper.ShownName + " · " + Numbers.Count(count) + " you saw", System.StringComparison.Ordinal),
                    $"Copper deposit's page shows the {Numbers.Count(count)} the character saw", MapSpots.Strip);
                yield return Picture(p, "show on map, the copper deposits you saw");
            }
            ShowOnMapAsItWas();
        }

        /// <summary>
        /// The server's squares (B) as a place's page shows them: squares round the character, as
        /// the self-test gives them, each standing where the map shows its 512 m, under the dots and
        /// the pins, cut to explored ground pixel for pixel as the map's fog lies, a count on one
        /// holding more than one, the strip counting them; Hide taking them away. Then a place whose
        /// squares the world's server told, where one is.
        /// </summary>
        private static IEnumerator ShowOnMapSquares(Probe p)
        {
            var player = Player.m_localPlayer;
            var map = Minimap.instance;
            if (player == null || map == null || Game.m_noMap || CompendiumMap.World == null) p.Skip("there is no character, or no map in this world");
            var here = player.transform.position;
            var middleX = PlaceSquares.Index(here.x);
            var middleZ = PlaceSquares.Index(here.z);
            // Five by five squares round the character's own, one of them holding three; two spots seen in the middle one.
            var squares = new List<(int X, int Z, int Left)>();
            for (var dx = -2; dx <= 2; dx++) for (var dz = -2; dz <= 2; dz++) squares.Add((middleX + dx, middleZ + dz, dx == 1 && dz == 1 ? 3 : 1));
            var spots = new List<(int X, int Z)> { ((int)Mathf.Round(here.x), (int)Mathf.Round(here.z)), ((int)Mathf.Round(here.x) + 20, (int)Mathf.Round(here.z)) };
            _zoomBefore = map.LargeZoom;
            _modeBefore = map.m_mode;
            map.LargeZoom = SpotsZoom;
            Session.Hide();
            Timing.ForgetBytesByPart();
            MapSpots.Show(MadeUpSpots, "Self-test places", Skin.KindColor(Kind.Location), spots, squares);
            yield return Until(() => MapSquares.SquaresShown >= squares.Count, 10);
            yield return new Wait(1);
            p.Check(MapSquares.SquaresShown == squares.Count, $"all {Numbers.Count(squares.Count)} squares in view stand on the map", Numbers.Count(MapSquares.SquaresShown));
            p.Within(Timing.MostMs("spots"), 16, "making and placing them holds no frame a sixtieth of a second", $"{Numbers.Amount(Timing.MostMs("spots"), 1)} ms at the most");
            p.Check(MapSpots.Strip == "Self-test places · 2 you saw · nearest beside you · 27 more in squares", "the strip counts the places in squares once, after those seen", MapSpots.Strip);
            p.Check(MapSquares.UnderDotsForTest && MapSpots.DotsUnderPinsForTest, "the squares stand first on the pins' layer, under the dots and every pin, clipped to the map's window");
            p.Check(MapSquares.CountsShown == 1, "the one square holding three carries its count", Numbers.Count(MapSquares.CountsShown));
            var (read, wrong, fog) = MapSquares.MaskAgainstMapForTest();
            p.Check(read > 0 && wrong == 0, "each square is cut to explored ground, pixel for pixel as the map's fog lies", $"{Numbers.Count(wrong)} of {Numbers.Count(read)} pixels otherwise");
            if (fog == 0) p.Untried("a square cut where the fog starts", "stand within a kilometre of unexplored ground", "fog");
            else p.Note($"{Numbers.Count(fog)} of the {Numbers.Count(read)} pixels under the squares lie in fog, and are left out");
            yield return Picture(p, "show on map, squares round you");
            map.LargeZoom = SeenOnMap.NearestZoom * 4f;
            yield return Until(() => MapSquares.SquaresShown > 0, 5);
            yield return null;
            yield return Picture(p, "show on map, a square near");

            MapSpots.Hide();
            yield return null;
            p.Check(MapSquares.SquaresShown == 0 && MapSpots.Strip == null, "Hide takes the squares away with the dots");
            map.LargeZoom = SpotsZoom;
            map.SetMapMode(Minimap.MapMode.Small);
            yield return null;

            // A place whose squares the world's server told, as its button shows them.
            var heard = CompendiumMap.Counts.Heard(_ => true);
            p.Note($"the server told of squares: {heard}");
            var told = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && !e.IsRoom && MapSpots.SquareCount(e) > 0);
            if (told == null)
            {
                p.Untried("a place's own squares from the server", heard == SquaresHeard.Told ? "explore ground holding a place you never came near" : "join a world whose server runs this Scry", "squares");
            }
            else
            {
                var count = MapSpots.SquareCount(told);
                MapSpots.Show(told);
                yield return Until(() => MapSquares.SquaresShown > 0, 10);
                var strip = MapSpots.Strip ?? "";
                p.Check(MapSpots.SquaresOf(told, SeenSpotsKeeper.Of(MapSpots.KindOf(told))).Count == count && (strip.Contains(" in squares") || strip.Contains(" in a square")),
                    $"{told.ShownName}'s page shows the {Numbers.Count(count)} squares the server told", MapSpots.Strip);
                yield return Picture(p, "show on map, a place's squares from the server");
            }
            ShowOnMapAsItWas();
        }

        /// <summary>Spots on a grid round a point, 40 across, so many a row, this many metres apart, one on the point.</summary>
        private static List<(int X, int Z)> Grid(Vector3 here, int many, int apart)
        {
            var spots = new List<(int X, int Z)>(many);
            for (var i = 0; i < many; i++) spots.Add(((int)Mathf.Round(here.x) + (i % 40 - 20) * apart, (int)Mathf.Round(here.z) + (i / 40 - 12) * apart));
            return spots;
        }

        /// <summary>Which pages show spots, and by which kind: a deposit and its broken form by the whole one, a place by its own key, none for a tree or a dungeon's room.</summary>
        private static void PagesShowingSpots(Probe p)
        {
            var copper = X.Find("rock4_copper");
            if (copper == null) p.Note("no rock4_copper page");
            else
            {
                p.Check(MapSpots.KindOf(copper) == "rock4_copper", "a copper deposit's page shows copper deposits", MapSpots.KindOf(copper) ?? "none");
                var broken = (copper.Source as GameObject).OrNull()?.GetComponent<Destructible>().OrNull()?.m_spawnWhenDestroyed;
                var brokenPage = broken != null ? X.Find(broken.name) : null;
                if (brokenPage != null) p.Check(MapSpots.KindOf(brokenPage) == "rock4_copper", $"and {broken.name}'s page the whole ones it comes of", MapSpots.KindOf(brokenPage) ?? "none");
            }
            var beech = X.Find("Beech1");
            if (beech != null) p.Check(MapSpots.KindOf(beech) == null, "a beech's page has no Show on map", MapSpots.KindOf(beech));
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && !e.IsRoom);
            if (place != null) p.Check(MapSpots.KindOf(place) == place.Key, $"a place's page ({place.Name}) shows places of its kind", MapSpots.KindOf(place) ?? "none");
            var room = X.Catalog.FirstOrDefault(e => e.IsRoom);
            if (room != null) p.Check(MapSpots.KindOf(room) == null, $"a dungeon room's page ({room.Name}) has none", MapSpots.KindOf(room));
            p.Note($"in this world the button {(MapSpots.BlockOf(1) == MapBlock.None ? "can show what was seen" : "is greyed: " + SpotWords.WhyNot(MapSpots.BlockOf(1)))}");
        }

        /// <summary>Takes the dots away and puts the map and its zoom back, should the part have stopped before it did.</summary>
        private static void ShowOnMapAsItWas()
        {
            MapSpots.Hide();
            var map = Minimap.instance;
            if (map == null) return;
            if (_zoomBefore is float zoom) map.LargeZoom = zoom;
            _zoomBefore = null;
            map.SetMapMode(_modeBefore ?? Minimap.MapMode.Small);
            _modeBefore = null;
        }
    }
}
