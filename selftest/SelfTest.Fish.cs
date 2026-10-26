using System.Collections;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's cases for a fish's page and the ways a kind's list groups.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// A Perch's page tells where it lives, when it comes, how it bites and fights by stars and
        /// what a catch brings, by the game's own figures; under the strict rules a made-up
        /// character who knows it but never caught one has how it finds a float kept back until it
        /// catches one. The fish of the catalog and their water are noted.
        /// </summary>
        private static IEnumerator FishPageTells(Probe p)
        {
            var perch = Pick(Kind.Item, "Fish1");
            if (perch == null)
            {
                p.Skip("there is no Perch in this world's catalog");
                yield break;
            }
            var facts = Facts.For(perch);
            string Pair(string label) => facts.Pairs.Where(x => x.Key == label).Select(x => x.Value).FirstOrDefault();
            p.Check(Pair(FishWords.Swims) == "1.5–3 m down", "a Perch swims 1.5 to 3 m down", Pair(FishWords.Swims));
            p.Check(Pair(FishWords.Water)?.StartsWith("where the bottom lies 1.5 to 3 m below the surface", System.StringComparison.Ordinal) == true, "where the bottom lies 1.5 to 3 m down", Pair(FishWords.Water));
            p.Check(Pair(FishWords.Comes) != null && Pair(FishWords.FindsFloat) != null && Pair(FishWords.Line) != null, "it tells when it comes, how it finds a float and when the line breaks", Pair(FishWords.Comes) + " / " + Pair(FishWords.FindsFloat) + " / " + Pair(FishWords.Line));
            var rod = Pick(Kind.Item, "FishingRod");
            if (rod != null) p.Check(Facts.For(rod).Pairs.Any(x => x.Key == "With a fish on"), "the rod tells what a fish on its float drains, read from the float its cast makes");
            var fight = facts.Rows.FirstOrDefault(r => r.Title == FishWords.FightTitle);
            p.Check(fight != null && fight.Columns.Length == 6, "its fight stands by stars, no star to four", fight == null ? "none" : string.Join(", ", fight.Columns));
            var raw = fight?.Lines.FirstOrDefault(l => l.Cells[0] == FishWords.RawFish).Cells;
            p.Check(raw != null && string.Join(",", raw.Skip(1)) == "1,4,7,10,13", "the prep table makes 1, 4, 7, 10 or 13 raw fish of one", raw == null ? "none" : string.Join(",", raw));
            p.Check(facts.Rows.Any(r => r.Part == "fish catch" && r.Items.Count > 0), "what a catch brings besides is told");

            var book = new CompendiumBook();
            book.Raise(perch.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Strict);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            X.Select(perch);
            var caught = EntryKeys.Did(Deed.Caught);
            p.Check(X.KeepsBack(caught), "under the strict rules how it finds a float waits for one caught");
            book.Prove(perch.Key, caught);
            view.Tell(book, CompendiumRule.Strict);
            X.Know(view);
            p.Check(!X.KeepsBack(caught), "once caught it shows");
            yield return PictureOf(p, "lives", "a Perch's page under the strict rules, one caught, where it lives");
            yield return PictureOf(p, "fishfight", "a Perch's page under the strict rules, one caught, its fight");
            Session.ShowKnownInstead(null);
            yield return Until(() => X.Compendium != view, 3);

            var fish = X.Catalog.Where(e => e.Kind == Kind.Item && Facts.For(e).Pairs.Any(x => x.Key == FishWords.Swims)).ToList();
            p.Note($"{Numbers.Count(fish.Count)} fish: " + string.Join("; ", fish.Take(20).Select(f => f.ShownName + " " + (Facts.For(f).Pairs.Where(x => x.Key == FishWords.Water).Select(x => x.Value).FirstOrDefault() ?? "no world spawn"))));
        }

        /// <summary>
        /// The Creatures tab groups by what taming allows, the Lox under Ridden and what can never
        /// be tamed folded away, and the Items tab by biome, the Perch under Meadows with two more,
        /// most items under a biome; each tab keeps its own way, and both go back to the way the
        /// player had them.
        /// </summary>
        private static IEnumerator ListsGroupOtherWays(Probe p)
        {
            var lox = Pick(Kind.Creature, "Lox");
            var perch = Pick(Kind.Item, "Fish1");
            if (lox == null || perch == null)
            {
                p.Skip("there is no Lox or Perch in this world's catalog");
                yield break;
            }
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            X.Text = "";
            X.KindFilter = Kind.Creature;
            var creaturesWere = X.Lens;
            X.Lens = GroupLens.Taming;
            p.Check(X.ListGroupOf(lox).Name == ListGrouping.Ridden, "the Lox stands under Ridden", X.ListGroupOf(lox).Name);
            var groups = X.Results.Select(e => X.ListGroupOf(e).Name).Distinct().ToList();
            p.Note("creatures by taming: " + string.Join(", ", groups.Select(g => g + " " + Numbers.Count(X.Results.Count(e => X.ListGroupOf(e).Name == g)))));
            X.Select(lox);
            yield return Until(() => ScryPanel.Drawn(PanelPart.GroupBar) > 0, 3);
            yield return Picture(p, "the Creatures tab grouped by taming");

            X.KindFilter = Kind.Item;
            var itemsWere = X.Lens;
            X.Lens = GroupLens.Biome;
            var group = X.ListGroupOf(perch);
            p.Check(group.Name == "Meadows" && group.More == 2, "the Perch stands under Meadows, two biomes more", group.Name + " +" + Numbers.Count(group.More));
            p.Note("items by biome: " + string.Join(", ", X.Results.Select(e => X.ListGroupOf(e).Name).Distinct().Select(g => g + " " + Numbers.Count(X.Results.Count(e => X.ListGroupOf(e).Name == g)))));
            // What only a recipe makes has no biome; what a creature drops stands under the creature's.
            var trophy = Pick(Kind.Item, "TrophyBoar");
            if (trophy != null) p.Check(X.ListGroupOf(trophy).Name == "Meadows", "a boar's trophy stands under the Meadows, where the boar lives", X.ListGroupOf(trophy).Name);
            X.Select(perch);
            yield return Picture(p, "the Items tab grouped by biome");

            X.KindFilter = Kind.Creature;
            p.Check(X.Lens == GroupLens.Taming, "the Creatures tab kept its own way");
            // Each tab back to the way the player had it.
            X.Lens = creaturesWere;
            X.KindFilter = Kind.Item;
            X.Lens = itemsWere;
            X.KindFilter = null;
        }
    }
}
