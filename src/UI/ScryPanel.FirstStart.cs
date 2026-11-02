using System.IO;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The first start (<see cref="FirstStart"/>): the key opening Scry told once where the game
    /// tells its own messages, a few seconds into a world; and the setup offered as Scry opens,
    /// over the panel's whole body: the key, the character's compendium, walking and looking while
    /// it is open, and what it puts on Valheim's map, each set already, so Done at once is fine.
    /// Later puts it off until Scry next opens; the settings' Run the setup again brings it back.
    /// Whether the key was told and the setup done is kept with the panel's place, in panel.txt.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static bool? _hinted, _setUp;
        private static bool _putOff, _setupAsked;
        private static Vector2 _setupScroll;
        private static float _setupHeight;
        private static Player _hintFor;
        private static float _hintSince;

        /// <summary>Whether the key was told and the setup done as panel.txt holds them, read from it afresh, for the self-test.</summary>
        [Diagnostic]
        public static (bool? Hinted, bool? SetUp) FirstStartOnFileForTest
        {
            get
            {
                var place = File.Exists(RectFile) ? PanelPlace.Read(File.ReadAllLines(RectFile)) : new PanelPlace();
                return (place.Hinted, place.SetUp);
            }
        }

        /// <summary>Whether the key was told and the setup done, and setting them for the self-test, as kept in panel.txt.</summary>
        [ForTest]
        public static (bool Hinted, bool SetUp) FirstStartForTest
        {
            get
            {
                ReadFirstStart();
                return (_hinted == true, _setUp == true);
            }
            set
            {
                _hinted = value.Hinted;
                _setUp = value.SetUp;
                KeepFirstStart();
            }
        }

        /// <summary>How many rows the setup drew at the last repaint, for the self-test.</summary>
        [Diagnostic]
        public static int SetupRowsDrawnForTest { get; private set; }

        /// <summary>The flags as read with the panel's place.</summary>
        private static void TakeFirstStart(PanelPlace place)
        {
            if (place.Hinted.HasValue) _hinted = place.Hinted;
            if (place.SetUp.HasValue) _setUp = place.SetUp;
        }

        /// <summary>The flags written with the panel's place.</summary>
        private static void GiveFirstStart(PanelPlace place)
        {
            place.Hinted = _hinted;
            place.SetUp = _setUp;
        }

        /// <summary>The flags read once from panel.txt, as the key may be told before the panel ever opens and reads its place.</summary>
        private static void ReadFirstStart()
        {
            if (_hinted.HasValue && _setUp.HasValue) return;
            var place = new PanelPlace();
            // A file that cannot be read counts as told and done, so nothing covers the panel or is told each start.
            var read = Guard.Run(Feature.PanelPlace, "reading the first start", () => place = File.Exists(RectFile) ? PanelPlace.Read(File.ReadAllLines(RectFile)) : new PanelPlace());
            _hinted = _hinted ?? (read ? place.Hinted ?? false : true);
            _setUp = _setUp ?? (read ? place.SetUp ?? false : true);
        }

        /// <summary>The flags written into panel.txt as it stands, the rest of it kept: the panel's place may not be read yet.</summary>
        private static void KeepFirstStart() => Guard.Run(Feature.PanelPlace, "remembering the first start", () =>
        {
            var lines = File.Exists(RectFile) ? File.ReadAllLines(RectFile) : System.Array.Empty<string>();
            Directory.CreateDirectory(Settings.DataFolder);
            File.WriteAllLines(RectFile, PanelPlace.WithFirstStart(lines, _hinted, _setUp));
        });

        /// <summary>Each frame: the key opening Scry told once, a few seconds after a character stands in a world.</summary>
        private static void FirstStartHint()
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                _hintFor = null;
                return;
            }
            if (player != _hintFor)
            {
                _hintFor = player;
                _hintSince = Time.unscaledTime;
            }
            if (_hinted == true) return;
            ReadFirstStart();
            // With the HUD hidden the game shows no message: told later, once it shows.
            if (!FirstStart.HintDue(_hinted == true, true, Time.unscaledTime - _hintSince) || MessageHud.instance == null || Hud.IsUserHidden()) return;
            _hinted = true;
            KeepFirstStart();
            MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, FirstStart.Hint(Settings.OpenKeyName));
        }

        /// <summary>Scry opening again: a setup put off with Later is offered once more.</summary>
        private static void FirstStartOpened() => _putOff = false;

        /// <summary>Whether the setup is shown: asked for by the settings, or due as Scry opens (<see cref="FirstStart.SetupDue"/>), not while the self-test runs.</summary>
        public static bool SetupShown
        {
            get
            {
                if (_setupAsked) return true;
                if (SelfTestHost.StripShown || SelfTestHost.Running) return false;
                ReadFirstStart();
                var book = CompendiumKeeper.Book;
                return FirstStart.SetupDue(_setUp == true, _putOff, book == null || !book.WelcomeDue);
            }
        }

        /// <summary>The setup shown again, as the settings' Run the setup again asks; the settings put away.</summary>
        public static void ShowSetup()
        {
            ShowSettings(false);
            _setupAsked = true;
        }

        /// <summary>The setup's Later: put off until Scry next opens.</summary>
        public static void SetupLater()
        {
            _putOff = true;
            _setupAsked = false;
        }

        /// <summary>The setup's Done: kept as done, and the character asked about its compendium.</summary>
        public static void SetupDone()
        {
            _setUp = true;
            _setupAsked = false;
            KeepFirstStart();
            // Where the server sets the rules nothing was asked of the character: it is asked where it may choose.
            var book = CompendiumKeeper.Book;
            if (book == null || Settings.World().RulesSet) return;
            book.Welcome();
            CompendiumKeeper.Save(now: true);
        }

        private static void SetupCard(Rect rect)
        {
            CountDrawn(PanelPart.SetupCard);
            Skin.Box(rect, Skin.Panel);
            var width = Mathf.Min(U(720f), rect.width - U(40f));
            var area = new Rect(rect.x + (rect.width - width) / 2f, rect.y + U(14f), width, rect.height - CardFootHeight - U(34f));
            var view = new Rect(0f, 0f, area.width - U(14f), Mathf.Max(_setupHeight, area.height));
            _setupScroll = GUI.BeginScrollView(area, _setupScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            var w = view.width - U(10f);
            var y = U(4f);
            GUI.Label(new Rect(0f, y, w, U(30f)), SettingsWords.SetupTitle, Skin.Big);
            y += U(36f);
            y = Wrapped(SettingsWords.SetupIntro, Skin.Wrap, w, y) + U(10f);
            var rows = 0;
            for (var i = 0; i < SettingsCatalog.Setup.Count; i++)
            {
                if (i == 1 && CompendiumKeeper.Book != null)
                {
                    y = CompendiumStep(w, y);
                    rows++;
                }
                var row = SettingsCatalog.Setup[i];
                Skin.Fill(new Rect(0f, y, w, 1f), Skin.Alpha(Skin.Text, 0.06f));
                y += U(10f);
                GUI.Label(new Rect(0f, y, w, U(24f)), row.Label, Skin.Label);
                y += U(28f);
                y += DrawSettingControl(row, new Rect(0f, y, w, U(26f))) + U(4f);
                y = Wrapped(row.Why, Skin.DimWrap, w, y) + U(8f);
                rows++;
            }
            GUI.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                _setupHeight = y + U(10f);
                SetupRowsDrawnForTest = rows;
            }

            var foot = rect.yMax - CardFootHeight - U(12f);
            var doneW = Skin.Width(Skin.Primary, SettingsWords.Done) + U(28f);
            var laterW = Skin.Width(Skin.Button, SettingsWords.Later) + U(24f);
            var right = area.xMax;
            if (GUI.Button(new Rect(right - doneW, foot, doneW, CardFootHeight), SettingsWords.Done, Skin.Primary)) SetupDone();
            if (GUI.Button(new Rect(right - doneW - U(8f) - laterW, foot, laterW, CardFootHeight), SettingsWords.Later, Skin.Button)) SetupLater();
            GUI.Label(new Rect(area.x, foot, area.width - doneW - laterW - U(24f), CardFootHeight), SettingsWords.SetupKept, Skin.DimLabel);
        }

        /// <summary>The character's compendium: its rules to choose, each saying what it means, the one chosen lit; where the server sets them, which it set. Where it ends.</summary>
        private static float CompendiumStep(float w, float y)
        {
            var book = CompendiumKeeper.Book;
            Skin.Fill(new Rect(0f, y, w, 1f), Skin.Alpha(Skin.Text, 0.06f));
            y += U(10f);
            GUI.Label(new Rect(0f, y, w, U(24f)), SettingsWords.SetupCompendium(Player.m_localPlayer.OrNull()?.GetPlayerName()), Skin.Label);
            y += U(28f);
            var world = Settings.World();
            if (world.RulesSet) return Wrapped(SettingsWords.SetupRulesSet(CompendiumWords.RuleName(world.Rule.Value)), Skin.DimWrap, w, y) + U(8f);
            var gap = U(8f);
            var boxW = (w - gap * 2f) / 3f;
            // Each box as tall as the longest words wrap in it, so none is cut.
            var meansH = 0f;
            foreach (var (_, _, means) in SettingsWords.SetupRules) meansH = Mathf.Max(meansH, Skin.DimWrap.CalcHeight(new GUIContent(means), boxW - U(20f)));
            var boxH = U(26f) + meansH + U(8f);
            for (var i = 0; i < SettingsWords.SetupRules.Length; i++)
            {
                var (rule, name, means) = SettingsWords.SetupRules[i];
                var box = new Rect(i * (boxW + gap), y, boxW, boxH);
                var on = book.ChosenRule == rule;
                if (GUI.Button(box, GUIContent.none, on ? Skin.On : Skin.Button) && !on) Choose(rule);
                GUI.Label(new Rect(box.x + U(10f), box.y + U(6f), box.width - U(20f), U(20f)), name, Skin.Label);
                GUI.Label(new Rect(box.x + U(10f), box.y + U(26f), box.width - U(20f), meansH), means, Skin.DimWrap);
            }
            return y + boxH + U(8f);
        }

        /// <summary>Words wrapped across a width at a height; where they end.</summary>
        private static float Wrapped(string words, GUIStyle style, float w, float y)
        {
            var h = style.CalcHeight(new GUIContent(words), w);
            GUI.Label(new Rect(0f, y, w, h), words, style);
            return y + h;
        }
    }
}
