using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// Dots that touch on the large map, stacks (<see cref="MarkStacks"/>): each stands where it
    /// lies, a pip under the stack's first dot for each dot there; the pointer resting on a stack
    /// fans its dots out round their spot, a thin line from each to it, and names every kind there
    /// until it leaves the fan; a dot pointed at in the fan is named alone, ringed, as any dot is.
    /// </summary>
    internal static partial class MapSpots
    {
        private static readonly MarkStacks Stacks = new MarkStacks();
        private static readonly StackPips Pips = new StackPips("Scry stack pips");

        /// <summary>What the stacks were gathered for: the dots, and the touch's zoom step.</summary>
        private static ((IReadOnlyList<(int X, int Z)>, IReadOnlyList<(int X, int Z)>, Sprite, int) Dots, int Step) _stackedFor;

        /// <summary>The stack fanned out, by its first dot; -1 for none.</summary>
        private static int _fanned = -1;

        /// <summary>What holds the fan, over the dots and pips; its dots, its lines, the mark on the spot, and which dot each fanned dot stands for.</summary>
        private static RectTransform _fanHolder;
        private static readonly List<(Image Picture, RectTransform Rect)> FanDots = new List<(Image, RectTransform)>();
        private static readonly List<(Image Picture, RectTransform Rect)> FanLines = new List<(Image, RectTransform)>();
        private static (Image Picture, RectTransform Rect) _fanSpot;
        private static readonly List<int> FanFrom = new List<int>();
        private static float _fanSize;
        private static Sprite _line;

        /// <summary>How near two dots stand to touch at the large map's zoom, in metres, for the self-test.</summary>
        [ForTest]
        public static float TouchForTest => _touch;

        private static float _touch;

        /// <summary>How many pips stand under stacks on the large map, for the self-test.</summary>
        [ForTest]
        public static int PipsShown => Pips.Shown;

        /// <summary>How many dots stand fanned out, for the self-test.</summary>
        [ForTest]
        public static int FannedShown => FanStanding;

        private static int FanStanding => _fanHolder != null && _fanHolder.gameObject.activeSelf ? FanFrom.Count : 0;

        /// <summary>Where the first dot standing in a stack stands on the screen; none, for the self-test.</summary>
        [ForTest]
        public static Vector2? StackedDotForTest()
        {
            for (var i = 0; i < _standing && i < PlacedFrom.Count; i++)
            {
                if (Stacks.CountAt(Stacks.AnchorOf(PlacedFrom[i])) > 1) return new Vector2(Dots[i].Rect.position.x, Dots[i].Rect.position.y);
            }
            return null;
        }

        /// <summary>Where the stack's spot stands on the screen while fanned, or a fanned dot's; none, for the self-test.</summary>
        [ForTest]
        public static Vector2? FanCenterForTest(int dot = -1)
        {
            if (FanStanding == 0) return null;
            var rect = dot < 0 ? _fanSpot.Rect : dot < FanDots.Count ? FanDots[dot].Rect : null;
            return rect == null ? (Vector2?)null : new Vector2(rect.position.x, rect.position.y);
        }

        /// <summary>
        /// The stacks gathered again as the dots or the zoom change: by where the dots lie, with
        /// the touch in metres, a dot's width on the map at its zoom; the zoom in steps of 2 %, so
        /// a wheel turn gathers a few times, not every frame. A fan closes as they are gathered.
        /// </summary>
        private static void GatherStacks(Minimap map, Rect window, Rect area, float size)
        {
            var touch = size * MarkStacks.Touching * window.width * map.m_textureSize * map.m_pixelSize / Mathf.Max(1f, area.width);
            var key = (_dotsFor, Mathf.RoundToInt(Mathf.Log(Mathf.Max(0.001f, touch)) * 50f));
            _touch = touch;
            if (key == _stackedFor) return;
            _stackedFor = key;
            Stacks.Gather(StandingDots.Count, i => (StandingDots[i].X, StandingDots[i].Z), touch);
            Unfan();
        }

        /// <summary>Whether a dot stands in the stack fanned out, so it stands in the fan instead of where it lies.</summary>
        private static bool InFan(int dot) => _fanned >= 0 && Stacks.AnchorOf(dot) == _fanned;

        /// <summary>A stack's pips stood under its first dot, standing where it lies; false where some could not be made this frame.</summary>
        private static bool PutPips(int dot, Vector2 at, float size, RectTransform like)
        {
            var count = Stacks.AnchorOf(dot) == dot ? Stacks.CountAt(dot) : 0;
            return count < 2 || Pips.Put(at, size, count, like);
        }

        /// <summary>A stack fanned out by its first dot, stood in the fan as the dots are placed next.</summary>
        private static void Fan(int anchor) => _fanned = anchor;

        /// <summary>The fan closed, its dots standing where they lie again as they are placed next.</summary>
        private static void Unfan()
        {
            if (_fanned < 0) return;
            _fanned = -1;
            HideFan();
        }

        private static void HideFan()
        {
            FanFrom.Clear();
            if (_fanHolder != null && _fanHolder.gameObject.activeSelf) _fanHolder.gameObject.SetActive(false);
        }

        /// <summary>
        /// The stack fanned stood round its spot at a point: a light mark on the spot, each dot,
        /// the first <see cref="MarkStacks.MostFanned"/>, round it at its place in the fan, a line
        /// from the spot to it, the lines under the dots.
        /// </summary>
        private static void PlaceFan(Vector2 at, float size, RectTransform like)
        {
            if (_fanHolder == null || _fanHolder.parent != _holder)
            {
                FanDots.Clear();
                FanLines.Clear();
                _fanHolder = StackPips.Holder(_holder, "Scry fanned stack");
                _fanSpot = StackPips.Piece(_fanHolder, like, "Scry fanned spot");
            }
            if (_fanHolder.GetSiblingIndex() != _holder.childCount - 1) _fanHolder.SetAsLastSibling();
            if (!_fanHolder.gameObject.activeSelf) _fanHolder.gameObject.SetActive(true);
            var members = Stacks.MembersOf(_fanned);
            var n = Mathf.Min(members.Count, MarkStacks.MostFanned);
            FanFrom.Clear();
            _fanSize = size;
            var lineWidth = Mathf.Max(1.5f, size * 0.07f);
            for (var k = 0; k < n; k++)
            {
                if (k == FanLines.Count) FanLines.Add(StackPips.Piece(_fanHolder, like, "Scry fan line"));
                if (k == FanDots.Count) FanDots.Add(StackPips.Piece(_fanHolder, like, "Scry fanned dot"));
                var (x, y) = MarkStacks.FanAt(k, n, size);
                var line = FanLines[k];
                line.Rect.anchoredPosition = at + new Vector2(x, y) / 2f;
                line.Rect.sizeDelta = new Vector2(Mathf.Sqrt(x * x + y * y), lineWidth);
                line.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(y, x) * Mathf.Rad2Deg);
                line.Picture.sprite = Line();
                if (!line.Picture.gameObject.activeSelf) line.Picture.gameObject.SetActive(true);
                StackPips.Stand(FanDots[k], at + new Vector2(x, y), size, StandingDots[members[k]].Picture);
                FanFrom.Add(members[k]);
            }
            // The lines under every dot, the spot's mark over the lines' ends.
            for (var k = 0; k < n; k++) FanLines[k].Rect.SetSiblingIndex(k);
            StackPips.Stand(_fanSpot, at, MarkStacks.PipSize(size) * 1.2f, StackPips.Picture());
            _fanSpot.Rect.SetSiblingIndex(n);
            for (var k = n; k < FanDots.Count; k++) FanDots[k].Picture.gameObject.SetActive(false);
            for (var k = n; k < FanLines.Count; k++) FanLines[k].Picture.gameObject.SetActive(false);
        }

        /// <summary>
        /// The pointer read on the stack fanned out: on one of its dots, that dot named alone and
        /// ringed; elsewhere within the fan, every kind there named; past it, the fan closed and
        /// false, so the pointer is read as on any other dot.
        /// </summary>
        private static bool ReadFan(Vector2 pointer, Player player, out (string Words, Rect? Ring) said)
        {
            said = (StackWords(_fanned, player), null);
            // Fanned this frame and not yet placed: the stack's words stand.
            if (FanStanding == 0) return true;
            var scale = _fanHolder.lossyScale.x;
            var half = _fanSize * scale / 2f;
            for (var k = FanFrom.Count - 1; k >= 0; k--)
            {
                var rect = FanDots[k].Rect;
                if (((Vector2)rect.position - pointer).sqrMagnitude > half * half) continue;
                said = (DotWords(FanFrom[k], player), RingOf(rect));
                return true;
            }
            var off = (pointer - (Vector2)_fanSpot.Rect.position) / Mathf.Max(0.0001f, scale);
            if (MarkStacks.StaysFanned(off.x, off.y, FanFrom.Count, _fanSize)) return true;
            Unfan();
            return false;
        }

        /// <summary>The words of a stack pointed at: every kind there, nearest first (<see cref="SpotWords.StackHover"/>).</summary>
        private static string StackWords(int anchor, Player player)
        {
            var marks = new List<(string Name, bool Told, float Metres, string Direction)>();
            foreach (var dot in Stacks.MembersOf(anchor))
            {
                var (x, z, _, name, told) = StandingDots[dot];
                var (dx, dz) = player != null ? (x - player.transform.position.x, z - player.transform.position.z) : (0f, 0f);
                marks.Add((name, told, Mathf.Sqrt(dx * dx + dz * dz), SpotWords.Direction(dx, dz)));
            }
            return SpotWords.StackHover(marks);
        }

        /// <summary>A dot's words: its kind, then how far and which way it is from the character; its name alone with no character.</summary>
        private static string DotWords(int dot, Player player)
        {
            var (x, z, _, name, told) = StandingDots[dot];
            if (player == null) return name;
            var (dx, dz) = (x - player.transform.position.x, z - player.transform.position.z);
            var away = Mathf.Sqrt(dx * dx + dz * dz);
            return told ? SpotWords.ToldHover(name, away, SpotWords.Direction(dx, dz)) : SpotWords.DotHover(name, away, SpotWords.Direction(dx, dz));
        }

        /// <summary>Where a dot stands on the screen, top down as the panel draws, to be ringed.</summary>
        private static Rect RingOf(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, Screen.height - corners[2].y, corners[2].x, Screen.height - corners[0].y);
        }

        /// <summary>A fan line's picture: plain, in Scry's writing colour, a little faint.</summary>
        private static Sprite Line()
        {
            if (_line != null) return _line;
            var texture = Kept.Add(new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "Scry fan line", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Skin.Alpha(Skin.Text, 0.75f);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _line = Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f)));
            return _line;
        }

        /// <summary>Lets go of the stacks, the pips and the fan with the dots.</summary>
        private static void ForgetStacks()
        {
            Pips.Forget();
            _fanned = -1;
            _fanHolder = null;
            _fanSpot = default;
            FanDots.Clear();
            FanLines.Clear();
            FanFrom.Clear();
            _stackedFor = default;
        }
    }
}
