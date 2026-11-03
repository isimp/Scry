using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class GameMarksTests
    {
        // Some places Valheim's map marks itself: the ones the game gives an icon on every map
        // (ZoneSystem.GetLocationIcons), and the ones a Vegvisir pinned for the character
        // (Game.DiscoverClosestLocation, its pin named and typed as the Vegvisir says). Where the map
        // shows exactly where one stands, Show on map does not shade a square saying it lies
        // somewhere inside: the game's own mark is its mark.

        private static readonly Dictionary<(string Pin, int Type), HashSet<string>> Pins = GameMarks.PlacesOfPins(new[]
        {
            ("Eikthyrnir", "$enemy_eikthyr", 9),
            ("Hildir_crypt", "$hud_pin_hildir1", 14),
        });

        [Fact]
        public void APlaceAVegvisirPinnedIsMarkedWhereItsPinStandsMatchedByThePinsNameAndKind()
        {
            var pins = new[] { (10f, 20f, "$enemy_eikthyr", 9), (500f, 500f, "$enemy_eikthyr", 0), (-30f, 40f, "Eikthyr here", 9) };
            Assert.Equal(new[] { (10, 20) }, GameMarks.MarksOf("Eikthyrnir", new (float, float, string)[0], pins, Pins));
            // A pin of the player's own, named alike but of another kind, or one the player named, is no mark of the game's.
            Assert.Empty(GameMarks.MarksOf("Hildir_crypt", new (float, float, string)[0], pins, Pins));
        }

        [Fact]
        public void APlaceTheGameGivesAnIconIsMarkedWhereTheIconStands()
        {
            var icons = new[] { (100.4f, -50.6f, "Vendor_BlackForest"), (7f, 7f, "StartTemple") };
            Assert.Equal(new[] { (100, -51) }, GameMarks.MarksOf("Vendor_BlackForest", icons, new (float, float, string, int)[0], Pins));
        }

        [Fact]
        public void AnEikthyrTheGameMarksIsNoSquareOfShowOnMapsWhileOneNeverSeenStill()
        {
            // Two Eikthyr altars told in two squares of explored ground; the Vegvisir pinned one.
            var squares = new Dictionary<(int X, int Z), int> { [(0, 0)] = 1, [(3, 3)] = 1 };
            var marks = GameMarks.MarksOf("Eikthyrnir", new (float, float, string)[0], new[] { (40f, 60f, "$enemy_eikthyr", 9) }, Pins);
            var shown = PlaceSquares.Shown(squares, GameMarks.With(new (int, int)[0], marks));
            Assert.Equal((3, 3, 1), Assert.Single(shown));
        }

        [Fact]
        public void APlaceBothSeenAndMarkedByTheGameIsCountedOnce()
        {
            // A square holding two: one seen, its spot where the game's pin stands too; the other still somewhere inside.
            var squares = new Dictionary<(int X, int Z), int> { [(0, 0)] = 2 };
            var seen = new[] { (40, 60) };
            var marks = new[] { (41, 59) };
            var with = GameMarks.With(seen, marks);
            Assert.Equal(new[] { (40, 60) }, with);
            Assert.Equal((0, 0, 1), Assert.Single(PlaceSquares.Shown(squares, with)));
            // Marked apart from every spot seen, it counts besides them.
            Assert.Equal(2, GameMarks.With(seen, new[] { (90, 100) }).Count);
        }
    }
}
