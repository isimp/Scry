using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The settings' Compendium rows of the character's own: its rules, Known up to and what each
    /// source of the world shows, read from its book and set as the compendium's box sets them
    /// (<see cref="SetBookRow"/>, <see cref="MarkDungeons"/>), so the two never differ, the card
    /// standing as they change; what the world's server keeps from a row dimmed as the box dims it. Under each, what the world allows of it: set there while you
    /// play alone or host, while a server decides it a line saying what it keeps from the row.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The compendium's rows as last drawn on the card, by row and choice, whether each is lit, and whether it is the lock beside the row; for the self-test.</summary>
        [Diagnostic]
        public static List<(string Row, string Choice, bool Lit, bool Lock)> SettingsBookCellsForTest { get; } = new List<(string, string, bool, bool)>();

        /// <summary>The settings shown at the section of the character's compendium; the box's gear, and the self-test.</summary>
        public static void OpenCompendiumSettings()
        {
            ShowSettings(true);
            for (var i = 0; i < SettingsCatalog.Sections.Count; i++)
            {
                if (HasCompendiumRows(SettingsCatalog.Sections[i])) ShownSettingsSection = i;
            }
        }

        /// <summary>The cells noted afresh at each repaint of the card, so none stands from a section no longer drawn.</summary>
        private static void BookCellsAnew()
        {
            if (Event.current.type == EventType.Repaint) SettingsBookCellsForTest.Clear();
        }

        /// <summary>The lines heading the section: whose compendium it is; before the book is read, that it is read in a world; with the rules Off, that the rest waits for them. Where they end.</summary>
        private static float BookIntro(float width, float y)
        {
            var book = CompendiumKeeper.Book;
            y = Wrapped(SettingsWords.CompendiumFor(Player.m_localPlayer.OrNull()?.GetPlayerName()), Skin.DimWrap, width, y) + U(6f);
            if (book == null) y = Wrapped(SettingsWords.NoBook, Skin.DimWrap, width, y) + U(6f);
            else if (book.Rule == CompendiumRule.Off) y = Wrapped(SettingsWords.RulesOff, Skin.DimWrap, width, y) + U(6f);
            return y + U(2f);
        }

        /// <summary>
        /// A row of the character's compendium, as the box draws it, none before its book is read;
        /// with the rules Off, Known up to and the sources shown but not set; while a server keeps
        /// something from it, the lock beside it saying what, short. The Map section's row of which
        /// dungeons are marked, its ticks. How tall it stood.
        /// </summary>
        private static float BookControl(SettingRow row, Rect at)
        {
            var book = CompendiumKeeper.Book;
            if (row.Book == BookPart.DungeonPins)
            {
                DungeonPinTicks(row, book, at);
                return at.height;
            }
            if (book == null) return at.height;
            float end;
            if (row.Book == BookPart.Rules) end = RulesSegments(row, book, at);
            else
            {
                var was = GUI.enabled;
                GUI.enabled = was && book.Rule != CompendiumRule.Off;
                end = row.Book == BookPart.KnownUpTo ? FloorStepper(row, book, at) : SourceLevels(row, book, at);
                GUI.enabled = was;
            }
            return Settings.WorldFromServer ? at.height + LockBeside(row, book, end, at) : at.height;
        }

        /// <summary>
        /// After a row's control, while a server keeps something from it: the lock and what it
        /// keeps, short, the whole sentence on its tip; under the control where it would run past
        /// the column. How much lower the row stands for it.
        /// </summary>
        private static float LockBeside(SettingRow row, CompendiumBook book, float end, Rect at)
        {
            var said = WorldRulesWords.OnRowShort(row.Book, row.Source, book.World);
            if (said == null) return 0f;
            CountDrawn(PanelPart.WorldLocked);
            var size = U(12f);
            var wordsW = Skin.Width(Skin.DimLabel, said) + U(4f);
            var beside = end + U(12f) + size + U(5f) + wordsW <= at.xMax;
            var x = beside ? end + U(12f) : at.x;
            var y = beside ? at.y : at.yMax + U(4f);
            Skin.Icon(new Rect(x, y + (at.height - size) / 2f, size, size), Skin.Lock, Skin.Faint);
            var words = new Rect(x + size + U(5f), y, wordsW, at.height);
            GUI.Label(words, said, Skin.DimLabel);
            if (new Rect(x, y, words.xMax - x, at.height).Contains(Event.current.mousePosition)) AskTip("settings-lock-" + row.Label, WorldRulesWords.OnRow(row.Book, row.Source, book.World));
            NoteSettingsBook(row.Label, said, false, isLock: true);
            return beside ? 0f : at.height + U(4f);
        }

        /// <summary>A segment the server keeps from the player: dimmed, and struck through.</summary>
        private static void StruckSegment(Rect at, string text, GUIStyle style)
        {
            DimSegment(at, text, style);
            var w = Skin.Width(style, text) - U(10f);
            Skin.Fill(new Rect(at.center.x - w / 2f, at.center.y, w, Mathf.Max(1f, U(1f))), Skin.Alpha(Skin.Faint, 0.8f));
        }

        /// <summary>
        /// Which dungeons the map marks: a tick for those you walked, kept in the config file, then
        /// one for each ground, the character's pin for it, set as the pin sets it; the grounds'
        /// shown but not set before the character's book is read.
        /// </summary>
        private static void DungeonPinTicks(SettingRow row, CompendiumBook book, Rect at)
        {
            var x = at.x;
            var box = U(16f);
            for (var i = 0; i < row.TickNames.Count; i++)
            {
                var ground = i > 0;
                var on = ground ? book != null && book.MarksDungeons(SettingsCatalog.PinnedGrounds[i - 1]) : SettingValue(row.Keys[0]) == SettingsCatalog.True;
                var was = GUI.enabled;
                GUI.enabled = was && (!ground || book != null);
                var square = new Rect(x, at.y + (at.height - box) / 2f, box, box);
                Skin.Box(square, on ? Skin.Accent : Skin.Raised);
                if (on) Skin.Icon(square, Skin.Tick, Skin.OnAccent);
                var nameW = Skin.Width(Skin.DimLabel, row.TickNames[i]);
                GUI.Label(new Rect(square.xMax + U(7f), at.y, nameW + U(4f), at.height), row.TickNames[i], Skin.DimLabel);
                if (GUI.Button(new Rect(x, at.y, box + U(11f) + nameW, at.height), GUIContent.none, GUIStyle.none)) SetDungeonTick(row, i, !on);
                GUI.enabled = was;
                NoteSettingsBook(row.Label, row.TickNames[i], on);
                x += box + U(11f) + nameW + U(20f);
            }
        }

        /// <summary>A tick of which dungeons the map marks set on or off, as a click sets it: the first in the config file, a ground's as its pin. The self-test sets them this way.</summary>
        public static void SetDungeonTick(SettingRow row, int tick, bool on)
        {
            if (tick == 0)
            {
                SetSetting(row, row.Keys[0], on ? SettingsCatalog.True : SettingsCatalog.False);
                return;
            }
            var book = CompendiumKeeper.Book;
            var source = SettingsCatalog.PinnedGrounds[tick - 1];
            if (book != null && book.MarksDungeons(source) != on) MarkDungeons(source, on);
        }

        /// <summary>
        /// A row of the character's compendium set to a value, as a click on the card sets it: a
        /// rule, Known up to a step from where it stands, a source's level; nothing the box would
        /// refuse, such as rules the server sets, a level above its cap, With insides where nothing
        /// found has insides, or anything but the rules while they are Off. The card stays. The
        /// self-test sets rows this way.
        /// </summary>
        public static void SetBookRow(SettingRow row, int value)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            if (row.Book == BookPart.Rules)
            {
                if (!book.World.RulesSet && Enum.IsDefined(typeof(CompendiumRule), value) && book.Rule != (CompendiumRule)value) Choose((CompendiumRule)value);
                return;
            }
            if (book.Rule == CompendiumRule.Off) return;
            if (row.Book == BookPart.KnownUpTo)
            {
                if (value == book.Floor - 1 && value >= 0 && book.World.FloorAllowed || value == book.Floor + 1 && CanRaise(book.Floor)) SetFloor(book, value);
                return;
            }
            var level = (SourceLevel)value;
            var shows = book.Shows(row.Source);
            if (row.Book != BookPart.Source || level == shows || Array.IndexOf(WorldSources.Offered(row.Source), level) < 0 || !book.World.Allows(row.Source, level)) return;
            if (level == SourceLevel.WithInsides && !OffersInsides(book, row.Source)) return;
            ShowSource(row.Source, level);
        }

        /// <summary>The rules joined into one piece, the one chosen lit; where the server sets them, the others dimmed, saying so. Where they end.</summary>
        private static float RulesSegments(SettingRow row, CompendiumBook book, Rect at)
        {
            var mouse = Event.current.mousePosition;
            var set = book.World.RulesSet;
            var x = at.x;
            for (var i = 0; i < CompendiumWords.Rules.Count; i++)
            {
                var rule = CompendiumWords.Rules[i];
                var name = CompendiumWords.RuleName(rule);
                var on = book.Rule == rule;
                var style = Skin.Choice(i, CompendiumWords.Rules.Count, on);
                var segment = new Rect(x, at.y, Skin.Width(style, name) + U(20f), at.height);
                if (set && !on) StruckSegment(segment, name, style);
                else if (GUI.Button(segment, name, style) && !on) SetBookRow(row, (int)rule);
                if (segment.Contains(mouse)) AskTip("settings-rule-" + name, set ? WorldRulesWords.RulesSet(book.Rule) : CompendiumWords.StepTip(rule));
                NoteSettingsBook(CompendiumWords.RulesCaption, name, on);
                x = segment.xMax;
            }
            return x;
        }

        /// <summary>Known up to's biome between arrows lowering and raising it, tier by tier, as far as the bosses beaten reach; off where the server keeps it off. Where it ends.</summary>
        private static float FloorStepper(SettingRow row, CompendiumBook book, Rect at)
        {
            var mouse = Event.current.mousePosition;
            var floor = book.Floor;
            var allowed = book.World.FloorAllowed;
            var name = CompendiumWords.FloorName(floor);
            var lower = new Rect(at.x, at.y, at.height, at.height);
            var value = new Rect(lower.xMax + U(6f), at.y, Mathf.Max(U(100f), Skin.Width(Skin.Value, name) + U(16f)), at.height);
            var raise = new Rect(value.xMax + U(6f), at.y, at.height, at.height);
            var was = GUI.enabled;
            GUI.enabled = was && allowed && floor > 0;
            if (GUI.Button(lower, "‹", Skin.IconButton)) SetBookRow(row, floor - 1);
            GUI.enabled = was && CanRaise(floor);
            if (GUI.Button(raise, "›", Skin.IconButton)) SetBookRow(row, floor + 1);
            GUI.enabled = was;
            GUI.Label(value, name, Skin.Value);
            NoteSettingsBook(CompendiumWords.FloorCaption, name, false);
            if (!allowed)
            {
                if (lower.Contains(mouse) || value.Contains(mouse) || raise.Contains(mouse)) AskTip("settings-floor-locked", WorldRulesWords.NoFloor);
                return raise.xMax;
            }
            if (floor > 0 && lower.Contains(mouse)) AskTip("settings-floor-down", CompendiumWords.LowerFloor(floor - 1));
            if (floor < Tiers.Last && (raise.Contains(mouse) || value.Contains(mouse)))
            {
                var boss = _settingsExplorer?.Find(Tiers.BossOf(floor + 1))?.ShownName ?? Naming.Readable(Tiers.BossOf(floor + 1));
                AskTip("settings-floor-up", CanRaise(floor) ? CompendiumWords.RaiseFloor(floor + 1, boss) : CompendiumWords.FloorWaits(boss));
            }
            return raise.xMax;
        }

        /// <summary>
        /// A source's levels joined into one piece, the one it shows lit; above what the server
        /// allows dimmed, With insides dimmed where nothing found has insides, each saying why; a
        /// ground's pin after them, marking its dungeons on the big map. Where they end.
        /// </summary>
        private static float SourceLevels(SettingRow row, CompendiumBook book, Rect at)
        {
            var source = row.Source;
            var mouse = Event.current.mousePosition;
            var shows = book.Shows(source);
            var offered = WorldSources.Offered(source);
            var sourceName = CompendiumWords.SourceName(source);
            var x = at.x;
            for (var o = 0; o < offered.Length; o++)
            {
                var level = offered[o];
                var name = CompendiumWords.LevelName(level);
                var capped = !book.World.Allows(source, level);
                var dimmed = capped || level == SourceLevel.WithInsides && shows != level && !OffersInsides(book, source);
                var style = Skin.Choice(o, offered.Length, shows == level);
                var segment = new Rect(x, at.y, Skin.Width(style, name) + U(20f), at.height);
                if (capped) StruckSegment(segment, name, style);
                else if (dimmed) DimSegment(segment, name, style);
                else if (GUI.Button(segment, name, style) && shows != level) SetBookRow(row, (int)level);
                if (segment.Contains(mouse)) AskTip("settings-level-" + source + level, capped ? WorldRulesWords.UpTo(source, book.World.Highest(source)) : dimmed ? CompendiumWords.NoInsides(source) : CompendiumWords.LevelTip(level));
                NoteSettingsBook(sourceName, name, shows == level);
                x = segment.xMax;
            }
            if (!WorldSources.HasGround(source)) return x;
            var pin = new Rect(x + U(10f), at.y, at.height, at.height);
            var marks = book.MarksDungeons(source);
            if (GUI.enabled && pin.Contains(mouse)) Skin.Box(pin, Skin.Hover);
            var inset = U(4f);
            Skin.Icon(new Rect(pin.x + inset, pin.y + inset, pin.width - inset * 2f, pin.height - inset * 2f), Skin.Doorway, marks ? Skin.Accent : Skin.Alpha(Skin.Faint, 0.45f));
            if (GUI.Button(pin, GUIContent.none, GUIStyle.none)) MarkDungeons(source, !marks);
            if (pin.Contains(mouse)) AskTip("settings-pin-" + source, Naming.Lines(CompendiumWords.PinName, CompendiumWords.PinTip(marks, Settings.DungeonMarksOnMap)));
            NoteSettingsBook(sourceName, "pin", marks);
            return pin.xMax;
        }

        private static void NoteSettingsBook(string row, string choice, bool lit, bool isLock = false)
        {
            if (Event.current.type == EventType.Repaint) SettingsBookCellsForTest.Add((row, choice, lit, isLock));
        }

        /// <summary>
        /// Under a row of the compendium's, what the world allows of it: while you play alone or
        /// host, its lock and name in the labels' column and its control under the row's own, set
        /// for players joining your world; while a server decides it, nothing here, the lock
        /// standing beside the row's control instead. Where it ends.
        /// </summary>
        private static float LimitLine(SettingRow row, float labelW, float width, float y)
        {
            var limit = row.Limit;
            // While a server decides it, what it keeps from the row stands beside the row's control (LockBeside).
            if (Settings.WorldFromServer) return y;
            Skin.Icon(new Rect(0f, y + U(5f), U(14f), U(14f)), Skin.Lock, Skin.Faint);
            var label = new Rect(U(20f), y, labelW - U(30f), U(24f));
            GUI.Label(label, Skin.Fit(Skin.DimLabel, limit.Label, label.width), Skin.DimLabel);
            if (label.Contains(Event.current.mousePosition)) AskTip("settings-limit-" + limit.Keys[0], Naming.Lines(limit.Label, limit.Why));
            var h = DrawSettingControl(limit, new Rect(labelW, y, width - labelW, U(24f)));
            return y + h + U(12f);
        }
    }
}
