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
        private static readonly MarkCounts FanCounts = new MarkCounts("Scry fanned counts");

        /// <summary>What the stacks were gathered for: the dots, and the touch's zoom step.</summary>
        private static ((IReadOnlyList<(int X, int Z)>, IReadOnlyList<(int X, int Z)>, Sprite, int) Dots, int Step, bool Merging) _stackedFor;

        /// <summary>The stack fanned out, by its first dot; -1 for none; and the fanned dot the pointer rests on, by the dot standing, -1 for none.</summary>
        private static int _fanPointed = -1;
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

        /// <summary>Whether the stack fanned out stands over the game's pins, for the self-test.</summary>
        [ForTest]
        public static bool FanOverPinsForTest => FanStanding > 0 && _over != null && _fanHolder.parent == _over;

        /// <summary>Whether the pips stand over the game's pins, for the self-test.</summary>
        [ForTest]
        public static bool PipsOverPinsForTest => Pips.Shown > 0 && _over != null && Pips.Parent == _over;

        /// <summary>Whether a copy of the dot pointed at stands over the game's pins, for the self-test.</summary>
        [ForTest]
        public static bool LiftedForTest => _lifted.Picture != null && _lifted.Picture.gameObject.activeInHierarchy;

        /// <summary>How many of the game's pins and names stand faded near a pointed mark, for the self-test.</summary>
        [ForTest]
        public static int PinsFadedForTest => Faded.Count;

        /// <summary>Where the first dot standing alone, in no stack, stands on the screen; none, for the self-test.</summary>
        [ForTest]
        public static Vector2? LoneDotForTest()
        {
            for (var i = 0; i < _standing && i < PlacedFrom.Count; i++)
            {
                if (Stacks.CountAt(Stacks.AnchorOf(PlacedFrom[i])) == 1) return new Vector2(Dots[i].Rect.position.x, Dots[i].Rect.position.y);
            }
            return null;
        }

        /// <summary>Where the first dot standing in a stack stands on the screen; none, for the self-test.</summary>
        [ForTest]
        public static Vector2? StackedDotForTest()
        {
            if (FanStanding > 0) return new Vector2(_fanSpot.Rect.position.x, _fanSpot.Rect.position.y);
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
            var merging = MapLook.Merges(Settings.FarMarks);
            var key = (_dotsFor, Mathf.RoundToInt(Mathf.Log(Mathf.Max(0.001f, touch)) * 50f), merging);
            _touch = touch;
            if (key == _stackedFor) return;
            _stackedFor = key;
            // A kind's own dots merged first where the settings ask, then the kinds stacked from what stands.
            GatherMerged(touch, merging);
            Stacks.Gather(StandingDots.Count, i => (StandingDots[i].X, StandingDots[i].Z), touch, null, Visible);
            // A right click in a spread leaves it open while it still holds several.
            var stays = TakeModesChanged() && _fanned >= 0 && Stacks.AnchorOf(_fanned) == _fanned && Stacks.CountAt(_fanned) > 1;
            if (!stays) Unfan();
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
            _fanPointed = -1;
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
            // Over the game's pins and their names where the settings ask, else over the dots alone.
            var parent = MapLook.PointedOver(Settings.MarksOverPins) && _over != null ? _over : _holder;
            if (_fanHolder == null || _fanHolder.parent != parent)
            {
                if (_fanHolder != null) Object.Destroy(_fanHolder.gameObject);
                FanDots.Clear();
                FanLines.Clear();
                _fanHolder = StackPips.Holder(parent, "Scry fanned stack");
                _fanSpot = StackPips.Piece(_fanHolder, like, "Scry fanned spot");
            }
            if (_fanHolder.GetSiblingIndex() != parent.childCount - 1) _fanHolder.SetAsLastSibling();
            if (!_fanHolder.gameObject.activeSelf) _fanHolder.gameObject.SetActive(true);
            FanCounts.Begin(_fanHolder);
            FanModes.Begin(_fanHolder, _fanHolder);
            var members = Stacks.MembersOf(_fanned);
            var n = Mathf.Min(members.Count, MarkStacks.MostFanned);
            FanFrom.Clear();
            _fanSize = size;
            var lineWidth = Mathf.Max(1.5f, size * 0.07f);
            // Round the middle of where the dots lie, each fanned to the side it lies on.
            var middle = Vector2.zero;
            var placed = 0;
            for (var k = 0; k < n; k++)
            {
                if (!FannedAt.TryGetValue(members[k], out var lies)) continue;
                middle += lies;
                placed++;
            }
            if (placed == n && n > 0) at = middle / n;
            var offsets = new (float X, float Y)[n];
            for (var k = 0; k < n; k++) offsets[k] = placed == n && FannedAt.TryGetValue(members[k], out var lies) ? (lies.x - at.x, lies.y - at.y) : (0f, 0f);
            var ways = MarkStacks.FanAngles(offsets);
            for (var k = 0; k < n; k++)
            {
                if (k == FanLines.Count) FanLines.Add(StackPips.Piece(_fanHolder, like, "Scry fan line"));
                if (k == FanDots.Count) FanDots.Add(StackPips.Piece(_fanHolder, like, "Scry fanned dot"));
                var (x, y) = MarkStacks.FanToward(ways[k], n, size);
                var line = FanLines[k];
                line.Rect.anchoredPosition = at + new Vector2(x, y) / 2f;
                line.Rect.sizeDelta = new Vector2(Mathf.Sqrt(x * x + y * y), lineWidth);
                line.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(y, x) * Mathf.Rad2Deg);
                line.Picture.sprite = Line();
                if (!line.Picture.gameObject.activeSelf) line.Picture.gameObject.SetActive(true);
                StackPips.Stand(FanDots[k], at + new Vector2(x, y), size, LookOf(StandingDots[members[k]].Picture));
                FanDots[k].Picture.color = ShownColour(members[k]);
                FanModes.Put(Minimap.instance, at + new Vector2(x, y), size, ModeOf(members[k]), LookOf(StandingDots[members[k]].Picture), PickColour(members[k]), like);
                // A merged mark in the fan keeps its count beside it.
                var merged = MergedCount(members[k]);
                if (merged > 1) FanCounts.Put(Minimap.instance, at + new Vector2(x, y), size, merged, StandingDots[members[k]].Told, ShownColour(members[k]).a, like);
                FanFrom.Add(members[k]);
            }
            // The lines under every dot, the spot's mark over the lines' ends.
            FanCounts.End();
            FanModes.End();
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
            _fanPointed = -1;
            // Fanned this frame and not yet placed: the stack's words stand.
            if (FanStanding == 0) return true;
            var scale = _fanHolder.lossyScale.x;
            var half = _fanSize * scale / 2f;
            for (var k = FanFrom.Count - 1; k >= 0; k--)
            {
                var rect = FanDots[k].Rect;
                if (((Vector2)rect.position - pointer).sqrMagnitude > half * half) continue;
                // A highlighted mark's own outline tells it is the one, no ring round it besides.
                said = (DotWords(FanFrom[k], player), ModeOf(FanFrom[k]) == SpotMode.Highlighted ? (Rect?)null : RingOf(rect));
                _fanPointed = FanFrom[k];
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
            var marks = new List<StackedMark>();
            var members = Stacks.MembersOf(anchor);
            for (var i = 0; i < members.Count; i++)
            {
                var (x, z, _, name, told) = StandingDots[members[i]];
                var (dx, dz) = player != null ? (x - player.transform.position.x, z - player.transform.position.z) : (0f, 0f);
                // A kind's merged mark counts every dot it stands for.
                for (var n = MergedCount(members[i]); n > 0; n--) marks.Add(new StackedMark(name, told, Mathf.Sqrt(dx * dx + dz * dz), SpotWords.Direction(dx, dz)));
            }
            return SpotWords.StackHover(marks);
        }

        /// <summary>A dot's words: its kind, then how far and which way it is from the character; its name alone with no character.</summary>
        private static string DotWords(int dot, Player player) => SpotWords.WithModeHint(DotSaid(dot, player), ModeOf(dot), MergedCount(dot));

        private static string DotSaid(int dot, Player player)
        {
            if (MergedCount(dot) > 1) return MergedWords(dot, player);
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

        /// <summary>
        /// What stands over the game's pins and their names (<see cref="MarksOverPins"/>): a holder
        /// last on the pins' names' layer, which shares the pins' layer's room, made with the dots
        /// so the dimming of the other pins keeps it bright as it does them; last again each frame,
        /// over every name made since. None where the map has no names' layer.
        /// </summary>
        private static RectTransform _over;

        /// <summary>The words of the dungeon mark the pointer rests on, which stand above what stands over the pins.</summary>
        private static RectTransform _wordsOver;

        /// <summary>The words that stand above everything on the names' layer, as a mark tells them; none as it leaves them.</summary>
        public static void WordsOverAll(RectTransform words) => _wordsOver = words;

        /// <summary>The copy of the dot pointed at, standing over the pins while the pointer rests on it.</summary>
        private static (Image Picture, RectTransform Rect) _lifted;

        /// <summary>The cross over that copy, where the dot pointed at is crossed off.</summary>
        private static (Image Picture, RectTransform Rect) _liftedCross;

        /// <summary>The game's pins and names faded near a pointed mark, each by a group of its own, with its see-through before; and what they were faded for.</summary>
        private static readonly List<(CanvasGroup Group, float Before)> Faded = new List<(CanvasGroup, float)>();
        private static (Vector2 At, float Reach) _fadedFor;

        private static readonly HarmonyLib.AccessTools.FieldRef<Minimap, List<Minimap.PinData>> GamePins = HarmonyLib.AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");

        /// <summary>The holder over the pins, made where it is missing and last on its layer.</summary>
        private static RectTransform OverPins(Minimap map)
        {
            var names = map.m_pinNameRootLarge;
            if (names == null) return null;
            if (_over == null || _over.parent != names)
            {
                if (_over != null) Object.Destroy(_over.gameObject);
                _over = StackPips.Holder(names, "Scry marks over pins");
            }
            if (!_over.gameObject.activeSelf) _over.gameObject.SetActive(true);
            // Last on its layer, but under the words of a mark pointed at, so neither moves the other each frame.
            var top = names.childCount - 1;
            if (top > 0 && _wordsOver != null && _wordsOver.parent == names && _wordsOver.gameObject.activeSelf && _wordsOver.GetSiblingIndex() == top) top--;
            if (_over.GetSiblingIndex() != top) _over.SetSiblingIndex(top);
            return _over;
        }

        /// <summary>
        /// Each frame after the pointer is read: the dot pointed at stood over the pins as a copy
        /// where the settings ask and every dot does not stand there already; the game's pins near
        /// the pointed mark faded where the settings ask (<see cref="FadeNear"/>).
        /// </summary>
        private static void LiftPointed(Minimap map, float size, RectTransform like)
        {
            var over = MapLook.PointedOver(Settings.MarksOverPins) && !MapLook.DotsOver(Settings.MarksOverPins);
            var dot = _pointedAt >= 0 && _pointedAt < _standing ? Dots[_pointedAt] : default;
            // A highlighted mark stands over the pins already, in its own look; a crossed one keeps its look and its cross.
            var mode = _pointedAt >= 0 && _pointedAt < PlacedFrom.Count ? ModeOf(PlacedFrom[_pointedAt]) : SpotMode.Plain;
            var crossed = mode == SpotMode.Crossed;
            if (over && dot.Rect != null && _over != null && like != null && mode != SpotMode.Highlighted)
            {
                if (_lifted.Rect == null) _lifted = StackPips.Piece(_over, like, "Scry pointed dot");
                if (crossed && _liftedCross.Rect == null) _liftedCross = StackPips.Piece(_over, like, "Scry pointed dot's cross");
                // Over all else there but the pointed mark's count.
                var top = _over.childCount - (PointedCount.Parent == _over ? 2 : 1);
                var liftedAt = crossed ? top - 1 : top;
                if (_lifted.Rect.GetSiblingIndex() != liftedAt) _lifted.Rect.SetSiblingIndex(liftedAt);
                StackPips.Stand(_lifted, dot.Rect.anchoredPosition, dot.Rect.sizeDelta.x, dot.Picture.sprite);
                _lifted.Picture.color = Skin.Alpha(Skin.MarkShown, SpotModes.ShareOf(mode, 1f));
                if (crossed)
                {
                    if (_liftedCross.Rect.GetSiblingIndex() != top) _liftedCross.Rect.SetSiblingIndex(top);
                    StackPips.Stand(_liftedCross, dot.Rect.anchoredPosition, dot.Rect.sizeDelta.x * SpotModes.CrossScale, SpotMarks.CrossPicture(map));
                }
            }
            else if (_lifted.Picture != null && _lifted.Picture.gameObject.activeSelf) _lifted.Picture.gameObject.SetActive(false);
            if (!crossed && _liftedCross.Picture != null && _liftedCross.Picture.gameObject.activeSelf) _liftedCross.Picture.gameObject.SetActive(false);
            FadeNear(map, size);
            ShowMembers(map, size, like);
            ShowPointedCount(map, like);
        }

        /// <summary>
        /// The game's pins and their names within reach of the pointed mark on the screen, a spread
        /// stack or a dot (<see cref="MarkStacks.FadeReach"/>), faded to the settings' share, each
        /// by a group laid on it, which the game leaves be as it sets each pin's own colour; worked
        /// out again only as the pointed mark moves on the screen, and put back as nothing is pointed.
        /// </summary>
        private static void FadeNear(Minimap map, float size)
        {
            var share = MapLook.NearShare(Settings.PinsNearPointer);
            var scale = _holder != null ? _holder.lossyScale.x : 1f;
            var near = (At: Vector2.zero, Reach: 0f);
            if (share < 1f && FanStanding > 0) near = (_fanSpot.Rect.position, MarkStacks.FadeReach(FanFrom.Count, _fanSize) * scale);
            else if (share < 1f && _pointedAt >= 0 && _pointedAt < _standing) near = (Dots[_pointedAt].Rect.position, MarkStacks.FadeReach(1, size) * scale);
            if (near == _fadedFor) return;
            _fadedFor = near;
            Unfade();
            if (near.Reach <= 0f) return;
            var pins = GamePins(map);
            if (pins == null) return;
            foreach (var pin in pins)
            {
                if (pin?.m_uiElement == null) continue;
                var at = (Vector2)pin.m_uiElement.position - near.At;
                if (!MapLook.Fades(at.x, at.y, near.Reach)) continue;
                Fade(pin.m_uiElement.gameObject, share);
                if (pin.m_NamePinData?.PinNameGameObject != null) Fade(pin.m_NamePinData.PinNameGameObject, share);
            }
        }

        private static void Fade(GameObject thing, float share)
        {
            var group = thing.GetComponent<CanvasGroup>();
            if (group == null) group = thing.AddComponent<CanvasGroup>();
            Faded.Add((group, group.alpha));
            group.alpha *= share;
        }

        /// <summary>What stands over the pins put away with the dots, and the faded pins back.</summary>
        private static void PutAwayOver()
        {
            Unfade();
            _fadedFor = default;
            if (_over != null && _over.gameObject.activeSelf) _over.gameObject.SetActive(false);
        }

        /// <summary>The faded pins back as they stood; a pin the game let go of meanwhile is passed over.</summary>
        private static void Unfade()
        {
            foreach (var (group, before) in Faded) if (group != null) group.alpha = before;
            Faded.Clear();
        }

        /// <summary>Lets go of the stacks, the pips and the fan with the dots.</summary>
        private static void ForgetStacks()
        {
            Pips.Forget();
            Unfade();
            if (_over != null) Object.Destroy(_over.gameObject);
            _over = null;
            _lifted = default;
            _liftedCross = default;
            _fadedFor = default;
            _fanned = -1;
            _fanPointed = -1;
            FanCounts.Forget();
            _fanHolder = null;
            _fanSpot = default;
            FanDots.Clear();
            FanLines.Clear();
            FanFrom.Clear();
            _stackedFor = default;
        }
    }
}
