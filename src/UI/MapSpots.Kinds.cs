using System.Collections.Generic;
using UnityEngine;

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
        private static bool _merging, _plain;

        /// <summary>Each standing dot's kind as a number, for only a kind's own dots to merge.</summary>
        private static readonly Dictionary<(string Kind, bool Told), int> KindNumbers = new Dictionary<(string Kind, bool Told), int>();
        private static readonly List<int> KindOfDot = new List<int>();

        /// <summary>Each dot's plain look, by its full picture: the same texture, cut to its middle.</summary>
        private static readonly Dictionary<Sprite, Sprite> Plains = new Dictionary<Sprite, Sprite>();

        /// <summary>The kind shown alone, none while all are; and whether that changed since the dots were last placed.</summary>
        private static string _solo;
        private static bool _soloChanged;

        /// <summary>The kinds kept that stand, for the kinds strip: each with its name, how many and its picture; and what they were worked out for.</summary>
        private static readonly List<(string Kind, string Name, int Count, Sprite Picture)> StripKinds = new List<(string, string, int, Sprite)>();
        private static (IReadOnlyList<(int X, int Z)>, IReadOnlyList<(int X, int Z)>, Sprite, int) _stripFor;

        /// <summary>Where the kinds strip was drawn and the frame, for its clicks to be kept from the map under it.</summary>
        private static Rect _kindsStripAt;
        private static int _kindsStripFrame = -10;

        /// <summary>How many merged marks show their count, for the self-test.</summary>
        [ForTest]
        public static int MergedShownForTest => Counts.Shown;

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

        /// <summary>The kind shown alone; none while all are.</summary>
        public static string ShownAlone => _solo;

        /// <summary>Whether the kinds strip stands: on in the settings, the large map open, two kinds or more kept standing.</summary>
        public static bool KindsStripShown => Settings.KindsStrip && LargeOpen && KindsStanding().Count >= 2 && !TargetPortalMap.MarksAside;

        /// <summary>The kinds kept that stand on the map, for the strip, worked out again only as the dots change.</summary>
        public static IReadOnlyList<(string Kind, string Name, int Count, Sprite Picture)> KindsStanding()
        {
            if (_stripFor == _dotsFor) return StripKinds;
            _stripFor = _dotsFor;
            StripKinds.Clear();
            var at = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (var i = 0; i < StandingDots.Count && i < StandingKinds.Count; i++)
            {
                var kind = StandingKinds[i];
                if (kind == null) continue;
                if (at.TryGetValue(kind, out var index))
                {
                    var (k, name, count, picture) = StripKinds[index];
                    StripKinds[index] = (k, name, count + 1, picture);
                    continue;
                }
                at[kind] = StripKinds.Count;
                StripKinds.Add((kind, StandingDots[i].Name, 1, StandingDots[i].Picture));
            }
            return StripKinds;
        }

        /// <summary>A kind shown alone, as a click on its chip does; the same kind again, or none, shows them all.</summary>
        public static void ShowAlone(string kind)
        {
            _solo = kind == null || kind == _solo ? null : kind;
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
            if (_solo != null) _soloChanged = true;
            _solo = null;
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
                KindOfDot.Add(number);
            }
            Merged.Gather(StandingDots.Count, i => (StandingDots[i].X, StandingDots[i].Z), touch, i => KindOfDot[i]);
        }

        /// <summary>Whether a dot stands on the map: one alone or the first of its kind's merged ones.</summary>
        private static bool Visible(int dot) => !_merging || Merged.AnchorOf(dot) == dot;

        /// <summary>How many dots a standing dot stands for: itself, or its kind's merged ones.</summary>
        private static int MergedCount(int dot) => _merging ? Mathf.Max(1, Merged.CountAt(dot)) : 1;

        /// <summary>Whether the dots stand plain at the zoom they are placed for, a dot spanning so many metres.</summary>
        private static void NotePlain(float dotMetres) => _plain = MapLook.PlainAt(Settings.FarMarks, dotMetres);

        private static float ShownSize(float size) => _plain ? size * SeenOnMap.PlainShare : size;

        private static Sprite LookOf(Sprite full) => _plain ? PlainOf(full) : full;

        /// <summary>A dot's colour: as drawn, or faded where another kind is shown alone.</summary>
        private static Color ShownColour(int dot) => _solo == null || (dot < StandingKinds.Count && StandingKinds[dot] == _solo) ? Skin.MarkShown : Skin.MarkFaded;

        /// <summary>A dot's plain look: its own picture's middle, the kind's shape or picture and its edge, without the gap and ring, made once for each picture.</summary>
        public static Sprite PlainOf(Sprite full)
        {
            if (full == null) return null;
            if (Plains.TryGetValue(full, out var plain) && plain != null) return plain;
            var whole = full.rect;
            var side = whole.width * SeenOnMap.PlainShare;
            var middle = new Rect(whole.x + (whole.width - side) / 2f, whole.y + (whole.height - side) / 2f, side, side);
            plain = Kept.Add(Sprite.Create(full.texture, middle, new Vector2(0.5f, 0.5f)));
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

        private static void ForgetKinds()
        {
            Counts.Forget();
            _merging = false;
            _plain = false;
            KindNumbers.Clear();
            KindOfDot.Clear();
            StripKinds.Clear();
            Plains.Clear();
            _stripFor = default;
        }
    }
}
