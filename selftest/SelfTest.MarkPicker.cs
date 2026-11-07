using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for picking a kept kind's mark in the On the map list.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// A click on a kept line's mark opens its picker: the eight colours, the four shapes, the
        /// game's ten map pictures, and a copper deposit's own picture, its ore. A picture picked
        /// stands on the large map at once, drawn inside the ring, and is kept with the character;
        /// a creature's own picture is its trophy, a place has none. Drawing a picture holds no
        /// frame long. What was kept, each in its mark, is put back.
        /// </summary>
        private static IEnumerator MarkPicked(Probe p)
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
            ScryPanel.KeepOnMap(kind, true);

            // The game's pictures are found, and the deposit's own, its ore.
            p.Check(KeptMarkPictures.GameFoundForTest == KeptMarks.Pictures, "every one of the game's map pictures offered is found in the game", $"{Numbers.Count(KeptMarkPictures.GameFoundForTest)} of {Numbers.Count(KeptMarks.Pictures)}");
            var ownName = KeptMarkPictures.OwnNameOf(kind);
            p.Check(ownName != null && KeptMarkPictures.OwnOf(kind) != null, "a copper deposit's own picture is what it drops", ownName ?? "none");
            var creature = X.Find("Greydwarf") != null ? "Greydwarf" : null;
            if (creature != null) p.Check(KeptMarkPictures.OwnNameOf(creature) is string trophy && X.Find("TrophyGreydwarf")?.ShownName == trophy, "a creature's own picture is its trophy", KeptMarkPictures.OwnNameOf(creature) ?? "none");
            else p.Untried("a creature's own picture is its trophy", "a game with Greydwarfs", "Greydwarf");
            var place = WorldCatalog.Current?.All.FirstOrDefault(e => e.Kind == Kind.Location && !e.IsRoom);
            if (place != null) p.Check(KeptMarkPictures.OwnOf(place.Key) == null, "a place has no picture of its own", place.Name);

            // The list's mark opens the picker over the list.
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            ScryPanel.OpenKeptListForTest(true);
            ScryPanel.OpenMarkPickerForTest(kind);
            yield return Until(() => ScryPanel.MarkPickerShownForTest, 3);
            var choices = ScryPanel.MarkChoicesForTest;
            p.Check(ScryPanel.MarkPickerShownForTest && choices.Count == KeptMarks.Colours + KeptMarks.Shapes + KeptMarks.Pictures + 1,
                "a kept line's mark opens its picker: every colour, every shape, the game's pictures and its own", $"{Numbers.Count(choices.Count)} choices");
            p.Check(choices.Contains(KeptMarks.Own(KeptMarks.ColourOf(book.MarkOf(kind) ?? 0))), "the picker offers the deposit's own picture in its colour");
            yield return Picture(p, "mark picker, a copper deposit's");

            // A game picture picked stands on the map at once, inside the ring.
            var hammer = KeptMarks.Pictured(5, 2);
            var drawn = System.Diagnostics.Stopwatch.StartNew();
            ScryPanel.PickMark(kind, hammer);
            var made = MapSpots.MarkPicture(hammer, kind);
            drawn.Stop();
            p.Check(book.MarkOf(kind) == hammer, "the mark picked is kept with the character");
            p.Within(drawn.Elapsed.TotalMilliseconds, 40, "drawing a mark in a picture of the game's holds no frame long", $"{Numbers.Amount(drawn.Elapsed.TotalMilliseconds, 2)} ms");
            p.Check(made != null && made.texture.name == "Scry pictured spot", "the hammer is drawn into the mark", (made != null ? made.texture.name : "none"));
            yield return Picture(p, "mark picker, the hammer picked");
            ScryPanel.OpenKeptListForTest(false);
            Session.Hide();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            LookCloserAtMarks();
            yield return Until(() => MapSpots.DotsShown == spots.Count && MapSpots.DotPictureForTest(0) == made, 10);
            p.Check(MapSpots.DotsShown == spots.Count && Enumerable.Range(0, spots.Count).All(d => MapSpots.DotPictureForTest(d) == made), "the large map's dots stand in the hammer at once",
                $"{Numbers.Count(MapSpots.DotsShown)} dots, {(MapSpots.DotPictureForTest(0) is Sprite first && first != null ? first.texture.name : "none")}");
            yield return Picture(p, "map, kept deposit in the hammer");

            // Its own picture on its colour.
            var own = KeptMarks.Own(1);
            drawn.Restart();
            ScryPanel.PickMark(kind, own);
            var ore = MapSpots.MarkPicture(own, kind);
            drawn.Stop();
            p.Within(drawn.Elapsed.TotalMilliseconds, 40, "drawing a mark in a kind's own picture holds no frame long", $"{Numbers.Amount(drawn.Elapsed.TotalMilliseconds, 2)} ms");
            yield return Until(() => MapSpots.DotPictureForTest(0) == ore, 10);
            p.Check(ore != null && ore.texture.name == "Scry pictured spot" && MapSpots.DotPictureForTest(0) == ore, "the deposit's own picture stands on the map as picked", (ore != null ? ore.texture.name : "none"));
            yield return Picture(p, "map, kept deposit in its own picture");
            MarkPickedAsItWas();
        }

        /// <summary>What was kept put back, each in its mark, the picker and the maps shut, the panel open.</summary>
        private static void MarkPickedAsItWas()
        {
            ScryPanel.OpenMarkPickerForTest(null);
            MarksLookedAtAsBefore();
            KeptOnMapAsItWas();
        }
    }
}
