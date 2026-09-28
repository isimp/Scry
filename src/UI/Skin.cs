using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The panel's look: colours, generated textures and text styles. Everything is made in code,
    /// so the mod ships no assets. Styles are rebuilt when the panel's scale changes, since text is
    /// sharper drawn at its real size than scaled.
    /// </summary>
    internal static class Skin
    {
        public static readonly Color Backdrop = new Color(0.066f, 0.071f, 0.086f, 0.97f);
        public static readonly Color Panel = new Color(0.098f, 0.105f, 0.126f, 1f);
        public static readonly Color Stage = new Color(0.105f, 0.112f, 0.135f, 1f);
        public static readonly Color Raised = new Color(0.150f, 0.160f, 0.190f, 1f);
        public static readonly Color RaisedHover = new Color(0.190f, 0.200f, 0.240f, 1f);
        public static readonly Color Hover = new Color(1f, 1f, 1f, 0.045f);
        public static readonly Color Outline = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Text = new Color(0.930f, 0.910f, 0.870f, 1f);
        public static readonly Color Dim = new Color(0.760f, 0.755f, 0.775f, 1f);
        public static readonly Color Faint = new Color(0.600f, 0.605f, 0.640f, 1f);
        public static readonly Color Accent = new Color(0.960f, 0.720f, 0.340f, 1f);
        public static readonly Color AccentSoft = new Color(0.960f, 0.720f, 0.340f, 0.16f);
        public static readonly Color OnAccent = new Color(0.110f, 0.080f, 0.040f, 1f);

        public static GUISkin Gui;
        public static GUIStyle Title, Subtitle, Label, Small, DimLabel, FaintLabel, Heading, Big, Wrap, DimWrap;
        public static GUIStyle RowName, RowSub, Glyph, Center, CenterDim;
        public static GUIStyle Button, Primary, On, Chip, ChipOn, Segment, SegmentOn, Close;
        public static GUIStyle Field, Placeholder, Tab, TabOn, Tip, IconButton, Cross;
        public static Texture2D Rounded, Pill, Circle, Star, StarHollow, Clock;

        private static float _builtScale = -1f;
        private static bool _warmed;
        private static int _warmNext;

        /// <summary>Every printable letter and the signs the panel draws: crosses, dots, arrows, the play mark, degrees.</summary>
        private static readonly GUIContent Sample = new GUIContent(Letters());

        private static string Letters()
        {
            var every = new System.Text.StringBuilder();
            for (var c = ' '; c <= '~'; c++) every.Append(c);
            every.Append("×·▸▾›‹▶°…");
            return every.ToString();
        }
        private static Font _body;
        private static Font _heading;
        private static readonly Dictionary<TintKey, Texture2D> Tinted = new Dictionary<TintKey, Texture2D>();

        /// <summary>A shape in a fill and an outline, as a key for its tinted copy, made without building a string on every box drawn.</summary>
        private readonly struct TintKey : IEquatable<TintKey>
        {
            private readonly int _shape;
            private readonly Color _fill;
            private readonly Color _outline;
            private readonly bool _outlined;

            public TintKey(Texture2D shape, Color fill, Color? outline)
            {
                _shape = shape.GetInstanceID();
                _fill = fill;
                _outline = outline ?? default;
                _outlined = outline.HasValue;
            }

            public bool Equals(TintKey other) => _shape == other._shape && _fill == other._fill && _outline == other._outline && _outlined == other._outlined;
            public override bool Equals(object obj) => obj is TintKey other && Equals(other);
            public override int GetHashCode() => ((_shape * 31 + _fill.GetHashCode()) * 31 + _outline.GetHashCode()) * 2 + (_outlined ? 1 : 0);
        }

        /// <summary>The colour of no kind in particular: all of them, or one not known.</summary>
        public static readonly Color Neutral = new Color(0.62f, 0.64f, 0.70f);

        public static Color KindColor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return new Color(0.92f, 0.47f, 0.42f);
                case Kind.Item: return new Color(0.96f, 0.76f, 0.36f);
                case Kind.Piece: return new Color(0.78f, 0.62f, 0.45f);
                case Kind.Resource: return new Color(0.72f, 0.86f, 0.30f);
                case Kind.Projectile: return new Color(0.98f, 0.56f, 0.28f);
                case Kind.Effect: return new Color(0.70f, 0.56f, 0.97f);
                case Kind.Sound: return new Color(0.42f, 0.80f, 0.82f);
                case Kind.StatusEffect: return new Color(0.54f, 0.84f, 0.52f);
                case Kind.Other: return new Color(0.50f, 0.66f, 0.96f);
                default: return Neutral;
            }
        }

        /// <summary>The short mark drawn where an entry has no icon of its own.</summary>
        public static string KindMark(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return "C";
                case Kind.Item: return "I";
                case Kind.Piece: return "P";
                case Kind.Resource: return "R";
                case Kind.Projectile: return "Pr";
                case Kind.Effect: return "Fx";
                case Kind.Sound: return "S";
                case Kind.StatusEffect: return "SE";
                default: return "O";
            }
        }

        /// <summary>
        /// Builds everything once at the main menu and measures every character at every size, so
        /// the one-off cost of IMGUI's first text lands there and not on the frame the panel opens.
        /// </summary>
        public static void Warm(float scale)
        {
            if (_warmed) return;

            try
            {
                Ensure(scale);

                // Every style the panel writes in, bold ones and the small sizes a label shrinks
                // to included, with the signs it draws besides letters; one style a frame, so the
                // warming itself never stalls one.
                var styles = new[]
                {
                    Title, Subtitle, Label, Small, DimLabel, FaintLabel, Heading, Big, Wrap, DimWrap, RowName, RowSub, Glyph, Center, CenterDim,
                    Button, Primary, On, Chip, ChipOn, Segment, SegmentOn, Close, Field, Placeholder, Tab, TabOn, Tip, IconButton, Cross,
                };
                if (_warmNext >= styles.Length)
                {
                    _warmed = true;
                    _warmNext = 0;
                    return;
                }
                var started = Timing.Start();
                var style = styles[_warmNext++];
                if (style != null) style.CalcSize(Sample);
                Timing.Add("skin glyphs", started);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry panel warm-up: {ex.Message}");
            }
        }

        /// <summary>
        /// How wide a text is in a style. Measuring text is the costly part of drawing the panel,
        /// and the panel is drawn several times a frame with mostly the same texts, so each size is
        /// measured once and kept.
        /// </summary>
        public static float Width(GUIStyle style, string text)
        {
            var key = new Measured(style, text, -1);
            if (!Known(key, out var width))
            {
                width = style.CalcSize(new GUIContent(text)).x;
                Keep(key, width);
            }
            return width;
        }

        /// <summary>
        /// How wide a text is, for one that is out of sight: measured if it was before, or while
        /// this frame's share for such texts lasts; otherwise told from the style's usual letter
        /// width until a later frame measures it. A list of hundreds (a person's clips) is so
        /// measured over a few frames rather than all in the one that shows it.
        /// </summary>
        public static float WidthSoon(GUIStyle style, string text)
        {
            var key = new Measured(style, text, -1);
            if (Known(key, out var width)) return width;
            if (_soonFrame != Time.frameCount)
            {
                _soonFrame = Time.frameCount;
                _soonMs = 0.0;
            }
            if (_soonMs >= SoonShareMs) return Estimate(style, text);

            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            width = Width(style, text);
            _soonMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return width;
        }

        private const double SoonShareMs = 1.5;
        private static int _soonFrame = -1;
        private static double _soonMs;
        private static readonly Dictionary<GUIStyle, float> LetterWidths = new Dictionary<GUIStyle, float>();

        /// <summary>A text's width told from its length and the style's usual letter width, measured once per style.</summary>
        private static float Estimate(GUIStyle style, string text)
        {
            if (!LetterWidths.TryGetValue(style, out var letter))
            {
                const string sample = "Attack Standing Idle walk run 0123456789";
                letter = Width(style, sample) / sample.Length;
                LetterWidths[style] = letter;
            }
            return letter * text.Length + style.padding.horizontal;
        }

        /// <summary>How tall a text is in a style, wrapped to a width, measured once and kept as <see cref="Width"/> is.</summary>
        public static float Height(GUIStyle style, string text, float width)
        {
            var key = new Measured(style, text, Mathf.RoundToInt(width * 2f));
            if (!Known(key, out var height))
            {
                height = style.CalcHeight(new GUIContent(text), width);
                Keep(key, height);
            }
            return height;
        }

        private static void Keep(Measured key, float value)
        {
            // Two generations: when the newer fills, it becomes the older and the oldest goes, so
            // texts still in use are found again in the older without all being measured anew.
            if (Sizes.Count >= 10000)
            {
                var older = OlderSizes;
                OlderSizes = Sizes;
                older.Clear();
                Sizes = older;
            }
            Sizes[key] = value;
        }

        private static bool Known(Measured key, out float value)
        {
            if (Sizes.TryGetValue(key, out value)) return true;
            if (!OlderSizes.TryGetValue(key, out value)) return false;
            Keep(key, value);
            return true;
        }

        private static Dictionary<Measured, float> Sizes = new Dictionary<Measured, float>();
        private static Dictionary<Measured, float> OlderSizes = new Dictionary<Measured, float>();

        /// <summary>A text measured in a style at its font size, and for a height, the width it wraps to (in half pixels; -1 for a width).</summary>
        private readonly struct Measured : IEquatable<Measured>
        {
            private readonly GUIStyle _style;
            private readonly int _fontSize;
            private readonly string _text;
            private readonly int _wrap;

            public Measured(GUIStyle style, string text, int wrap)
            {
                _style = style;
                _fontSize = style.fontSize;
                _text = text ?? "";
                _wrap = wrap;
            }

            public bool Equals(Measured other) => ReferenceEquals(_style, other._style) && _fontSize == other._fontSize && _wrap == other._wrap && _text == other._text;
            public override bool Equals(object obj) => obj is Measured other && Equals(other);
            public override int GetHashCode() => ((System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_style) * 31 + _fontSize) * 31 + _wrap) * 31 + _text.GetHashCode();
        }

        /// <summary>
        /// Looks for the game's fonts again the next time the styles are needed, when they were not
        /// loaded yet at the main menu. Once both are found this does nothing.
        /// </summary>
        public static void LookForFontsAgain()
        {
            if (_body == null || _heading == null) _builtScale = -1f;
        }

        /// <summary>Makes sure the styles exist at this scale. Only callable from OnGUI.</summary>
        public static void Ensure(float scale)
        {
            if (Gui != null && Mathf.Approximately(scale, _builtScale)) return;
            _builtScale = scale;
            LetterWidths.Clear();

            // Timed by part: the first time falls in the game's own loading, where it is hard to see.
            var started = Timing.Start();
            FindFonts();
            Timing.Add("skin fonts", started);
            started = Timing.Start();
            MakeTextures();
            Timing.Add("skin textures", started);

            if (Gui == null) Gui = UnityEngine.Object.Instantiate(GUI.skin);
            Gui.font = _body;

            int Px(float v) => Mathf.Max(1, Mathf.RoundToInt(v * scale));

            GUIStyle Style(float size, Color color, FontStyle weight = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
            {
                var style = new GUIStyle(Gui.label)
                {
                    font = _body,
                    fontSize = Px(size),
                    fontStyle = weight,
                    alignment = anchor,
                    wordWrap = false,
                    clipping = TextClipping.Clip,
                    richText = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                style.normal.textColor = color;
                style.hover.textColor = color;
                return style;
            }

            Title = Style(24f, Accent, FontStyle.Bold);
            if (_heading != null) Title.font = _heading;
            Subtitle = Style(13f, Faint);
            Label = Style(14f, Skin.Text);
            Small = Style(12f, Skin.Text);
            DimLabel = Style(13f, Dim);
            FaintLabel = Style(12f, Faint);
            Heading = Style(11f, Faint, FontStyle.Bold);
            Big = Style(19f, Skin.Text, FontStyle.Bold);
            Wrap = Style(13f, Skin.Text);
            Wrap.wordWrap = true;
            Wrap.alignment = TextAnchor.UpperLeft;
            DimWrap = Style(13f, Dim);
            DimWrap.wordWrap = true;
            DimWrap.alignment = TextAnchor.UpperLeft;
            RowName = Style(14f, Skin.Text);
            RowSub = Style(12f, Faint);
            Glyph = Style(11f, Skin.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Center = Style(15f, Skin.Text, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center.wordWrap = true;
            CenterDim = Style(13f, Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            CenterDim.wordWrap = true;

            Button = Boxed(Style(13f, Skin.Text, FontStyle.Normal, TextAnchor.MiddleCenter), Raised, RaisedHover, Shade(Raised, 1.35f), Rounded, scale);
            Primary = Boxed(Style(13f, OnAccent, FontStyle.Bold, TextAnchor.MiddleCenter), Accent, Shade(Accent, 1.08f), Shade(Accent, 0.85f), Rounded, scale);
            On = Boxed(Style(13f, Accent, FontStyle.Bold, TextAnchor.MiddleCenter), new Color(0.96f, 0.72f, 0.34f, 0.20f), new Color(0.96f, 0.72f, 0.34f, 0.28f), new Color(0.96f, 0.72f, 0.34f, 0.36f), Rounded, scale);
            Chip = Boxed(Style(12f, Dim, FontStyle.Normal, TextAnchor.MiddleCenter), Raised, RaisedHover, Shade(Raised, 1.35f), Pill, scale);
            // Lit by its colour alone: bolder text would no longer fit a chip measured unlit.
            ChipOn = Boxed(Style(12f, OnAccent, FontStyle.Normal, TextAnchor.MiddleCenter), Accent, Shade(Accent, 1.08f), Shade(Accent, 0.85f), Pill, scale);
            Segment = Boxed(Style(12f, Dim, FontStyle.Normal, TextAnchor.MiddleCenter), Raised, RaisedHover, Shade(Raised, 1.35f), Rounded, scale);
            SegmentOn = Boxed(Style(12f, OnAccent, FontStyle.Bold, TextAnchor.MiddleCenter), Accent, Shade(Accent, 1.08f), Shade(Accent, 0.85f), Rounded, scale);
            Close = Boxed(Style(20f, Dim, FontStyle.Normal, TextAnchor.MiddleCenter), new Color(0, 0, 0, 0), RaisedHover, Shade(RaisedHover, 1.3f), Rounded, scale);
            Close.hover.textColor = Skin.Text;
            Close.padding = new RectOffset(0, 0, 0, 0);

            // A bare � for small spots such as inside the search box, with no padding to squeeze it out.
            Cross = Style(19f, Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            Cross.clipping = TextClipping.Overflow;
            IconButton = Boxed(Style(13f, Dim, FontStyle.Normal, TextAnchor.MiddleCenter), Raised, RaisedHover, Shade(Raised, 1.35f), Rounded, scale);
            IconButton.padding = new RectOffset(0, 0, 0, 0);

            // Kind tabs: flat until hovered, filled when chosen. The left padding leaves room for the kind's dot.
            Tab = Boxed(Style(13f, Dim, FontStyle.Normal, TextAnchor.MiddleLeft), new Color(0, 0, 0, 0), RaisedHover, Shade(RaisedHover, 1.3f), Pill, scale);
            Tab.padding = new RectOffset(Px(24f), Px(12f), 0, 0);
            Tab.hover.textColor = Skin.Text;
            TabOn = Boxed(Style(13f, OnAccent, FontStyle.Bold, TextAnchor.MiddleLeft), Accent, Shade(Accent, 1.08f), Shade(Accent, 0.85f), Pill, scale);
            TabOn.padding = Tab.padding;
            Tab.richText = true;
            TabOn.richText = true;

            Tip = Boxed(Style(13f, Skin.Text), new Color(0.13f, 0.14f, 0.17f, 0.98f), new Color(0.13f, 0.14f, 0.17f, 0.98f), new Color(0.13f, 0.14f, 0.17f, 0.98f), Rounded, scale);
            Tip.normal.background = Tint(Rounded, new Color(0.13f, 0.14f, 0.17f, 0.98f), new Color(1f, 1f, 1f, 0.16f));
            Tip.wordWrap = true;
            Tip.alignment = TextAnchor.UpperLeft;
            Tip.padding = new RectOffset(Px(10f), Px(10f), Px(7f), Px(7f));

            Field = new GUIStyle(Gui.textField)
            {
                font = _body,
                fontSize = Px(15f),
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(Px(12f), Px(30f), 0, 0),
                border = new RectOffset(10, 10, 10, 10),
                clipping = TextClipping.Clip,
            };
            Field.normal.background = Tint(Rounded, new Color(0.055f, 0.060f, 0.073f, 1f), Outline);
            Field.hover.background = Tint(Rounded, new Color(0.060f, 0.065f, 0.080f, 1f), new Color(1f, 1f, 1f, 0.14f));
            Field.focused.background = Tint(Rounded, new Color(0.060f, 0.065f, 0.080f, 1f), new Color(0.96f, 0.72f, 0.34f, 0.65f));
            Field.onNormal.background = Field.normal.background;
            Field.normal.textColor = Field.hover.textColor = Field.focused.textColor = Skin.Text;
            Gui.settings.cursorColor = Accent;
            Gui.settings.selectionColor = new Color(0.96f, 0.72f, 0.34f, 0.35f);

            Placeholder = Style(15f, Faint);
            Placeholder.padding = new RectOffset(Px(12f), 0, 0, 0);

            // Scrollbars: a slim thumb and no arrows.
            Gui.verticalScrollbar = new GUIStyle
            {
                fixedWidth = Px(8f),
                border = new RectOffset(4, 4, 4, 4),
                margin = new RectOffset(Px(4f), 0, 0, 0),
            };
            Gui.verticalScrollbar.normal.background = Tint(Pill, new Color(1f, 1f, 1f, 0.03f), null);
            Gui.verticalScrollbarThumb = new GUIStyle
            {
                fixedWidth = Px(8f),
                border = new RectOffset(4, 4, 4, 4),
            };
            Gui.verticalScrollbarThumb.normal.background = Tint(Pill, new Color(1f, 1f, 1f, 0.16f), null);
            Gui.verticalScrollbarThumb.hover.background = Tint(Pill, new Color(1f, 1f, 1f, 0.26f), null);
            Gui.verticalScrollbarThumb.active.background = Tint(Pill, new Color(0.96f, 0.72f, 0.34f, 0.7f), null);
            Gui.verticalScrollbarUpButton = new GUIStyle { fixedHeight = 0f, fixedWidth = 0f };
            Gui.verticalScrollbarDownButton = new GUIStyle { fixedHeight = 0f, fixedWidth = 0f };

            // Sliders: a thin track with a round thumb, laid out on a rect of the thumb's height.
            Gui.horizontalSlider = new GUIStyle
            {
                border = new RectOffset(4, 4, 0, 0),
                fixedHeight = 0f,
            };
            Gui.horizontalSlider.normal.background = Track();
            Gui.horizontalSliderThumb = new GUIStyle
            {
                fixedWidth = Px(14f),
                fixedHeight = Px(14f),
            };
            Gui.horizontalSliderThumb.normal.background = Tint(Circle, Accent, null);
            Gui.horizontalSliderThumb.hover.background = Tint(Circle, Shade(Accent, 1.1f), null);
            Gui.horizontalSliderThumb.active.background = Tint(Circle, Color.white, null);

            _warmed = false;
            _warmNext = 0;
        }

        /// <summary>
        /// A flat fill, for lines and small marks. Rounded boxes keep their corners at full size,
        /// so anything thinner than the corners has to be drawn this way.
        /// </summary>
        public static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var was = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = was;
        }

        /// <summary>A rounded box filled with a colour.</summary>
        public static void Box(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawSliced(rect, Tint(Rounded, color, null), 10);
        }

        /// <summary>A rounded box with an outline.</summary>
        public static void Box(Rect rect, Color color, Color outline)
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawSliced(rect, Tint(Rounded, color, outline), 10);
        }

        /// <summary>A pill (fully rounded ends) filled with a colour.</summary>
        public static void PillBox(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawSliced(rect, Tint(Pill, color, null), 15);
        }

        public static void Icon(Rect rect, Texture2D texture, Color color)
        {
            if (Event.current.type != EventType.Repaint || texture == null) return;
            var was = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = was;
        }

        private static readonly GUIStyle Sliced = new GUIStyle();

        private static void DrawSliced(Rect rect, Texture2D texture, int border)
        {
            Sliced.normal.background = texture;
            Sliced.border = border == 15 ? PillBorder : border == 10 ? BoxBorder : new RectOffset(border, border, border, border);
            Sliced.Draw(rect, false, false, false, false);
        }

        private static readonly RectOffset BoxBorder = new RectOffset(10, 10, 10, 10);
        private static readonly RectOffset PillBorder = new RectOffset(15, 15, 15, 15);

        private static GUIStyle Boxed(GUIStyle text, Color normal, Color hover, Color active, Texture2D shape, float scale)
        {
            var style = new GUIStyle(text)
            {
                border = shape == Pill ? new RectOffset(15, 15, 15, 15) : new RectOffset(10, 10, 10, 10),
                padding = new RectOffset(Mathf.RoundToInt(12 * scale), Mathf.RoundToInt(12 * scale), Mathf.RoundToInt(4 * scale), Mathf.RoundToInt(4 * scale)),
            };
            style.normal.background = Tint(shape, normal, null);
            style.hover.background = Tint(shape, hover, null);
            style.active.background = Tint(shape, active, null);
            style.hover.textColor = text.normal.textColor;
            style.active.textColor = text.normal.textColor;
            return style;
        }

        private static Color Shade(Color color, float factor)
        {
            return new Color(Mathf.Clamp01(color.r * factor), Mathf.Clamp01(color.g * factor), Mathf.Clamp01(color.b * factor), color.a);
        }

        /// <summary>
        /// The game's own fonts when they are loaded: its serif for text and its runic face for
        /// the title. Unity's default font stands in for either one that is not.
        /// </summary>
        private static void FindFonts()
        {
            if (_body != null && _heading != null) return;

            foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (font == null) continue;
                var name = font.name;
                if (_body == null && name.Equals("AveriaSerifLibre-Regular", StringComparison.OrdinalIgnoreCase)) _body = font;
                if (_heading == null && (name.Equals("Norsebold", StringComparison.OrdinalIgnoreCase) || name.Equals("Norse", StringComparison.OrdinalIgnoreCase))) _heading = font;
            }

            if (_body != null || _heading != null)
            {
                Plugin.Note($"Scry panel fonts: text {(_body != null ? _body.name : "default")}, title {(_heading != null ? _heading.name : "default")}.");
            }
        }

        // ----- Textures -----

        private static void MakeTextures()
        {
            if (Rounded != null) return;

            Rounded = Shape(32, 32, (x, y) => RoundedCoverage(x, y, 32, 32, 8f));
            Pill = Shape(32, 32, (x, y) => RoundedCoverage(x, y, 32, 32, 16f));
            Circle = Shape(32, 32, (x, y) => RoundedCoverage(x, y, 32, 32, 16f));
            Star = Shape(32, 32, (x, y) => StarCoverage(x, y, 32, filled: true));
            StarHollow = Shape(32, 32, (x, y) => StarCoverage(x, y, 32, filled: false));
            Clock = Shape(32, 32, ClockCoverage);
        }

        /// <summary>A clock face: a ring, a hand pointing up and a shorter one pointing right.</summary>
        private static bool ClockCoverage(float x, float y)
        {
            const float c = 16f;
            var dx = x - c;
            var dy = y - c;
            var d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d >= 11.5f && d <= 14.5f) return true;
            if (Mathf.Abs(dx) <= 1.4f && dy >= -1.4f && dy <= 8.5f) return true;
            return Mathf.Abs(dy) <= 1.4f && dx >= -1.4f && dx <= 6.5f;
        }

        /// <summary>A white shape whose alpha is the coverage function, supersampled for smooth edges.</summary>
        private static Texture2D Shape(int width, int height, Func<float, float, bool> inside)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[width * height];
            const int samples = 4;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < samples; sy++)
                    for (var sx = 0; sx < samples; sx++)
                    {
                        if (inside(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples)) hits++;
                    }
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(hits * 255 / (samples * samples)));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static bool RoundedCoverage(float x, float y, float w, float h, float r)
        {
            var cx = Mathf.Clamp(x, r, w - r);
            var cy = Mathf.Clamp(y, r, h - r);
            var dx = x - cx;
            var dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool StarCoverage(float x, float y, int size, bool filled)
        {
            var c = size / 2f;
            var inOuter = InStar(x - c, y - c + 1f, size * 0.47f, size * 0.20f);
            if (filled) return inOuter;
            return inOuter && !InStar(x - c, y - c + 1f, size * 0.33f, size * 0.12f);
        }

        /// <summary>Whether a point lies inside a five-pointed star, point up.</summary>
        private static bool InStar(float x, float y, float outer, float inner)
        {
            var points = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                var radius = i % 2 == 0 ? outer : inner;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            var inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                if ((points[i].y > y) != (points[j].y > y)
                    && x < (points[j].x - points[i].x) * (y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>A copy of a white shape in a colour, with an optional outline, made once per colour.</summary>
        public static Texture2D Tint(Texture2D shape, Color fill, Color? outline)
        {
            var key = new TintKey(shape, fill, outline);
            if (Tinted.TryGetValue(key, out var known) && known != null) return known;

            var source = shape.GetPixels32();
            var pixels = new Color32[source.Length];
            var w = shape.width;
            var h = shape.height;

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var a = source[y * w + x].a / 255f;
                    var color = fill;

                    if (outline.HasValue && a > 0f)
                    {
                        // An outline where the shape's coverage drops off within a pixel or so of its edge.
                        var edge = EdgeDistance(source, w, h, x, y);
                        if (edge <= 1) color = Color.Lerp(outline.Value, fill, edge == 1 ? 0.35f : 0f);
                    }

                    pixels[y * w + x] = new Color(color.r, color.g, color.b, color.a * a);
                }
            }

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Tinted[key] = texture;
            return texture;
        }

        private static int EdgeDistance(Color32[] pixels, int w, int h, int x, int y)
        {
            for (var d = 0; d <= 1; d++)
            {
                for (var oy = -d - 1; oy <= d + 1; oy++)
                for (var ox = -d - 1; ox <= d + 1; ox++)
                {
                    var nx = x + ox;
                    var ny = y + oy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || pixels[ny * w + nx].a < 128) return d;
                }
            }
            return 2;
        }

        /// <summary>A slider track: a thin rounded line across the middle of the control.</summary>
        private static Texture2D Track()
        {
            const int w = 16;
            const int h = 14;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var onLine = y >= 5 && y <= 8;
                    pixels[y * w + x] = onLine ? new Color32(255, 255, 255, 46) : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
