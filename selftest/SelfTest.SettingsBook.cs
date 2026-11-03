using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for the character's compendium standing in the settings as in its box.</summary>
    internal static partial class SelfTest
    {
        /// <summary>A source's level as it was before the part set it, put back however the part ends.</summary>
        private static (WorldSource Source, SourceLevel Level)? _sourceBefore;

        /// <summary>
        /// The character's compendium has one home: the box's gear opens the settings at the
        /// Compendium section, which draws the rules, Known up to and each source lit as the book
        /// holds them; a level set there shows lit in the box at once, as one setting; what the
        /// world's server allows stands under each row, the player's to set while alone or hosting,
        /// locked while a server decides it; the box keeps no row of the map's marks, which the
        /// Map section holds. Pictured; the level set is put back.
        /// </summary>
        private static IEnumerator CompendiumSettingsShown(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "the character's book is read")) yield break;
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var cards = ScryPanel.Drawn(PanelPart.SettingsCard);
            ScryPanel.OpenCompendiumSettings();
            yield return Until(() => ScryPanel.Drawn(PanelPart.SettingsCard) > cards + 1, 3);
            var section = SettingsCatalog.Sections[ScryPanel.ShownSettingsSection];
            p.Check(ScryPanel.SettingsShown && section.Name == "Compendium", "the box's gear opens the settings at the Compendium section", section.Name);
            p.Check(ScryPanel.SettingsRowsDrawnForTest == section.Rows.Count, "the section draws the rules, Known up to, each source and the news",
                $"{Numbers.Count(ScryPanel.SettingsRowsDrawnForTest)} of {Numbers.Count(section.Rows.Count)} rows");
            var unlike = CardUnlikeBook(book);
            p.Check(unlike.Count == 0, "each row stands lit as the character's book holds it", string.Join("; ", unlike));
            yield return Picture(p, "settings card, compendium");
            LocksBeside(p, book, section);

            var limits = SettingsCatalog.AllRows.Where(r => r.Limit != null).Select(r => r.Limit).ToList();
            if (Settings.WorldFromServer) p.Check(limits.All(ScryPanel.SettingLocked), "what this server allows of each row is locked while it decides it");
            else p.Check(limits.Count > 0 && !limits.Any(ScryPanel.SettingLocked), "playing alone or hosting, what your world allows of each row is yours to set under it");

            if (book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so its sources wait for its rules and are not set here");
                ScryPanel.ShowSettings(false);
                yield break;
            }
            // A level other than the one shown, allowed and with no rooms asked of it, set as a click on the card sets it.
            var picked = WorldSources.All.SelectMany(s => WorldSources.Offered(s).Where(l => l != book.Shows(s) && l != SourceLevel.WithInsides && book.World.Allows(s, l))
                .Select(l => ((WorldSource Source, SourceLevel Level)?)(s, l))).FirstOrDefault();
            if (!(picked is (WorldSource, SourceLevel) pick))
            {
                p.Note("no source offers another level allowed here, so none is set");
                ScryPanel.ShowSettings(false);
                yield break;
            }
            _sourceBefore = (pick.Source, book.Chosen(pick.Source));
            var row = section.Rows.Single(r => r.Book == BookPart.Source && r.Source == pick.Source);
            ScryPanel.SetBookRow(row, (int)pick.Level);
            var drawn = ScryPanel.Drawn(PanelPart.SourcesControl);
            var cardDrawn = ScryPanel.Drawn(PanelPart.SettingsCard);
            ScryPanel.HoldCompendiumBox(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SourcesControl) > drawn + 2 && ScryPanel.Drawn(PanelPart.SettingsCard) > cardDrawn + 2, 3);
            p.Check(ScryPanel.SettingsShown && book.Shows(pick.Source) == pick.Level, "a level set on the card is kept, and the card stays open for the next");
            var name = CompendiumWords.SourceName(pick.Source);
            var level = CompendiumWords.LevelName(pick.Level);
            var onCard = ScryPanel.SettingsBookCellsForTest.Where(c => c.Row == name && c.Lit).Select(c => c.Choice).ToList();
            var inBox = ScryPanel.BookCellsForTest.Where(c => c.Row == name && c.Lit && c.Column >= 0).Select(c => c.Label).ToList();
            p.Check(onCard.SequenceEqual(new[] { level }) && inBox.SequenceEqual(new[] { level }), $"{name} set to {level} stands lit so on the card and in the box at once",
                $"card {string.Join(", ", onCard)}, box {string.Join(", ", inBox)}");
            var boxRows = ScryPanel.BookCellsForTest.Select(c => c.Row).Distinct().ToList();
            var known = WorldSources.All.Select(CompendiumWords.SourceName).Append("rules").Append("floor").ToList();
            p.Check(boxRows.All(known.Contains), "the box holds the rules, Known up to and the sources alone, the map's marks left to the Map section", string.Join(", ", boxRows.Except(known)));
            yield return Picture(p, "compendium box over its settings");

            // What the box would refuse, the card refuses too: a level above the server's cap, or With insides where nothing found has any.
            var refused = WorldSources.All.SelectMany(s => WorldSources.Offered(s).Where(l => !book.World.Allows(s, l)).Select(l => ((WorldSource Source, SourceLevel Level)?)(s, l))).FirstOrDefault();
            if (refused is (WorldSource capSource, SourceLevel capLevel))
            {
                var before = book.Chosen(capSource);
                ScryPanel.SetBookRow(section.Rows.Single(r => r.Book == BookPart.Source && r.Source == capSource), (int)capLevel);
                p.Check(book.Chosen(capSource) == before, "a level above what the server allows is refused on the card, as in the box", $"{CompendiumWords.SourceName(capSource)} {CompendiumWords.LevelName(capLevel)}");
            }
            else p.Note("the server caps no source here, so a refused level is not tried");
            CompendiumSettingsAsTheyWere();
        }

        /// <summary>While a server keeps something from a row, its lock stands beside the row's control, short; nothing beside a row it leaves whole.</summary>
        private static void LocksBeside(Probe p, CompendiumBook book, SettingSection section)
        {
            var drawn = ScryPanel.SettingsBookCellsForTest;
            var wrong = new List<string>();
            foreach (var row in section.Rows.Where(r => r.Control == SettingControl.Book))
            {
                var expected = Settings.WorldFromServer ? WorldRulesWords.OnRowShort(row.Book, row.Source, book.World) : null;
                var locks = drawn.Where(c => c.Row == row.Label && c.Lock).Select(c => c.Choice).ToList();
                if (expected == null ? locks.Count > 0 : !locks.SequenceEqual(new[] { expected })) wrong.Add($"{row.Label}: {expected ?? "none"} expected");
            }
            if (!Settings.WorldFromServer) p.Note("no server decides this world, so no lock stands beside a row");
            p.Check(wrong.Count == 0, "beside each row the server keeps something from, its lock and what it keeps, short; nothing beside the rest", string.Join("; ", wrong));
        }

        /// <summary>Where the card's rows of the compendium stand otherwise than the book holds them: each lit choice by row, against the book's.</summary>
        private static List<string> CardUnlikeBook(CompendiumBook book)
        {
            var cells = ScryPanel.SettingsBookCellsForTest;
            var unlike = new List<string>();
            void Same(string row, string expected)
            {
                var lit = cells.Where(c => c.Row == row && c.Lit).Select(c => c.Choice).ToList();
                if (!lit.SequenceEqual(new[] { expected })) unlike.Add($"{row}: {string.Join(", ", lit)} lit for {expected}");
            }
            Same(CompendiumWords.RulesCaption, CompendiumWords.RuleName(book.Rule));
            foreach (var source in WorldSources.All) Same(CompendiumWords.SourceName(source), CompendiumWords.LevelName(book.Shows(source)));
            var floor = cells.Where(c => c.Row == CompendiumWords.FloorCaption && !c.Lock).Select(c => c.Choice).ToList();
            if (!floor.SequenceEqual(new[] { CompendiumWords.FloorName(book.Floor) })) unlike.Add($"Known up to: {string.Join(", ", floor)} for {CompendiumWords.FloorName(book.Floor)}");
            return unlike;
        }

        /// <summary>The level set put back as it was chosen, the box let go and the card put away.</summary>
        private static void CompendiumSettingsAsTheyWere()
        {
            if (_sourceBefore is (WorldSource source, SourceLevel level)) ScryPanel.ShowSource(source, level);
            _sourceBefore = null;
            ScryPanel.HoldCompendiumBox(false);
            ScryPanel.ShowSettings(false);
        }
    }
}
