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
        public static GUIStyle Field, Placeholder, Tab, TabOn, Tip, IconButton;
        public static Texture2D Rounded, Pill, Circle, Star, StarHollow;

        private static float _builtScale = -1f;
        private static bool _warmed;
        private static Font _body;
        private static Font _heading;
        private static readonly Dictionary<string, Texture2D> Tinted = new Dictionary<string, Texture2D>();

        public static Color KindColor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Creature: return new Color(0.92f, 0.47f, 0.42f);
                case Kind.Item: return new Color(0.96f, 0.76f, 0.36f);
                case Kind.Piece: return new Color(0.78f, 0.62f, 0.45f);
                case Kind.Projectile: return new Color(0.98f, 0.56f, 0.28f);
                case Kind.Effect: return new Color(0.70f, 0.56f, 0.97f);
                case Kind.Sound: return new Color(0.42f, 0.80f, 0.82f);
                case Kind.StatusEffect: return new Color(0.54f, 0.84f, 0.52f);
                default: return new Color(0.62f, 0.64f, 0.70f);
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
            _warmed = true;

            try
            {
                Ensure(scale);

                var every = new System.Text.StringBuilder();
                for (var c = ' '; c <= '~'; c++) every.Append(c);
                var sample = new GUIContent(every.ToString());

                foreach (var style in new[] { Title, Subtitle, Label, Small, Heading, Big, RowName, RowSub, Glyph, Button, Chip, Field, Tab, TabOn, Tip })
                {
                    style.CalcSize(sample);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry panel warm-up: {ex.Message}");
            }
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

            FindFonts();
            MakeTextures();

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
            ChipOn = Boxed(Style(12f, OnAccent, FontStyle.Bold, TextAnchor.MiddleCenter), Accent, Shade(Accent, 1.08f), Shade(Accent, 0.85f), Pill, scale);
            Segment = Boxed(Style(12f, Dim, FontStyle.Normal, TextAnchor.MiddleCenter), Raised, RaisedHover, Shade(Raised, 1.35f), Rounded, scale);
            SegmentOn = Boxed(Style(12f, OnAccent, FontStyle.Bold, TextAnchor.MiddleCenter), Accent, Shade(Accent, 1.08f), Shade(Accent, 0.85f), Rounded, scale);
            Close = Boxed(Style(20f, Dim, FontStyle.Normal, TextAnchor.MiddleCenter), new Color(0, 0, 0, 0), RaisedHover, Shade(RaisedHover, 1.3f), Rounded, scale);
            Close.hover.textColor = Skin.Text;
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
            Sliced.border = new RectOffset(border, border, border, border);
            Sliced.Draw(rect, false, false, false, false);
        }

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
                Plugin.Log.LogInfo($"Scry panel fonts: text {(_body != null ? _body.name : "default")}, title {(_heading != null ? _heading.name : "default")}.");
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
            var key = shape.GetInstanceID() + ":" + fill + ":" + outline;
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
