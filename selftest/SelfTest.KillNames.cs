using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for kills the game counts under a name several creatures share.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Both Dvergr rogues show one name, by which the game counts kills: a made-up book that
        /// fought the Mistlands rogue, its records holding a kill of that name with the Ashlands
        /// entered, credits the Mistlands rogue alone, as the one met in play, through the
        /// catch-up the character's own records go through.
        /// </summary>
        private static void SharedKillNamesTell(Probe p)
        {
            var rogue = Pick(Kind.Creature, "Dverger");
            var ashRogue = Pick(Kind.Creature, "DvergerAshlands");
            var mist = X.Find(EntryKeys.For(Kind.Biome, "Mistlands"));
            var ash = X.Find(EntryKeys.For(Kind.Biome, "AshLands"));
            if (rogue == null || ashRogue == null || mist == null || ash == null)
            {
                p.Note("no Dvergr rogue of the Mistlands and of the Ashlands, or no such biomes: shared kill names untried");
                return;
            }
            var shared = rogue.Tokens.Where(t => t != null).Intersect(ashRogue.Tokens.Where(t => t != null)).ToList();
            if (!p.Check(shared.Count > 0, "both Dvergr rogues go by one name, by which the game counts their kills",
                $"{string.Join(", ", rogue.Tokens)} / {string.Join(", ", ashRogue.Tokens)}")) return;
            var records = new CompendiumRecords();
            records.Killed.Add(shared[0]);
            foreach (var biome in new[] { mist, ash }) records.Biomes.Add(biome.Tokens.First(t => t != null));
            var book = new CompendiumBook();
            book.Raise(rogue.Key, KnownLevel.Known, way: CameBy.Fought);
            book.Witness(rogue.Key, mist.Key);
            CompendiumKeeper.CatchUpFor(X.Entries).Fill(records, book, firstLoad: false);
            p.Check(book.Level(ashRogue.Key) == KnownLevel.Unknown && book.Proved(rogue.Key, EntryKeys.Did(Deed.Killed)),
                "a kill of that name stands for the Mistlands rogue met in play alone, though the Ashlands were entered",
                $"Ashlands rogue {book.Level(ashRogue.Key)}, Mistlands rogue's kill {(book.Proved(rogue.Key, EntryKeys.Did(Deed.Killed)) ? "shown" : "kept back")}");
        }
    }
}
