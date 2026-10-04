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
        private enum Slide { None, Volume, Size, Speed }

        private static Slide _slide;
        private static Entry _slideFor;
        private static bool _sliding;

        /// <summary>Where the open slider's icon, box, track and value are, on the screen.</summary>
        private static Rect _slideIcon, _slideBox, _slideTrack, _slideValue;

        /// <summary>Whether a slider is open, for the self-test.</summary>
        public static bool SliderOpen => _slide != Slide.None;

        /// <summary>Opens the volume or the size slider for an entry under a point of the screen, as its icon would, for the self-test.</summary>
        public static void OpenSlider(Entry entry, bool size, Vector2 under)
        {
            _slide = size ? Slide.Size : Slide.Volume;
            _slideFor = entry;
            SlidePlace(new Rect(under, Vector2.zero));
        }

        /// <summary>Closes the View box and any slider.</summary>
        public static void CloseBoxes()
        {
            _slide = Slide.None;
            _sliding = false;
            CloseViewMenu();
        }

        /// <summary>
        /// The icons beside the name, right to left from <paramref name="right"/>: size while the
        /// stage shows a model, volume, animation speed where it has animations, and the one
        /// Repeat for whatever plays. Returns the width they take.
        /// </summary>
        private static float SlideIcons(Entry entry, Modifiers modifiers, float right, float y, bool sized)
        {
            var size = U(22f);
            var x = right;
            if (sized)
            {
                x -= size;
                SlideIcon(Slide.Size, entry, new Rect(x, y, size, size), Skin.Resize, !Mathf.Approximately(modifiers.Scale, 1f),
                    HeaderSliders.SizeTip(modifiers.Scale));
                x -= U(4f);
            }
            x -= size;
            SlideIcon(Slide.Volume, entry, new Rect(x, y, size, size), Skin.Speaker, !Mathf.Approximately(modifiers.Volume, 1f),
                HeaderSliders.VolumeTip(modifiers.Volume));
            var animated = Previews.Clips().Count > 0;
            if (animated)
            {
                x -= U(4f) + size;
                SlideIcon(Slide.Speed, entry, new Rect(x, y, size, size), Skin.Speed, !Mathf.Approximately(modifiers.AnimationSpeed, 1f),
                    HeaderSliders.SpeedTip(modifiers.AnimationSpeed));
            }
            if (animated || entry.Kind == Kind.Sound || entry.Kind == Kind.Effect)
            {
                x -= U(4f) + size;
                RepeatIcon(new Rect(x, y, size, size));
            }
            return right - x;
        }

        /// <summary>The one Repeat for whatever plays: lit while on, a click switching it and remembering it.</summary>
        private static void RepeatIcon(Rect rect)
        {
            var hover = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint) Skin.Icon(rect, Skin.Repeat, Previews.Repeat ? Skin.Accent : hover ? Skin.Text : Skin.Dim);
            if (hover) AskTip("repeat", ActionWords.Repeat(Previews.Repeat));
            if (!GUI.Button(rect, GUIContent.none, GUIStyle.none)) return;
            Previews.Repeat = !Previews.Repeat;
            SaveRects();
        }

        private static void SlideIcon(Slide kind, Entry entry, Rect rect, Texture2D icon, bool changed, string tip)
        {
            var open = _slide == kind && _slideFor == entry;
            var hover = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint) Skin.Icon(rect, icon, open || changed ? Skin.Accent : hover ? Skin.Text : Skin.Dim);
            if (hover && !open) AskTip("slide:" + kind, tip);

            // Where it is on the screen each frame, so the box follows the panel.
            var screen = OnScreen(rect);
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
            _slide == Slide.Volume ? HeaderSliders.VolumeShare(modifiers.Volume)
            : _slide == Slide.Speed ? HeaderSliders.SpeedShare(modifiers.AnimationSpeed)
            : HeaderSliders.SizeShare(modifiers.Scale);

        /// <summary>Where the selection's own value is along the open slider's track: where a click on the value puts it back.</summary>
        private static float OwnShare() =>
            _slide == Slide.Volume ? HeaderSliders.VolumeShare(1f) : _slide == Slide.Speed ? HeaderSliders.SpeedShare(1f) : 0.5f;

        private static void SlideTo(Modifiers modifiers, float share)
        {
            if (_slide == Slide.Volume)
            {
                modifiers.Volume = HeaderSliders.VolumeAt(share);
                Loudness.Gain = modifiers.Volume;
            }
            else if (_slide == Slide.Speed)
            {
                modifiers.AnimationSpeed = HeaderSliders.SpeedAt(share);
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
                    if (_slideValue.Contains(mouse)) SlideTo(modifiers, OwnShare());
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
            var modifiers = explorer.Modifiers;
            var box = Here(_slideBox);
            Skin.Box(box, Skin.Panel, Skin.Outline);

            var track = Here(_slideTrack);
            var line = new Rect(track.x, track.center.y - U(1.5f), track.width, U(3f));
            var share = SlideShare(modifiers);
            Skin.Fill(line, Skin.Alpha(Skin.Text, 0.25f));
            Skin.Fill(new Rect(line.x, line.y, line.width * share, line.height), Skin.Accent);
            // The selection's own size or speed, a mark where it is on the track.
            if (_slide != Slide.Volume) Skin.Fill(new Rect(track.x + track.width * OwnShare() - U(0.5f), track.center.y - U(5f), U(1f), U(10f)), Skin.Alpha(Skin.Text, 0.4f));
            var knob = U(12f);
            Skin.Icon(new Rect(track.x + track.width * share - knob / 2f, track.center.y - knob / 2f, knob, knob), Skin.Circle, Skin.Text);

            var value = Here(_slideValue);
            GUI.Label(value, _slide == Slide.Volume ? HeaderSliders.VolumeLabel(modifiers.Volume)
                : _slide == Slide.Speed ? HeaderSliders.SpeedLabel(modifiers.AnimationSpeed)
                : HeaderSliders.SizeLabel(modifiers.Scale), Skin.DimLabel);
            if (value.Contains(Event.current.mousePosition)) AskTip("slidevalue", "Back to its own");
            CountDrawn(PanelPart.Slider);
        }

        /// <summary>Lets go of the entry a slider was last opened for.</summary>
        private static void ForgetSlide()
        {
            _slideFor = null;
        }
    }
}
