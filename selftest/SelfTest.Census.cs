using System.Collections;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for the world's census.</summary>
    internal static partial class SelfTest
    {
        /// <summary>How *The whole world* showed before the test chose Counted; put back after it.</summary>
        private static SourceLevel? _censusBefore;

        /// <summary>
        /// With *The whole world* at Counted, the world's server tells how many of each kind of
        /// place this world holds, never where: every kind it names is heard of, a made-up
        /// character's page of a crypt it knows says how many this world holds and how many lie in
        /// ground explored, and the Locations tab counts against the kinds the world holds. What
        /// the character chose is put back.
        /// </summary>
        private static IEnumerator WorldCensus(Probe p)
        {
            var book = CompendiumKeeper.Book;
            var world = CompendiumMap.World;
            var entry = Crypt();
            if (book == null || world == null || entry == null)
            {
                p.Skip("there is no character's book, world or crypt");
                yield break;
            }
            if (!ZNet.instance.IsServer())
            {
                yield return Until(() => EventLink.Server != Scry.ServerScry.Waiting, 15);
                if (!ServerScries.Answers(EventLink.Server))
                {
                    p.Skip("the server runs no Scry of this version, so it tells no census");
                    yield break;
                }
            }
            _censusBefore = book.Shows(WorldSource.WholeWorld);
            ScryPanel.ShowSource(WorldSource.WholeWorld, SourceLevel.Counted);
            var counts = CompendiumMap.Counts;
            yield return Until(() => counts.CensusKnown, 20);
            if (!p.Check(counts.CensusKnown, "the world's server tells its census")) yield break;
            var kinds = counts.HeldKinds.ToList();
            p.Note($"this world holds {Numbers.Count(kinds.Sum(k => counts.Held(k)))} places of {Numbers.Count(kinds.Count)} kinds; {Numbers.Count(counts.Held(PlaceOf(entry).Prefab))} of them {entry.Name}");
            p.Check(kinds.Count > 0 && kinds.All(k => book.Found(world, WorldSource.WholeWorld).Contains(k)), "every kind it names is kept as the whole world's");

            // A made-up character knowing the crypt: its page says how many this world holds.
            var made = new CompendiumBook();
            made.Raise(entry.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.InWorld(world);
            view.Tell(made, CompendiumRule.Loose);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            Select(entry);
            yield return Until(() => (ScryPanel.CensusLineForTest ?? "").StartsWith("This world holds", System.StringComparison.Ordinal), 3);
            var line = ScryPanel.CensusLineForTest;
            p.Check(line != null && line.StartsWith("This world holds", System.StringComparison.Ordinal), "the crypt's page says how many this world holds", line);
            p.Check(X.TotalOf(Kind.Location) < X.AllOf(Kind.Location), "the Locations tab counts against the kinds this world holds",
                $"{Numbers.Count(X.FoundOf(Kind.Location))}/{Numbers.Count(X.TotalOf(Kind.Location))} of {Numbers.Count(X.AllOf(Kind.Location))}");
            yield return PictureOf(p, "facts", "a crypt's page with the world's census");
            ScryPanel.HoldCompendiumBox(true);
            yield return Picture(p, "compendium box the whole world counted");
            CensusAsItWas();
        }

        /// <summary>Puts back how *The whole world* showed, and lets go of the box and the made-up character.</summary>
        private static void CensusAsItWas()
        {
            ScryPanel.HoldCompendiumBox(false);
            if (_censusBefore is SourceLevel before) ScryPanel.ShowSource(WorldSource.WholeWorld, before);
            _censusBefore = null;
        }
    }
}
