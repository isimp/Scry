using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// A crowded map, as the settings ask (<see cref="FarMarks"/>): a kind's dots that would cover
    /// each other merged into its first one, with how many on a tab beside it, the rest standing
    /// for it, before the kinds stack together (<see cref="MarkStacks"/>); far out, every mark in
    /// its plain look, the middle of its own picture without the ring, smaller; and one kind shown
    /// alone from the kinds strip, the others faded (<see cref="SeenOnMap.SoloOthers"/>).
    /// </summary>
    internal static partial class MapSpots
    {
        private static readonly MarkStacks Merged = new MarkStacks();
        private static readonly MarkCounts Counts = new MarkCounts("Scry merged counts");

        /// <summary>The count of the merged mark the pointer rests on, over the dots it brings up and the game's pins.</summary>
        private static readonly MarkCounts PointedCount = new MarkCounts("Scry pointed count");
        private static bool _merging, _plain;

        /// <summary>Each standing dot's kind as a number, for only a kind's own dots to merge.</summary>
        private static readonly Dictionary<(string Kind, bool Told), int> KindNumbers = new Dictionary<(string Kind, bool Told), int>();
        private static readonly List<int> KindOfDot = new List<int>();

        /// <summary>How much of a plain picture a plain mark shows: its shape out to a square's corners, all it holds.</summary>
        private const float PlainCut = 0.75f;

        /// <summary>Each dot's plain look, by its full picture: its plain picture cut to its middle.</summary>
        private static readonly Dictionary<Sprite, Sprite> Plains = new Dictionary<Sprite, Sprite>();

        /// <summary>The kinds picked on the kinds strip to stand out, none while all do; and whether they changed since the dots were last placed.</summary>
        private static readonly HashSet<string> Picked = new HashSet<string>(System.StringComparer.Ordinal);
        private static bool _soloChanged;

        /// <summary>The kinds kept that stand, for the kinds strip: each with its name, how many you saw and how many the server told of, and its picture; and what they were worked out for.</summary>
        private static readonly List<(string Kind, string Name, int Seen, int Told, Sprite Picture, int Crossed)> StripKinds = new List<(string, string, int, int, Sprite, int)>();
        private static ((IReadOnlyList<(int X, int Z)>, IReadOnlyList<(int X, int Z)>, Sprite, int) Dots, int Marks) _stripFor;

        /// <summary>Where the kinds strip was drawn and the frame, for its clicks to be kept from the map under it.</summary>
        private static Rect _kindsStripAt;
        private static int _kindsStripFrame = -10;

        /// <summary>How many merged marks show their count, for the self-test.</summary>
        [ForTest]
        public static int MergedShownForTest => Counts.Shown;

        /// <summary>How many merged marks' counts stand faded with their marks, for the self-test.</summary>
        [ForTest]
        public static int FadedCountsForTest => Counts.Faded;

        /// <summary>Whether the merged mark pointed at shows its count over all that stands with it, for the self-test.</summary>
        [ForTest]
        public static bool PointedCountOnTopForTest => PointedCount.Shown == 1 && PointedCount.StandsLast;

        /// <summary>How many counts stand over the rest for a merged mark pointed at, for the self-test.</summary>
        [ForTest]
        public static int PointedCountsForTest => PointedCount.Shown;

        /// <summary>The middle of the kinds list on the screen, for the self-test.</summary>
        [ForTest]
        public static Vector2 KindsListMiddleForTest => new Vector2(_kindsStripAt.center.x, Screen.height - _kindsStripAt.center.y);

        /// <summary>Whether the dots stand in their plain look, for the self-test.</summary>
        [ForTest]
        public static bool PlainForTest => _plain;

        /// <summary>How many dots standing are faded, of a kind not shown alone, for the self-test.</summary>
        [ForTest]
        public static int FadedDotsForTest
        {
            get
            {
                var faded = 0;
                for (var i = 0; i < _standing && i < Dots.Count; i++) if (Dots[i].Picture.color == Skin.MarkFaded) faded++;
                return faded;
            }
        }

        /// <summary>Whether a kind is picked to stand out on the kinds strip.</summary>
        public static bool IsPicked(string kind) => kind != null && Picked.Contains(kind);

        /// <summary>How much of its full look a kind stands at: all of it while none is picked or it is, else a faded dot's.</summary>
        public static float ShownShare(string kind) => Picked.Count == 0 || IsPicked(kind) ? 1f : Skin.MarkFaded.a;

        /// <summary>How many kinds are picked to stand out; none while all do.</summary>
        public static int PickedCount => Picked.Count;

        /// <summary>Whether the kinds strip stands: on in the settings, the large map open, two kinds or more kept standing.</summary>
        public static bool KindsStripShown => Settings.KindsStrip && LargeOpen && KindChips().Count >= 2 && !TargetPortalMap.MarksAside;

        /// <summary>The kinds with dungeon marks kept, each with its name and how many, as the marks last told; and how often that changed.</summary>
        private static readonly List<(string Kind, string Name, int Count, int Crossed)> KindsMarked = new List<(string, string, int, int)>();
        private static int _markedVersion;

        /// <summary>The kinds with dungeon marks kept, for the self-test.</summary>
        [ForTest]
        public static int MarkedKindsForTest => KindsMarked.Count;

        /// <summary>The kinds the dungeon marks kept are of, told by the marks as they are listed.</summary>
        public static void MarkedKindsAre(IReadOnlyList<(string Kind, string Name, int Count, int Crossed)> kinds)
        {
            if (kinds.Count == 0 && KindsMarked.Count == 0) return;
            KindsMarked.Clear();
            KindsMarked.AddRange(kinds);
            _markedVersion++;
        }

        /// <summary>The kinds kept that stand on the map, for the strip, by their dots and a dungeon's marks too, worked out again only as those change.</summary>
        public static IReadOnlyList<(string Kind, string Name, int Seen, int Told, Sprite Picture, int Crossed)> KindsStanding()
        {
            var now = (_dotsFor, _markedVersion);
            if (_stripFor == now) return StripKinds;
            _stripFor = now;
            StripKinds.Clear();
            var at = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (var i = 0; i < StandingDots.Count && i < StandingKinds.Count; i++)
            {
                if (StandingKinds[i] == null) continue;
                var told = StandingDots[i].Told;
                TallyKind(at, StandingKinds[i], StandingDots[i].Name, told ? 0 : 1, told ? 1 : 0, StandingDots[i].Picture, ModeOf(i) == SpotMode.Crossed ? 1 : 0);
            }
            // A dungeon stands by its mark rather than a dot, the amber of the dungeons on the list.
            foreach (var (kind, name, count, crossed) in KindsMarked) TallyKind(at, kind, name, count, 0, MarkPicture(KeptMarks.Dungeons, CompendiumBook.Dungeons), crossed);
            return StripKinds;
        }

        /// <summary>So many of a kind counted in; the picture of one you saw before one the server told of, as the strip names the kind by it.</summary>
        private static void TallyKind(Dictionary<string, int> at, string kind, string name, int seen, int told, Sprite picture, int crossed)
        {
            if (at.TryGetValue(kind, out var index))
            {
                var was = StripKinds[index];
                StripKinds[index] = (was.Kind, was.Name, was.Seen + seen, was.Told + told, was.Seen == 0 && seen > 0 ? picture : was.Picture, was.Crossed + crossed);
                return;
            }
            at[kind] = StripKinds.Count;
            StripKinds.Add((kind, name, seen, told, picture, crossed));
        }

        /// <summary>The kinds list's chips: a family's kinds standing under one chip, as a place's numbered ones, a kind of no family alone; and what they were worked out for.</summary>
        private static readonly List<(string Name, int Seen, int Told, Sprite Picture, string[] Kinds, int Crossed)> Chips = new List<(string, int, int, Sprite, string[], int)>();
        private static (((IReadOnlyList<(int X, int Z)>, IReadOnlyList<(int X, int Z)>, Sprite, int), int) Kinds, int Regroups) _chipsFor;

        /// <summary>The kinds list's categories in order, each with how many stand under it, all its kinds, and which chips are its.</summary>
        private static readonly List<(KeptCategory Category, int Count, string[] Kinds, int First, int Chips)> KindCategories = new List<(KeptCategory, int, string[], int, int)>();

        /// <summary>The kinds list's categories, worked out with its chips.</summary>
        public static IReadOnlyList<(KeptCategory Category, int Count, string[] Kinds, int First, int Chips)> KindGroups()
        {
            KindChips();
            return KindCategories;
        }

        /// <summary>The chips stood in their categories' order, and each category's told.</summary>
        private static void GroupChips()
        {
            KindCategories.Clear();
            var sorted = new List<(string Name, int Seen, int Told, Sprite Picture, string[] Kinds, int Crossed)>(Chips.Count);
            foreach (var category in KeptCategories.All)
            {
                var first = sorted.Count;
                var count = 0;
                var kinds = new List<string>();
                foreach (var chip in Chips)
                {
                    if (MapKept.CategoryOf(chip.Kinds[0]) != category) continue;
                    sorted.Add(chip);
                    kinds.AddRange(chip.Kinds);
                    count += chip.Seen + chip.Told;
                }
                if (sorted.Count > first) KindCategories.Add((category, count, kinds.ToArray(), first, sorted.Count - first));
            }
            Chips.Clear();
            Chips.AddRange(sorted);
        }

        /// <summary>The kinds list's chips, worked out again only as the dots change (<see cref="KindsList.Group"/>).</summary>
        public static IReadOnlyList<(string Name, int Seen, int Told, Sprite Picture, string[] Kinds, int Crossed)> KindChips()
        {
            var kinds = KindsStanding();
            // Again as the catalog is read or grouped anew too, a family's kinds coming under one chip.
            var now = (_stripFor, WorldCatalog.Current?.Regroups ?? -1);
            if (_chipsFor == now && (Chips.Count > 0 || kinds.Count == 0)) return Chips;
            _chipsFor = now;
            Chips.Clear();
            var under = new List<string>(kinds.Count);
            var families = new List<Family>(kinds.Count);
            foreach (var (kind, _, _, _, _, _) in kinds)
            {
                var family = WorldCatalog.Find(kind)?.Family;
                var grouped = family != null && family.Kinds.Count > 1 ? family : null;
                families.Add(grouped);
                under.Add(grouped != null ? grouped.Head.Key : kind);
            }
            foreach (var (_, members) in KindsList.Group(under))
            {
                var first = kinds[members[0]];
                var (seen, told, crossed) = (0, 0, 0);
                var names = new string[members.Count];
                for (var i = 0; i < members.Count; i++)
                {
                    seen += kinds[members[i]].Seen;
                    told += kinds[members[i]].Told;
                    crossed += kinds[members[i]].Crossed;
                    names[i] = kinds[members[i]].Kind;
                }
                Chips.Add((families[members[0]]?.Name ?? first.Name, seen, told, first.Picture, names, crossed));
            }
            GroupChips();
            return Chips;
        }

        /// <summary>Whether every kind under a chip is picked, so the chip stands lit.</summary>
        public static bool IsPicked(IReadOnlyList<string> kinds)
        {
            if (kinds.Count == 0) return false;
            foreach (var kind in kinds) if (!Picked.Contains(kind)) return false;
            return true;
        }

        /// <summary>Whether any kind under a heading is picked, so it stands outlined.</summary>
        public static bool AnyPicked(IReadOnlyList<string> kinds)
        {
            foreach (var kind in kinds) if (Picked.Contains(kind)) return true;
            return false;
        }

        /// <summary>A chip's kinds picked to stand out together, or let go together where all are picked, as a click on the chip does.</summary>
        public static void ShowAlone(IReadOnlyList<string> kinds)
        {
            var all = IsPicked(kinds);
            foreach (var kind in kinds)
            {
                if (all) Picked.Remove(kind);
                else Picked.Add(kind);
            }
            _soloChanged = true;
        }

        /// <summary>A single kind picked to stand out, or let go again, as a click on a chip of it alone does; none, all stand alike; for the self-test.</summary>
        [ForTest]
        public static void ShowAlone(string kind)
        {
            if (kind == null) Picked.Clear();
            else if (!Picked.Remove(kind)) Picked.Add(kind);
            _soloChanged = true;
        }

        /// <summary>Whether the kind shown alone changed since the dots were last placed, taken once.</summary>
        private static bool TakeSoloChange()
        {
            var changed = _soloChanged;
            _soloChanged = false;
            return changed;
        }

        /// <summary>All the kinds shown again as the map closes.</summary>
        private static void ForgetAlone()
        {
            if (Picked.Count > 0) _soloChanged = true;
            Picked.Clear();
        }

        /// <summary>Where the kinds strip was drawn, for its clicks to be kept from the map under it.</summary>
        public static void KindsStripDrawn(Rect at)
        {
            _kindsStripAt = at;
            _kindsStripFrame = Time.frameCount;
        }

        private static bool PointerOnKindsStrip(Vector2 gui) => Time.frameCount <= _kindsStripFrame + 1 && _kindsStripAt.Contains(gui);

        /// <summary>
        /// A kind's dots that would cover each other gathered into its first, where the settings
        /// merge them, by the same touch as the stacks, before the stacks gather the dots left
        /// standing; none merged otherwise.
        /// </summary>
        private static void GatherMerged(float touch, bool merging)
        {
            _merging = merging;
            if (!merging) return;
            KindNumbers.Clear();
            KindOfDot.Clear();
            for (var i = 0; i < StandingDots.Count; i++)
            {
                var kind = i < StandingKinds.Count ? StandingKinds[i] ?? "" : "";
                // A kind told of by the server merges apart from the same kind seen, as its hollow dot differs.
                var key = (kind, StandingDots[i].Told);
                if (!KindNumbers.TryGetValue(key, out var number)) KindNumbers[key] = number = KindNumbers.Count;
                KindOfDot.Add(SpotModes.MergeGroup(number, ModeOf(i), i));
            }
            Merged.Gather(StandingDots.Count, i => (StandingDots[i].X, StandingDots[i].Z), touch, i => KindOfDot[i]);
        }

        /// <summary>Whether a dot stands on the map: one alone or the first of its kind's merged ones.</summary>
        private static bool Visible(int dot) => (!_merging || Merged.AnchorOf(dot) == dot) && ModeStands(ModeOf(dot));

        /// <summary>How many dots a standing dot stands for: itself, or its kind's merged ones.</summary>
        private static int MergedCount(int dot) => _merging ? Mathf.Max(1, Merged.CountAt(dot)) : 1;

        /// <summary>Whether the dots stand plain at the zoom they are placed for, a dot spanning so many metres.</summary>
        private static void NotePlain(float dotMetres) => _plain = MapLook.PlainAt(Settings.FarMarks, dotMetres);

        private static float ShownSize(float size) => _plain ? size * SeenOnMap.PlainShare : size;

        private static Sprite LookOf(Sprite full) => _plain ? PlainOf(full) : full;

        /// <summary>A dot's colour: as drawn, or faded where other kinds are picked to stand out.</summary>
        private static Color ShownColour(int dot)
        {
            var picked = PickColour(dot);
            return Skin.Alpha(picked, SpotModes.ShareOf(ModeOf(dot), picked.a));
        }

        /// <summary>A dot's colour by the kinds picked alone, whatever its mode.</summary>
        private static Color PickColour(int dot) => Picked.Count == 0 || (dot < StandingKinds.Count && IsPicked(StandingKinds[dot])) ? Skin.MarkShown : Skin.MarkFaded;

        /// <summary>A dot's plain look: the kind's shape or picture and its edge drawn without the gap and ring, its middle shown, made once for each picture.</summary>
        public static Sprite PlainOf(Sprite full)
        {
            if (full == null) return null;
            if (Plains.TryGetValue(full, out var plain) && plain != null) return plain;
            // Drawn without its ring where the picture was made here, else the full one cut closer.
            var drawn = PlainMakers.TryGetValue(full, out var make) ? make() : null;
            var from = drawn != null ? drawn : full;
            var whole = from.rect;
            var side = whole.width * (drawn != null ? PlainCut : SeenOnMap.PlainShare * 0.85f);
            var middle = new Rect(whole.x + (whole.width - side) / 2f, whole.y + (whole.height - side) / 2f, side, side);
            plain = Kept.Add(Sprite.Create(from.texture, middle, new Vector2(0.5f, 0.5f)));
            Plains[full] = plain;
            return plain;
        }

        /// <summary>A merged mark's words: how many of its kind stand there, and how far and which way the nearest is.</summary>
        private static string MergedWords(int dot, Player player)
        {
            var (_, _, _, name, told) = StandingDots[dot];
            var nearest = float.MaxValue;
            var (ndx, ndz) = (0f, 0f);
            foreach (var each in Merged.MembersOf(dot))
            {
                var (x, z, _, _, _) = StandingDots[each];
                var (dx, dz) = player != null ? (x - player.transform.position.x, z - player.transform.position.z) : (0f, 0f);
                var away = Mathf.Sqrt(dx * dx + dz * dz);
                if (away >= nearest) continue;
                nearest = away;
                (ndx, ndz) = (dx, dz);
            }
            return SpotWords.MergedHover(name, MergedCount(dot), told, nearest, SpotWords.Direction(ndx, ndz));
        }

        /// <summary>A merged mark clicked: the map zoomed in on it, centred where it stands, until its dots come apart; false where the click is on none.</summary>
        public static bool ZoomOnMerged(Vector2 screen)
        {
            var map = Minimap.instance;
            var player = Player.m_localPlayer;
            if (map == null || player == null || map.m_mode != Minimap.MapMode.Large || _holder == null || !_holder.gameObject.activeInHierarchy) return false;
            var (from, size) = DotUnder(screen);
            if (from < 0 || MergedCount(from) < 2) return false;
            if (!MapMarks.Clicked((_pressedAt.x, _pressedAt.y), (screen.x, screen.y), size)) return false;
            var (x, z, _, _, _) = StandingDots[from];
            map.LargeZoom = MapLook.ZoomInOn(map.LargeZoom);
            MapOffset(map) = new Vector3(x - player.transform.position.x, 0f, z - player.transform.position.z);
            return true;
        }

        /// <summary>The picked kinds' dots over the game's pins while any are picked: a copy of each over the pins, how many stand and stood.</summary>
        private static readonly List<(Image Picture, RectTransform Rect)> PickedOver = new List<(Image, RectTransform)>();
        private static int _pickedUsed, _pickedLive;

        /// <summary>How many picked dots stand over the game's pins, for the self-test.</summary>
        [ForTest]
        public static int PickedOverForTest => _pickedLive;

        private static void BeginPickedOver() => _pickedUsed = 0;

        /// <summary>A picked kind's dot stood over the game's pins as well, so it stands out above them; none while no kind is picked.</summary>
        private static void PutPickedOver(int dot, Vector2 at, float size, Sprite picture, RectTransform like)
        {
            if (Picked.Count == 0 || _over == null || like == null || dot >= StandingKinds.Count || !IsPicked(StandingKinds[dot]) || ModeOf(dot) != SpotMode.Plain) return;
            if (_pickedUsed == PickedOver.Count) PickedOver.Add(StackPips.Piece(_over, like, "Scry picked dot over pins"));
            StackPips.Stand(PickedOver[_pickedUsed++], at, size, picture);
        }

        private static void EndPickedOver()
        {
            for (var i = _pickedUsed; i < _pickedLive && i < PickedOver.Count; i++) PickedOver[i].Picture.gameObject.SetActive(false);
            _pickedLive = _pickedUsed;
        }

        /// <summary>The dots a merged mark pointed at stands for, each at its own spot in an accent ring, and how many stand.</summary>
        private static readonly List<(Image Picture, RectTransform Rect)> Members = new List<(Image, RectTransform)>();
        private static int _membersShown;
        private static Sprite _activeRing;

        /// <summary>How many of a pointed merged mark's dots stand at their own spots, for the self-test.</summary>
        [ForTest]
        public static int MembersShownForTest => _membersShown;

        /// <summary>
        /// While the pointer rests on a kind's merged mark, every other dot it stands for at its own
        /// spot, in the mark's look inside an accent ring, so where they lie shows without the
        /// merged mark coming apart; the mark pointed at keeps its own ring. Over the game's pins
        /// with what else the pointer brings up; put away as the pointer leaves it.
        /// </summary>
        private static void ShowMembers(Minimap map, float size, RectTransform like)
        {
            var dot = PointedDot();
            var parent = _over != null ? _over : _holder;
            var shown = 0;
            if (dot >= 0 && MergedCount(dot) > 1 && parent != null && like != null && map.m_mapImageLarge != null)
            {
                var window = map.m_mapImageLarge.uvRect;
                var area = map.m_mapImageLarge.rectTransform.rect;
                var look = ShownSize(size);
                foreach (var member in Merged.MembersOf(dot))
                {
                    if (member == dot) continue;
                    var (x, z, picture, _, _) = StandingDots[member];
                    if (!MapMarks.Place(new Vec3(x, 0f, z), map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (area.width, area.height), out var at)) continue;
                    while (Members.Count < (shown + 1) * 2) Members.Add(StackPips.Piece(parent, like, "Scry merged mark's dot"));
                    var spot = new Vector2(at.X, at.Y);
                    StackPips.Stand(Members[shown * 2], spot, look * 1.45f, ActiveRing());
                    StackPips.Stand(Members[shown * 2 + 1], spot, look, LookOf(picture));
                    shown++;
                }
            }
            for (var i = shown * 2; i < Members.Count; i++) if (Members[i].Picture.gameObject.activeSelf) Members[i].Picture.gameObject.SetActive(false);
            _membersShown = shown;
        }

        /// <summary>The dot the pointer rests on: a mark at rest, or one fanned out of a stack; -1 for none.</summary>
        private static int PointedDot() => _fanPointed >= 0 ? _fanPointed : _pointedAt >= 0 && _pointedAt < _standing && _pointedAt < PlacedFrom.Count ? PlacedFrom[_pointedAt] : -1;

        /// <summary>
        /// The count of a merged mark pointed at stood again over everything, the dots it brings up
        /// and their rings too, at its full look as the mark pointed at stands; so the number stays
        /// readable where its dots lie close. None while no merged mark is pointed at.
        /// </summary>
        private static void ShowPointedCount(Minimap map, RectTransform like)
        {
            PointedCount.Begin(_over != null ? _over : _holder);
            var dot = PointedDot();
            if (dot >= 0 && MergedCount(dot) > 1)
            {
                var fanned = _fanPointed >= 0 ? FanFrom.IndexOf(_fanPointed) : -1;
                if (fanned >= 0) PointedCount.Put(map, FanDots[fanned].Rect.anchoredPosition, _fanSize, MergedCount(dot), StandingDots[dot].Told, 1f, like);
                else if (_fanPointed < 0) PointedCount.Put(map, Dots[_pointedAt].Rect.anchoredPosition, Dots[_pointedAt].Rect.sizeDelta.x, MergedCount(dot), StandingDots[dot].Told, 1f, like);
            }
            PointedCount.End();
        }

        /// <summary>The ring round a merged mark's dots shown while it is pointed at: the accent between dark halos, made once.</summary>
        public static Sprite ActiveRing()
        {
            if (_activeRing != null) return _activeRing;
            const int side = 32;
            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var r = Mathf.Sqrt((x + 0.5f - side / 2f) * (x + 0.5f - side / 2f) + (y + 0.5f - side / 2f) * (y + 0.5f - side / 2f)) / (side / 2f);
                    pixels[y * side + x] = r > 0.74f && r <= 0.88f ? Skin.Accent : r > 0.66f && r <= 0.98f ? Skin.Alpha(Skin.OnAccent, 0.6f) : Skin.Alpha(Skin.OnAccent, 0f);
                }
            }
            var texture = Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry merged mark's ring", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _activeRing = Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            return _activeRing;
        }

        private static void ForgetKinds()
        {
            Members.Clear();
            PickedOver.Clear();
            _pickedUsed = _pickedLive = 0;
            _membersShown = 0;
            Counts.Forget();
            PointedCount.Forget();
            MarkedKindsAre(System.Array.Empty<(string, string, int, int)>());
            ForgetModes();
            _merging = false;
            _plain = false;
            KindCategories.Clear();
            KindNumbers.Clear();
            KindOfDot.Clear();
            StripKinds.Clear();
            Plains.Clear();
            _stripFor = default;
            Chips.Clear();
            _chipsFor = default;
        }
    }
}
