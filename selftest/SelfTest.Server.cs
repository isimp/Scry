using System.Collections;

namespace Scry
{
    /// <summary>The self-test's case for the world's server in the compendium's box.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// The compendium's box names the world's server first among those played with, by
        /// whether it runs Scry, and for a server without Scry, made up in place of the world's,
        /// its faint line says what that leaves out; where this game runs the world, no server is
        /// named.
        /// </summary>
        private static IEnumerator ServerNamed(Probe p)
        {
            var book = CompendiumKeeper.Book;
            if (book == null || book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so the box names no one");
                yield break;
            }
            yield return Until(() => EventLink.Server != Scry.ServerScry.Waiting, 15);
            var real = EventLink.Server;
            var (row, _) = ScryPanel.PlayingWithForTest;
            p.Note($"the world's server: {real}; the row reads \"{row}\"");
            p.Check(real == Scry.ServerScry.Here ? row == null || !row.StartsWith("The server", System.StringComparison.Ordinal) : row != null && row.StartsWith("The server", System.StringComparison.Ordinal),
                real == Scry.ServerScry.Here ? "where this game runs the world, no server is named" : "the row names the server first", row);

            EventLink.ServerForTest(Scry.ServerScry.None);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            ScryPanel.HoldCompendiumBox(true);
            var (without, hint) = ScryPanel.PlayingWithForTest;
            p.Check(without != null && without.StartsWith("The server \u00b7 no Scry", System.StringComparison.Ordinal), "a server without Scry is named so", without);
            p.Check(hint != null && hint.StartsWith("The server runs no Scry: what your explored and shared maps show", System.StringComparison.Ordinal), "and the line under it says what that leaves out", hint);
            yield return Picture(p, "compendium box server without Scry");
            ServerAsItWas();
        }

        /// <summary>The world's server's Scry as the world's again, the box let go.</summary>
        private static void ServerAsItWas()
        {
            EventLink.ServerForTest(null);
            ScryPanel.HoldCompendiumBox(false);
        }
    }
}
