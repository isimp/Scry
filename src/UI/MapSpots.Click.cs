using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A click on a dot on the large map opens its kind's page in Scry, as a click on a dungeon's
    /// mark opens the dungeon (<see cref="MapMarkClick"/>): the map goes back to the minimap and the
    /// panel shows the page. A dot alone or one of a spread stack is clicked; a stack at rest
    /// spreads first as the pointer comes to it. The click is read where it is let go, as the
    /// pointer's words stand aside while the button is held, and counts only where it was pressed
    /// within half a dot of there, so a drag of the map is no click. Where Scry's dots stand under
    /// every pin, a pin of the game's under the pointer takes the click instead
    /// (<see cref="MapLook.ClickIsMarks"/>).
    /// </summary>
    internal static partial class MapSpots
    {
        /// <summary>Where the pointer was as the left button was last pressed on the large map.</summary>
        private static Vector2 _pressedAt;

        /// <summary>The left button pressed at a point of the screen, as a player's press is noted, for the self-test to click there.</summary>
        [ForTest]
        public static void PressedForTest(Vector2 at) => _pressedAt = at;

        /// <summary>Each frame the dots stand: where the left button is pressed, for the click let go to be told from a drag.</summary>
        private static void NotePress()
        {
            if (Input.GetMouseButtonDown(0)) _pressedAt = ZInput.pointerPosition;
        }

        /// <summary>
        /// A click of the map's own at a point of the screen: the key of the page of the dot's kind
        /// there, for the click to open it (<see cref="MapMarkClick"/>); none where it is on no
        /// dot, on a stack at rest, a drag, or a pin the click is the game's for.
        /// </summary>
        public static string ClickedKind(Vector2 screen)
        {
            var map = Minimap.instance;
            if (map == null || map.m_mode != Minimap.MapMode.Large || _holder == null || !_holder.gameObject.activeInHierarchy) return null;
            var (from, size) = DotUnder(screen);
            if (from < 0 || from >= StandingKinds.Count) return null;
            if (!MapMarks.Clicked((_pressedAt.x, _pressedAt.y), (screen.x, screen.y), size)) return null;
            if (!MapLook.ClickIsMarks(Settings.MarksOverPins, PinUnder(map, screen, size))) return null;
            // A kind is its page's key: the page opens at once where the catalog is read, else once
            // it is, as a dungeon's does; a line of several kinds opens its first.
            var kind = StandingKinds[from];
            if (string.IsNullOrEmpty(kind)) return null;
            return WorldCatalog.Find(kind)?.Key ?? MapKept.KindsIn(kind)[0];
        }

        /// <summary>Whether the pointer stands on a dot a click would open, so a double-click there adds no pin.</summary>
        public static bool PointerOnDot() => DotUnder(ZInput.pointerPosition).From >= 0;

        /// <summary>The dot standing under a point of the screen, a fanned one first, and how wide a dot stands there; -1 for none, or for a stack at rest.</summary>
        private static (int From, float Size) DotUnder(Vector2 screen)
        {
            if (FanStanding > 0)
            {
                var half = _fanSize * _fanHolder.lossyScale.x / 2f;
                for (var k = FanFrom.Count - 1; k >= 0; k--)
                {
                    if (((Vector2)FanDots[k].Rect.position - screen).sqrMagnitude <= half * half) return (FanFrom[k], half * 2f);
                }
            }
            if (_standing == 0) return (-1, 0f);
            var dotHalf = Dots[0].Rect.rect.width * Dots[0].Rect.lossyScale.x / 2f;
            for (var i = _standing - 1; i >= 0 && i < PlacedFrom.Count; i--)
            {
                if (((Vector2)Dots[i].Rect.position - screen).sqrMagnitude > dotHalf * dotHalf) continue;
                var from = PlacedFrom[i];
                return Stacks.CountAt(Stacks.AnchorOf(from)) > 1 ? (-1, 0f) : (from, dotHalf * 2f);
            }
            return (-1, 0f);
        }

        /// <summary>Whether a pin of the game's stands under a point of the screen, within half a dot of it.</summary>
        private static bool PinUnder(Minimap map, Vector2 screen, float size)
        {
            var pins = GamePins(map);
            if (pins == null) return false;
            foreach (var pin in pins)
            {
                if (pin?.m_uiElement == null || !pin.m_uiElement.gameObject.activeInHierarchy) continue;
                if (((Vector2)pin.m_uiElement.position - screen).sqrMagnitude <= size * size / 4f) return true;
            }
            return false;
        }
    }
}
