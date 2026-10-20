using System.Collections;

namespace Scry
{
    /// <summary>The self-test's case for what the world's server allows the compendium.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// What the world's server allows, made up in place of its settings: the strict rules for
        /// everyone, no peeking, no *Known up to*, shared maps up to seen. The character's book
        /// follows them while its own choices stay beneath, a peek ends, and the compendium's box
        /// shows what the server sets locked and dimmed; put back, the choices are the
        /// character's again. The server's own settings are told as they stand.
        /// </summary>
        private static IEnumerator WorldRulesShown(Probe p)
        {
            var book = CompendiumKeeper.Book;
            if (book == null)
            {
                p.Skip("there is no character's book");
                yield break;
            }
            var real = Settings.World();
            p.Note($"this world's settings{(Settings.WorldFromServer ? ", as its server sets them" : ", this game's own")}: rules {real.Rule?.ToString() ?? "the players' choice"}, peeking {(real.Peeking ? "allowed" : "not allowed")}, Known up to {(real.FloorAllowed ? "allowed" : "not allowed")}, shared maps up to {real.Highest(WorldSource.SharedMaps)}");

            var chosenRule = book.ChosenRule;
            var chosenShared = book.Chosen(WorldSource.SharedMaps);
            var made = new WorldRules { Rule = CompendiumRule.Strict, Peeking = false, FloorAllowed = false };
            made.UpTo(WorldSource.SharedMaps, SourceLevel.Seen);
            CompendiumKeeper.WorldForTest(made);
            p.Check(book.Rule == CompendiumRule.Strict && book.ChosenRule == chosenRule, "the book follows the rules the server sets, its own choice kept beneath", $"{book.Rule}, chosen {book.ChosenRule}");
            p.Check(book.Floor == 0, "Known up to shows nothing where the server allows none");
            p.Check(book.Shows(WorldSource.SharedMaps) <= SourceLevel.Seen && book.Chosen(WorldSource.SharedMaps) == chosenShared, "shared maps show no more than seen, the choice kept", $"{book.Shows(WorldSource.SharedMaps)}, chosen {book.Chosen(WorldSource.SharedMaps)}");

            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            X.Peeking = true;
            yield return Until(() => !X.Peeking, 3);
            p.Check(!X.Peeking, "a peek ends where the server allows none");
            var locked = ScryPanel.Drawn(PanelPart.WorldLocked);
            ScryPanel.HoldCompendiumBox(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.WorldLocked) > locked + 2, 3);
            p.Check(ScryPanel.Drawn(PanelPart.WorldLocked) > locked, "the box shows what the server sets locked and dimmed");
            yield return Picture(p, "compendium box under the server's rules");

            WorldRulesAsTheyWere();
            p.Check(book.Rule == chosenRule && book.Shows(WorldSource.SharedMaps) == chosenShared, "put back, the choices are the character's again");
        }

        /// <summary>The world's own rules again, the box let go and no peek.</summary>
        private static void WorldRulesAsTheyWere()
        {
            CompendiumKeeper.WorldForTest(null);
            ScryPanel.HoldCompendiumBox(false);
            X.Peeking = false;
        }
    }
}
