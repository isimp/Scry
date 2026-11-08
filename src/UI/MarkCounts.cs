using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// How many dots a kind's merged mark stands for, on a small dark tab at its upper right, in
    /// the game's pin-name writing, as a square's count is written; in a holder over the marks,
    /// made from a pool, a few a frame, the rest in the frames after.
    /// </summary>
    internal sealed class MarkCounts
    {
        /// <summary>How many tabs may be made in a frame, so a map opening on many merged marks holds no frame.</summary>
        private const int MostMadeAFrame = 30;

        /// <summary>A tab's height and the room either side of its count, in the screen's points, and its writing's size.</summary>
        private const float TabHeight = 14f, TabPad = 4f, TabDigit = 7f, TabWriting = 12f;

        private sealed class Tab
        {
            public RectTransform Rect;
            public RawImage Back;
            public CanvasGroup Group;
            public GameObject Words;
            public int Said = -1;
            public bool? Told;
        }

        /// <summary>A told count's tab: a light edge round a faint middle, hollow as the server's told dots are; made once.</summary>
        private static Texture2D _hollow;

        private static Texture2D Hollow()
        {
            if (_hollow != null) return _hollow;
            const int side = 16;
            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var edge = x < 2 || y < 2 || x >= side - 2 || y >= side - 2;
                    pixels[y * side + x] = edge ? Skin.Text : Skin.Alpha(Skin.TipFill, 0.45f);
                }
            }
            _hollow = Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry told count", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            _hollow.SetPixels32(pixels);
            _hollow.Apply(false, false);
            return _hollow;
        }

        private readonly string _name;
        private RectTransform _holder;
        private readonly List<Tab> _tabs = new List<Tab>();
        private int _used, _live, _made;

        public MarkCounts(string name) => _name = name;

        /// <summary>How many tabs stood at the last placing, for the self-test.</summary>
        public int Shown => _live;

        /// <summary>How many tabs standing are faded with their marks, for the self-test.</summary>
        public int Faded
        {
            get
            {
                var faded = 0;
                for (var i = 0; i < _live && i < _tabs.Count; i++) if (_tabs[i].Group != null && _tabs[i].Group.alpha < 1f) faded++;
                return faded;
            }
        }

        /// <summary>What the tabs' holder stands in, none before it is made.</summary>
        public Transform Parent => _holder != null ? _holder.parent : null;

        /// <summary>Whether the tabs stand over all else in what holds them, for the self-test.</summary>
        public bool StandsLast => _holder != null && _holder.GetSiblingIndex() == _holder.parent.childCount - 1;

        /// <summary>A placing begun: the holder made in what holds the marks, and every tab free again; a holder gone with what held it taking its tabs along.</summary>
        public void Begin(RectTransform marks)
        {
            if (_holder == null || _holder.parent != marks) Forget();
            if (_holder == null && marks != null) _holder = StackPips.Holder(marks, _name);
            _used = 0;
            _made = 0;
        }

        /// <summary>A merged mark's count stood at its upper right, on a hollow tab where the server told of them, as bright as its mark (<paramref name="shown"/>); false where its tab could not be made this frame.</summary>
        public bool Put(Minimap map, Vector2 at, float size, int count, bool told, float shown, RectTransform like)
        {
            if (_used == _tabs.Count && !Make(map, like)) return false;
            var tab = _tabs[_used++];
            if (tab.Told != told)
            {
                tab.Back.texture = told ? Hollow() : Texture2D.whiteTexture;
                tab.Back.color = told ? Skin.MarkShown : Skin.Alpha(Skin.TipFill, 0.9f);
                tab.Told = told;
            }
            if (tab.Said != count)
            {
                if (tab.Words != null) PinNames.SetText(tab.Words, Numbers.Count(count));
                tab.Rect.sizeDelta = new Vector2(TabPad * 2f + TabDigit * Numbers.Count(count).Length, TabHeight);
                tab.Said = count;
            }
            tab.Rect.anchoredPosition = at + new Vector2(size * 0.35f + tab.Rect.sizeDelta.x / 2f, size * 0.45f);
            // Faded with its mark where other kinds are picked to stand out.
            if (tab.Group.alpha != shown) tab.Group.alpha = shown;
            if (!tab.Rect.gameObject.activeSelf) tab.Rect.gameObject.SetActive(true);
            return true;
        }

        /// <summary>A placing ended: the tabs no merged mark holds now put away, and the holder last again, over the marks made while placing.</summary>
        public void End()
        {
            if (_holder != null && _holder.GetSiblingIndex() != _holder.parent.childCount - 1) _holder.SetAsLastSibling();
            for (var i = _used; i < _live && i < _tabs.Count; i++) _tabs[i].Rect.gameObject.SetActive(false);
            _live = _used;
        }

        public void Forget()
        {
            if (_holder != null) Object.Destroy(_holder.gameObject);
            _holder = null;
            _tabs.Clear();
            _used = _live = 0;
        }

        /// <summary>A tab laid out as a mark is, centred on its point: a dark plate holding the count in the pin names' writing.</summary>
        private bool Make(Minimap map, RectTransform like)
        {
            if (_holder == null || like == null || _made >= MostMadeAFrame) return false;
            _made++;
            var made = new GameObject("Scry merged count", typeof(RectTransform));
            var rect = (RectTransform)made.transform;
            rect.SetParent(_holder, false);
            rect.anchorMin = like.anchorMin;
            rect.anchorMax = like.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            made.SetActive(false);
            var group = made.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var back = made.AddComponent<RawImage>();
            back.texture = Texture2D.whiteTexture;
            back.color = Skin.Alpha(Skin.TipFill, 0.9f);
            back.raycastTarget = false;
            var words = PinNames.Make(map, rect, "Scry merged count words");
            if (words != null)
            {
                PinNames.Centred(words, TabWriting);
                words.SetActive(true);
            }
            _tabs.Add(new Tab { Rect = rect, Back = back, Group = group, Words = words });
            return true;
        }
    }
}
