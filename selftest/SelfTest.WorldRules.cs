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
        /// shows what the server sets locked and dimmed, and a notice says what it sets until it
        /// is put away; put back, the choices are the character's again and the notice told as it
        /// was. The server's own settings are told as they stand.
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
            ScryPanel.HoldCompendiumBox(false);

            // The notice says what the server sets, once: put away, it stays away in this world.
            var world = CompendiumMap.World;
            var said = WorldRulesWords.Notice(made);
            // The made-up rules' notice kept as told, as a run stopped before putting it back
            // leaves it, is told afresh and not put back.
            var before = book.RulesTold(world);
            _rulesToldBefore = (world, before == said ? null : before);
            book.RulesToldAgain(world, null);
            var notices = ScryPanel.Drawn(PanelPart.RulesNotice);
            yield return Until(() => ScryPanel.Drawn(PanelPart.RulesNotice) > notices, 3);
            p.Check(ScryPanel.Drawn(PanelPart.RulesNotice) > notices && ScryPanel.RulesNoticeLine == said, "a notice says what the server sets", ScryPanel.RulesNoticeLine ?? "no notice");
            yield return Picture(p, "notice of the server's rules");
            ScryPanel.TellRules();
            p.Check(ScryPanel.RulesNoticeLine == null && book.ToldRules(world, said), "put away, it stays away in this world", book.RulesTold(world) ?? "never told");

            WorldRulesAsTheyWere();
            p.Check(book.Rule == chosenRule && book.Shows(WorldSource.SharedMaps) == chosenShared, "put back, the choices are the character's again");
        }

        /// <summary>What the world's notice of its server's rules said as last told before the made-up rules were, to put back.</summary>
        private static (string World, string Said)? _rulesToldBefore;

        /// <summary>The world's own rules again, the box let go, no peek, and the notice told as it was.</summary>
        private static void WorldRulesAsTheyWere()
        {
            CompendiumKeeper.WorldForTest(null);
            ScryPanel.HoldCompendiumBox(false);
            X.Peeking = false;
            if (_rulesToldBefore is (string world, string said)) CompendiumKeeper.Book?.RulesToldAgain(world, said);
            _rulesToldBefore = null;
        }
    }
}
