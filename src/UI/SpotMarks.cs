using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// What a spot's mode lays on its mark (<see cref="SpotModes"/>): the game's own cross over a
    /// crossed one, in a holder over the marks, and over a highlighted one a bigger copy on its own
    /// shape in the accent, so the outline follows it, in a holder over the game's pins; made from a
    /// pool as the marks are.
    /// </summary>
    internal sealed class SpotMarks
    {
        private RectTransform _crosses, _highlights;
        private readonly List<(Image Picture, RectTransform Rect)> _crossed = new List<(Image, RectTransform)>();
        private readonly List<(Image Picture, RectTransform Rect)> _high = new List<(Image, RectTransform)>();
        private int _crossedUsed, _highUsed, _crossedLive, _highLive;

        /// <summary>How many crosses and how many highlighted marks stood at the last placing, for the self-test.</summary>
        public int CrossesShown => _crossedLive;

        public int HighlightsShown => _highLive;

        private static Sprite _cross;

        /// <summary>
        /// The cross of a pin crossed off: the game's own, the picture its pins show when crossed
        /// (their child <c>Checked</c>), so ours match; drawn in the warning colour where the map has none.
        /// </summary>
        public static Sprite CrossPicture(Minimap map)
        {
            if (_cross != null) return _cross;
            var game = map != null && map.m_pinPrefab != null ? map.m_pinPrefab.transform.Find("Checked") : null;
            var image = game != null ? game.GetComponent<Image>() : null;
            if (image != null && image.sprite != null) return _cross = image.sprite;
            const int side = 32;
            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var off = Mathf.Min(Mathf.Abs(x - y), Mathf.Abs(x + y - side + 1));
                    var inside = x >= 4 && x < side - 4 && y >= 4 && y < side - 4;
                    pixels[y * side + x] = inside && off <= 2 ? Skin.Warn : Skin.Alpha(Skin.Warn, 0f);
                }
            }
            var texture = Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry cross", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return _cross = Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
        }

        /// <summary>A placing begun: the holders made over the marks and over the pins, and every piece free again; holders gone with what held them taking their pieces along.</summary>
        public void Begin(RectTransform marks, RectTransform overPins)
        {
            if (_crosses == null || _crosses.parent != marks || _highlights == null || _highlights.parent != overPins) Forget();
            if (_crosses == null && marks != null) _crosses = StackPips.Holder(marks, "Scry spots crossed");
            if (_highlights == null && overPins != null) _highlights = StackPips.Holder(overPins, "Scry spots highlighted");
            _crossedUsed = _highUsed = 0;
        }

        /// <summary>What a spot's mode lays on its mark at a point, none for a plain one; the colours as its kind stands, faded where another is picked.</summary>
        public void Put(Minimap map, Vector2 at, float size, SpotMode mode, Sprite picture, Color colour, RectTransform like)
        {
            if (like == null || mode == SpotMode.Plain) return;
            if (mode == SpotMode.Crossed)
            {
                if (_crosses == null) return;
                if (_crossedUsed == _crossed.Count) _crossed.Add(StackPips.Piece(_crosses, like, "Scry cross"));
                var cross = _crossed[_crossedUsed++];
                StackPips.Stand(cross, at, size * SpotModes.CrossScale, CrossPicture(map));
                cross.Picture.color = colour;
                return;
            }
            if (_highlights == null) return;
            while (_high.Count < (_highUsed + 1) * 2) _high.Add(StackPips.Piece(_highlights, like, "Scry highlighted mark"));
            var outline = _high[_highUsed * 2];
            var copy = _high[_highUsed * 2 + 1];
            _highUsed++;
            StackPips.Stand(outline, at, size * SpotModes.HighlightScale * SpotModes.OutlineScale, MapSpots.Silhouette(picture));
            StackPips.Stand(copy, at, size * SpotModes.HighlightScale, picture);
            outline.Picture.color = colour;
            copy.Picture.color = colour;
        }

        /// <summary>A placing ended: the pieces no spot holds now put away, and the holders last again, over what was made while placing.</summary>
        public void End()
        {
            if (_crosses != null && _crosses.GetSiblingIndex() != _crosses.parent.childCount - 1) _crosses.SetAsLastSibling();
            if (_highlights != null && _highlights.GetSiblingIndex() != _highlights.parent.childCount - 1) _highlights.SetAsLastSibling();
            for (var i = _crossedUsed; i < _crossed.Count; i++) if (_crossed[i].Picture.gameObject.activeSelf) _crossed[i].Picture.gameObject.SetActive(false);
            for (var i = _highUsed * 2; i < _high.Count; i++) if (_high[i].Picture.gameObject.activeSelf) _high[i].Picture.gameObject.SetActive(false);
            _crossedLive = _crossedUsed;
            _highLive = _highUsed;
        }

        public void Forget()
        {
            if (_crosses != null) Object.Destroy(_crosses.gameObject);
            if (_highlights != null) Object.Destroy(_highlights.gameObject);
            _crosses = _highlights = null;
            _crossed.Clear();
            _high.Clear();
            _crossedUsed = _highUsed = _crossedLive = _highLive = 0;
        }
    }
}
