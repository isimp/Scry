using System.Collections;
using System.IO;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's cases for the settings card, the compendium's guide and the question of keeping a compendium.</summary>
    internal static partial class SelfTest
    {
        private static (StageQuality Quality, bool News, float Speed)? _settingsBefore;

        /// <summary>
        /// The gear in the title row opens the settings card in the list's place, each row's
        /// choices on one line beside its label, and closes it again. A fast stage is filmed at half the size, and the View box's speed goes on to the
        /// next; each setting is put back as it was.
        /// </summary>
        private static IEnumerator SettingsCardShown(Probe p)
        {
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            _settingsBefore = (Settings.StageQuality, Settings.NewsNotice, Settings.SpinSpeed);
            var gears = ScryPanel.Drawn(PanelPart.SettingsGear);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SettingsGear) > gears, 3);
            p.Check(ScryPanel.Drawn(PanelPart.SettingsGear) > gears, "the gear stands in the title row");

            var cards = ScryPanel.Drawn(PanelPart.SettingsCard);
            ScryPanel.ShowSettings(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SettingsCard) > cards, 3);
            p.Check(ScryPanel.SettingsShown && ScryPanel.Drawn(PanelPart.SettingsCard) > cards, "the gear opens the settings card in the list's place");
            var wrapped = ScryPanel.Drawn(PanelPart.SettingsRowWrapped);
            cards = ScryPanel.Drawn(PanelPart.SettingsCard);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SettingsCard) > cards + 3, 3);
            // Rows wrap where the list is too narrow for them, as they should; where it is wide enough, none does.
            var fit = ScryPanel.SettingsFitForTest;
            if (fit.Needed > fit.Width) p.Note($"the list is {Numbers.Fixed(fit.Width, 0)} wide and the widest row needs {Numbers.Fixed(fit.Needed, 0)}, so rows wrap");
            else p.Check(ScryPanel.Drawn(PanelPart.SettingsRowWrapped) == wrapped, "every row of the settings card stands on one line beside its label where the list is wide enough",
                $"{Numbers.Count(ScryPanel.Drawn(PanelPart.SettingsRowWrapped) - wrapped)} rows wrapped over {Numbers.Count(ScryPanel.Drawn(PanelPart.SettingsCard) - cards)} frames");
            yield return Picture(p, "settings card");
            ScryPanel.ShowSettings(false);
            p.Check(!ScryPanel.SettingsShown, "and closes it");

            // A stage filmed fast is filmed at half the size, and in full again at the stage's own.
            var troll = Pick(Kind.Creature, "Troll", "Greydwarf");
            if (troll != null)
            {
                Select(troll);
                yield return Until(() => CopyOf(troll) != null && Stage.Texture != null, 10);
                var full = Stage.Texture != null ? Stage.Texture.width : 0;
                Settings.StageQuality = StageQuality.Fast;
                yield return Until(() => Stage.Texture != null && Stage.Texture.width < full, 3);
                var fast = Stage.Texture != null ? Stage.Texture.width : 0;
                p.Check(full > 0 && Mathf.Abs(fast - Mathf.Max(32, full / 2)) <= 1, "a fast stage is filmed at half the size", $"{Numbers.Count(full)} wide in full, {Numbers.Count(fast)} fast");
                Settings.StageQuality = StageQuality.Full;
                yield return Until(() => Stage.Texture != null && Stage.Texture.width == full, 3);
                p.Check(Stage.Texture != null && Stage.Texture.width == full, "and in full at the stage's own size again");

                var speed = Settings.SpinSpeed;
                ScryPanel.NextSpinSpeedForTest();
                p.Check(ScryPanel.ViewRowsForTest(troll).Contains("Speed") && Settings.SpinSpeed != speed, "the View box's speed goes on to the next, kept in the config file", $"{Numbers.Fixed(speed, 0)} to {Numbers.Fixed(Settings.SpinSpeed, 0)}");
            }
            else p.Note("there is no creature, so the stage's quality and speed are not tried");
            SettingsAsTheyWere();
            p.Check(Settings.StageQuality == _settingsBefore?.Quality || _settingsBefore == null, "each setting is put back as it was");
        }

        /// <summary>The settings tried put back, the card put away.</summary>
        private static void SettingsAsTheyWere()
        {
            ScryPanel.ShowSettings(false);
            if (_settingsBefore is (StageQuality quality, bool news, float speed))
            {
                Settings.StageQuality = quality;
                Settings.NewsNotice = news;
                Settings.SpinSpeed = speed;
            }
            _settingsBefore = null;
        }

        /// <summary>A made-up file set aside, as a book that could not be read is: the notice names it; put away, it is gone. No file is touched.</summary>
        private static IEnumerator SetAsideTold(Probe p)
        {
            var file = "Scry-selftest-book.dat.unreadable-" + CompendiumKeeper.Stamp();
            var path = Path.Combine(Settings.CacheFolder, "compendium", file);
            SetAsideNotice.Files.Tell(SetAsideKind.Compendium, path);
            var strips = ScryPanel.Drawn(PanelPart.SetAsideNotice);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SetAsideNotice) > strips + 1, 3);
            p.Check(ScryPanel.SetAsideLineForTest == CompendiumWords.SetAsideNotice(SetAsideKind.Compendium, file), "the notice says the book could not be read and what it was set aside as", ScryPanel.SetAsideLineForTest ?? "no notice");
            yield return Picture(p, "a book set aside");
            SetAsideNotice.Files.PutAway();
            yield return null;
            p.Check(ScryPanel.SetAsideLineForTest == null, "put away, the notice is gone");
        }

        /// <summary>The ? in the compendium's box opens its guide in the list's place; Close puts it away.</summary>
        private static IEnumerator GuideShown(Probe p)
        {
            if (CompendiumKeeper.Book == null)
            {
                p.Skip("there is no character's book");
                yield break;
            }
            Session.ShowKnownInstead(null);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var buttons = ScryPanel.Drawn(PanelPart.GuideButton);
            ScryPanel.HoldCompendiumBox(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.GuideButton) > buttons, 3);
            p.Check(ScryPanel.Drawn(PanelPart.GuideButton) > buttons, "the compendium's box shows its guide's ?");
            ScryPanel.HoldCompendiumBox(false);

            var cards = ScryPanel.Drawn(PanelPart.GuideCard);
            ScryPanel.ShowGuide(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.GuideCard) > cards, 3);
            p.Check(ScryPanel.GuideShown && ScryPanel.Drawn(PanelPart.GuideCard) > cards, "the guide opens in the list's place");
            yield return Picture(p, "compendium guide");
            ScryPanel.ShowGuide(false);
            p.Check(!ScryPanel.GuideShown, "and is put away");

            // Asking for the list, the recent here, puts a card in its place away, as the list is what is wanted.
            ScryPanel.ShowGuide(true);
            yield return null;
            ScryPanel.ToggleRecentForTest();
            yield return null;
            p.Check(!ScryPanel.GuideShown, "pressing the recent puts the guide away, so the list asked for shows");
            ScryPanel.ToggleRecentForTest();
        }

        /// <summary>The question of keeping a compendium draws in the list's place, asked here whatever the book says; nothing is answered, so the book is as it was.</summary>
        private static IEnumerator WelcomeShown(Probe p)
        {
            var book = CompendiumKeeper.Book;
            if (book == null)
            {
                p.Skip("there is no character's book");
                yield break;
            }
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            ScryPanel.CloseCards();
            var (rule, welcomed) = (book.ChosenRule, book.Welcomed);
            var cards = ScryPanel.Drawn(PanelPart.WelcomeCard);
            ScryPanel.AskWelcomeForTest(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.WelcomeCard) > cards, 3);
            p.Check(ScryPanel.Drawn(PanelPart.WelcomeCard) > cards, "the question of keeping a compendium draws in the list's place");
            yield return Picture(p, "keep a compendium question");
            ScryPanel.AskWelcomeForTest(false);
            p.Check(book.ChosenRule == rule && book.Welcomed == welcomed, "asked without an answer, the book is as it was", $"{book.ChosenRule}, {(book.Welcomed ? "asked" : "never asked")}");
        }
    }
}
