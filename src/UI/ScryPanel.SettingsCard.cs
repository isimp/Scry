using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The settings card, opened by the gear: every setting Scry keeps, drawn from its catalogue
    /// (<see cref="SettingsCatalog"/>) over the panel's whole body, so it shows with the list
    /// folded too. The sections stand on its left; the one chosen fills the rest, each row its
    /// label, its control and its explanation under it, always shown. A switch sets what is on or
    /// off, joined choices one of a few, ticks several at once, a slider a value in its unit,
    /// set once it is let go. Each section resets to its defaults. What the world's server decides
    /// is shown but not set while a server decides it. Each change is written to the config file
    /// at once, through the file's own words for its values.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>Whether the next key pressed becomes the one opening the panel.</summary>
        private static bool _keyWaiting;

        /// <summary>Whether the settings card waits for the key to open Scry with, so the key that opens it now does nothing meanwhile.</summary>
        public static bool KeyWaiting => _keyWaiting && SettingsShown;

        /// <summary>Whether the settings card is shown, and showing or putting it away; the gear and the self-test.</summary>
        public static bool SettingsShown => _card == ListCard.Settings;

        public static void ShowSettings(bool shown)
        {
            if (shown) ShowCard(ListCard.Settings);
            else CloseCard(ListCard.Settings);
            _keyWaiting = false;
        }

        /// <summary>
        /// While Press a key waits, the next key pressed becomes the one opening the panel, Escape
        /// keeping the one there is: taken before the panel's own keys, which would close the panel
        /// on Escape and pass by a key no text box holds. Waiting ends as the card is put away.
        /// Whether a key was taken.
        /// </summary>
        internal static bool KeyCaptured()
        {
            if (!_keyWaiting) return false;
            if (!SettingsShown)
            {
                _keyWaiting = false;
                return false;
            }
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return false;
            // A modifier pressed alone waits for the key it goes with.
            if (Settings.ModsOf(new[] { e.keyCode }) != KeyMods.None)
            {
                e.Use();
                return true;
            }
            if (e.keyCode != KeyCode.Escape) Settings.SetOpenKey(e.keyCode, Settings.HeldMods());
            _keyWaiting = false;
            e.Use();
            return true;
        }

        private static int _section;
        private static Vector2 _sectionScroll;
        private static float _sectionHeight;

        /// <summary>A slider's value while it is dragged, by the setting's key, set once it is let go.</summary>
        private static readonly Dictionary<string, float> SliderHeld = new Dictionary<string, float>();

        /// <summary>How many rows the section shown drew at the last repaint, for the self-test.</summary>
        [Diagnostic]
        public static int SettingsRowsDrawnForTest { get; private set; }

        /// <summary>The section shown, by its place among them, chosen by a click on its name or by the self-test.</summary>
        public static int ShownSettingsSection
        {
            get => _section;
            set
            {
                _section = Mathf.Clamp(value, 0, SettingsCatalog.Sections.Count - 1);
                _sectionScroll = Vector2.zero;
                SettingsRowsDrawnForTest = -1;
            }
        }

        private static void SettingsCard(Rect rect)
        {
            var e = Event.current;
            CountDrawn(PanelPart.SettingsCard);
            var drawn = Timing.Start();
            Skin.Box(rect, Skin.Panel);
            var navW = Mathf.Min(U(200f), rect.width * 0.3f);
            SettingsSections(new Rect(rect.x, rect.y, navW, rect.height - CardFootHeight - U(20f)));
            Skin.Fill(new Rect(rect.x + navW, rect.y + U(12f), 1f, rect.height - U(24f)), Skin.Alpha(Skin.Text, 0.08f));

            var area = new Rect(rect.x + navW + U(16f), rect.y + U(8f), rect.width - navW - U(24f), rect.height - CardFootHeight - U(26f));
            var view = new Rect(0f, 0f, area.width - U(14f), Mathf.Max(_sectionHeight, area.height));
            _sectionScroll = GUI.BeginScrollView(area, _sectionScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            var height = SettingsSection(SettingsCatalog.Sections[_section], view.width - U(10f));
            GUI.EndScrollView();
            if (e.type == EventType.Repaint) _sectionHeight = height;

            var close = new Rect(rect.xMax - U(96f), rect.yMax - CardFootHeight - U(10f), U(80f), CardFootHeight);
            if (GUI.Button(close, SettingsWords.Close, Skin.Button)) ShowSettings(false);
            Timing.Add("panel settings", drawn);
        }

        /// <summary>The card's title, its sections to choose from, each with how many settings it holds, and where every change is kept.</summary>
        private static void SettingsSections(Rect nav)
        {
            GUI.Label(new Rect(nav.x + U(16f), nav.y + U(12f), nav.width - U(24f), U(26f)), SettingsWords.Title, Skin.Big);
            var y = nav.y + U(50f);
            for (var i = 0; i < SettingsCatalog.Sections.Count; i++)
            {
                var section = SettingsCatalog.Sections[i];
                var row = new Rect(nav.x + U(6f), y, nav.width - U(12f), U(30f));
                var on = i == _section;
                if (on)
                {
                    Skin.Box(row, Skin.Alpha(Skin.Accent, 0.16f));
                    Skin.Fill(new Rect(row.x, row.y + U(5f), U(3f), row.height - U(10f)), Skin.Accent);
                }
                if (GUI.Button(row, GUIContent.none, GUIStyle.none) && !on) ShownSettingsSection = i;
                GUI.Label(new Rect(row.x + U(12f), row.y, row.width - U(44f), row.height), section.Name, on ? Skin.Label : Skin.DimLabel);
                GUI.Label(new Rect(row.xMax - U(36f), row.y, U(28f), row.height), Numbers.Count(SettingCount(section)), Skin.DimRight);
                y += U(32f);
            }
            var kept = Skin.DimWrap.CalcHeight(new GUIContent(SettingsWords.KeptAtOnce), nav.width - U(28f));
            GUI.Label(new Rect(nav.x + U(16f), y + U(14f), nav.width - U(28f), kept), SettingsWords.KeptAtOnce, Skin.DimWrap);
        }

        /// <summary>How many settings a section holds, a tick each.</summary>
        private static int SettingCount(SettingSection section)
        {
            var count = 0;
            foreach (var row in section.Rows) count += row.Keys.Count;
            return count;
        }

        /// <summary>A section's rows, under its name and its reset; what a server decides, with a line saying whose it is. How tall it stood.</summary>
        private static float SettingsSection(SettingSection section, float width)
        {
            var y = U(6f);
            GUI.Label(new Rect(0f, y, width * 0.6f, U(28f)), section.Name, Skin.Big);
            var resetW = Skin.Width(Skin.Button, SettingsWords.Reset) + U(16f);
            // Where a server decides every row, a reset would set nothing: none offered.
            if (!AllLocked(section) && GUI.Button(new Rect(width - resetW, y, resetW, U(26f)), SettingsWords.Reset, Skin.Button)) ResetSettings(section);
            y += U(38f);
            if (HasServerRows(section))
            {
                var line = Settings.WorldFromServer ? SettingsWords.ServerDecides : SettingsWords.ServerYours;
                var h = Skin.DimWrap.CalcHeight(new GUIContent(line), width);
                Skin.Icon(new Rect(0f, y + U(1f), U(14f), U(14f)), Skin.Lock, Skin.Faint);
                GUI.Label(new Rect(U(20f), y, width - U(20f), h), line, Skin.DimWrap);
                y += h + U(10f);
            }
            var labelW = Mathf.Min(U(220f), width * 0.34f);
            string group = null;
            var drawn = 0;
            foreach (var row in section.Rows)
            {
                if (!ShowsSettingRow(row)) continue;
                if (row.Group != group)
                {
                    group = row.Group;
                    if (group != null)
                    {
                        GUI.Label(new Rect(0f, y + U(6f), width, U(20f)), group, Skin.FaintLabel);
                        y += U(28f);
                    }
                }
                Skin.Fill(new Rect(0f, y, width, 1f), Skin.Alpha(Skin.Text, 0.05f));
                y += U(9f);
                GUI.Label(new Rect(0f, y, labelW - U(10f), U(26f)), row.Label, Skin.Label);
                var controlW = width - labelW;
                var enabled = GUI.enabled;
                GUI.enabled = enabled && !SettingLocked(row);
                y += DrawSettingControl(row, new Rect(labelW, y, controlW, U(26f))) + U(5f);
                GUI.enabled = enabled;
                var whyH = Skin.DimWrap.CalcHeight(new GUIContent(row.Why), controlW);
                GUI.Label(new Rect(labelW, y, controlW, whyH), row.Why, Skin.DimWrap);
                y += whyH + U(10f);
                drawn++;
            }
            if (Event.current.type == EventType.Repaint) SettingsRowsDrawnForTest = drawn;
            return y + U(10f);
        }

        private static bool AllLocked(SettingSection section)
        {
            foreach (var row in section.Rows) if (!SettingLocked(row)) return false;
            return true;
        }

        private static bool HasServerRows(SettingSection section)
        {
            foreach (var row in section.Rows) if (row.ServerDecides) return true;
            return false;
        }

        /// <summary>Whether a row is drawn: what only the game running the world reads is left out while a server decides the world, as this game's own would tell nothing of it.</summary>
        public static bool ShowsSettingRow(SettingRow row) => !(row.Group == SettingsCatalog.RunningTheWorld && Settings.WorldFromServer);

        /// <summary>Whether a row is shown but not set: what the world's server decides, while one connected decides it.</summary>
        public static bool SettingLocked(SettingRow row) => row.ServerDecides && Settings.WorldFromServer;

        /// <summary>A row's control, drawn in its kind; how tall it stood.</summary>
        private static float DrawSettingControl(SettingRow row, Rect at)
        {
            switch (row.Control)
            {
                case SettingControl.Key: return KeyControl(at);
                case SettingControl.Switch: return SwitchControl(row, at);
                case SettingControl.Choices: return ChoicesControl(row, at);
                case SettingControl.Ticks: return TicksControl(row, at);
                default: return SliderControl(row, at);
            }
        }

        /// <summary>The key opening the panel, sunken as a value read, and the button that waits for another.</summary>
        private static float KeyControl(Rect at)
        {
            var keyName = _keyWaiting ? SettingsWords.Waiting : Settings.OpenKeyName;
            var keyW = Mathf.Max(U(60f), Skin.Width(Skin.Value, keyName) + U(16f));
            GUI.Label(new Rect(at.x, at.y, keyW, at.height), keyName, Skin.Value);
            var press = new Rect(at.x + keyW + U(6f), at.y, Skin.Width(Skin.Button, SettingsWords.PressAKey) + U(12f), at.height);
            if (GUI.Button(press, SettingsWords.PressAKey, _keyWaiting ? Skin.On : Skin.Button)) _keyWaiting = !_keyWaiting;
            if (press.Contains(Event.current.mousePosition)) AskTip("settings-key", SettingsWords.PressTip);
            return at.height;
        }

        /// <summary>A switch: a track lit when on, its knob at the end it stands at, and the word for it.</summary>
        private static float SwitchControl(SettingRow row, Rect at)
        {
            var key = row.Keys[0];
            var on = SettingValue(key) == SettingsCatalog.True;
            var track = new Rect(at.x, at.y + (at.height - U(20f)) / 2f, U(36f), U(20f));
            Skin.PillBox(track, on ? Skin.Accent : Skin.Raised);
            var knob = U(14f);
            Skin.Icon(new Rect(on ? track.xMax - knob - U(3f) : track.x + U(3f), track.y + U(3f), knob, knob), Skin.Circle, on ? Skin.OnAccent : Skin.Faint);
            var word = on ? SettingsWords.On : SettingsWords.Off;
            GUI.Label(new Rect(track.xMax + U(9f), at.y, U(60f), at.height), word, Skin.DimLabel);
            if (GUI.Button(new Rect(at.x, at.y, track.width + U(60f), at.height), GUIContent.none, GUIStyle.none)) SetSetting(row, key, SettingsCatalog.Flipped(SettingValue(key)));
            return at.height;
        }

        /// <summary>One of a few, joined into one piece, the one set lit.</summary>
        private static float ChoicesControl(SettingRow row, Rect at)
        {
            var key = row.Keys[0];
            var value = SettingValue(key);
            var x = at.x;
            for (var i = 0; i < row.Choices.Count; i++)
            {
                var choice = row.Choices[i];
                var on = string.Equals(choice.Value, value, StringComparison.OrdinalIgnoreCase);
                var style = Skin.Choice(i, row.Choices.Count, on);
                var w = Skin.Width(style, choice.Name) + U(20f);
                if (GUI.Button(new Rect(x, at.y, w, at.height), choice.Name, style) && !on) SetSetting(row, key, choice.Value);
                x += w;
            }
            return at.height;
        }

        /// <summary>Several at once, each its box ticked where it is on, and its name.</summary>
        private static float TicksControl(SettingRow row, Rect at)
        {
            var x = at.x;
            var box = U(16f);
            for (var i = 0; i < row.Keys.Count; i++)
            {
                var key = row.Keys[i];
                var on = SettingValue(key) == SettingsCatalog.True;
                var square = new Rect(x, at.y + (at.height - box) / 2f, box, box);
                Skin.Box(square, on ? Skin.Accent : Skin.Raised);
                if (on) Skin.Icon(square, Skin.Tick, Skin.OnAccent);
                var nameW = Skin.Width(Skin.DimLabel, row.TickNames[i]);
                GUI.Label(new Rect(square.xMax + U(7f), at.y, nameW + U(4f), at.height), row.TickNames[i], Skin.DimLabel);
                if (GUI.Button(new Rect(x, at.y, box + U(11f) + nameW, at.height), GUIContent.none, GUIStyle.none)) SetSetting(row, key, SettingsCatalog.Flipped(SettingValue(key)));
                x += box + U(11f) + nameW + U(20f);
            }
            return at.height;
        }

        /// <summary>A slider and its value in its unit; the value set once the slider is let go, as the panel changing size under a dragged slider would move it from the mouse.</summary>
        private static float SliderControl(SettingRow row, Rect at)
        {
            var key = row.Keys[0];
            var stored = SettingNumber(key);
            var shown = SliderHeld.TryGetValue(key, out var held) ? held : stored;
            var bar = new Rect(at.x, at.y, Mathf.Min(U(240f), at.width * 0.55f), at.height);
            // Only a drag moves it: a value the config holds between two steps stands as it is until then.
            var raw = GUI.HorizontalSlider(BarIn(bar.x, bar.y, bar.width, bar.height), shown, row.Min, row.Max);
            if (!Mathf.Approximately(raw, shown)) SliderHeld[key] = SettingsCatalog.Stepped(row, raw);
            var now = SliderHeld.TryGetValue(key, out var moved) ? moved : stored;
            GUI.Label(new Rect(bar.xMax + U(14f), at.y, at.width - bar.width - U(14f), at.height), row.Shown(now), Skin.DimLabel);
            if (SliderHeld.TryGetValue(key, out var letGo) && GUIUtility.hotControl == 0)
            {
                SliderHeld.Remove(key);
                SetSetting(row, key, Stored.Exact(letGo));
            }
            return at.height;
        }

        /// <summary>
        /// A setting's value in force, in the config file's words for it: a server's while it
        /// decides it, as the config file's own reading would give this game's value kept beneath
        /// (<c>ConfigSync.PreventSavingServerInfo</c>); none before the settings are bound.
        /// </summary>
        private static string SettingValue(string key)
        {
            var entry = Settings.Entry(key);
            return entry == null ? null : TomlTypeConverter.ConvertToString(entry.BoxedValue, entry.SettingType);
        }

        /// <summary>A setting's number, for a slider.</summary>
        private static float SettingNumber(string key) => Stored.TryExact(SettingValue(key), out var value) ? value : 0f;

        /// <summary>
        /// A row's setting set to a value, in the config file's own words for it, as its control
        /// sets it: the key opening the panel through its own check, a number turned to the
        /// setting's type, nothing while the row is locked. The self-test sets rows this way.
        /// </summary>
        public static void SetSetting(SettingRow row, string key, string value)
        {
            var entry = Settings.Entry(key);
            if (entry == null || SettingLocked(row)) return;
            if (row.Control == SettingControl.Key)
            {
                var shortcut = KeyboardShortcut.Deserialize(value);
                Settings.SetOpenKey(shortcut.MainKey, Settings.ModsOf(shortcut.Modifiers));
                return;
            }
            if (entry.SettingType == typeof(int) && Stored.TryExact(value, out var number))
            {
                entry.BoxedValue = (int)Math.Round(number);
                return;
            }
            entry.SetSerializedValue(value);
        }

        /// <summary>Every setting of a section back to its default, but those a server decides while it does.</summary>
        public static void ResetSettings(SettingSection section)
        {
            foreach (var row in section.Rows)
            {
                if (SettingLocked(row)) continue;
                foreach (var key in row.Keys)
                {
                    var entry = Settings.Entry(key);
                    if (entry != null) entry.BoxedValue = entry.DefaultValue;
                }
            }
            SliderHeld.Clear();
        }
    }
}
