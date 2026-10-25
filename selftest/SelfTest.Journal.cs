using System.Collections;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for the You box's moments, Learned by day and the notice's news.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// A made-up character's book with moments: the Troll first seen at dusk in the rain with
        /// a star, come to know at night with a spear in hand, and twice the character's death; a
        /// boar seen later; wood caught up from the game's records. The Troll's You box tells it
        /// all, Learned groups by day with the older ones and what has no day folded, and the
        /// notice tells what happened in words.
        /// </summary>
        private static readonly string[] DayHeadings = { "Day 22  1", "Day 12  1", "Earlier  1" };
        private static readonly bool[] DaysFolded = { false, false, true };

        private static IEnumerator YouAndLearnedRead(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll");
            var boar = Pick(Kind.Creature, "Boar");
            var wood = Pick(Kind.Item, "Wood");
            var spear = Pick(Kind.Item, "SpearFlint");
            if (troll == null || boar == null || wood == null || spear == null)
            {
                p.Skip("there is no troll, boar, wood or flint spear");
                yield break;
            }
            // Its moments are of the world played, by its name as a character's are: a moment of no world would stand under a heading of its own.
            var world = X.WorldNow;
            var book = new CompendiumBook();
            book.Now = () => new Moment(9, "BlackForest", DayPart.Dusk, "Rain", null, world);
            book.Raise(troll.Key, KnownLevel.Seen, stars: 1);
            book.Now = () => new Moment(12, "BlackForest", DayPart.Night, "Clear", spear.Name, world);
            book.Raise(troll.Key, KnownLevel.Known, way: CameBy.Fought);
            book.Died(troll.Key);
            book.Now = () => new Moment(21, "BlackForest", world: world);
            book.Died(troll.Key);
            book.Now = () => new Moment(22, "Meadows", DayPart.Day, world: world);
            book.Raise(boar.Key, KnownLevel.Seen);
            book.CatchingUp = true;
            book.Raise(wood.Key, KnownLevel.Known);
            book.CatchingUp = false;

            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Loose);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            X.Select(troll);
            yield return Until(() => ScryPanel.YouLinesForTest.Any(l => l.Label == "First seen"), 3);

            var lines = ScryPanel.YouLinesForTest;
            p.Note("the Troll's You box: " + string.Join("; ", lines.Select(l => l.Label + ": " + l.Value)));
            string Line(string label) => lines.FirstOrDefault(l => l.Label == label).Value;
            p.Check(Line("First seen") == "day 9, at dusk in the rain, in the Black Forest, with 1 star", "the first sighting tells its day, time, weather, biome and stars", Line("First seen"));
            p.Check(Line("Came to know it") == $"day 12, at night, in the Black Forest, fighting it with your {spear.ShownName}", "coming to know it tells its moment and what was in hand", Line("Came to know it"));
            p.Check(Line("Killed by it") == "twice, last on day 21", "the deaths it dealt are counted with the last", Line("Killed by it"));
            yield return PictureOf(p, "facts", "you Troll, first seen, came to know and killed you");

            // Learned, by day, the folds as the list starts them, the selection in the newest day.
            ScryPanel.FoldsAsAtFirstForTest();
            X.Select(boar);
            X.LearnedOnly = true;
            yield return null;
            yield return null;
            var headings = ScryPanel.ListHeadingsForTest;
            p.Note("Learned: " + string.Join("; ", headings.Select(h => h.Heading + (h.Folded ? " (folded)" : ""))));
            p.Check(headings.Select(h => h.Heading).SequenceEqual(DayHeadings), "Learned is grouped by day, newest first, what has no day last");
            p.Check(headings.Select(h => h.Folded).SequenceEqual(DaysFolded), "the newest two days are open, older days and what has no day are folded");
            p.Check(X.LearnedNote(boar) == "seen" && X.LearnedNote(troll) == "fought" && X.LearnedNote(wood) == "known", "each row says how it came", $"{X.LearnedNote(boar)}, {X.LearnedNote(troll)}, {X.LearnedNote(wood)}");
            yield return Picture(p, "learned by day");
            // The selection moved into a folded day opens it.
            X.Select(troll);
            yield return null;
            yield return null;
            var opened = ScryPanel.ListHeadingsForTest;
            p.Check(opened.Any(h => h.Heading.StartsWith("Day 12", System.StringComparison.Ordinal) && !h.Folded), "the selection moved into a folded day opens it",
                string.Join("; ", opened.Select(h => h.Heading + (h.Folded ? " (folded)" : ""))));
            // A row of Learned opens its page at what changed, the line telling it lit a moment.
            var came = NewsArrival.You(CompendiumWords.CameLabel);
            yield return Until(() => ScryPanel.ArrivedForTest == came, 3);
            p.Check(ScryPanel.ArrivedForTest == came, "a row of Learned opens its page at what changed: the Troll's at how it came to be known, lit", ScryPanel.ArrivedForTest ?? "nothing arrived at");
            yield return Picture(p, "learned by day, Troll selected");

            // The notice in words.
            p.Check(ScryPanel.NewsLine == "Since day 12: came to know Troll, saw Boar, and read 1 thing from the game's records of your character", "the notice tells what happened in words", ScryPanel.NewsLine);
            X.LearnedOnly = false;
        }
    }
}
