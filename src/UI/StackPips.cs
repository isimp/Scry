using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// The pips under stacks of marks on a map (<see cref="MarkStacks"/>), the large map's or the
    /// minimap's: small light dots in a dark halo, in a holder over the marks' own, made from a
    /// pool as the marks are, a few a frame, the rest in the frames after.
    /// </summary>
    internal sealed class StackPips
    {
        /// <summary>How many pips may be made in a frame, so a map opening on many stacks holds no frame.</summary>
        private const int MostMadeAFrame = 60;

        private readonly string _name;
        private RectTransform _holder;
        private readonly List<(Image Picture, RectTransform Rect)> _pips = new List<(Image, RectTransform)>();
        private int _used, _live, _made;
        private static Sprite _picture;

        public StackPips(string name) => _name = name;

        /// <summary>How many pips stood at the last placing, for the self-test.</summary>
        public int Shown => _live;

        /// <summary>What the pips' holder stands in: the marks' holder, or what stands over the game's pins.</summary>
        public Transform Parent => _holder != null ? _holder.parent : null;

        /// <summary>A placing begun: the holder made in what holds the marks, over them, and every pip free again.</summary>
        public void Begin(RectTransform marks)
        {
            if (_holder != null && _holder.parent != marks) Forget();
            if (_holder == null && marks != null) _holder = Holder(marks, _name);
            _used = 0;
            _made = 0;
        }

        /// <summary>The pips of a stack of so many marks stood under its mark at a point; false where some could not be made this frame.</summary>
        public bool Put(Vector2 at, float size, int marks, RectTransform like)
        {
            var pips = MarkStacks.Pips(marks);
            var width = MarkStacks.PipSize(size);
            for (var i = 0; i < pips; i++)
            {
                if (_used == _pips.Count && !Make(like)) return false;
                var (x, y) = MarkStacks.PipAt(i, pips, size);
                Stand(_pips[_used++], at + new Vector2(x, y), width, Picture());
            }
            return true;
        }

        /// <summary>A placing ended: the pips no stack holds now put away, and the holder last again, over the marks made while placing.</summary>
        public void End()
        {
            if (_holder != null && _holder.GetSiblingIndex() != _holder.parent.childCount - 1) _holder.SetAsLastSibling();
            for (var i = _used; i < _live && i < _pips.Count; i++) _pips[i].Picture.gameObject.SetActive(false);
            _live = _used;
        }

        public void Forget()
        {
            if (_holder != null) Object.Destroy(_holder.gameObject);
            _holder = null;
            _pips.Clear();
            _used = _live = 0;
        }

        private bool Make(RectTransform like)
        {
            if (_holder == null || like == null || _made >= MostMadeAFrame) return false;
            _made++;
            _pips.Add(Piece(_holder, like, "Scry stack pip"));
            return true;
        }

        /// <summary>A holder as big as what it stands in, last in it so it stands over the marks.</summary>
        public static RectTransform Holder(RectTransform parent, string name)
        {
            var made = new GameObject(name, typeof(RectTransform));
            var holder = (RectTransform)made.transform;
            holder.SetParent(parent, false);
            holder.anchorMin = Vector2.zero;
            holder.anchorMax = Vector2.one;
            holder.offsetMin = Vector2.zero;
            holder.offsetMax = Vector2.zero;
            holder.SetAsLastSibling();
            return holder;
        }

        /// <summary>A picture laid out as a mark is, centred, taking no rays, so the map under it gets every click.</summary>
        public static (Image Picture, RectTransform Rect) Piece(RectTransform holder, RectTransform like, string name)
        {
            var made = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)made.transform;
            rect.SetParent(holder, false);
            rect.anchorMin = like.anchorMin;
            rect.anchorMax = like.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            var picture = made.AddComponent<Image>();
            picture.raycastTarget = false;
            made.SetActive(false);
            return (picture, rect);
        }

        /// <summary>A picture stood at a point, as wide as given, round; each set only as it changes.</summary>
        public static void Stand((Image Picture, RectTransform Rect) piece, Vector2 at, float width, Sprite picture)
        {
            if (piece.Rect.anchoredPosition != at) piece.Rect.anchoredPosition = at;
            var sized = new Vector2(width, width);
            if (piece.Rect.sizeDelta != sized) piece.Rect.sizeDelta = sized;
            if (piece.Rect.localRotation != Quaternion.identity) piece.Rect.localRotation = Quaternion.identity;
            if (piece.Picture.sprite != picture) piece.Picture.sprite = picture;
            if (!piece.Picture.gameObject.activeSelf) piece.Picture.gameObject.SetActive(true);
        }

        /// <summary>
        /// A pip's picture, drawn once: Scry's writing colour in the middle, as the marks' ring, a
        /// dark halo round it so it reads on light ground and dark; each pixel the mean of four by
        /// four points in it, so it stays round drawn a few pixels wide.
        /// </summary>
        public static Sprite Picture()
        {
            if (_picture != null) return _picture;
            const int side = 32, points = 4;
            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var sum = Skin.Alpha(Skin.OnAccent, 0f) * 0f;
                    var a = 0f;
                    for (var py = 0; py < points; py++)
                    {
                        for (var px = 0; px < points; px++)
                        {
                            var dx = (x + (px + 0.5f) / points - side / 2f) / (side / 2f);
                            var dy = (y + (py + 0.5f) / points - side / 2f) / (side / 2f);
                            var share = Mathf.Sqrt(dx * dx + dy * dy);
                            var shade = share <= 0.62f ? Skin.Text : share <= 1f ? Skin.Alpha(Skin.OnAccent, 0.7f) : Skin.Alpha(Skin.OnAccent, 0f);
                            sum += shade * shade.a;
                            a += shade.a;
                        }
                    }
                    pixels[y * side + x] = a > 0f ? Skin.Alpha(sum / a, a / (points * points)) : Skin.Alpha(Skin.OnAccent, 0f);
                }
            }
            var texture = Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry stack pip", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _picture = Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            return _picture;
        }
    }
}
