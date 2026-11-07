using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for the mark beside a lit Keep on the map, on a kind's page and on the map's strip.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Kept from its page, a kind's mark stands beside the lit Keep on the map, and a click on
        /// it opens the picker under it; a kind not kept has none. The map strip's Keep on the map
        /// has the same mark beside it, its picker under it, and a pick stands on the map at once.
        /// Taken off, the mark and its picker go. What was kept, each in its mark, is put back.
        /// </summary>
        private static IEnumerator MarkOfferedAsKept(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            var player = Player.m_localPlayer;
            var kind = X.Find("rock4_copper") != null ? "rock4_copper" : null;
            if (book == null || player == null || Minimap.instance == null || Game.m_noMap || kind == null)
            {
                p.Skip("there is no character, no map, or no copper deposit in this game");
                yield break;
            }
            KeepWhatIsKept(book);
            yield return MapCodeCompiled(p);
            var spots = SpreadSpots(player.transform.position);
            MapSpots.SpotsInsteadForTest(k => k == kind ? spots : new List<(int X, int Z)>());
            ScryPanel.TakeAllOffMap();

            // Not kept: no mark beside Keep on the map.
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var keeps = ScryPanel.Drawn(PanelPart.PageKeep);
            Select(X.Find(kind));
            yield return Until(() => ScryPanel.Drawn(PanelPart.PageKeep) > keeps + 1, 5);
            p.Check(ScryPanel.Drawn(PanelPart.PageKeep) > keeps && ScryPanel.MarkPieceForTest(strip: false) == null, "a kind not kept shows no mark beside its page's Keep on the map");

            // Kept as its page's button keeps it, its mark joins the lit button.
            ScryPanel.KeepOnMap(kind, true);
            yield return Until(() => ScryPanel.MarkPieceForTest(strip: false) == kind, 3);
            p.Check(ScryPanel.MarkPieceForTest(strip: false) == kind, "kept from its page, its mark stands beside the lit Keep on the map");

            // A click on the mark opens the picker under it, every choice offered.
            ScryPanel.PressMarkPieceForTest(strip: false);
            yield return Until(() => ScryPanel.MarkPickerShownForTest, 3);
            var choices = KeptMarks.Colours + KeptMarks.Shapes + KeptMarks.Pictures + 1;
            p.Check(ScryPanel.MarkPickerShownForTest && ScryPanel.MarkPickerFromForTest == "Page" && ScryPanel.MarkChoicesForTest.Count == choices,
                "a click on the mark beside Keep on the map opens the picker, every colour, shape and picture offered", $"from {ScryPanel.MarkPickerFromForTest ?? "nowhere"}, {Numbers.Count(ScryPanel.MarkChoicesForTest.Count)} choices");
            var (anchor, top, height) = ScryPanel.MarkPickerPlaceForTest;
            p.Check(top >= anchor.yMax - 0.5f || top + height <= anchor.y + 0.5f, "the picker stands under the mark, or over it where the panel ends",
                $"mark {Numbers.Amount(anchor.y, 0)} to {Numbers.Amount(anchor.yMax, 0)}, picker from {Numbers.Amount(top, 0)}");
            yield return Picture(p, "page, the mark picker beside Keep on the map");
            var fire = KeptMarks.Pictured(2, 0);
            ScryPanel.PickMark(kind, fire);
            p.Check(book.MarkOf(kind) == fire && ScryPanel.MarkPickerShownForTest, "a pick from the page is kept, the picker staying open for another");
            yield return Picture(p, "page, the fire picked beside Keep on the map");
            ScryPanel.OpenMarkPickerForTest(null);

            // The map strip's Keep on the map has the mark beside it too, its picker under it.
            Session.Hide();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            LookCloserAtMarks();
            var marks = ScryPanel.Drawn(PanelPart.MapKeepMark);
            MapSpots.Show(X.Find(kind));
            yield return Until(() => ScryPanel.Drawn(PanelPart.MapKeepMark) > marks + 1 && ScryPanel.MarkPieceForTest(strip: true) == kind, 5);
            p.Check(ScryPanel.MarkPieceForTest(strip: true) == kind, "the map strip's Keep on the map has the kept kind's mark beside it");
            ScryPanel.PressMarkPieceForTest(strip: true);
            yield return Until(() => ScryPanel.MarkPickerShownForTest && ScryPanel.MarkPickerFromForTest == "Strip", 3);
            (anchor, top, height) = ScryPanel.MarkPickerPlaceForTest;
            p.Check(ScryPanel.MarkPickerShownForTest && ScryPanel.MarkPickerFromForTest == "Strip" && (top >= anchor.yMax - 0.5f || top + height <= anchor.y + 0.5f),
                "a click on the strip's mark opens the picker under it, over the map", $"from {ScryPanel.MarkPickerFromForTest ?? "nowhere"}, mark to {Numbers.Amount(anchor.yMax, 0)}, picker from {Numbers.Amount(top, 0)}");
            yield return Picture(p, "map strip, the mark picker beside Keep on the map");
            var own = KeptMarks.Own(4);
            ScryPanel.PickMark(kind, own);
            var ore = MapSpots.MarkPicture(own, kind);
            yield return Until(() => MapSpots.DotPictureForTest(0) == ore, 10);
            p.Check(book.MarkOf(kind) == own && MapSpots.DotPictureForTest(0) == ore, "a pick from the strip stands on the map at once", ore != null ? ore.texture.name : "none");
            yield return Picture(p, "map strip, the deposit in its own picture");

            // Taken off, the mark and its picker go.
            ScryPanel.KeepOnMap(kind, false);
            yield return Until(() => !ScryPanel.MarkPickerShownForTest && ScryPanel.MarkPieceForTest(strip: true) == null, 3);
            p.Check(!ScryPanel.MarkPickerShownForTest && ScryPanel.MarkPieceForTest(strip: true) == null, "taken off the map, its mark and its picker go from the strip");
            MapSpots.Hide();
            MarkOfferedAsItWas();
        }

        /// <summary>What was kept put back, each in its mark, the picker and the maps shut, the panel open.</summary>
        private static void MarkOfferedAsItWas()
        {
            ScryPanel.OpenMarkPickerForTest(null);
            MarksLookedAtAsBefore();
            MapSpots.Hide();
            KeptOnMapAsItWas();
        }

        /// <summary>The large map's zoom before a part looked closer at its marks; put back after.</summary>
        private static float? _zoomBeforeMarks;

        /// <summary>Three made-up spots of a kind round the character, far enough apart, and from the character's own arrow, to be told apart on a map looked at closely.</summary>
        private static List<(int X, int Z)> SpreadSpots(Vector3 here) => new List<(int X, int Z)>
        {
            ((int)here.x + 140, (int)here.z + 20), ((int)here.x - 150, (int)here.z + 90), ((int)here.x + 30, (int)here.z - 160),
        };

        /// <summary>
        /// The large map looked at closely, as play sees a mark near, so its picture can be judged
        /// in the part's pictures; every ground counted shown, so the made-up spots stand.
        /// </summary>
        private static void LookCloserAtMarks()
        {
            CompendiumMap.ShownEverywhereForTest(true);
            var map = Minimap.instance;
            if (map == null) return;
            if (_zoomBeforeMarks == null) _zoomBeforeMarks = map.LargeZoom;
            map.LargeZoom = 0.04f;
        }

        /// <summary>The large map's zoom put back, and only the map's own ground counted shown again.</summary>
        private static void MarksLookedAtAsBefore()
        {
            CompendiumMap.ShownEverywhereForTest(false);
            if (_zoomBeforeMarks is float was && Minimap.instance != null) Minimap.instance.LargeZoom = was;
            _zoomBeforeMarks = null;
        }
    }
}
