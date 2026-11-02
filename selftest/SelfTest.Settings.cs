using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's cases for the settings card, the compendium's guide and the question of keeping a compendium.</summary>
    internal static partial class SelfTest
    {
        /// <summary>Every setting the card shows, as the config file wrote it before the part, put back after it.</summary>
        private static Dictionary<string, string> _settingsBefore;
        private static bool? _listHiddenBefore;

        /// <summary>
        /// The gear opens the settings card over the panel's body, the list folded or not; each
        /// section shows every setting it holds; every setting takes each value its control
        /// offers and reads back as set; a section's reset brings its defaults back; what the
        /// world's server decides is locked while a server decides it. A fast stage is filmed at
        /// half the size, and the View box's speed goes on to the next. Every setting is put back
        /// as it was.
        /// </summary>
        private static IEnumerator SettingsCardShown(Probe p)
        {
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            _settingsBefore = new Dictionary<string, string>();
            foreach (var key in SettingsCatalog.AllRows.SelectMany(r => r.Keys))
            {
                var entry = Settings.Entry(key);
                if (entry != null) _settingsBefore[key] = entry.GetSerializedValue();
            }
            var gears = ScryPanel.Drawn(PanelPart.SettingsGear);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SettingsGear) > gears, 3);
            p.Check(ScryPanel.Drawn(PanelPart.SettingsGear) > gears, "the gear stands in the title row");

            // Folded away, the list leaves the card its place all the same.
            _listHiddenBefore = ScryPanel.ListHidden;
            ScryPanel.ListHidden = true;
            var cards = ScryPanel.Drawn(PanelPart.SettingsCard);
            ScryPanel.ShowSettings(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SettingsCard) > cards, 3);
            p.Check(ScryPanel.SettingsShown && ScryPanel.Drawn(PanelPart.SettingsCard) > cards, "the gear opens the settings card over the panel, the list folded away");
            ScryPanel.ListHidden = false;

            // Each section, shown as a click on its name shows it, draws every row it holds.
            var short_ = new List<string>();
            for (var i = 0; i < SettingsCatalog.Sections.Count; i++)
            {
                ScryPanel.ShownSettingsSection = i;
                yield return null;
                yield return null;
                var section = SettingsCatalog.Sections[i];
                var expected = section.Rows.Count(ScryPanel.ShowsSettingRow);
                if (ScryPanel.SettingsRowsDrawnForTest != expected) short_.Add($"{section.Name} {Numbers.Count(ScryPanel.SettingsRowsDrawnForTest)} of {Numbers.Count(expected)}");
                if (section.Name == "Panel" || section.Name == "Map" || section.Name == "This server") yield return Picture(p, "settings card, " + section.Name.ToLowerInvariant());
            }
            p.Check(short_.Count == 0, "every section shows every setting it holds", string.Join("; ", short_));

            // Each value a control offers, set as a click sets it, reads back as set: the key opening the panel aside, set by a key pressed.
            var wrong = new List<string>();
            var tried = 0;
            // Hosting with others connected, what the server decides is left as it is: each change would go out to them.
            var othersHere = ZNet.instance != null && ZNet.instance.IsServer() && ZNet.instance.GetPeers().Count > 0;
            if (othersHere) p.Note("others are connected to this world, so what the server decides is not tried here");
            foreach (var row in SettingsCatalog.AllRows)
            {
                if (row.Control == SettingControl.Key || row.Control == SettingControl.Book || ScryPanel.SettingLocked(row) || row.ServerDecides && othersHere) continue;
                foreach (var key in row.Keys)
                {
                    foreach (var value in Offered(row))
                    {
                        ScryPanel.SetSetting(row, key, value);
                        tried++;
                        var read = Settings.Entry(key)?.GetSerializedValue();
                        if (!SameValue(read, value)) wrong.Add($"{key} set {value} read {read ?? "nothing"}");
                    }
                    if (_settingsBefore.TryGetValue(key, out var was)) Settings.Entry(key)?.SetSerializedValue(was);
                }
            }
            p.Check(tried > 0 && wrong.Count == 0, $"every setting the card sets takes each value its control offers ({Numbers.Count(tried)} tried)", string.Join("; ", wrong.Take(5)));

            OpenKeyKept(p);

            // A section's reset brings back its defaults.
            var panel = SettingsCatalog.Sections.First(s => s.Name == "Panel");
            Settings.TooltipDelay = 2f;
            ScryPanel.ResetSettings(panel);
            var notDefault = panel.Rows.SelectMany(r => r.Keys).Where(k => Settings.Entry(k) is ConfigEntryBase e && !Equals(e.BoxedValue, e.DefaultValue)).ToList();
            p.Check(notDefault.Count == 0 && Mathf.Approximately(Settings.TooltipDelay, 0.35f), "a section's reset brings back each of its settings' defaults", string.Join(", ", notDefault));
            RestoreSettings();

            // What the server decides: locked while a server decides it, the player's own otherwise.
            var server = SettingsCatalog.AllRows.Where(r => r.ServerDecides).ToList();
            if (Settings.WorldFromServer)
            {
                var row = server.First(r => r.Control == SettingControl.Switch);
                var key = row.Keys[0];
                var before = Settings.Entry(key)?.GetSerializedValue();
                ScryPanel.SetSetting(row, key, before == "true" ? "false" : "true");
                p.Check(server.All(ScryPanel.SettingLocked) && Settings.Entry(key)?.GetSerializedValue() == before, "on a server deciding them, what it decides is shown locked and a click sets nothing");
            }
            else p.Check(!server.Any(ScryPanel.SettingLocked), "playing alone or hosting, what a server would decide is the player's own to set");
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
            var kept = _settingsBefore;
            SettingsAsTheyWere();
            p.Check(kept != null && kept.All(pair => Settings.Entry(pair.Key)?.GetSerializedValue() == pair.Value), "every setting is put back as it was");
        }

        /// <summary>
        /// The key opening the panel as the config file holds it: a key alone, as older configs
        /// wrote it, reads as it was; one with Shift reads with it; a mouse click is refused, as
        /// the card's own way of setting it would refuse it. Put back after.
        /// </summary>
        private static void OpenKeyKept(Probe p)
        {
            var entry = Settings.Entry("OpenKey");
            var row = SettingsCatalog.Sections.SelectMany(s => s.Rows).First(r => r.Control == SettingControl.Key);
            if (entry == null)
            {
                p.Note("there is no open key in the config file");
                return;
            }
            var was = entry.GetSerializedValue();
            entry.SetSerializedValue("F7");
            var alone = (Settings.OpenKey, Settings.OpenMods, Settings.OpenKeyName);
            entry.SetSerializedValue("F7 + LeftShift");
            var shifted = (Settings.OpenKey, Settings.OpenMods, Settings.OpenKeyName);
            ScryPanel.SetSetting(row, "OpenKey", "Mouse0");
            var refused = Settings.OpenKeyName;
            entry.SetSerializedValue(was);
            p.Check(alone == (KeyCode.F7, KeyMods.None, "F7") && shifted == (KeyCode.F7, KeyMods.Shift, "Shift+F7") && refused == "Shift+F7",
                "the open key reads as an older config wrote it, takes Shift with it, and refuses a mouse click", $"{alone.Item3}, {shifted.Item3}, then {refused}");
        }

        /// <summary>The values a row's control offers, as the config file writes them: both ways of a switch or tick, each choice, a slider's ends.</summary>
        private static IEnumerable<string> Offered(SettingRow row)
        {
            switch (row.Control)
            {
                case SettingControl.Switch:
                case SettingControl.Ticks:
                    return new[] { "false", "true" };
                case SettingControl.Choices:
                    return row.Choices.Select(c => c.Value);
                default:
                    return new[] { row.Min, row.Max }.Select(Stored.Exact);
            }
        }

        /// <summary>Whether a value read back is the one set: alike as words, or as numbers.</summary>
        private static bool SameValue(string read, string set) =>
            string.Equals(read, set, System.StringComparison.OrdinalIgnoreCase)
            || Stored.TryExact(read, out var a) && Stored.TryExact(set, out var b) && Mathf.Abs(a - b) < 0.001f;

        /// <summary>Every setting the part changed back as the config file had it.</summary>
        private static void RestoreSettings()
        {
            if (_settingsBefore == null) return;
            foreach (var pair in _settingsBefore) Settings.Entry(pair.Key)?.SetSerializedValue(pair.Value);
        }

        /// <summary>The settings tried put back, the list as it was, the card put away.</summary>
        private static void SettingsAsTheyWere()
        {
            ScryPanel.ShowSettings(false);
            RestoreSettings();
            if (_listHiddenBefore is bool hidden) ScryPanel.ListHidden = hidden;
            _listHiddenBefore = null;
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

        /// <summary>
        /// The first start: the key told by its name as set; the setup shown again from the
        /// settings over the panel's body, every row drawn, the compendium's step among them where
        /// there is a character's book; Later putting it away; Done keeping it done where the
        /// character was asked about its compendium before, so nothing is answered for the player.
        /// Whether the key was told and the setup done is put back as it was.
        /// </summary>
        private static IEnumerator SetupShown(Probe p)
        {
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            ScryPanel.CloseCards();
            var before = ScryPanel.FirstStartForTest;
            _firstStartBefore = before;

            var cards = ScryPanel.Drawn(PanelPart.SetupCard);
            ScryPanel.ShowSetup();
            yield return Until(() => ScryPanel.Drawn(PanelPart.SetupCard) > cards + 1, 3);
            var rows = SettingsCatalog.Setup.Count + (CompendiumKeeper.Book != null ? 1 : 0);
            p.Check(ScryPanel.SetupShown && ScryPanel.SetupRowsDrawnForTest == rows, "Run the setup again shows the setup over the panel, every row drawn",
                $"{Numbers.Count(ScryPanel.SetupRowsDrawnForTest)} of {Numbers.Count(rows)} rows");
            yield return Picture(p, "first start setup");

            ScryPanel.SetupLater();
            yield return null;
            p.Check(!ScryPanel.SetupShown, "Later puts it away");

            var book = CompendiumKeeper.Book;
            if (book == null || !book.Asked) p.Note("this character was never asked about its compendium, so Done is not tried: it would answer for the player");
            else
            {
                ScryPanel.ShowSetup();
                yield return null;
                ScryPanel.SetupDone();
                yield return null;
                p.Check(!ScryPanel.SetupShown && ScryPanel.FirstStartForTest.SetUp, "Done keeps the setup done and puts it away");
            }
            SetupAsItWas();
            var onFile = ScryPanel.FirstStartOnFileForTest;
            p.Check(onFile.Hinted == before.Hinted && onFile.SetUp == before.SetUp, "whether the key was told and the setup done is put back as it was, in the panel's file",
                $"{onFile.Hinted}, {onFile.SetUp} for {before.Hinted}, {before.SetUp}");
        }

        private static (bool Hinted, bool SetUp)? _firstStartBefore;

        /// <summary>The setup put away and its flags put back as they were, however the part ended.</summary>
        private static void SetupAsItWas()
        {
            if (_firstStartBefore is (bool, bool) before) ScryPanel.FirstStartForTest = before;
            _firstStartBefore = null;
            ScryPanel.SetupLater();
        }
    }
}
