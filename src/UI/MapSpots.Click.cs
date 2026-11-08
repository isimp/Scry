using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A click on a dot on the large map (<see cref="MapMarkClick"/>) goes through its modes, as the
    /// game's click crosses its pins (<see cref="SpotModes"/>), and a right click opens a menu with its
    /// page (<see cref="MapMenu"/>). A dot alone or one of a spread stack is clicked; a stack at rest
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

        /// <summary>Whether the pointer stands on a dot a click is for, so a double-click there adds no pin.</summary>
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
