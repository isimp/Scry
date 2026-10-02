using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The header's small sliders, the selection's volume and its size, each opened from an icon
    /// beside its name (<see cref="HeaderSliders"/>): a short track in a box under the icon, the
    /// value beside it, a click on which puts it back to the selection's own. The box takes the
    /// mouse before anything under it and is drawn over everything; Escape, a click elsewhere or
    /// another selection closes it, and the wheel over it nudges the value. An icon is lit while
    /// its value differs from the selection's own.
    /// </summary>
    internal static partial class ScryPanel
    {
        private enum Slide { None, Volume, Size }

        private static Slide _slide;
        private static Entry _slideFor;
        private static bool _sliding;

        /// <summary>Where the open slider's icon, box, track and value are, on the screen.</summary>
        private static Rect _slideIcon, _slideBox, _slideTrack, _slideValue;

        /// <summary>Whether a slider is open, for the self-test.</summary>
        public static bool SliderOpen => _slide != Slide.None;

        /// <summary>The icons beside the name, right to left from <paramref name="right"/>: size while the stage shows a model, then volume. Returns the width they take.</summary>
        private static float SlideIcons(Entry entry, Modifiers modifiers, float right, float y, bool sized)
        {
            var size = U(22f);
            var x = right;
            if (sized)
            {
                x -= size;
                SlideIcon(Slide.Size, entry, new Rect(x, y, size, size), Skin.Resize, !Mathf.Approximately(modifiers.Scale, 1f),
                    $"Size {HeaderSliders.SizeLabel(modifiers.Scale)}: click to change it");
                x -= U(4f);
            }
            x -= size;
            SlideIcon(Slide.Volume, entry, new Rect(x, y, size, size), Skin.Speaker, !Mathf.Approximately(modifiers.Volume, 1f),
                $"Volume {HeaderSliders.VolumeLabel(modifiers.Volume)}: click to change it");
            return right - x;
        }

        private static void SlideIcon(Slide kind, Entry entry, Rect rect, Texture2D icon, bool changed, string tip)
        {
            var open = _slide == kind && _slideFor == entry;
            var hover = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint) Skin.Icon(rect, icon, open || changed ? Skin.Accent : hover ? Skin.Text : Skin.Dim);
            if (hover && !open) AskTip("slide:" + kind, tip);

            // Where it is on the screen each frame, so the box follows the panel.
            var screen = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size);
            if (open) SlidePlace(screen);
            if (!GUI.Button(rect, GUIContent.none, GUIStyle.none)) return;
            if (open)
            {
                _slide = Slide.None;
                return;
            }
            _slide = kind;
            _slideFor = entry;
            SlidePlace(screen);
        }

        /// <summary>The box under the icon, its right edge on the icon's.</summary>
        private static void SlidePlace(Rect icon)
        {
            _slideIcon = icon;
            var w = U(200f);
            var h = U(32f);
            _slideBox = new Rect(icon.xMax - w, icon.yMax + U(4f), w, h);
            _slideTrack = new Rect(_slideBox.x + U(14f), _slideBox.y + (h - U(16f)) / 2f, w - U(86f), U(16f));
            _slideValue = new Rect(_slideTrack.xMax + U(10f), _slideBox.y, _slideBox.xMax - _slideTrack.xMax - U(16f), h);
        }

        private static float SlideShare(Modifiers modifiers) =>
            _slide == Slide.Volume ? HeaderSliders.VolumeShare(modifiers.Volume) : HeaderSliders.SizeShare(modifiers.Scale);

        private static void SlideTo(Modifiers modifiers, float share)
        {
            if (_slide == Slide.Volume)
            {
                modifiers.Volume = HeaderSliders.VolumeAt(share);
                Loudness.Gain = modifiers.Volume;
            }
            else
            {
                modifiers.Scale = HeaderSliders.SizeAt(share);
            }
        }

        /// <summary>
        /// Before anything draws: the open box takes a press, drag, release or wheel over it; a
        /// press elsewhere, but on its icon, closes it, as do Escape and another selection. True
        /// when it took the event.
        /// </summary>
        private static bool SlideInput(Explorer explorer)
        {
            if (_slide == Slide.None) return false;
            if (explorer.Selected != _slideFor)
            {
                _slide = Slide.None;
                _sliding = false;
                return false;
            }
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _slide = Slide.None;
                e.Use();
                return true;
            }
            var mouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
            var over = _slideBox.Contains(mouse);
            var modifiers = explorer.Modifiers;
            float Along() => (mouse.x - _slideTrack.x) / Mathf.Max(1f, _slideTrack.width);
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!over)
                    {
                        if (!_slideIcon.Contains(mouse)) _slide = Slide.None;
                        return false;
                    }
                    if (_slideValue.Contains(mouse)) SlideTo(modifiers, _slide == Slide.Volume ? HeaderSliders.VolumeShare(1f) : 0.5f);
                    else
                    {
                        _sliding = true;
                        SlideTo(modifiers, Along());
                    }
                    e.Use();
                    return true;
                case EventType.MouseDrag:
                    if (!_sliding) return false;
                    SlideTo(modifiers, Along());
                    e.Use();
                    return true;
                case EventType.MouseUp:
                    if (!_sliding && !over) return false;
                    _sliding = false;
                    e.Use();
                    return true;
                case EventType.ScrollWheel:
                    if (!over) return false;
                    // A step each notch: 5% of volume, or a fiftieth of the size's track.
                    var step = _slide == Slide.Volume ? 0.05f / Modifiers.MaxVolume : 0.02f;
                    SlideTo(modifiers, SlideShare(modifiers) - Mathf.Sign(e.delta.y) * step);
                    e.Use();
                    return true;
            }
            return false;
        }

        /// <summary>Draws the open box over everything else.</summary>
        private static void SlideDraw(Explorer explorer)
        {
            if (_slide == Slide.None || Event.current.type != EventType.Repaint) return;
            Rect Here(Rect screen) => new Rect(GUIUtility.ScreenToGUIPoint(screen.position), screen.size);
            var modifiers = explorer.Modifiers;
            var box = Here(_slideBox);
            Skin.Box(box, Skin.Panel, Skin.Outline);

            var track = Here(_slideTrack);
            var line = new Rect(track.x, track.center.y - U(1.5f), track.width, U(3f));
            var share = SlideShare(modifiers);
            Skin.Fill(line, new Color(Skin.Text.r, Skin.Text.g, Skin.Text.b, 0.25f));
            Skin.Fill(new Rect(line.x, line.y, line.width * share, line.height), Skin.Accent);
            // The selection's own size, a mark in the middle of the track.
            if (_slide == Slide.Size) Skin.Fill(new Rect(track.center.x - U(0.5f), track.center.y - U(5f), U(1f), U(10f)), new Color(Skin.Text.r, Skin.Text.g, Skin.Text.b, 0.4f));
            var knob = U(12f);
            Skin.Icon(new Rect(track.x + track.width * share - knob / 2f, track.center.y - knob / 2f, knob, knob), Skin.Circle, Skin.Text);

            var value = Here(_slideValue);
            GUI.Label(value, _slide == Slide.Volume ? HeaderSliders.VolumeLabel(modifiers.Volume) : HeaderSliders.SizeLabel(modifiers.Scale), Skin.DimLabel);
            if (value.Contains(Event.current.mousePosition)) AskTip("slidevalue", "Back to its own");
        }
    }
}
